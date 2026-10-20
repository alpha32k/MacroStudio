using System.Windows.Data;
using System.Windows.Markup;

namespace MacroStudio.App.Localization;

/// <summary>XAML usage: Text="{l:Loc Settings_Theme}". The text follows language changes at runtime.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension(string key) : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
