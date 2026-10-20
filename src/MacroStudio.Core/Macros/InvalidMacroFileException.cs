namespace MacroStudio.Core.Macros;

/// <summary>A .msmacro file that is too large, not valid JSON, or outside the limits of <see cref="MacroValidator"/>.</summary>
public sealed class InvalidMacroFileException : Exception
{
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
}
