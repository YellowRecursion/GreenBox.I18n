using System;

namespace GreenBox.I18n
{
    /// <summary>
    /// Contains stable machine-readable codes produced while compiling or formatting messages.
    /// </summary>
    public static class I18nMessageDiagnosticCodes
    {
        /// <summary>Indicates that a message does not follow the supported syntax.</summary>
        public const string InvalidSyntax = "invalid_syntax";

        /// <summary>Indicates that a value required by a message was not supplied.</summary>
        public const string MissingArgument = "missing_argument";

        /// <summary>Indicates that a matcher has no mandatory catch-all variant.</summary>
        public const string MissingFallbackVariant = "missing_fallback_variant";

        /// <summary>Indicates that a matcher variant does not have one key per selector.</summary>
        public const string VariantKeyMismatch = "variant_key_mismatch";

        /// <summary>Indicates that a backslash is followed by a character that cannot be escaped.</summary>
        public const string InvalidEscape = "invalid_escape";

        /// <summary>Indicates that a valid operation cannot be represented by the runtime numeric type.</summary>
        public const string UnsupportedOperation = "unsupported_operation";
    }

    /// <summary>
    /// Describes a problem found while compiling or formatting a localized message.
    /// </summary>
    public readonly struct I18nMessageDiagnostic
    {
        internal I18nMessageDiagnostic(string code, string message, int position, string? argumentName = null)
        {
            Code = code;
            Message = message;
            Position = position;
            ArgumentName = argumentName;
        }

        /// <summary>Gets the stable machine-readable diagnostic code.</summary>
        public string Code { get; }

        /// <summary>Gets the human-readable explanation.</summary>
        public string Message { get; }

        /// <summary>Gets the zero-based source position, or -1 when no source position applies.</summary>
        public int Position { get; }

        /// <summary>Gets the affected argument name, when the diagnostic belongs to an argument.</summary>
        public string? ArgumentName { get; }
    }
}
