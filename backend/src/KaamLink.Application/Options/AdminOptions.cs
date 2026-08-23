namespace KaamLink.Application.Options;

/// <summary>
/// Platform-admin bootstrap. Phones listed here receive IsPlatformAdmin=true AT
/// REGISTRATION ONLY (config changes never mutate existing users). Development/
/// staging convenience — production admin access is granted by a controlled
/// operational procedure, not by editing config. Bound from "Admin".
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string[] BootstrapPhones { get; init; } = [];
}
