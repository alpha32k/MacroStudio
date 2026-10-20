using CommunityToolkit.Mvvm.ComponentModel;

namespace MacroStudio.App.Localization;

/// <summary>A choice in a list (combo box, menu) whose label follows the current language.</summary>
public sealed class LocalizedOption<T> : ObservableObject
{
    private readonly Func<Localizer, string> _label;

    public LocalizedOption(T value, Func<Localizer, string> label)
    {
        Value = value;
        _label = label;
        Localizer.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public T Value { get; }

    public string Label => _label(Localizer.Instance);

    public static LocalizedOption<T> FromKey(T value, string resourceKey) => new(value, l => l[resourceKey]);

    // Screen readers and UI Automation read list items through ToString.
    public override string ToString() => Label;
}
