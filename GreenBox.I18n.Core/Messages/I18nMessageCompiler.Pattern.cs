using System;
using System.Collections.Generic;

namespace GreenBox.I18n
{
    public static partial class I18nMessageCompiler
    {
        private static bool TryCompileParts(
            string pattern,
            int patternOffset,
            List<string> argumentNames,
            List<InputDeclaration>? declarations,
            out I18nCompiledMessage.MessagePart[] result,
            out I18nMessageDiagnostic diagnostic)
        {
            var parts = new List<I18nCompiledMessage.MessagePart>();
            var text = new System.Text.StringBuilder();
            int textStart = 0;

            for (int position = 0; position < pattern.Length; position++)
            {
                char character = pattern[position];
                if (character == '\\')
                {
                    if (position + 1 >= pattern.Length || !IsEscapable(pattern[position + 1]))
                    {
                        result = Array.Empty<I18nCompiledMessage.MessagePart>();
                        diagnostic = new I18nMessageDiagnostic(
                            I18nMessageDiagnosticCodes.InvalidEscape,
                            "Backslash must escape one of: backslash, '{', '|', or '}'.",
                            patternOffset + position);
                        return false;
                    }

                    text.Append(pattern[++position]);
                    continue;
                }

                if (character != '{')
                {
                    if (character == '}')
                    {
                        result = Array.Empty<I18nCompiledMessage.MessagePart>();
                        diagnostic = SyntaxDiagnostic(
                            "A literal closing brace in message text must be escaped.",
                            patternOffset + position);
                        return false;
                    }

                    text.Append(character);
                    continue;
                }

                if (position + 1 >= pattern.Length || pattern[position + 1] != '$')
                {
                    result = Array.Empty<I18nCompiledMessage.MessagePart>();
                    diagnostic = SyntaxDiagnostic(
                        "A literal opening brace in message text must be escaped.",
                        patternOffset + position);
                    return false;
                }

                int close = pattern.IndexOf('}', position + 2);
                if (close < 0)
                {
                    result = Array.Empty<I18nCompiledMessage.MessagePart>();
                    diagnostic = SyntaxDiagnostic("Variable expression has no closing brace.", patternOffset + position);
                    return false;
                }

                string expression = pattern.Substring(position + 2, close - position - 2).Trim();
                int annotationStart = expression.IndexOf(' ');
                string name = I18nUnicode.NormalizeNfc(annotationStart < 0
                    ? expression
                    : expression.Substring(0, annotationStart));
                if (!IsSupportedName(name))
                {
                    result = Array.Empty<I18nCompiledMessage.MessagePart>();
                    diagnostic = SyntaxDiagnostic(
                        "Variable name is empty or contains unsupported characters.",
                        patternOffset + position);
                    return false;
                }

                if (text.Length > 0)
                {
                    parts.Add(
                        new I18nCompiledMessage.MessagePart(
                            I18nCompiledMessage.MessagePartKind.Text,
                            text.ToString(),
                            patternOffset + textStart));
                    text.Clear();
                }

                InputDeclaration? declaration = declarations == null
                    ? null
                    : FindDeclaration(declarations, name);
                InputType valueType = declaration?.Type ?? InputType.Unspecified;
                I18nCompiledMessage.NumberOptions numberOptions =
                    declaration?.NumberOptions ?? I18nCompiledMessage.NumberOptions.Default;
                if (annotationStart >= 0)
                {
                    string annotation = expression.Substring(annotationStart).Trim();
                    if (!TryParseInputAnnotation(
                            annotation,
                            out valueType,
                            out numberOptions,
                            declaration))
                    {
                        result = Array.Empty<I18nCompiledMessage.MessagePart>();
                        diagnostic = SyntaxDiagnostic(
                            $"Variable annotation '{annotation}' contains an unsupported, incompatible, or invalid option.",
                            patternOffset + position + 2 + annotationStart);
                        return false;
                    }
                }

                bool isNumber = valueType == InputType.Number ||
                    valueType == InputType.OrdinalNumber ||
                    valueType == InputType.ExactNumber ||
                    valueType == InputType.Percent;
                string sourceName = declaration?.SourceName ?? name;
                parts.Add(
                    new I18nCompiledMessage.MessagePart(
                        isNumber
                            ? I18nCompiledMessage.MessagePartKind.NumberVariable
                            : I18nCompiledMessage.MessagePartKind.Variable,
                        sourceName,
                        patternOffset + position,
                        numberOptions));

                if (declaration == null && !ContainsArgument(argumentNames, sourceName))
                {
                    argumentNames.Add(sourceName);
                }

                position = close;
                textStart = close + 1;
            }

            if (text.Length > 0 || parts.Count == 0)
            {
                parts.Add(
                    new I18nCompiledMessage.MessagePart(
                        I18nCompiledMessage.MessagePartKind.Text,
                        text.ToString(),
                        patternOffset + textStart));
            }

            result = parts.ToArray();
            diagnostic = default;
            return true;
        }

        private static bool ContainsArgument(List<string> arguments, string name)
        {
            for (int index = 0; index < arguments.Count; index++)
            {
                if (I18nUnicode.EqualsNfc(arguments[index], name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadLiteral(
            string source,
            ref int position,
            out string value,
            out I18nMessageDiagnostic diagnostic)
        {
            int start = position;
            if (source[position] != '|')
            {
                while (position < source.Length && !char.IsWhiteSpace(source[position]))
                {
                    position++;
                }

                value = source.Substring(start, position - start);
                diagnostic = default;
                return true;
            }

            position++;
            var result = new System.Text.StringBuilder();
            while (position < source.Length)
            {
                char character = source[position++];
                if (character == '|')
                {
                    value = result.ToString();
                    diagnostic = default;
                    return true;
                }

                if (character == '\\')
                {
                    if (position >= source.Length || !IsEscapable(source[position]))
                    {
                        value = string.Empty;
                        diagnostic = new I18nMessageDiagnostic(
                            I18nMessageDiagnosticCodes.InvalidEscape,
                            "Quoted literal contains an invalid escape sequence.",
                            position - 1);
                        return false;
                    }

                    character = source[position++];
                }

                result.Append(character);
            }

            value = string.Empty;
            diagnostic = SyntaxDiagnostic("Quoted literal has no closing '|'.", start);
            return false;
        }

        private static bool IsEscapable(char character)
        {
            return character == '\\' || character == '{' || character == '|' || character == '}';
        }
    }
}
