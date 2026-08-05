namespace GreenBox.I18n
{
    /// <summary>
    /// Represents the result of an atomic operation affecting multiple catalog entries.
    /// </summary>
    public sealed class I18nBatchEditResult
    {
        private I18nBatchEditResult(bool hasChanges, I18nEditError? error)
        {
            HasChanges = hasChanges;
            Error = error;
        }

        /// <summary>Gets whether the operation completed successfully.</summary>
        public bool IsSuccess => Error == null;

        /// <summary>Gets whether the catalog was modified.</summary>
        public bool HasChanges { get; }

        /// <summary>Gets the expected operation error, if one occurred.</summary>
        public I18nEditError? Error { get; }

        internal static I18nBatchEditResult Success(bool hasChanges)
        {
            return new I18nBatchEditResult(hasChanges, null);
        }

        internal static I18nBatchEditResult Failure(string code, string message)
        {
            return new I18nBatchEditResult(false, new I18nEditError(code, message));
        }
    }
}
