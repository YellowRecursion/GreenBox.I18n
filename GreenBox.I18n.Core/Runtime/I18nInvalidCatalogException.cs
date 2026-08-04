using System;

namespace GreenBox.I18n
{
    /// <summary>
    /// Represents an attempt to create a runtime from an invalid source catalog.
    /// </summary>
    public sealed class I18nInvalidCatalogException : ArgumentException
    {
        /// <summary>
        /// Initializes a new invalid catalog exception.
        /// </summary>
        /// <param name="validationResult">The validation result that rejected the catalog.</param>
        public I18nInvalidCatalogException(I18nValidationResult validationResult)
            : base(CreateMessage(validationResult), "catalog")
        {
            ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        }

        /// <summary>
        /// Gets the complete validation result that rejected the catalog.
        /// </summary>
        public I18nValidationResult ValidationResult { get; }

        private static string CreateMessage(I18nValidationResult validationResult)
        {
            if (validationResult == null)
            {
                throw new ArgumentNullException(nameof(validationResult));
            }

            if (validationResult.Diagnostics.Count == 0)
            {
                return "The catalog is invalid.";
            }

            return $"The catalog is invalid: {validationResult.Diagnostics[0]}";
        }
    }
}
