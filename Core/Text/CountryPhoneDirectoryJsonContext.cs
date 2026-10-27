using System.Text.Json.Serialization;

namespace KaamLink.Core.Text;

/// <summary>Strongly typed, trim-safe model for the bundled numbering-plan index.</summary>
internal sealed record CountryPhoneDirectoryData(
    string? MetadataVersion,
    List<CountryPhoneDirectoryItem> Countries);

internal sealed record CountryPhoneDirectoryItem(
    string IsoCode,
    string CallingCode,
    string? NationalPrefix,
    bool KeepNationalPrefix);

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CountryPhoneDirectoryData))]
internal sealed partial class CountryPhoneDirectoryJsonContext : JsonSerializerContext;
