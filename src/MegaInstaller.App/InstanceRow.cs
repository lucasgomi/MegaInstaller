using System.ComponentModel;
using System.Runtime.CompilerServices;
using MegaInstaller.Core.Models;

namespace MegaInstaller.App;

/// <summary>Thin, bindable wrapper around an <see cref="InstanceDefinition"/> for the Home grid.</summary>
public sealed class InstanceRow : INotifyPropertyChanged
{
    public InstanceRow(InstanceDefinition instance, int programCount)
    {
        Instance = instance;
        ProgramCount = programCount;
    }

    public InstanceDefinition Instance { get; }

    public string Name
    {
        get => Instance.Name;
        set
        {
            if (Instance.Name == value) return;
            Instance.Name = value;
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => Instance.Description;
        set
        {
            if (Instance.Description == value) return;
            Instance.Description = value;
            OnPropertyChanged();
        }
    }

    public int ProgramCount { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
