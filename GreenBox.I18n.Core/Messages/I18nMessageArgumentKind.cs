namespace GreenBox.I18n
{
    /// <summary>
    /// Describes the value kind expected by an external MessageFormat argument.
    /// </summary>
    public enum I18nMessageArgumentKind
    {
        /// <summary>The message does not impose a more specific value kind.</summary>
        Unspecified,

        /// <summary>The argument is used as text or by a string selector.</summary>
        String,

        /// <summary>The argument is formatted or selected as a number.</summary>
        Number,
    }
}
