using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>What the troubleshooter made of one failed install.</summary>
public sealed class InstallDiagnosis
{
    public required string Summary { get; init; }

    /// <summary>What to try, in plain language.</summary>
    public required string Advice { get; init; }

    /// <summary>Argument strings worth retrying with, most promising first. May be empty.</summary>
    public IReadOnlyList<string> SuggestedArguments { get; init; } = Array.Empty<string>();

    /// <summary>True when the exit code says the installer needs elevation it didn't have.</summary>
    public bool NeedsElevation { get; init; }
}

/// <summary>
/// Turns an install failure into something actionable: what the exit code
/// actually means for that installer family, and which argument sets are
/// worth retrying. Pure logic - the UI decides whether to act on it.
/// </summary>
public static class InstallDiagnostics
{
    private const int ErrorAccessDenied = 5;
    private const int ErrorInvalidParameter = 87;
    private const int ErrorElevationRequired = 740;
    private const int ErrorCancelled = 1223;

    public static InstallDiagnosis Analyze(InstallerEntry entry, InstallResult result)
    {
        if (result.Outcome == InstallOutcome.FileNotFound)
        {
            return new InstallDiagnosis
            {
                Summary = "No se encontró el archivo del instalador.",
                Advice = "Comprueba que el archivo sigue en la carpeta de instaladores y que el nombre coincide con el del programa.",
            };
        }

        if (result.Outcome == InstallOutcome.Cancelled)
        {
            return new InstallDiagnosis
            {
                Summary = "La instalación se canceló.",
                Advice = "Se canceló desde MegaInstaller o se rechazó el aviso de administrador (UAC). Vuelve a intentarlo y acepta la elevación.",
                NeedsElevation = true,
            };
        }

        if (result.ExitCode is not { } exitCode)
        {
            return new InstallDiagnosis
            {
                Summary = "El instalador no llegó a ejecutarse.",
                Advice = result.Message ?? "Windows no pudo iniciar el proceso. Comprueba que el archivo no esté bloqueado ni en cuarentena del antivirus.",
                SuggestedArguments = SilentArgsCatalog.GetAlternativeArguments(entry.Type),
            };
        }

        var known = DescribeExitCode(entry.Type, exitCode);
        if (known is not null)
        {
            return known;
        }

        return new InstallDiagnosis
        {
            Summary = $"El instalador terminó con el código {exitCode}.",
            Advice = entry.Type == InstallerType.Unknown
                ? "El tipo de instalador está sin identificar, así que puede que los argumentos silenciosos no sean los suyos. Usa \"Detectar tipo\" y prueba los argumentos sugeridos."
                : "El código no es uno de los conocidos para este tipo. Prueba con otros argumentos silenciosos o ejecuta el instalador a mano una vez para ver qué pide.",
            SuggestedArguments = SilentArgsCatalog.GetAlternativeArguments(entry.Type),
        };
    }

    private static InstallDiagnosis? DescribeExitCode(InstallerType type, int exitCode)
    {
        // Codes Windows Installer defines, which Burn bundles and
        // InstallShield's MSI wrappers pass through unchanged.
        var msiFamily = type is InstallerType.Msi or InstallerType.WixBurn or InstallerType.InstallShield;

        switch (exitCode)
        {
            // Inno Setup defines its own meaning for 5 ("setup aborted"),
            // so it's matched before the generic ERROR_ACCESS_DENIED below.
            case ErrorAccessDenied when type == InstallerType.InnoSetup:
                return new InstallDiagnosis
                {
                    Summary = "Inno Setup abortó la instalación (código 5).",
                    Advice = "Suele ser porque el programa ya está en ejecución o requiere cerrar algo. Cierra la aplicación y reintenta.",
                };

            case ErrorAccessDenied:
            case ErrorElevationRequired:
                return new InstallDiagnosis
                {
                    Summary = "El instalador necesita permisos de administrador.",
                    Advice = "Marca \"Ejecutar como administrador\" en el programa, o reinicia MegaInstaller como administrador para que todas las instalaciones hereden la elevación con un solo aviso.",
                    NeedsElevation = true,
                };

            case ErrorInvalidParameter:
                return new InstallDiagnosis
                {
                    Summary = "El instalador rechazó los argumentos.",
                    Advice = "Los argumentos no son los que entiende este instalador. Prueba con otro juego de flags silenciosos.",
                    SuggestedArguments = SilentArgsCatalog.GetAlternativeArguments(type),
                };

            case ErrorCancelled:
                return new InstallDiagnosis
                {
                    Summary = "Se rechazó el aviso de administrador (UAC).",
                    Advice = "Vuelve a intentarlo aceptando la elevación, o reinicia MegaInstaller como administrador para aceptarla una sola vez.",
                    NeedsElevation = true,
                };

            case 1602 when msiFamily:
                return Msi("El usuario canceló la instalación.", "El instalador mostró interfaz y se cerró. Añade los argumentos silenciosos para que no pida nada.", type);

            case 1603 when msiFamily:
                return new InstallDiagnosis
                {
                    Summary = "Error grave durante la instalación (1603).",
                    Advice = "Es el error genérico de MSI. Las causas típicas: falta de permisos, el programa ya está instalado, o falta espacio en disco. " +
                             "Reintenta con registro activado (/l*v) para que el propio instalador escriba el motivo en un archivo.",
                    SuggestedArguments = SilentArgsCatalog.GetAlternativeArguments(type),
                };

            case 1618 when msiFamily:
                return Msi("Hay otra instalación en curso (1618).", "Windows Installer solo admite una instalación a la vez. Espera a que acabe la otra, o pon estos programas en órdenes distintos para que no vayan en paralelo.", type);

            case 1619 when msiFamily:
                return Msi("No se pudo abrir el paquete (1619).", "El .msi no existe en esa ruta o está corrupto. Vuelve a descargarlo.", type);

            case 1620 when msiFamily:
                return Msi("El paquete no es válido (1620).", "El archivo está dañado o no es realmente un MSI. Vuelve a descargarlo.", type);

            case 1625 when msiFamily:
                return Msi("Una directiva del sistema bloquea la instalación (1625).", "Una política de grupo impide instalar este paquete. Hace falta un administrador del equipo.", type);

            case 1638 when msiFamily:
                return Msi("Ya hay otra versión instalada (1638).", "Desinstala la versión anterior primero, o usa el instalador de actualización del fabricante.", type);

            case 1639 when msiFamily:
                return new InstallDiagnosis
                {
                    Summary = "Argumento de línea de comandos no válido (1639).",
                    Advice = "msiexec no entendió alguno de los argumentos. Revísalos o prueba con los sugeridos.",
                    SuggestedArguments = SilentArgsCatalog.GetAlternativeArguments(type),
                };

            case 2 when type == InstallerType.InnoSetup:
                return new InstallDiagnosis
                {
                    Summary = "El usuario canceló el asistente (Inno Setup, código 2).",
                    Advice = "El instalador mostró interfaz. Asegúrate de incluir /VERYSILENT /SUPPRESSMSGBOXES para que no pregunte nada.",
                    SuggestedArguments = SilentArgsCatalog.GetAlternativeArguments(type),
                };

            default:
                return null;
        }
    }

    private static InstallDiagnosis Msi(string summary, string advice, InstallerType type) => new()
    {
        Summary = summary,
        Advice = advice,
        SuggestedArguments = SilentArgsCatalog.GetAlternativeArguments(type),
    };
}
