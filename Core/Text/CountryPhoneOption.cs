namespace KaamLink.Core.Text;

/// <summary>One selectable numbering-plan region shown in the sign-in country picker.</summary>
public sealed record CountryPhoneOption(
    string IsoCode,
    string CallingCode,
    string NationalPrefix,
    bool KeepNationalPrefix,
    string DisplayName,
    string EnglishName)
{
    public string CallingCodeLabel => $"{IsoCode}  +{CallingCode}";

    public string PickerLabel => $"{DisplayName}  (+{CallingCode})";

    public string SearchText => $"{DisplayName} {EnglishName} {IsoCode} +{CallingCode}";
}
