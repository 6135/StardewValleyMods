using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UIFramework.Data.Expressions
{
    /// <summary>Token categories produced by the <see cref="Lexer"/>.</summary>
    internal enum TokenKind : byte
    {
        End,
        Number,
        String,
        Identifier,

        /// <summary><c>@owner/name</c>; the text excludes the <c>@</c>.</summary>
        AtReference,

        /// <summary>An unquoted key inside a scope qualifier bracket, e.g. <c>owner/menu</c> in <c>menu[owner/menu]</c>.</summary>
        RawKey,

        LeftParen,
        RightParen,
        LeftBracket,
        RightBracket,
        Comma,
        Dot,
        Question,
        Colon,
        Plus,
        Minus,
        Star,
        Slash,
        Percent,
        Bang,
        Less,
        LessEqual,
        Greater,
        GreaterEqual,
        EqualEqual,
        BangEqual,
        AndAnd,
        OrOr
    }

    /// <summary>One lexical token with its source position.</summary>
    internal readonly record struct Token
    {
        internal Token(TokenKind kind, int position, string? text = null, double number = 0)
        {
            Kind = kind;
            Position = position;
            Text = text;
            Number = number;
        }

        internal TokenKind Kind { get; }

        /// <summary>Zero-based offset of the token's first character in the source.</summary>
        internal int Position { get; }

        /// <summary>Identifier name, string contents, reference name or raw key (null for punctuation).</summary>
        internal string? Text { get; }

        /// <summary>Value of a number token.</summary>
        internal double Number { get; }

        public override string ToString() => Text ?? Kind.ToString();
    }

    /// <summary>
    /// Turns expression source into tokens in one pass. Never throws: the first problem stops the scan and is
    /// reported through <see cref="Tokenize"/>'s <c>error</c>.
    /// </summary>
    /// <remarks>
    /// Scope-qualifier brackets directly after a qualifier root (<see cref="QualifierRoots"/>: <c>menu[owner/menu]</c>,
    /// <c>el[myId]</c>) whose contents are only key characters (letters, digits, <c>_ - . / : @</c>) are lexed as one
    /// <see cref="TokenKind.RawKey"/>, so owner ids with slashes need no quotes. Wrap the content in parentheses or quotes
    /// to force an expression. After any other identifier (locals such as <c>row[menu.col]</c>, <c>list[i]</c>) the
    /// brackets always hold an expression.
    /// </remarks>
    internal static class Lexer
    {
        /// <summary>The punctuation tokens that are always one character.</summary>
        private static readonly Dictionary<char, TokenKind> SingleCharTokens = new()
        {
            ['('] = TokenKind.LeftParen,
            [')'] = TokenKind.RightParen,
            ['['] = TokenKind.LeftBracket,
            [']'] = TokenKind.RightBracket,
            [','] = TokenKind.Comma,
            ['.'] = TokenKind.Dot,
            ['?'] = TokenKind.Question,
            [':'] = TokenKind.Colon,
            ['+'] = TokenKind.Plus,
            ['-'] = TokenKind.Minus,
            ['*'] = TokenKind.Star,
            ['/'] = TokenKind.Slash,
            ['%'] = TokenKind.Percent
        };

        /// <summary>The outcome of trying to read an operand token (number, identifier, string, reference).</summary>
        private enum OperandScan
        {
            /// <summary>The character does not start an operand.</summary>
            None,

            /// <summary>An operand token was added.</summary>
            Read,

            /// <summary>The operand is invalid (the error is set).</summary>
            Failed
        }

        /// <summary>Tokenize <paramref name="source"/>; on failure returns false with a positioned error.</summary>
        internal static bool Tokenize(string source, List<Token> tokens, out ParseError? error)
        {
            error = null;
            int i = 0;
            while (true)
            {
                while (i < source.Length && char.IsWhiteSpace(source[i]))
                {
                    i++;
                }

                if (i >= source.Length)
                {
                    tokens.Add(new Token(TokenKind.End, i));
                    return true;
                }

                int start = i;
                OperandScan operand = ReadOperand(source, ref i, tokens, out error);
                if (operand == OperandScan.Read)
                {
                    continue;
                }

                if (operand == OperandScan.Failed || !TryReadPunctuation(source, start, out TokenKind kind, out int length, out error))
                {
                    return false;
                }

                i += length;
                tokens.Add(new Token(kind, start));

                if (kind == TokenKind.LeftBracket && FollowsRootIdentifier(tokens) && TryReadRawKey(source, ref i, out string? key, out int keyStart))
                {
                    tokens.Add(new Token(TokenKind.RawKey, keyStart, key));
                }
            }
        }

        /// <summary>Read a number, identifier, string or <c>@</c> reference at <paramref name="i"/>.</summary>
        private static OperandScan ReadOperand(string source, ref int i, List<Token> tokens, out ParseError? error)
        {
            error = null;
            char c = source[i];
            int start = i;

            if (char.IsDigit(c))
            {
                if (!ReadNumber(source, ref i, out double number))
                {
                    error = new ParseError("Invalid number.", start);
                    return OperandScan.Failed;
                }

                tokens.Add(new Token(TokenKind.Number, start, source.Substring(start, i - start), number));
                return OperandScan.Read;
            }

            if (IsIdentifierStart(c))
            {
                while (i < source.Length && IsIdentifierPart(source[i]))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Identifier, start, source.Substring(start, i - start)));
                return OperandScan.Read;
            }

            if (c == '"' || c == '\'')
            {
                if (!ReadString(source, ref i, out string? text, out error))
                {
                    return OperandScan.Failed;
                }

                tokens.Add(new Token(TokenKind.String, start, text));
                return OperandScan.Read;
            }

            if (c == '@')
            {
                if (!ReadReference(source, ref i, out string? name))
                {
                    error = new ParseError("Expected a reference name after '@' (e.g. @owner/name).", start);
                    return OperandScan.Failed;
                }

                tokens.Add(new Token(TokenKind.AtReference, start, name));
                return OperandScan.Read;
            }

            return OperandScan.None;
        }

        /// <summary>Read the punctuation / operator token at <paramref name="start"/>; false (with an error) for an invalid character.</summary>
        private static bool TryReadPunctuation(string source, int start, out TokenKind kind, out int length, out ParseError? error)
        {
            error = null;
            char c = source[start];
            char next = start + 1 < source.Length ? source[start + 1] : '\0';
            length = 1;
            if (SingleCharTokens.TryGetValue(c, out kind))
            {
                return true;
            }

            switch (c)
            {
                case '!':
                    kind = next == '=' ? TokenKind.BangEqual : TokenKind.Bang;
                    length = next == '=' ? 2 : 1;
                    return true;
                case '<':
                    kind = next == '=' ? TokenKind.LessEqual : TokenKind.Less;
                    length = next == '=' ? 2 : 1;
                    return true;
                case '>':
                    kind = next == '=' ? TokenKind.GreaterEqual : TokenKind.Greater;
                    length = next == '=' ? 2 : 1;
                    return true;
                case '=' when next == '=':
                    kind = TokenKind.EqualEqual;
                    length = 2;
                    return true;
                case '=':
                    error = new ParseError("Assignment is not allowed in expressions; use '==' to compare.", start);
                    return false;
                case '&' when next == '&':
                    kind = TokenKind.AndAnd;
                    length = 2;
                    return true;
                case '|' when next == '|':
                    kind = TokenKind.OrOr;
                    length = 2;
                    return true;
                default:
                    error = new ParseError($"Unexpected character '{c}'.", start);
                    return false;
            }
        }

        private static bool IsIdentifierStart(char c) => c == '_' || char.IsLetter(c);

        private static bool IsIdentifierPart(char c) => c == '_' || char.IsLetterOrDigit(c);

        private static bool IsKeyChar(char c) => c is '_' or '-' or '.' or '/' or ':' or '@' || char.IsLetterOrDigit(c);

        /// <summary>The roots whose brackets take a raw key (the state scopes, <c>el</c>, <c>ctx</c> and <c>model</c>).</summary>
        private static readonly HashSet<string> QualifierRoots = new(StringComparer.Ordinal) { "menu", "session", "player", "stat", "config", "el", "ctx", "model" };

        /// <summary>True when the bracket just added follows a qualifier root that starts a path (not <c>.name[</c>).</summary>
        private static bool FollowsRootIdentifier(List<Token> tokens)
        {
            int bracket = tokens.Count - 1;
            if (bracket < 1 || tokens[bracket - 1].Kind != TokenKind.Identifier || !QualifierRoots.Contains(tokens[bracket - 1].Text ?? string.Empty))
            {
                return false;
            }

            return bracket < 2 || tokens[bracket - 2].Kind != TokenKind.Dot;
        }

        /// <summary>Read <c>owner/menu</c> up to the closing bracket (which is left for the normal scan).</summary>
        private static bool TryReadRawKey(string source, ref int i, out string? key, out int keyStart)
        {
            key = null;
            int j = i;
            while (j < source.Length && source[j] == ' ')
            {
                j++;
            }

            keyStart = j;
            int end = j;
            while (end < source.Length && IsKeyChar(source[end]))
            {
                end++;
            }

            int close = end;
            while (close < source.Length && source[close] == ' ')
            {
                close++;
            }

            if (end == keyStart || close >= source.Length || source[close] != ']')
            {
                return false;
            }

            key = source.Substring(keyStart, end - keyStart);
            i = close;
            return true;
        }

        /// <summary><c>@owner/name</c>: the owner may contain dots (mod ids); the name after the slash may not, so <c>@a.b/c.d</c> is <c>@a.b/c</c> then <c>.d</c>.</summary>
        private static bool ReadReference(string source, ref int i, out string? name)
        {
            int start = i + 1;
            int j = start;
            while (j < source.Length && (IsIdentifierPart(source[j]) || source[j] == '.' || source[j] == '-'))
            {
                j++;
            }

            if (j < source.Length && source[j] == '/')
            {
                j++;
                int nameStart = j;
                while (j < source.Length && (IsIdentifierPart(source[j]) || source[j] == '-'))
                {
                    j++;
                }

                if (j == nameStart)
                {
                    name = null;
                    return false;
                }
            }
            else
            {
                // no owner qualifier: the reference ends at the first dot (which becomes member access)
                j = start;
                while (j < source.Length && (IsIdentifierPart(source[j]) || source[j] == '-'))
                {
                    j++;
                }
            }

            if (j == start)
            {
                name = null;
                return false;
            }

            name = source.Substring(start, j - start);
            i = j;
            return true;
        }

        private static bool ReadNumber(string source, ref int i, out double value)
        {
            int start = i;
            while (i < source.Length && char.IsDigit(source[i]))
            {
                i++;
            }

            if (i + 1 < source.Length && source[i] == '.' && char.IsDigit(source[i + 1]))
            {
                i++;
                while (i < source.Length && char.IsDigit(source[i]))
                {
                    i++;
                }
            }

            if (i < source.Length && (source[i] == 'e' || source[i] == 'E'))
            {
                int mark = i;
                i++;
                if (i < source.Length && (source[i] == '+' || source[i] == '-'))
                {
                    i++;
                }

                if (i < source.Length && char.IsDigit(source[i]))
                {
                    while (i < source.Length && char.IsDigit(source[i]))
                    {
                        i++;
                    }
                }
                else
                {
                    i = mark;
                }
            }

            return double.TryParse(source.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
        }

        private static bool ReadString(string source, ref int i, out string? text, out ParseError? error)
        {
            char quote = source[i];
            int start = i;
            i++;
            StringBuilder builder = new();
            while (i < source.Length)
            {
                char c = source[i++];
                if (c == quote)
                {
                    text = builder.ToString();
                    error = null;
                    return true;
                }

                if (c == '\\' && i < source.Length)
                {
                    char escaped = source[i++];
                    switch (escaped)
                    {
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 'u' when i + 4 <= source.Length && ushort.TryParse(source.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code):
                            builder.Append((char)code);
                            i += 4;
                            break;
                        default:
                            // \\ \' \" \$ \{ \} and anything else: the character itself
                            builder.Append(escaped);
                            break;
                    }
                }
                else
                {
                    builder.Append(c);
                }

                if (builder.Length > ExpressionLimits.MaxStringLength)
                {
                    text = null;
                    error = new ParseError($"String literal is longer than {ExpressionLimits.MaxStringLength} characters.", start);
                    return false;
                }
            }

            text = null;
            error = new ParseError("Unterminated string literal.", start);
            return false;
        }
    }
}
