using System.Text.Json.Serialization;

namespace MacroStudio.Core.Macros;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(MacroDefinition))]
internal sealed partial class MacroJsonContext : JsonSerializerContext;
