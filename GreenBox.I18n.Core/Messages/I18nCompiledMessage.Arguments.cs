using System;
using System.Globalization;
using System.Text;

namespace GreenBox.I18n
{
    public sealed partial class I18nCompiledMessage
    {
        internal interface IArgumentSource
        {
            bool TryAppend(string name, StringBuilder output);

            bool TryGetNumber(string name, out decimal value);

            bool TryGetString(string name, out string value);
        }

        private readonly struct NoArguments : IArgumentSource
        {
            public bool TryAppend(string name, StringBuilder output) => false;

            public bool TryGetNumber(string name, out decimal value)
            {
                value = default;
                return false;
            }

            public bool TryGetString(string name, out string value)
            {
                value = string.Empty;
                return false;
            }
        }

        private readonly struct OneArgument<T1> : IArgumentSource
        {
            private readonly string _name1;
            private readonly T1 _value1;

            public OneArgument(string name1, T1 value1)
            {
                _name1 = name1;
                _value1 = value1;
            }

            public bool TryAppend(string name, StringBuilder output)
            {
                if (!I18nUnicode.EqualsNfc(name, _name1))
                {
                    return false;
                }

                AppendValue(output, _value1);
                return true;
            }

            public bool TryGetNumber(string name, out decimal value)
            {
                if (I18nUnicode.EqualsNfc(name, _name1))
                {
                    return TryConvertNumber(_value1, out value);
                }

                value = default;
                return false;
            }

            public bool TryGetString(string name, out string value)
            {
                if (I18nUnicode.EqualsNfc(name, _name1))
                {
                    value = ConvertToString(_value1);
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }

        private readonly struct TwoArguments<T1, T2> : IArgumentSource
        {
            private readonly string _name1;
            private readonly T1 _value1;
            private readonly string _name2;
            private readonly T2 _value2;

            public TwoArguments(string name1, T1 value1, string name2, T2 value2)
            {
                _name1 = name1;
                _value1 = value1;
                _name2 = name2;
                _value2 = value2;
            }

            public bool TryAppend(string name, StringBuilder output)
            {
                if (I18nUnicode.EqualsNfc(name, _name1))
                {
                    AppendValue(output, _value1);
                    return true;
                }

                if (I18nUnicode.EqualsNfc(name, _name2))
                {
                    AppendValue(output, _value2);
                    return true;
                }

                return false;
            }

            public bool TryGetNumber(string name, out decimal value)
            {
                if (I18nUnicode.EqualsNfc(name, _name1))
                {
                    return TryConvertNumber(_value1, out value);
                }

                if (I18nUnicode.EqualsNfc(name, _name2))
                {
                    return TryConvertNumber(_value2, out value);
                }

                value = default;
                return false;
            }

            public bool TryGetString(string name, out string value)
            {
                if (I18nUnicode.EqualsNfc(name, _name1))
                {
                    value = ConvertToString(_value1);
                    return true;
                }

                if (I18nUnicode.EqualsNfc(name, _name2))
                {
                    value = ConvertToString(_value2);
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }

        private readonly struct CombinedArguments<TLeft, TRight> : IArgumentSource
            where TLeft : struct, IArgumentSource
            where TRight : struct, IArgumentSource
        {
            private readonly TLeft _left;
            private readonly TRight _right;

            public CombinedArguments(TLeft left, TRight right)
            {
                _left = left;
                _right = right;
            }

            public bool TryAppend(string name, StringBuilder output)
            {
                return _left.TryAppend(name, output) || _right.TryAppend(name, output);
            }

            public bool TryGetNumber(string name, out decimal value)
            {
                return _left.TryGetNumber(name, out value) || _right.TryGetNumber(name, out value);
            }

            public bool TryGetString(string name, out string value)
            {
                return _left.TryGetString(name, out value) || _right.TryGetString(name, out value);
            }
        }

        private readonly struct ManyArguments : IArgumentSource
        {
            private readonly (string Name, object? Value)[] _arguments;

            public ManyArguments((string Name, object? Value)[] arguments)
            {
                _arguments = arguments;
            }

            public bool TryAppend(string name, StringBuilder output)
            {
                for (int i = 0; i < _arguments.Length; i++)
                {
                    if (I18nUnicode.EqualsNfc(name, _arguments[i].Name))
                    {
                        AppendValue(output, _arguments[i].Value);
                        return true;
                    }
                }

                return false;
            }

            public bool TryGetNumber(string name, out decimal value)
            {
                for (int i = 0; i < _arguments.Length; i++)
                {
                    if (I18nUnicode.EqualsNfc(name, _arguments[i].Name))
                    {
                        return TryConvertNumber(_arguments[i].Value, out value);
                    }
                }

                value = default;
                return false;
            }

            public bool TryGetString(string name, out string value)
            {
                for (int i = 0; i < _arguments.Length; i++)
                {
                    if (I18nUnicode.EqualsNfc(name, _arguments[i].Name))
                    {
                        value = ConvertToString(_arguments[i].Value);
                        return true;
                    }
                }

                value = string.Empty;
                return false;
            }
        }

        private static CombinedArguments<TLeft, TRight> Combine<TLeft, TRight>(TLeft left, TRight right)
            where TLeft : struct, IArgumentSource
            where TRight : struct, IArgumentSource
        {
            return new CombinedArguments<TLeft, TRight>(left, right);
        }

        private static string ConvertToString<T>(T value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static bool TryConvertNumber<T>(T value, out decimal number)
        {
            try
            {
                if (value == null || value is bool || value is char)
                {
                    number = default;
                    return false;
                }

                number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is InvalidCastException ||
                exception is OverflowException)
            {
                number = default;
                return false;
            }
        }

        private static void AppendValue<T>(StringBuilder output, T value)
        {
            if (value == null)
            {
                return;
            }

            if (value is string text)
            {
                output.Append(text);
                return;
            }

            if (value is IFormattable formattable)
            {
                output.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                return;
            }

            output.Append(value.ToString());
        }
    }
}
