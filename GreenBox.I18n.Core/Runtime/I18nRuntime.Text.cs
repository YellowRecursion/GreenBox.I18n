namespace GreenBox.I18n
{
    public sealed partial class I18nRuntime
    {
        /// <summary>Gets localized text and discards non-throwing formatting diagnostics.</summary>
        public string Text(long id) => Format(id).Text;

        /// <summary>Gets localized text with one named argument.</summary>
        public string Text<T1>(long id, (string Name, T1 Value) argument1) =>
            Format(id, argument1).Text;

        /// <summary>Gets localized text with two named arguments.</summary>
        public string Text<T1, T2>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2) => Format(id, argument1, argument2).Text;

        /// <summary>Gets localized text with three named arguments.</summary>
        public string Text<T1, T2, T3>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3) => Format(id, argument1, argument2, argument3).Text;

        /// <summary>Gets localized text with four named arguments.</summary>
        public string Text<T1, T2, T3, T4>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4) => Format(id, argument1, argument2, argument3, argument4).Text;

        /// <summary>Gets localized text with five named arguments.</summary>
        public string Text<T1, T2, T3, T4, T5>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5) => Format(id, argument1, argument2, argument3, argument4, argument5).Text;

        /// <summary>Gets localized text with six named arguments.</summary>
        public string Text<T1, T2, T3, T4, T5, T6>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6) =>
            Format(id, argument1, argument2, argument3, argument4, argument5, argument6).Text;

        /// <summary>Gets localized text with seven named arguments.</summary>
        public string Text<T1, T2, T3, T4, T5, T6, T7>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7) =>
            Format(id, argument1, argument2, argument3, argument4, argument5, argument6, argument7).Text;

        /// <summary>Gets localized text with eight named arguments.</summary>
        public string Text<T1, T2, T3, T4, T5, T6, T7, T8>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7,
            (string Name, T8 Value) argument8) =>
            Format(id, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8).Text;

        /// <summary>Gets localized text with an uncommon number of named arguments.</summary>
        public string Text(long id, params (string Name, object? Value)[] arguments) =>
            Format(id, arguments).Text;
    }
}
