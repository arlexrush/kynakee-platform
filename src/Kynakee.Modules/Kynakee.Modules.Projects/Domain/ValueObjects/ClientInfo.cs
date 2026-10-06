namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    public sealed record ClientInfo
    {
        public string Name { get; }
        public string? PersonalIdentifier { get; }
        public string? BusinessIdentifier { get; }
        public ClientKind? Kind { get; }
        public string? TaxCountry { get; }
        public string? Email { get; }
        public string? Phone { get; }

        public ClientInfo(
            string name,
            string? personalIdentifier = null,
            string? businessIdentifier = null,
            ClientKind? kind = null,
            string? taxCountry = null,
            string? email = null,
            string? phone = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Client name is required.",
                    nameof(name));
            }

            if (kind.HasValue && !Enum.IsDefined(kind.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            var normalizedPersonalIdentifier = NormalizeIdentifier(personalIdentifier, nameof(personalIdentifier));
            var normalizedBusinessIdentifier = NormalizeIdentifier(businessIdentifier, nameof(businessIdentifier));
            var normalizedTaxCountry = string.IsNullOrWhiteSpace(taxCountry)
                ? null
                : taxCountry.Trim().ToUpperInvariant();

            if (normalizedTaxCountry is not null &&
                (normalizedTaxCountry.Length != 2 ||
                 normalizedTaxCountry.Any(character => character is < 'A' or > 'Z')))
            {
                throw new ArgumentException(
                    "Tax country must be a two-letter ISO 3166-1 alpha-2 code.",
                    nameof(taxCountry));
            }

            if (normalizedPersonalIdentifier is not null && normalizedBusinessIdentifier is not null)
            {
                throw new ArgumentException(
                    "A client cannot have both a personal and a business identifier.",
                    nameof(businessIdentifier));
            }

            if (normalizedPersonalIdentifier is not null || normalizedBusinessIdentifier is not null)
            {
                if (!kind.HasValue)
                {
                    throw new ArgumentException(
                        "Client kind is required when an identifier is provided.",
                        nameof(kind));
                }

                if (normalizedTaxCountry is null)
                {
                    throw new ArgumentException(
                        "Tax country is required when an identifier is provided.",
                        nameof(taxCountry));
                }

                if ((normalizedPersonalIdentifier is not null && kind != ClientKind.NaturalPerson) ||
                    (normalizedBusinessIdentifier is not null && kind != ClientKind.LegalEntity))
                {
                    throw new ArgumentException(
                        "The identifier must match the client kind.",
                        nameof(kind));
                }
            }

            Name = name.Trim();
            PersonalIdentifier = normalizedPersonalIdentifier;
            BusinessIdentifier = normalizedBusinessIdentifier;
            Kind = kind;
            TaxCountry = normalizedTaxCountry;
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        }

        private static string? NormalizeIdentifier(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim().ToUpperInvariant();

            if (normalized.Length > 20 ||
                !IsAsciiLetterOrDigit(normalized[0]) ||
                !IsAsciiLetterOrDigit(normalized[^1]) ||
                normalized.Any(character =>
                    !IsAsciiLetterOrDigit(character) && character is not (' ' or '-' or '.' or '/')))
            {
                throw new ArgumentException(
                    "Identifier must contain 1 to 20 letters or digits, with optional internal spaces or separators.",
                    parameterName);
            }

            return normalized;
        }

        private static bool IsAsciiLetterOrDigit(char character) =>
            character is >= 'A' and <= 'Z' or >= '0' and <= '9';
    }
}
