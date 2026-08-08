using GreenBox.I18n.Unity;

namespace GreenBox.I18n.Development
{
    /// <summary>
    /// Provides deterministic IL patterns for the editor usage-scanner self-test.
    /// This code is compiled but never needs to be executed.
    /// </summary>
    internal static class I18nUsageScanTestFixture
    {
        private const long ConstantEntryId = 3857333080842830453;
        private static readonly long StaticReadonlyEntryId = 3857333080842831200;

        private static void EmitCases()
        {
            // Direct 64-bit literal.
            _ = global::I18n.Text(3857333080842830204);

            // A const field is inlined at the usage location.
            _ = global::I18n.Text(ConstantEntryId);

            // A literal stored in a local remains an IL constant at this location.
            long localEntryId = 3857333080842830706;
            _ = global::I18n.Text(localEntryId);

            // Two equal ID, file, and line locations must be deduplicated.
            _ = global::I18n.Text(3857333080842830951); _ = global::I18n.Text(3857333080842830951);

            // This line loads a field; the literal itself exists in the type initializer above.
            _ = global::I18n.Text(StaticReadonlyEntryId);

            NestedUsage.Emit();

            // A valid-looking ID in a comment must not become IL: 3857333080842831726.

#if GREENBOX_I18N_USAGE_SCAN_DISABLED
            // This symbol is intentionally undefined, so the compiler must remove the whole branch.
            _ = i18n.Text(3857333080842831939);
#endif
        }

        private static class NestedUsage
        {
            public static void Emit()
            {
                _ = global::I18n.Text(3857333080842831465);
            }
        }
    }
}
