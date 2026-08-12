#nullable enable

using GreenBox.I18n.Unity.Editor.Catalogs;

namespace GreenBox.I18n.Unity.Editor
{
    public static partial class I18nEditor
    {
        /// <summary>Gets localized text using the catalog default locale or an explicitly requested locale.</summary>
        public static string Text(long id, string? localeId = null)
        {
            if (id == 0)
            {
                return global::I18n.NonePlaceholder;
            }

            I18nRuntime runtime = I18nEditorLocalizationCache.Current.GetRuntime(localeId);
            return Report(id, runtime, runtime.Format(id));
        }

        /// <summary>Gets and formats localized text with one named argument.</summary>
        public static string Text<T1>(
            long id,
            (string Name, T1 Value) argument1,
            string? localeId = null)
        {
            if (id == 0)
            {
                return global::I18n.NonePlaceholder;
            }

            I18nRuntime runtime = I18nEditorLocalizationCache.Current.GetRuntime(localeId);
            return Report(id, runtime, runtime.Format(id, argument1));
        }

        /// <summary>Gets and formats localized text with two named arguments.</summary>
        public static string Text<T1, T2>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            string? localeId = null)
        {
            if (id == 0)
            {
                return global::I18n.NonePlaceholder;
            }

            I18nRuntime runtime = I18nEditorLocalizationCache.Current.GetRuntime(localeId);
            return Report(id, runtime, runtime.Format(id, argument1, argument2));
        }

        /// <summary>Gets and formats localized text with 3 named arguments.</summary>
        public static string Text<T1, T2, T3>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            string? localeId = null)
        {
            if (id == 0)
            {
                return global::I18n.NonePlaceholder;
            }

            I18nRuntime runtime = I18nEditorLocalizationCache.Current.GetRuntime(localeId);
            return Report(id, runtime, runtime.Format(id, argument1, argument2, argument3));
        }

        /// <summary>Gets and formats localized text with 4 named arguments.</summary>
        public static string Text<T1, T2, T3, T4>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            string? localeId = null)
        {
            if (id == 0)
            {
                return global::I18n.NonePlaceholder;
            }

            I18nRuntime runtime = I18nEditorLocalizationCache.Current.GetRuntime(localeId);
            return Report(id, runtime, runtime.Format(id, argument1, argument2, argument3, argument4));
        }

        /// <summary>Gets and formats localized text with 5 named arguments.</summary>
        public static string Text<T1, T2, T3, T4, T5>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            string? localeId = null)
        {
            if (id == 0)
            {
                return global::I18n.NonePlaceholder;
            }

            I18nRuntime runtime = I18nEditorLocalizationCache.Current.GetRuntime(localeId);
            return Report(
                id,
                runtime,
                runtime.Format(id, argument1, argument2, argument3, argument4, argument5));
        }

        /// <summary>Gets and formats localized text with 6 named arguments.</summary>
        public static string Text<T1, T2, T3, T4, T5, T6>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            string? localeId = null)
        {
            if (id == 0)
            {
                return global::I18n.NonePlaceholder;
            }

            I18nRuntime runtime = I18nEditorLocalizationCache.Current.GetRuntime(localeId);
            return Report(
                id,
                runtime,
                runtime.Format(
                    id, argument1, argument2, argument3, argument4, argument5, argument6));
        }

        /// <summary>Gets and formats localized text with 7 named arguments.</summary>
        public static string Text<T1, T2, T3, T4, T5, T6, T7>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7,
            string? localeId = null)
        {
            if (id == 0)
            {
                return global::I18n.NonePlaceholder;
            }

            I18nRuntime runtime = I18nEditorLocalizationCache.Current.GetRuntime(localeId);
            return Report(
                id,
                runtime,
                runtime.Format(
                    id, argument1, argument2, argument3, argument4,
                    argument5, argument6, argument7));
        }

        /// <summary>Gets and formats localized text with 8 named arguments.</summary>
        public static string Text<T1, T2, T3, T4, T5, T6, T7, T8>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7,
            (string Name, T8 Value) argument8,
            string? localeId = null)
        {
            if (id == 0)
            {
                return global::I18n.NonePlaceholder;
            }

            I18nRuntime runtime = I18nEditorLocalizationCache.Current.GetRuntime(localeId);
            return Report(
                id,
                runtime,
                runtime.Format(
                    id, argument1, argument2, argument3, argument4,
                    argument5, argument6, argument7, argument8));
        }

        /// <summary>Gets and formats localized text with an uncommon number of named arguments.</summary>
        public static string Text(
            long id,
            (string Name, object? Value)[] arguments,
            string? localeId = null)
        {
            if (id == 0)
            {
                return global::I18n.NonePlaceholder;
            }

            I18nRuntime runtime = I18nEditorLocalizationCache.Current.GetRuntime(localeId);
            return Report(id, runtime, runtime.Format(id, arguments));
        }

        private static string Report(
            long id,
            I18nRuntime runtime,
            I18nMessageFormatResult result)
        {
            return I18nRuntimeDiagnosticReporter.Report(id, runtime.CurrentLocale.Id, result);
        }
    }
}
