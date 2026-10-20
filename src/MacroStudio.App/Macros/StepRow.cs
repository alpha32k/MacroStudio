using Wpf.Ui.Controls;

namespace MacroStudio.App.Macros;

/// <summary>One line of a step list: number, icon, readable description and the wait before the step.</summary>
public sealed record StepRow(int Number, SymbolRegular Icon, string Description, int DelayBeforeMs);
