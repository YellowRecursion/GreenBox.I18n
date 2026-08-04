using System;

namespace GreenBox.I18n
{
    /// <summary>
    /// Provides data for an <see cref="I18nRuntime.LocaleChanged"/> event.
    /// </summary>
    public sealed class I18nLocaleChangedEventArgs : EventArgs
    {
        internal I18nLocaleChangedEventArgs(
            I18nRuntimeLocale previousLocale,
            I18nRuntimeLocale currentLocale)
        {
            PreviousLocale = previousLocale;
            CurrentLocale = currentLocale;
        }

        /// <summary>
        /// Gets the locale that was active before the change.
        /// </summary>
        public I18nRuntimeLocale PreviousLocale { get; }

        /// <summary>
        /// Gets the locale that is active after the change.
        /// </summary>
        public I18nRuntimeLocale CurrentLocale { get; }
    }
}
