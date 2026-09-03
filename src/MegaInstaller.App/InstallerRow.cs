using System.ComponentModel;
using System.Runtime.CompilerServices;
using MegaInstaller.Core.Models;

namespace MegaInstaller.App;

/// <summary>Thin, bindable wrapper around an <see cref="InstallerEntry"/> for the grid.</summary>
public sealed class InstallerRow : INotifyPropertyChanged
{
    public InstallerRow(InstallerEntry entry)
    {
        Entry = entry;
    }

    public InstallerEntry Entry { get; }

    public bool Enabled
    {
        get => Entry.Enabled;
        set
        {
            if (Entry.Enabled == value) return;
            Entry.Enabled = value;
            OnPropertyChanged();
        }
    }

    public string Name
    {
        get => Entry.Name;
        set
        {
            if (Entry.Name == value) return;
            Entry.Name = value;
            OnPropertyChanged();
        }
    }

    public string FileName => Entry.FileName;

    public string Type => Entry.Type.ToString();

    public string Arguments => Entry.Arguments;

    public bool RunAsAdmin => Entry.RunAsAdmin;

    public int Order => Entry.Order;

    private string _status = "Pendiente";

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public void RefreshAll()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(Arguments));
        OnPropertyChanged(nameof(RunAsAdmin));
        OnPropertyChanged(nameof(Order));
        OnPropertyChanged(nameof(Enabled));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
