namespace GreenBox.I18n
{
    /// <summary>
    /// Describes the destination path of one entry in an atomic move operation.
    /// </summary>
    public sealed class I18nEntryMove
    {
        /// <summary>
        /// Creates an entry move.
        /// </summary>
        /// <param name="id">The stable entry ID.</param>
        /// <param name="path">The destination path.</param>
        public I18nEntryMove(long id, string? path)
        {
            Id = id;
            Path = path;
        }

        /// <summary>Gets the stable entry ID.</summary>
        public long Id { get; }

        /// <summary>Gets the destination path.</summary>
        public string? Path { get; }
    }
}
