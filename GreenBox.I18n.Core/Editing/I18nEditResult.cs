namespace GreenBox.I18n
{
    /// <summary>
    /// Contains stable machine-readable codes produced by catalog editing operations.
    /// </summary>
    public static class I18nEditCodes
    {
        /// <summary>Indicates that an entry ID has an invalid value.</summary>
        public const string InvalidId = "invalid_id";

        /// <summary>Indicates that no entry has the requested ID.</summary>
        public const string EntryNotFound = "entry_not_found";

        /// <summary>Indicates that one entry occurs more than once in a batch operation.</summary>
        public const string DuplicateEntryMove = "duplicate_entry_move";

        /// <summary>Indicates that the requested path has an invalid format.</summary>
        public const string InvalidPath = "invalid_path";

        /// <summary>Indicates that another entry already uses the requested path.</summary>
        public const string DuplicatePath = "duplicate_path";

        /// <summary>Indicates that the requested locale is not declared by the catalog.</summary>
        public const string UnknownLocale = "unknown_locale";

        /// <summary>Indicates that a unique random entry ID could not be allocated.</summary>
        public const string IdSpaceExhausted = "id_space_exhausted";

        /// <summary>Indicates that an entry delta would produce an invalid catalog.</summary>
        public const string InvalidEntryDelta = "invalid_entry_delta";
    }

    /// <summary>
    /// Describes an expected failure produced by a catalog editing operation.
    /// </summary>
    public sealed class I18nEditError
    {
        internal I18nEditError(string code, string message)
        {
            Code = code;
            Message = message;
        }

        /// <summary>
        /// Gets the stable machine-readable error code.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the human-readable description of the error.
        /// </summary>
        public string Message { get; }
    }

    /// <summary>
    /// Represents the result of an operation that edits one catalog entry.
    /// </summary>
    public sealed class I18nEditResult
    {
        private I18nEditResult(I18nEntry? entry, bool hasChanges, I18nEditError? error)
        {
            Entry = entry;
            HasChanges = hasChanges;
            Error = error;
        }

        /// <summary>
        /// Gets a value indicating whether the operation completed successfully.
        /// </summary>
        public bool IsSuccess => Error == null;

        /// <summary>
        /// Gets a value indicating whether the operation changed the catalog.
        /// </summary>
        public bool HasChanges { get; }

        /// <summary>
        /// Gets the affected entry when the operation completed successfully.
        /// </summary>
        public I18nEntry? Entry { get; }

        /// <summary>
        /// Gets the expected operation error, or <see langword="null"/> after success.
        /// </summary>
        public I18nEditError? Error { get; }

        internal static I18nEditResult Success(I18nEntry entry, bool hasChanges)
        {
            return new I18nEditResult(entry, hasChanges, null);
        }

        internal static I18nEditResult Failure(string code, string message)
        {
            return new I18nEditResult(null, false, new I18nEditError(code, message));
        }
    }
}
