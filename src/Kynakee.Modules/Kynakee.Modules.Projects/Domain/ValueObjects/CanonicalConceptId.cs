using System.Text.RegularExpressions;

namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    /// <summary>
    /// Identificador inmutable y normalizado para un concepto canónico.
    /// </summary>
    /// <remarks>Normaliza el valor aplicando Trim y ToUpperInvariant; valida que no esté vacío, que no supere
    /// 100 caracteres y que solo contenga letras mayúsculas, dígitos y guiones bajos (A-Z, 0-9, _). Se lanza
    /// ArgumentException si las condiciones no se cumplen. ToString() devuelve el valor normalizado.</remarks>
    public sealed record CanonicalConceptId
    {
        private static readonly Regex Format =
            new(
                "^[A-Z0-9_]+$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Value { get; }

        public CanonicalConceptId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Canonical concept identifier is required.",
                    nameof(value));
            }

            var normalized = value.Trim().ToUpperInvariant();

            if (normalized.Length > 100)
            {
                throw new ArgumentException(
                    "Canonical concept identifier cannot exceed 100 characters.",
                    nameof(value));
            }

            if (!Format.IsMatch(normalized))
            {
                throw new ArgumentException(
                    "Canonical concept identifier must contain only uppercase letters, numbers and underscores.",
                    nameof(value));
            }

            Value = normalized;
        }

        public override string ToString() => Value;
    }
}
