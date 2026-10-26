#!/usr/bin/env python3
"""Static/data checks for the offline international country picker.

This is not a .NET build or an execution of the C# normalizer. It validates the
bundled metadata and the important cross-file wiring; the vector check below is an
independent reference implementation of the current E.164-envelope normalization.
"""
from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parent.parent
asset = root / "Resources/Raw/country-calling-codes.json"
project = (root / "KaamLink.csproj").read_text(encoding="utf-8")
directory = (root / "Core/Text/CountryPhoneDirectory.cs").read_text(encoding="utf-8")
context = (root / "Core/Text/CountryPhoneDirectoryJsonContext.cs").read_text(encoding="utf-8")
normalizer = (root / "Core/Text/PhoneInput.cs").read_text(encoding="utf-8")
vm = (root / "Features/Auth/LoginViewModel.cs").read_text(encoding="utf-8")
page = (root / "Features/Auth/LoginPage.xaml").read_text(encoding="utf-8")

checks = 0
failures: list[str] = []

def check(condition: bool, message: str) -> None:
    global checks
    checks += 1
    if not condition:
        failures.append(message)

try:
    data = json.loads(asset.read_text(encoding="utf-8"))
except (OSError, json.JSONDecodeError) as error:
    print(f"FAIL: cannot load country calling-code asset: {error}")
    sys.exit(1)

countries = data.get("countries", [])
by_iso = {country.get("isoCode"): country for country in countries}
check(data.get("metadataVersion") == "1.13.15", "metadata version is not pinned/documented")
check(len(countries) >= 240, f"expected broad region coverage, got {len(countries)} entries")
check(len(by_iso) == len(countries), "country region ISO codes are not unique")
check(all(re.fullmatch(r"[A-Z]{2}", code or "") for code in by_iso), "invalid region ISO code")
check(all(re.fullmatch(r"[1-9][0-9]{0,2}", c.get("callingCode", "")) for c in countries),
      "calling code outside the E.164 country-code shape")
check(len([c for c in countries if c.get("callingCode") == "1"]) > 10,
      "shared +1 calling-code regions are missing")
for iso in ("AE", "GB", "US", "CA", "IN", "PK", "PH", "BR", "IT", "AU", "NZ", "ZA"):
    check(iso in by_iso, f"common region {iso} missing from bundled data")

check('<MauiAsset Include="Resources\\Raw\\**"' in project,
      "country directory/license files are not packaged as MAUI assets")
check('OpenAppPackageFileAsync(AssetName)' in directory and '"country-calling-codes.json"' in directory,
      "directory loader does not open the bundled logical asset")
check("CountryPhoneDirectoryJsonContext.Default.CountryPhoneDirectoryData" in directory
      and "[JsonSerializable(typeof(CountryPhoneDirectoryData))]" in context,
      "bundled JSON is not using its trim-safe source-generated serializer")
check("using System.Text.Json;" in context
      and "using System.Text.Json.Serialization;" in context,
      "CountryPhoneDirectoryJsonContext is missing a System.Text.Json import "
      "(JsonSerializerDefaults / JsonSerializerContext CS0103)")
check("Microsoft.Maui.Storage" in directory, "MAUI FileSystem namespace is not imported")
check("GetDeviceRegionIsoCode" in vm and "CountryPhoneDirectory.LoadAsync" in vm,
      "login view model does not initialize region/default country from offline metadata")
check("PhoneInput.TryNormalize(Phone, SelectedCountry)" in vm,
      "OTP request is not normalized against the selected region")
check('Command="{Binding OpenCountryPickerCommand}"' in page
      and 'Text="{Binding CountrySearchText}"' in page
      and 'CommandParameter="{Binding .}"' in page,
      "country picker button/search/select bindings are incomplete")
check("StartsWith(\"00\"" in normalizer and "StartsWith('+')" in normalizer
      and 'country.CallingCode' in normalizer,
      "normalizer does not preserve international input and apply selected calling code")
check("7,14" in normalizer and "envelope" in normalizer.lower(),
      "normalizer must make the E.164 envelope limitation explicit")

# Independent examples check the specified algorithm (not a call into C#).
def normalize(raw: str, country: dict) -> str | None:
    raw = raw.translate(str.maketrans("٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹", "01234567890123456789"))
    cleaned = re.sub(r"[\s\-()]", "", raw.strip())
    if cleaned.startswith("00"):
        cleaned = "+" + cleaned[2:]
    calling = country["callingCode"]
    if not cleaned.startswith("+"):
        if not cleaned.isascii() or not cleaned.isdigit():
            return None
        if cleaned.startswith(calling) and len(cleaned) > len(calling) + 7:
            cleaned = "+" + cleaned
        else:
            prefix = country.get("nationalPrefix", "")
            if not country.get("keepNationalPrefix", False) and prefix \
                    and cleaned.startswith(prefix) and len(cleaned) > len(prefix) + 6:
                cleaned = cleaned[len(prefix):]
            cleaned = "+" + calling + cleaned
    return cleaned if re.fullmatch(r"\+[1-9]\d{7,14}", cleaned) else None

vectors = [
    ("0501234567", "AE", "+971501234567"),
    ("020 7123 4567", "GB", "+442071234567"),
    ("2025550123", "US", "+12025550123"),
    ("12025550123", "US", "+12025550123"),
    ("00442071234567", "AE", "+442071234567"),
    ("+61412345678", "AE", "+61412345678"),
    ("02 1234 5678", "IT", "+390212345678"),
    ("011 98888 7777", "BR", "+5511988887777"),
    ("٠٥٠١٢٣٤٥٦٧", "AE", "+971501234567"),
]
for raw, iso, expected in vectors:
    actual = normalize(raw, by_iso[iso])
    check(actual == expected, f"normalization example {iso}/{raw!r}: expected {expected}, got {actual}")

print("Global phone directory — static/data validation (not a .NET runtime test)")
print(f"Metadata: {data.get('metadataVersion')}, {len(countries)} regions, {asset.stat().st_size:,} bytes")
if failures:
    print(f"\n{checks} checks, {len(failures)} failure(s):")
    for failure in failures:
        print("  -", failure)
    sys.exit(1)
print(f"\n{checks} checks, 0 failures")
print("Directory coverage, MAUI asset wiring, picker bindings, and reference vectors PASS")
