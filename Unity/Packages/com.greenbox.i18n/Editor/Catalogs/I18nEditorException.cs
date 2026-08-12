#nullable enable

using System;

namespace GreenBox.I18n.Unity.Editor
{
    /// <summary>
    /// Describes a catalog edit that GreenBox I18n could not safely complete.
    /// </summary>
    public sealed class I18nEditorException : InvalidOperationException
    {
        internal I18nEditorException(string message)
            : base(message)
        {
        }

        internal I18nEditorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
