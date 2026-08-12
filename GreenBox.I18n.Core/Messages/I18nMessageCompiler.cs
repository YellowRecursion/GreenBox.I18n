using System;
using System.Collections.Generic;
using System.Globalization;

namespace GreenBox.I18n
{
    /// <summary>
    /// Parses the supported Unicode MessageFormat 2 syntax into reusable messages.
    /// </summary>
    public static partial class I18nMessageCompiler
    {
        private static readonly HashSet<string> PluralCategories = new HashSet<string>(StringComparer.Ordinal)
        {
            "zero",
            "one",
            "two",
            "few",
            "many",
            "other",
        };

        /// <summary>Compiles message source without throwing for invalid message syntax.</summary>
        /// <param name="source">The MessageFormat 2 source.</param>
        /// <returns>A prepared message or syntax diagnostics.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        public static I18nMessageCompilation Compile(string source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!source.StartsWith(".input", StringComparison.Ordinal))
            {
                return CompileSimplePattern(source, 0, new List<string>(), null);
            }

            if (!TryParseDeclarations(
                    source,
                    out int bodyOffset,
                    out List<InputDeclaration> declarations,
                    out I18nMessageDiagnostic declarationDiagnostic))
            {
                return Failed(declarationDiagnostic);
            }

            var argumentNames = new List<string>(declarations.Count);
            for (int i = 0; i < declarations.Count; i++)
            {
                if (declarations[i].IsInput)
                {
                    argumentNames.Add(declarations[i].Name);
                }
            }

            if (source.AsSpan(bodyOffset).StartsWith("{{".AsSpan(), StringComparison.Ordinal))
            {
                if (!TryReadQuotedPattern(source, bodyOffset, out string pattern, out int patternOffset, out int end) ||
                    !IsOnlyWhitespace(source, end))
                {
                    return Failed("Expected one complete quoted pattern after the declarations.", bodyOffset);
                }

                return CompileSimplePattern(pattern, patternOffset, argumentNames, declarations);
            }

            if (source.AsSpan(bodyOffset).StartsWith(".match".AsSpan(), StringComparison.Ordinal))
            {
                return CompileMatcher(source, bodyOffset, declarations, argumentNames);
            }

            return Failed("Expected a quoted pattern or matcher after the message declarations.", bodyOffset);
        }

        private static I18nMessageCompilation CompileSimplePattern(
            string pattern,
            int patternOffset,
            List<string> argumentNames,
            List<InputDeclaration>? declarations)
        {
            if (!TryCompileParts(
                    pattern,
                    patternOffset,
                    argumentNames,
                    declarations,
                    out I18nCompiledMessage.MessagePart[] parts,
                    out I18nMessageDiagnostic diagnostic))
            {
                return Failed(diagnostic);
            }

            return new I18nMessageCompilation(
                new I18nCompiledMessage(
                    parts,
                    null,
                    argumentNames,
                    BuildArgumentKinds(argumentNames, declarations, parts)),
                Array.Empty<I18nMessageDiagnostic>());
        }

        private static List<I18nMessageArgumentKind> BuildArgumentKinds(
            List<string> argumentNames,
            List<InputDeclaration>? declarations,
            I18nCompiledMessage.MessagePart[]? parts = null)
        {
            var result = new List<I18nMessageArgumentKind>(argumentNames.Count);
            for (int index = 0; index < argumentNames.Count; index++)
            {
                InputDeclaration? declaration = declarations == null
                    ? null
                    : FindDeclaration(declarations, argumentNames[index]);
                I18nMessageArgumentKind kind = declaration?.Type switch
                {
                    InputType.String => I18nMessageArgumentKind.String,
                    InputType.Number or
                    InputType.OrdinalNumber or
                    InputType.ExactNumber or
                    InputType.Percent => I18nMessageArgumentKind.Number,
                    _ => I18nMessageArgumentKind.Unspecified,
                };

                if (kind == I18nMessageArgumentKind.Unspecified && parts != null)
                {
                    for (int partIndex = 0; partIndex < parts.Length; partIndex++)
                    {
                        if (parts[partIndex].Kind == I18nCompiledMessage.MessagePartKind.NumberVariable &&
                            string.Equals(parts[partIndex].Value, argumentNames[index], StringComparison.Ordinal))
                        {
                            kind = I18nMessageArgumentKind.Number;
                            break;
                        }
                    }
                }

                result.Add(kind);
            }

            return result;
        }

        private static bool TryParseDeclarations(
            string source,
            out int bodyOffset,
            out List<InputDeclaration> declarations,
            out I18nMessageDiagnostic diagnostic)
        {
            declarations = new List<InputDeclaration>();
            bodyOffset = 0;

            while (bodyOffset < source.Length &&
                   source.AsSpan(bodyOffset).StartsWith(".input".AsSpan(), StringComparison.Ordinal))
            {
                int lineEnd = source.IndexOf('\n', bodyOffset);
                if (lineEnd < 0)
                {
                    diagnostic = SyntaxDiagnostic("Input declaration must be followed by a message body.", bodyOffset);
                    return false;
                }

                string declaration = source.Substring(bodyOffset, lineEnd - bodyOffset).TrimEnd('\r');
                const string prefix = ".input {$";
                if (!declaration.StartsWith(prefix, StringComparison.Ordinal) ||
                    !declaration.EndsWith("}", StringComparison.Ordinal))
                {
                    diagnostic = SyntaxDiagnostic("Input declaration has invalid syntax.", bodyOffset);
                    return false;
                }

                string expression = declaration.Substring(prefix.Length, declaration.Length - prefix.Length - 1).Trim();
                string name;
                InputType type;
                I18nCompiledMessage.NumberOptions numberOptions = I18nCompiledMessage.NumberOptions.Default;
                int annotation = expression.IndexOf(' ');
                if (annotation < 0)
                {
                    name = I18nUnicode.NormalizeNfc(expression);
                    type = InputType.Unspecified;
                }
                else
                {
                    name = I18nUnicode.NormalizeNfc(expression.Substring(0, annotation));
                    string annotationText = expression.Substring(annotation).Trim();
                    if (!TryParseInputAnnotation(annotationText, out type, out numberOptions))
                    {
                        diagnostic = SyntaxDiagnostic(
                            $"Input annotation '{annotationText}' contains an unsupported or invalid option.",
                            bodyOffset + prefix.Length + annotation);
                        return false;
                    }
                }

                if (!IsSupportedName(name))
                {
                    diagnostic = SyntaxDiagnostic("Input variable name is invalid.", bodyOffset + prefix.Length);
                    return false;
                }

                for (int i = 0; i < declarations.Count; i++)
                {
                    if (string.Equals(declarations[i].Name, name, StringComparison.Ordinal))
                    {
                        diagnostic = SyntaxDiagnostic($"Input variable '{name}' is declared more than once.", bodyOffset);
                        return false;
                    }
                }

                declarations.Add(new InputDeclaration(name, name, type, numberOptions, true));
                bodyOffset = lineEnd + 1;
            }

            while (bodyOffset < source.Length &&
                   source.AsSpan(bodyOffset).StartsWith(".local".AsSpan(), StringComparison.Ordinal))
            {
                int lineEnd = source.IndexOf('\n', bodyOffset);
                if (lineEnd < 0)
                {
                    diagnostic = SyntaxDiagnostic("Local declaration must be followed by a message body.", bodyOffset);
                    return false;
                }

                string declaration = source.Substring(bodyOffset, lineEnd - bodyOffset).TrimEnd('\r');
                if (!TryParseLocalDeclaration(
                        declaration,
                        declarations,
                        out InputDeclaration local,
                        out string localError))
                {
                    diagnostic = SyntaxDiagnostic(localError, bodyOffset);
                    return false;
                }

                declarations.Add(local);
                bodyOffset = lineEnd + 1;
            }

            diagnostic = default;
            return true;
        }

        private static bool TryParseLocalDeclaration(
            string source,
            List<InputDeclaration> declarations,
            out InputDeclaration result,
            out string error)
        {
            const string prefix = ".local $";
            int equals = source.IndexOf('=', prefix.Length);
            if (!source.StartsWith(prefix, StringComparison.Ordinal) ||
                equals <= prefix.Length ||
                equals + 1 >= source.Length)
            {
                result = default;
                error = "Local declaration has invalid syntax.";
                return false;
            }

            string name = I18nUnicode.NormalizeNfc(
                source.Substring(prefix.Length, equals - prefix.Length).Trim());
            if (!IsSupportedName(name) || FindDeclaration(declarations, name) != null)
            {
                result = default;
                error = $"Local variable '{name}' is invalid or already declared.";
                return false;
            }

            string rightHandSide = source.Substring(equals + 1).Trim();
            if (!rightHandSide.StartsWith("{$", StringComparison.Ordinal) ||
                !rightHandSide.EndsWith("}", StringComparison.Ordinal))
            {
                result = default;
                error = "Local declaration must contain a variable expression.";
                return false;
            }

            string expression = rightHandSide.Substring(2, rightHandSide.Length - 3).Trim();
            int annotationStart = expression.IndexOf(' ');
            string referencedName = I18nUnicode.NormalizeNfc(annotationStart < 0
                ? expression
                : expression.Substring(0, annotationStart));
            InputDeclaration? referenced = FindDeclaration(declarations, referencedName);
            if (referenced == null)
            {
                result = default;
                error = $"Local variable '{name}' references unknown variable '{referencedName}'.";
                return false;
            }

            InputType type = referenced.Value.Type;
            I18nCompiledMessage.NumberOptions options = referenced.Value.NumberOptions;
            if (annotationStart >= 0)
            {
                string annotation = expression.Substring(annotationStart).Trim();
                if (!TryParseInputAnnotation(annotation, out type, out options, referenced))
                {
                    result = default;
                    error = $"Local variable '{name}' contains an unsupported or incompatible annotation.";
                    return false;
                }
            }

            result = new InputDeclaration(
                name,
                referenced.Value.SourceName,
                type,
                options,
                false);
            error = string.Empty;
            return true;
        }

        private static bool TryParseInputAnnotation(
            string source,
            out InputType type,
            out I18nCompiledMessage.NumberOptions numberOptions,
            InputDeclaration? inherited = null)
        {
            numberOptions = I18nCompiledMessage.NumberOptions.Default;
            if (string.Equals(source, ":string", StringComparison.Ordinal))
            {
                type = InputType.String;
                return true;
            }

            string[] tokens = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool isInteger = tokens.Length > 0 &&
                string.Equals(tokens[0], ":integer", StringComparison.Ordinal);
            bool isPercent = tokens.Length > 0 &&
                string.Equals(tokens[0], ":percent", StringComparison.Ordinal);
            bool isOffset = tokens.Length > 0 &&
                string.Equals(tokens[0], ":offset", StringComparison.Ordinal);
            if (tokens.Length == 0 ||
                (!isInteger && !isPercent && !isOffset &&
                 !string.Equals(tokens[0], ":number", StringComparison.Ordinal)))
            {
                type = default;
                return false;
            }

            if (inherited != null)
            {
                bool inheritedNumber = inherited.Value.Type == InputType.Percent ||
                    inherited.Value.Type == InputType.Number ||
                    inherited.Value.Type == InputType.OrdinalNumber ||
                    inherited.Value.Type == InputType.ExactNumber;
                bool inheritedUntyped = inherited.Value.Type == InputType.Unspecified;
                if (!inheritedUntyped && !inheritedNumber)
                {
                    type = default;
                    return false;
                }
            }

            I18nCompiledMessage.NumberOptions inheritedOptions =
                inherited?.NumberOptions ?? I18nCompiledMessage.NumberOptions.Default;
            if (isOffset)
            {
                if (inherited == null || tokens.Length != 2)
                {
                    type = default;
                    return false;
                }

                int equals = tokens[1].IndexOf('=');
                if (equals <= 0 || equals == tokens[1].Length - 1 ||
                    !int.TryParse(
                        tokens[1].Substring(equals + 1),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int amount) ||
                    amount < 0 ||
                    amount > 99)
                {
                    type = default;
                    return false;
                }

                string operation = tokens[1].Substring(0, equals);
                if (operation != "add" && operation != "subtract")
                {
                    type = default;
                    return false;
                }

                type = inherited.Value.Type == InputType.Unspecified
                    ? InputType.Number
                    : inherited.Value.Type;
                numberOptions = inheritedOptions.WithOffset(
                    operation == "add" ? amount : -amount);
                return true;
            }

            type = isPercent
                ? InputType.Percent
                : inherited != null &&
                  (inherited.Value.Type == InputType.OrdinalNumber ||
                   inherited.Value.Type == InputType.ExactNumber)
                    ? inherited.Value.Type
                    : InputType.Number;
            int minimumFractionDigits = inherited == null ? -1 : inheritedOptions.MinimumFractionDigits;
            int maximumFractionDigits = inherited == null
                ? (isInteger || isPercent ? 0 : -1)
                : inheritedOptions.MaximumFractionDigits;
            int minimumSignificantDigits = inherited == null ? -1 : inheritedOptions.MinimumSignificantDigits;
            int maximumSignificantDigits = inherited == null ? -1 : inheritedOptions.MaximumSignificantDigits;
            int minimumIntegerDigits = inherited == null ? 1 : inheritedOptions.MinimumIntegerDigits;
            int roundingIncrement = inherited == null ? 1 : inheritedOptions.RoundingIncrement;
            I18nCompiledMessage.NumberGrouping grouping = inherited == null
                ? I18nCompiledMessage.NumberGrouping.Auto
                : inheritedOptions.Grouping;
            I18nCompiledMessage.NumberSignDisplay signDisplay = inherited == null
                ? I18nCompiledMessage.NumberSignDisplay.Auto
                : inheritedOptions.SignDisplay;
            I18nCompiledMessage.TrailingZeroDisplay trailingZeroDisplay = inherited == null
                ? I18nCompiledMessage.TrailingZeroDisplay.Auto
                : inheritedOptions.TrailingZeroDisplay;
            I18nCompiledMessage.NumberRoundingMode roundingMode = inherited == null
                ? I18nCompiledMessage.NumberRoundingMode.HalfExpand
                : inheritedOptions.RoundingMode;
            I18nCompiledMessage.NumberRoundingPriority roundingPriority = inherited == null
                ? I18nCompiledMessage.NumberRoundingPriority.Auto
                : inheritedOptions.RoundingPriority;
            if (isInteger)
            {
                minimumFractionDigits = -1;
                maximumFractionDigits = 0;
                minimumSignificantDigits = -1;
                roundingIncrement = 1;
                trailingZeroDisplay = I18nCompiledMessage.TrailingZeroDisplay.Auto;
            }
            else if (isPercent && maximumFractionDigits < 0)
            {
                maximumFractionDigits = 0;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < tokens.Length; i++)
            {
                int equals = tokens[i].IndexOf('=');
                if (equals <= 0 || equals == tokens[i].Length - 1)
                {
                    return false;
                }

                string name = tokens[i].Substring(0, equals);
                string value = tokens[i].Substring(equals + 1);
                if (!names.Add(name))
                {
                    return false;
                }

                if (isInteger &&
                    name != "select" &&
                    name != "signDisplay" &&
                    name != "useGrouping" &&
                    name != "minimumIntegerDigits" &&
                    name != "maximumSignificantDigits")
                {
                    return false;
                }


                if (isPercent &&
                    (name == "select" ||
                     name == "minimumIntegerDigits" ||
                     name == "roundingIncrement"))
                {
                    return false;
                }

                if (name == "select")
                {
                    if (value == "plural")
                    {
                        type = InputType.Number;
                    }
                    else if (value == "ordinal")
                    {
                        type = InputType.OrdinalNumber;
                    }
                    else if (value == "exact")
                    {
                        type = InputType.ExactNumber;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (name == "minimumFractionDigits")
                {
                    if (!TryParseDigitSize(value, out minimumFractionDigits))
                    {
                        return false;
                    }
                }
                else if (name == "maximumFractionDigits")
                {
                    if (!TryParseDigitSize(value, out maximumFractionDigits))
                    {
                        return false;
                    }
                }
                else if (name == "minimumIntegerDigits")
                {
                    if (!TryParseDigitSize(value, out minimumIntegerDigits) || minimumIntegerDigits == 0)
                    {
                        return false;
                    }
                }
                else if (name == "minimumSignificantDigits")
                {
                    if (!TryParseDigitSize(value, out minimumSignificantDigits) || minimumSignificantDigits == 0)
                    {
                        return false;
                    }
                }
                else if (name == "maximumSignificantDigits")
                {
                    if (!TryParseDigitSize(value, out maximumSignificantDigits) || maximumSignificantDigits == 0)
                    {
                        return false;
                    }
                }
                else if (name == "useGrouping")
                {
                    grouping = value switch
                    {
                        "auto" => I18nCompiledMessage.NumberGrouping.Auto,
                        "always" => I18nCompiledMessage.NumberGrouping.Always,
                        "never" => I18nCompiledMessage.NumberGrouping.Never,
                        "min2" => I18nCompiledMessage.NumberGrouping.Min2,
                        _ => (I18nCompiledMessage.NumberGrouping)byte.MaxValue,
                    };
                    if ((byte)grouping == byte.MaxValue)
                    {
                        return false;
                    }
                }
                else if (name == "signDisplay")
                {
                    signDisplay = value switch
                    {
                        "auto" => I18nCompiledMessage.NumberSignDisplay.Auto,
                        "always" => I18nCompiledMessage.NumberSignDisplay.Always,
                        "exceptZero" => I18nCompiledMessage.NumberSignDisplay.ExceptZero,
                        "negative" => I18nCompiledMessage.NumberSignDisplay.Negative,
                        "never" => I18nCompiledMessage.NumberSignDisplay.Never,
                        _ => (I18nCompiledMessage.NumberSignDisplay)byte.MaxValue,
                    };
                    if ((byte)signDisplay == byte.MaxValue)
                    {
                        return false;
                    }
                }
                else if (name == "trailingZeroDisplay")
                {
                    trailingZeroDisplay = value switch
                    {
                        "auto" => I18nCompiledMessage.TrailingZeroDisplay.Auto,
                        "stripIfInteger" => I18nCompiledMessage.TrailingZeroDisplay.StripIfInteger,
                        _ => (I18nCompiledMessage.TrailingZeroDisplay)byte.MaxValue,
                    };
                    if ((byte)trailingZeroDisplay == byte.MaxValue)
                    {
                        return false;
                    }
                }
                else if (name == "roundingMode")
                {
                    roundingMode = value switch
                    {
                        "ceil" => I18nCompiledMessage.NumberRoundingMode.Ceil,
                        "floor" => I18nCompiledMessage.NumberRoundingMode.Floor,
                        "expand" => I18nCompiledMessage.NumberRoundingMode.Expand,
                        "trunc" => I18nCompiledMessage.NumberRoundingMode.Trunc,
                        "halfCeil" => I18nCompiledMessage.NumberRoundingMode.HalfCeil,
                        "halfFloor" => I18nCompiledMessage.NumberRoundingMode.HalfFloor,
                        "halfExpand" => I18nCompiledMessage.NumberRoundingMode.HalfExpand,
                        "halfTrunc" => I18nCompiledMessage.NumberRoundingMode.HalfTrunc,
                        "halfEven" => I18nCompiledMessage.NumberRoundingMode.HalfEven,
                        _ => (I18nCompiledMessage.NumberRoundingMode)byte.MaxValue,
                    };
                    if ((byte)roundingMode == byte.MaxValue)
                    {
                        return false;
                    }
                }
                else if (name == "roundingPriority")
                {
                    roundingPriority = value switch
                    {
                        "auto" => I18nCompiledMessage.NumberRoundingPriority.Auto,
                        "morePrecision" => I18nCompiledMessage.NumberRoundingPriority.MorePrecision,
                        "lessPrecision" => I18nCompiledMessage.NumberRoundingPriority.LessPrecision,
                        _ => (I18nCompiledMessage.NumberRoundingPriority)byte.MaxValue,
                    };
                    if ((byte)roundingPriority == byte.MaxValue)
                    {
                        return false;
                    }
                }
                else if (name == "roundingIncrement")
                {
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out roundingIncrement) ||
                        !IsSupportedRoundingIncrement(roundingIncrement))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            if (minimumFractionDigits >= 0 &&
                maximumFractionDigits >= 0 &&
                minimumFractionDigits > maximumFractionDigits)
            {
                return false;
            }

            if (minimumSignificantDigits >= 0 &&
                maximumSignificantDigits >= 0 &&
                minimumSignificantDigits > maximumSignificantDigits)
            {
                return false;
            }

            if (minimumSignificantDigits >= 0 && maximumSignificantDigits < 0)
            {
                maximumSignificantDigits = 28;
            }

            if (roundingIncrement != 1 &&
                (minimumFractionDigits < 0 ||
                 maximumFractionDigits < 0 ||
                 minimumFractionDigits != maximumFractionDigits ||
                 maximumSignificantDigits >= 0))
            {
                return false;
            }

            numberOptions = new I18nCompiledMessage.NumberOptions(
                minimumFractionDigits,
                maximumFractionDigits,
                minimumSignificantDigits,
                maximumSignificantDigits,
                minimumIntegerDigits,
                roundingIncrement,
                grouping,
                signDisplay,
                trailingZeroDisplay,
                roundingMode,
                roundingPriority,
                isPercent
                    ? I18nCompiledMessage.NumberStyle.Percent
                    : I18nCompiledMessage.NumberStyle.Decimal,
                inherited?.NumberOptions.Offset ?? 0m);
            return true;
        }

        private static bool TryParseDigitSize(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) &&
                   result >= 0 &&
                   result <= 28;
        }

        private static bool IsSupportedRoundingIncrement(int value)
        {
            return value == 1 || value == 2 || value == 5 || value == 10 ||
                   value == 20 || value == 25 || value == 50 || value == 100 ||
                   value == 200 || value == 250 || value == 500 || value == 1000 ||
                   value == 2000 || value == 2500 || value == 5000;
        }

        private static bool TryReadQuotedPattern(
            string source,
            int start,
            out string pattern,
            out int patternOffset,
            out int end)
        {
            pattern = string.Empty;
            patternOffset = start;
            end = start;

            if (start + 1 >= source.Length || source[start] != '{' || source[start + 1] != '{')
            {
                return false;
            }

            int position = start + 2;
            patternOffset = position;
            while (position < source.Length - 1)
            {
                if (source[position] == '\\')
                {
                    position += 2;
                    continue;
                }

                if (source[position] == '{' && source[position + 1] == '$')
                {
                    int expressionEnd = source.IndexOf('}', position + 2);
                    if (expressionEnd < 0)
                    {
                        return false;
                    }

                    position = expressionEnd + 1;
                    continue;
                }

                if (source[position] == '}' && source[position + 1] == '}')
                {
                    pattern = source.Substring(patternOffset, position - patternOffset);
                    end = position + 2;
                    return true;
                }

                position++;
            }

            return false;
        }

        private static bool IsOnlyWhitespace(string source, int start)
        {
            for (int i = start; i < source.Length; i++)
            {
                if (!char.IsWhiteSpace(source[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void SkipWhitespace(string source, ref int position)
        {
            while (position < source.Length && char.IsWhiteSpace(source[position]))
            {
                position++;
            }
        }

        private static void SkipHorizontalWhitespace(string source, ref int position, int end)
        {
            while (position < end && (source[position] == ' ' || source[position] == '\t'))
            {
                position++;
            }
        }

        private static bool IsSupportedName(string name)
        {
            if (name.Length == 0 || (!char.IsLetter(name[0]) && name[0] != '_'))
            {
                return false;
            }

            for (int i = 1; i < name.Length; i++)
            {
                if (!IsNameCharacter(name[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsNameCharacter(char character)
        {
            return char.IsLetterOrDigit(character) || character == '_' || character == '-';
        }

        private static I18nMessageCompilation Failed(string message, int position)
        {
            return Failed(SyntaxDiagnostic(message, position));
        }

        private static I18nMessageCompilation Failed(I18nMessageDiagnostic diagnostic)
        {
            return new I18nMessageCompilation(null, new[] { diagnostic });
        }

        private static I18nMessageDiagnostic SyntaxDiagnostic(string message, int position)
        {
            return new I18nMessageDiagnostic(
                I18nMessageDiagnosticCodes.InvalidSyntax,
                message,
                position);
        }

        private readonly struct InputDeclaration
        {
            public InputDeclaration(
                string name,
                string sourceName,
                InputType type,
                I18nCompiledMessage.NumberOptions numberOptions,
                bool isInput)
            {
                Name = name;
                SourceName = sourceName;
                Type = type;
                NumberOptions = numberOptions;
                IsInput = isInput;
            }

            public string Name { get; }
            public string SourceName { get; }

            public InputType Type { get; }
            public I18nCompiledMessage.NumberOptions NumberOptions { get; }
            public bool IsInput { get; }
        }

        private enum InputType
        {
            Unspecified,
            Number,
            OrdinalNumber,
            ExactNumber,
            Percent,
            String,
        }
    }
}
