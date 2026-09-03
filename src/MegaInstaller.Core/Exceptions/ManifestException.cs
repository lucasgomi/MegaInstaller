namespace MegaInstaller.Core.Exceptions;

/// <summary>Thrown when megainstaller.json exists but cannot be parsed.</summary>
public sealed class ManifestException : Exception
{
    public ManifestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
