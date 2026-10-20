namespace MacroStudio.App.Pages;

public partial class MacrosPage : ContentPage
{
    public MacrosPage(MacrosViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
