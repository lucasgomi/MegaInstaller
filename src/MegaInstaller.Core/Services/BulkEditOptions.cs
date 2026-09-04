namespace MegaInstaller.Core.Services;

/// <summary>
/// What to change across several installers at once. Each field is
/// opt-in: null (or, for AddTagsText, empty) means "leave this field
/// exactly as each entry already has it."
/// </summary>
public sealed class BulkEditOptions
{
    public string? Arguments { get; set; }
    public string? InstallDir { get; set; }
    public bool? RunAsAdmin { get; set; }
    public int? Order { get; set; }
    public string? AddTagsText { get; set; }
}
