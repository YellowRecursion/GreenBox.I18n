using System;
using System.Collections.Generic;
using System.Globalization;

namespace GreenBox.I18n
{
    public static partial class I18nMessageCompiler
    {
        private static I18nMessageCompilation CompileMatcher(
            string source,
            int bodyOffset,
            List<InputDeclaration> declarations,
            List<string> argumentNames)
        {
            int position = bodyOffset + ".match".Length;
            int matchLineEnd = source.IndexOf('\n', position);
            if (matchLineEnd < 0)
            {
                return Failed("Matcher must contain at least one variant.", position);
            }

            var selectors = new List<I18nCompiledMessage.MessageSelector>();
            while (position < matchLineEnd)
            {
                SkipHorizontalWhitespace(source, ref position, matchLineEnd);
                if (position >= matchLineEnd)
                {
                    break;
                }

                if (source[position] != '$')
                {
                    return Failed("Matcher selector must be a declared variable.", position);
                }

                int selectorStart = ++position;
                while (position < matchLineEnd && IsNameCharacter(source[position]))
                {
                    position++;
                }

                string selectorName = I18nUnicode.NormalizeNfc(
                    source.Substring(selectorStart, position - selectorStart));
                InputDeclaration? declaration = FindDeclaration(declarations, selectorName);
                if (declaration == null || declaration.Value.Type == InputType.Unspecified)
                {
                    return Failed("Matcher selector must be declared with a supported annotation.", selectorStart);
                }

                selectors.Add(
                    new I18nCompiledMessage.MessageSelector(
                        declaration.Value.SourceName,
                        declaration.Value.Type switch
                        {
                            InputType.Number => I18nCompiledMessage.MessageSelectorKind.CardinalNumber,
                            InputType.OrdinalNumber => I18nCompiledMessage.MessageSelectorKind.OrdinalNumber,
                            InputType.ExactNumber => I18nCompiledMessage.MessageSelectorKind.ExactNumber,
                            InputType.Percent => I18nCompiledMessage.MessageSelectorKind.CardinalNumber,
                            _ => I18nCompiledMessage.MessageSelectorKind.String,
                        },
                        declaration.Value.NumberOptions));
            }

            if (selectors.Count == 0)
            {
                return Failed("Matcher must contain at least one selector.", bodyOffset);
            }

            position = matchLineEnd + 1;
            var variants = new List<I18nCompiledMessage.MessageVariant>();
            var variantKeys = new HashSet<string>(StringComparer.Ordinal);
            bool hasWildcard = false;

            while (position < source.Length)
            {
                SkipWhitespace(source, ref position);
                if (position >= source.Length)
                {
                    break;
                }

                int variantStart = position;
                var keys = new I18nCompiledMessage.MessageVariantKey[selectors.Count];
                var keyIdentity = new System.Text.StringBuilder();
                bool allWildcard = true;
                for (int selectorIndex = 0; selectorIndex < selectors.Count; selectorIndex++)
                {
                    SkipWhitespace(source, ref position);
                    if (position >= source.Length ||
                        (position + 1 < source.Length && source[position] == '{' && source[position + 1] == '{'))
                    {
                        return FailedVariantKeyMismatch(variantStart);
                    }

                    int keyStart = position;
                    if (!TryReadLiteral(
                            source,
                            ref position,
                            out string key,
                            out I18nMessageDiagnostic keyDiagnostic))
                    {
                        return Failed(keyDiagnostic);
                    }

                    if (!TryCompileVariantKey(
                            selectors[selectorIndex].Kind,
                            key,
                            out I18nCompiledMessage.MessageVariantKey compiledKey))
                    {
                        return Failed($"Matcher key '{key}' is invalid for its selector.", keyStart);
                    }

                    keys[selectorIndex] = compiledKey;
                    allWildcard &= compiledKey.Kind == I18nCompiledMessage.MessageVariantKeyKind.Wildcard;
                    if (selectorIndex > 0)
                    {
                        keyIdentity.Append('\u001f');
                    }

                    keyIdentity.Append(compiledKey.Value);
                }

                if (!variantKeys.Add(keyIdentity.ToString()))
                {
                    return Failed("Matcher variant keys are duplicated.", variantStart);
                }

                SkipWhitespace(source, ref position);
                if (!TryReadQuotedPattern(source, position, out string pattern, out int patternOffset, out int end))
                {
                    return FailedVariantKeyMismatch(variantStart);
                }

                if (!TryCompileParts(
                        pattern,
                        patternOffset,
                        argumentNames,
                        declarations,
                        out I18nCompiledMessage.MessagePart[] parts,
                        out I18nMessageDiagnostic patternDiagnostic))
                {
                    return Failed(patternDiagnostic);
                }

                variants.Add(new I18nCompiledMessage.MessageVariant(keys, parts));
                hasWildcard |= allWildcard;
                position = end;
            }

            if (!hasWildcard)
            {
                return new I18nMessageCompilation(
                    null,
                    new[]
                    {
                        new I18nMessageDiagnostic(
                            I18nMessageDiagnosticCodes.MissingFallbackVariant,
                            "Matcher must contain a catch-all '*' variant.",
                            bodyOffset),
                    });
            }

            return new I18nMessageCompilation(
                new I18nCompiledMessage(
                    Array.Empty<I18nCompiledMessage.MessagePart>(),
                    new I18nCompiledMessage.MessageMatcher(selectors.ToArray(), variants.ToArray()),
                    argumentNames),
                Array.Empty<I18nMessageDiagnostic>());
        }

        private static InputDeclaration? FindDeclaration(List<InputDeclaration> declarations, string name)
        {
            for (int i = 0; i < declarations.Count; i++)
            {
                if (string.Equals(declarations[i].Name, name, StringComparison.Ordinal))
                {
                    return declarations[i];
                }
            }

            return null;
        }

        private static bool TryCompileVariantKey(
            I18nCompiledMessage.MessageSelectorKind selectorKind,
            string key,
            out I18nCompiledMessage.MessageVariantKey result)
        {
            if (key == "*")
            {
                result = new I18nCompiledMessage.MessageVariantKey(
                    I18nCompiledMessage.MessageVariantKeyKind.Wildcard, key, default);
                return true;
            }

            if (selectorKind == I18nCompiledMessage.MessageSelectorKind.String)
            {
                result = new I18nCompiledMessage.MessageVariantKey(
                    I18nCompiledMessage.MessageVariantKeyKind.String,
                    I18nUnicode.NormalizeNfc(key),
                    default);
                return true;
            }

            if (IsCanonicalInteger(key) &&
                decimal.TryParse(
                    key,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out decimal number))
            {
                result = new I18nCompiledMessage.MessageVariantKey(
                    I18nCompiledMessage.MessageVariantKeyKind.ExactNumber, key, number);
                return true;
            }

            if (selectorKind == I18nCompiledMessage.MessageSelectorKind.ExactNumber)
            {
                result = default;
                return false;
            }

            if (PluralCategories.Contains(key))
            {
                result = new I18nCompiledMessage.MessageVariantKey(
                    I18nCompiledMessage.MessageVariantKeyKind.Category, key, default);
                return true;
            }

            result = default;
            return false;
        }

        private static bool IsCanonicalInteger(string value)
        {
            if (value.Length == 0 || value[0] == '+')
            {
                return false;
            }

            int digitStart = value[0] == '-' ? 1 : 0;
            if (digitStart == value.Length ||
                (value[digitStart] == '0' && value.Length - digitStart != 1) ||
                (digitStart == 1 && value.Length == 2 && value[1] == '0'))
            {
                return false;
            }

            for (int index = digitStart; index < value.Length; index++)
            {
                if (value[index] < '0' || value[index] > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static I18nMessageCompilation FailedVariantKeyMismatch(int position)
        {
            return new I18nMessageCompilation(
                null,
                new[]
                {
                    new I18nMessageDiagnostic(
                        I18nMessageDiagnosticCodes.VariantKeyMismatch,
                        "Every matcher variant must have exactly one key per selector.",
                        position),
                });
        }
    }
}
