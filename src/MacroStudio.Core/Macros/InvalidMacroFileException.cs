namespace MacroStudio.Core.Macros;

/// <summary>A .msmacro file that is too large, not valid JSON, or outside the limits of <see cref="MacroValidator"/>.</summary>
public sealed class InvalidMacroFileException : Exception
{
    /// <summary>The first invalid step's position in the macro, starting at one.</summary>
    public int? StepNumber { get; }

    public InvalidMacroFileException()
    {
    }

    public InvalidMacroFileException(string message)
        : base(message)
    {
    }

    public InvalidMacroFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal InvalidMacroFileException(string message, int? stepNumber, Exception? innerException = null)
        : base(message, innerException)
    {
        StepNumber = stepNumber;
    }
}
