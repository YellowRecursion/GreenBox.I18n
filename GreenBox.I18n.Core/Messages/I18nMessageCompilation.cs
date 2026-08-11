using System;
using System.Collections.Generic;

namespace GreenBox.I18n
{
    /// <summary>
    /// Contains a prepared message or the problems that prevented it from being prepared.
    /// </summary>
    public sealed class I18nMessageCompilation
    {
        internal I18nMessageCompilation(
            I18nCompiledMessage? message,
            IReadOnlyList<I18nMessageDiagnostic> diagnostics)
        {
            Message = message;
            Diagnostics = diagnostics;
        }

        /// <summary>Gets the prepared message, or null when compilation failed.</summary>
        public I18nCompiledMessage? Message { get; }

        /// <summary>Gets all compilation diagnostics in source order.</summary>
        public IReadOnlyList<I18nMessageDiagnostic> Diagnostics { get; }

        /// <summary>Gets whether the message was compiled successfully.</summary>
        public bool IsSuccess => Message != null;
    }

    /// <summary>
    /// Contains formatted text and non-throwing runtime diagnostics.
    /// </summary>
    public readonly struct I18nMessageFormatResult
    {
        private readonly IReadOnlyList<I18nMessageDiagnostic>? _diagnostics;

        internal I18nMessageFormatResult(string text, IReadOnlyList<I18nMessageDiagnostic> diagnostics)
        {
            Text = text;
            _diagnostics = diagnostics;
        }

        /// <summary>Gets the formatted text, including readable placeholders for missing values.</summary>
        public string Text { get; }

        /// <summary>Gets all formatting diagnostics.</summary>
        public IReadOnlyList<I18nMessageDiagnostic> Diagnostics =>
            _diagnostics ?? Array.Empty<I18nMessageDiagnostic>();

        /// <summary>Gets whether formatting completed without diagnostics.</summary>
        public bool IsSuccess => _diagnostics == null || _diagnostics.Count == 0;
    }
}
