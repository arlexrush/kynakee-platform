using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Domain.ValueObjects;

public sealed record Email
{
    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            !MailAddress.TryCreate(normalized, out var address) ||
            !string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return ResultFactory.Failure<Email>(
                ApplicationError.Validation("IDENTITY_EMAIL_INVALID", "A valid email address is required."));
        }

        return ResultFactory.Success(new Email(normalized.ToUpperInvariant()));
    }
}

public sealed record PhoneNumber
{
    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    public static Result<PhoneNumber> Create(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            !Regex.IsMatch(normalized, @"\A\+[1-9][0-9]{7,14}\z", RegexOptions.CultureInvariant))
        {
            return ResultFactory.Failure<PhoneNumber>(
                ApplicationError.Validation("IDENTITY_PHONE_INVALID", "Phone number must use E.164 format."));
        }

        return ResultFactory.Success(new PhoneNumber(normalized));
    }
}

public sealed record TenantSlug
{
    private TenantSlug(string value) => Value = value;

    public string Value { get; }

    public static Result<TenantSlug> Create(string? value)
    {
#pragma warning disable CA1308 // Tenant slugs are intentionally lowercase URL identifiers.
        var normalized = value?.Trim().ToLowerInvariant();
#pragma warning restore CA1308
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 63 ||
            !Regex.IsMatch(normalized, @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant))
        {
            return ResultFactory.Failure<TenantSlug>(
                ApplicationError.Validation("IDENTITY_TENANT_SLUG_INVALID", "Tenant slug must contain lowercase letters, digits and single hyphens."));
        }

        return ResultFactory.Success(new TenantSlug(normalized));
    }
}

public sealed record TaxId
{
    private TaxId(string value, string countryCode)
    {
        Value = value;
        CountryCode = countryCode;
    }

    public string Value { get; }

    public string CountryCode { get; }

    public static Result<TaxId> Create(string? value, string? countryCode)
    {
        var normalizedCountry = countryCode?.Trim().ToUpperInvariant();
        var normalizedValue = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCountry) || normalizedCountry.Length != 2 ||
            normalizedCountry.Any(character => character is < 'A' or > 'Z'))
        {
            return ResultFactory.Failure<TaxId>(
                ApplicationError.Validation("IDENTITY_TAX_COUNTRY_INVALID", "Tax country must be a two-letter ISO code."));
        }

        if (string.IsNullOrWhiteSpace(normalizedValue) || normalizedValue.Length > 32 ||
            normalizedValue.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            return ResultFactory.Failure<TaxId>(
                ApplicationError.Validation("IDENTITY_TAX_ID_INVALID", "Tax identifier format is invalid."));
        }

        if (normalizedCountry == "ES" && !IsValidSpanishTaxId(normalizedValue))
        {
            return ResultFactory.Failure<TaxId>(
                ApplicationError.Validation("IDENTITY_TAX_ID_INVALID", "Spanish tax identifier is invalid."));
        }

        return ResultFactory.Success(new TaxId(normalizedValue, normalizedCountry));
    }

    private static bool IsValidSpanishTaxId(string value)
    {
        const string controlLetters = "TRWAGMYFPDXBNJZSQVHLCKE";
        if (value.Length == 9 && value[..8].All(char.IsAsciiDigit) &&
            controlLetters[int.Parse(value[..8], CultureInfo.InvariantCulture) % 23] == value[8])
        {
            return true;
        }

        if (value.Length == 9 && value[0] is 'X' or 'Y' or 'Z' &&
            value[1..8].All(char.IsAsciiDigit))
        {
            var prefix = value[0] switch { 'X' => '0', 'Y' => '1', _ => '2' };
            var number = int.Parse(prefix + value[1..8], CultureInfo.InvariantCulture);
            return controlLetters[number % 23] == value[8];
        }

        if (value.Length != 9 || !"ABCDEFGHJNPQRSUVW".Contains(value[0], StringComparison.Ordinal) ||
            !value[1..8].All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var index = 1; index <= 7; index++)
        {
            var digit = value[index] - '0';
            if (index % 2 == 1)
            {
                var doubled = digit * 2;
                sum += doubled / 10 + doubled % 10;
            }
            else
            {
                sum += digit;
            }
        }

        var control = (10 - sum % 10) % 10;
        var numericControl = (char)('0' + control);
        var letterControl = "JABCDEFGHI"[control];
        return value[0] switch
        {
            'A' or 'B' or 'E' or 'H' => numericControl == value[8],
            'K' or 'P' or 'Q' or 'S' => letterControl == value[8],
            _ => numericControl == value[8] || letterControl == value[8]
        };
    }
}

public sealed record Address
{
    private Address(
        string country,
        string? region,
        string? province,
        string? municipality,
        string? postalCode,
        string? street)
    {
        Country = country;
        Region = region;
        Province = province;
        Municipality = municipality;
        PostalCode = postalCode;
        Street = street;
    }

    public string Country { get; }

    public string? Region { get; }

    public string? Province { get; }

    public string? Municipality { get; }

    public string? PostalCode { get; }

    public string? Street { get; }

    public static Result<Address> Create(
        string? country,
        string? region = null,
        string? province = null,
        string? municipality = null,
        string? postalCode = null,
        string? street = null)
    {
        var normalizedCountry = country?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCountry) || normalizedCountry.Length != 2 ||
            normalizedCountry.Any(character => character is < 'A' or > 'Z'))
        {
            return ResultFactory.Failure<Address>(
                ApplicationError.Validation("IDENTITY_ADDRESS_COUNTRY_INVALID", "Country must be a two-letter ISO code."));
        }

        return ResultFactory.Success(new Address(
            normalizedCountry,
            Normalize(region),
            Normalize(province),
            Normalize(municipality),
            Normalize(postalCode),
            Normalize(street)));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CompanySettings
{
    private CompanySettings(
        decimal administration,
        decimal profit,
        decimal quality,
        decimal safetyHealth,
        decimal environment,
        decimal contingency)
    {
        Administration = administration;
        Profit = profit;
        Quality = quality;
        SafetyHealth = safetyHealth;
        Environment = environment;
        Contingency = contingency;
    }

    public decimal Administration { get; }

    public decimal Profit { get; }

    public decimal Quality { get; }

    public decimal SafetyHealth { get; }

    public decimal Environment { get; }

    public decimal Contingency { get; }

    public static CompanySettings Default { get; } = new(0, 0, 0, 0, 0, 0);

    public static Result<CompanySettings> Create(
        decimal administration,
        decimal profit,
        decimal quality,
        decimal safetyHealth,
        decimal environment,
        decimal contingency)
    {
        if (new[] { administration, profit, quality, safetyHealth, environment, contingency }
            .Any(value => value is < 0 or > 100))
        {
            return ResultFactory.Failure<CompanySettings>(
                ApplicationError.Validation("IDENTITY_SETTINGS_PERCENTAGE_INVALID", "Settings percentages must be between 0 and 100."));
        }

        return ResultFactory.Success(new CompanySettings(
            administration, profit, quality, safetyHealth, environment, contingency));
    }
}

public sealed record CompanyBranding
{
    private CompanyBranding(string? companyName, string? primaryColor, Uri? logoUrl, string? email)
    {
        CompanyName = companyName;
        PrimaryColor = primaryColor;
        LogoUrl = logoUrl;
        Email = email;
    }

    public string? CompanyName { get; }

    public string? PrimaryColor { get; }

    public Uri? LogoUrl { get; }

    public string? Email { get; }

    public static Result<CompanyBranding> Create(
        string? companyName = null,
        string? primaryColor = null,
        Uri? logoUrl = null,
        string? email = null)
    {
        var normalizedColor = string.IsNullOrWhiteSpace(primaryColor)
            ? null
            : primaryColor.Trim().ToUpperInvariant();
        if (normalizedColor is not null &&
            !Regex.IsMatch(normalizedColor, @"\A#[0-9A-F]{6}\z", RegexOptions.CultureInvariant))
        {
            return ResultFactory.Failure<CompanyBranding>(
                ApplicationError.Validation("IDENTITY_BRANDING_COLOR_INVALID", "Primary color must be a six-digit hexadecimal color."));
        }

        if (logoUrl is not null &&
            (!logoUrl.IsAbsoluteUri || logoUrl.Scheme != Uri.UriSchemeHttps))
        {
            return ResultFactory.Failure<CompanyBranding>(
                ApplicationError.Validation("IDENTITY_BRANDING_LOGO_URL_INVALID", "Logo URL must be an absolute HTTPS URL."));
        }

        string? normalizedEmail = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailResult = global::Kynakee.Modules.Identity.Domain.ValueObjects.Email.Create(email);
            if (emailResult.IsFailure)
            {
                return ResultFactory.Failure<CompanyBranding>(emailResult.Error!);
            }

            normalizedEmail = emailResult.Value!.Value;
        }

        return ResultFactory.Success(new CompanyBranding(
            Normalize(companyName), normalizedColor, logoUrl, normalizedEmail));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}