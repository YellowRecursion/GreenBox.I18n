package com.greenbox.i18n.rider;

/** Finds decimal numeric tokens while ignoring C# comments, strings, and character literals. */
final class CSharpNumericTokenScanner {
    private CSharpNumericTokenScanner() {
    }

    static void scan(String text, TokenConsumer consumer) {
        int offset = 0;
        while (offset < text.length()) {
            char current = text.charAt(offset);

            if (current == '/' && hasNext(text, offset, '/')) {
                offset = skipLineComment(text, offset + 2);
            } else if (current == '/' && hasNext(text, offset, '*')) {
                offset = skipBlockComment(text, offset + 2);
            } else if (current == '@' && hasNext(text, offset, '"')) {
                offset = skipVerbatimString(text, offset + 2);
            } else if (current == '"') {
                offset = skipString(text, offset + 1);
            } else if (current == '\'') {
                offset = skipCharacter(text, offset + 1);
            } else if (Character.isDigit(current)) {
                offset = readNumericToken(text, offset, consumer);
            } else {
                offset++;
            }
        }
    }

    private static int readNumericToken(String text, int offset, TokenConsumer consumer) {
        if (offset > 0 && isIdentifierPart(text.charAt(offset - 1))) {
            return skipDigits(text, offset);
        }

        int endOffset = skipDigits(text, offset);
        if (endOffset < text.length() && isIdentifierPart(text.charAt(endOffset))) {
            return endOffset;
        }

        consumer.accept(offset, text.substring(offset, endOffset));
        return endOffset;
    }

    private static int skipDigits(String text, int offset) {
        int result = offset;
        while (result < text.length() && Character.isDigit(text.charAt(result))) {
            result++;
        }
        return result;
    }

    private static int skipLineComment(String text, int offset) {
        int result = offset;
        while (result < text.length() && text.charAt(result) != '\n') {
            result++;
        }
        return result;
    }

    private static int skipBlockComment(String text, int offset) {
        int result = offset;
        while (result + 1 < text.length()) {
            if (text.charAt(result) == '*' && text.charAt(result + 1) == '/') {
                return result + 2;
            }
            result++;
        }
        return text.length();
    }

    private static int skipString(String text, int offset) {
        int result = offset;
        while (result < text.length()) {
            char current = text.charAt(result);
            if (current == '\\') {
                result += 2;
            } else if (current == '"') {
                return result + 1;
            } else {
                result++;
            }
        }
        return text.length();
    }

    private static int skipVerbatimString(String text, int offset) {
        int result = offset;
        while (result < text.length()) {
            if (text.charAt(result) != '"') {
                result++;
            } else if (hasNext(text, result, '"')) {
                result += 2;
            } else {
                return result + 1;
            }
        }
        return text.length();
    }

    private static int skipCharacter(String text, int offset) {
        int result = offset;
        while (result < text.length()) {
            char current = text.charAt(result);
            if (current == '\\') {
                result += 2;
            } else if (current == '\'') {
                return result + 1;
            } else {
                result++;
            }
        }
        return text.length();
    }

    private static boolean hasNext(String text, int offset, char expected) {
        return offset + 1 < text.length() && text.charAt(offset + 1) == expected;
    }

    private static boolean isIdentifierPart(char value) {
        return Character.isLetterOrDigit(value) || value == '_';
    }

    @FunctionalInterface
    interface TokenConsumer {
        void accept(int offset, String value);
    }
}
