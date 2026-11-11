using System.Text.Json.Serialization;

namespace MacroStudio.Core.Macros;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(MacroDefinition))]
internal sealed partial class MacroJsonContext : JsonSerializerContext;
