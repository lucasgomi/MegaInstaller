namespace MegaInstaller.Core.Services;

/// <summary>
/// The on-disk convention for custom-uploaded instance icons: a
/// "custom:filename.png" IconKey references a file under
/// "&lt;installers folder&gt;/CustomTheme/". Lives in Core (rather than
/// next to the App's icon catalog that loads them) because export/import
/// needs the same convention to package and restore these files without
/// depending on the WinForms layer.
/// </summary>
public static class CustomIconPaths
{
    public const string FolderName = "CustomTheme";

    private const string KeyPrefix = "custom:";

    public static string BuildKey(string fileName) => KeyPrefix + fileName;

    public static bool IsCustomKey(string? key) => key is not null && key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    /// <summary>The bare file name for a custom key (e.g. "custom:abc.png" -> "abc.png"), or null if the key isn't a custom one.</summary>
    public static string? FileNameFromKey(string? key) => IsCustomKey(key) ? key![KeyPrefix.Length..] : null;
}
