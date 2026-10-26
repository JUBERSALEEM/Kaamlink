using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace KaamLink.Core.Text;

/// <summary>
/// Offline country/region and calling-code directory. The small metadata file is
/// shipped with the app so the picker works before sign-in and without a network.
/// Display names are resolved by the device's globalization data.
/// </summary>
public static class CountryPhoneDirectory
{
    private const string AssetName = "country-calling-codes.json";
    public static async Task<IReadOnlyList<CountryPhoneOption>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(AssetName);
        var data = await JsonSerializer.DeserializeAsync(
            stream, CountryPhoneDirectoryJsonContext.Default.CountryPhoneDirectoryData, cancellationToken);

        if (data?.Countries is not { Count: > 0 })
            throw new InvalidDataException("The bundled phone country directory is empty.");

        var countries = new List<CountryPhoneOption>(data.Countries.Count);
        foreach (var item in data.Countries)
        {
            if (string.IsNullOrWhiteSpace(item.IsoCode)
                || string.IsNullOrWhiteSpace(item.CallingCode))
                continue;

            string displayName;
            string englishName;
            try
            {
                var region = new RegionInfo(item.IsoCode);
                displayName = region.DisplayName;
                englishName = region.EnglishName;
            }
            catch (ArgumentException)
            {
                // Some special regions in numbering metadata aren't recognized by
                // every Android ICU release; keep them selectable by their ISO code.
                displayName = item.IsoCode;
                englishName = item.IsoCode;
            }

            countries.Add(new CountryPhoneOption(
                item.IsoCode,
                item.CallingCode,
                item.NationalPrefix ?? string.Empty,
                item.KeepNationalPrefix,
                displayName,
                englishName));
        }

        return countries
            .OrderBy(country => country.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static string GetDeviceRegionIsoCode()
    {
        try
        {
            var iso = RegionInfo.CurrentRegion.TwoLetterISORegionName;
            return string.IsNullOrWhiteSpace(iso) ? "AE" : iso;
        }
        catch
        {
            var cultureRegion = CultureInfo.CurrentCulture.Name.Split('-').LastOrDefault();
            return string.IsNullOrWhiteSpace(cultureRegion) ? "AE" : cultureRegion;
        }
    }

}
