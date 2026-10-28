using System.Text.Json.Serialization;

namespace MacroStudio.Core.Macros.Steps;

/// <summary>
/// One action of a macro. The "type" field in the JSON file tells which kind of step it is.
/// Properties use "set", not "init": the JSON source generator resets a missing init-only property to
/// null/0/false, so a file without "isEnabled" would load every step disabled.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MouseClickStep), "click")]
[JsonDerivedType(typeof(MouseDragStep), "drag")]
[JsonDerivedType(typeof(MouseWheelStep), "wheel")]
[JsonDerivedType(typeof(KeyPressStep), "key")]
[JsonDerivedType(typeof(HotkeyStep), "hotkey")]
[JsonDerivedType(typeof(TypeTextStep), "text")]
[JsonDerivedType(typeof(FocusWindowStep), "focusWindow")]
[JsonDerivedType(typeof(CopyStep), "copy")]
[JsonDerivedType(typeof(PasteStep), "paste")]
[JsonDerivedType(typeof(WaitStep), "wait")]
[JsonDerivedType(typeof(OpenStep), "open")]
[JsonDerivedType(typeof(WaitForStep), "waitFor")]
[JsonDerivedType(typeof(IfStep), "if")]
[JsonDerivedType(typeof(ImageClickStep), "imageClick")]
public abstract record MacroStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Time between the end of the previous step and the start of this one, as recorded.</summary>
    public int DelayBeforeMs { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? Note { get; set; }
}
