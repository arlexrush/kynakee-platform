namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    public sealed record GeoLocation
    {
        public string Country { get; }
        public string? Region { get; }
        public string? Province { get; }
        public string? Municipality { get; }
        public string? PostalCode { get; }
        public string? Address { get; }
        public decimal? Latitude { get; }
        public decimal? Longitude { get; }

        public GeoLocation(
            string country,
            string? region = null,
            string? province = null,
            string? municipality = null,
            string? postalCode = null,
            string? address = null,
            decimal? latitude = null,
            decimal? longitude = null)
        {
            if (string.IsNullOrWhiteSpace(country))
            {
                throw new ArgumentException(
                    "Country is required.",
                    nameof(country));
            }

            var normalizedCountry = country.Trim().ToUpperInvariant();

            if (normalizedCountry.Length != 2 ||
                normalizedCountry.Any(character => character is < 'A' or > 'Z'))
            {
                throw new ArgumentException(
                    "Country must be a two-letter ISO 3166-1 alpha-2 code.",
                    nameof(country));
            }

            if (latitude is < -90 or > 90)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(latitude),
                    "Latitude must be between -90 and 90.");
            }

            if (longitude is < -180 or > 180)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(longitude),
                    "Longitude must be between -180 and 180.");
            }

            if (latitude.HasValue != longitude.HasValue)
            {
                throw new ArgumentException(
                    "Latitude and longitude must be provided together.",
                    latitude.HasValue ? nameof(longitude) : nameof(latitude));
            }

            if (latitude.HasValue && string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException(
                    "Address must be provided if latitude is provided.",
                    nameof(address));
            }

            Country = normalizedCountry;
            Region = Normalize(region);
            Province = Normalize(province);
            Municipality = Normalize(municipality);
            PostalCode = Normalize(postalCode);
            Address = Normalize(address);
            Latitude = latitude;
            Longitude = longitude;
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
