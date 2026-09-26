using System;
using System.Collections.Generic;

namespace UIFramework.Data.Expressions
{
    /// <summary>
    /// Pratt parser from tokens to an immutable <see cref="Node"/> tree. Never throws to callers: problems come back
    /// as a positioned <see cref="ParseError"/>.
    /// </summary>
    /// <remarks>
    /// Binding powers, loosest first: <c>?:</c> (right-associative), <c>||</c>, <c>&amp;&amp;</c>, <c>== !=</c>,
    /// <c>&lt; &lt;= &gt; &gt;=</c>, <c>+ -</c>, <c>* / %</c>, prefix <c>! -</c>, postfix <c>. [] ()</c>.
    /// </remarks>
    internal sealed class Parser
    {
        private const int TernaryPower = 1;
        private const int PrefixPower = 8;
        private const int PostfixPower = 9;

        /// <summary>Root key of a path written with a leading dot (<c>.day</c>), resolved against the enclosing <c>With</c> scope.</summary>
        internal const string RelativeRoot = ".";

        /// <summary>Maximum arguments in one call.</summary>
        private const int MaxArguments = 32;

        private readonly List<Token> tokens;
        private int index;
        private int nesting;

        private Parser(List<Token> tokens)
        {
            this.tokens = tokens;
        }

        /// <summary>Parse <paramref name="source"/> as a single expression.</summary>
        internal static bool TryParse(string? source, out Node? root, out ParseError? error)
        {
            root = null;
            if (string.IsNullOrWhiteSpace(source))
            {
                error = new ParseError("Empty expression.", 0);
                return false;
            }

            if (source.Length > ExpressionLimits.MaxSourceLength)
            {
                error = new ParseError($"Expression is longer than {ExpressionLimits.MaxSourceLength} characters.", 0);
                return false;
            }

            List<Token> tokens = new();
            if (!Lexer.Tokenize(source, tokens, out error))
            {
                return false;
            }

            Parser parser = new(tokens);
            try
            {
                Node node = parser.ParseExpression(0);
                Token end = parser.Peek();
                if (end.Kind != TokenKind.End)
                {
                    throw new ParseException($"Unexpected {Describe(end)}.", end.Position);
                }

                root = node;
                error = null;
                return true;
            }
            catch (ParseException ex)
            {
                error = new ParseError(ex.Message, ex.Position);
                return false;
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Pratt core
        // -------------------------------------------------------------------------------------------------------------

        private Node ParseExpression(int minPower)
        {
            if (++nesting > ExpressionLimits.MaxDepth)
            {
                throw new ParseException($"Expression is nested more than {ExpressionLimits.MaxDepth} levels deep.", Peek().Position);
            }

            try
            {
                Node left = ParsePrefix();
                while (true)
                {
                    Token token = Peek();
                    int power = LeftPower(token.Kind);
                    if (power <= minPower)
                    {
                        return left;
                    }

                    Advance();
                    left = Checked(ParseInfix(left, token, power));
                }
            }
            finally
            {
                nesting--;
            }
        }

        private Node ParsePrefix()
        {
            Token token = Advance();
            switch (token.Kind)
            {
                case TokenKind.Number:
                    return new LiteralNode(DataValue.FromNumber(token.Number), token.Position);

                case TokenKind.String:
                    return new LiteralNode(DataValue.FromString(token.Text), token.Position);

                case TokenKind.Identifier:
                {
                    string name = token.Text!;
                    if (Peek().Kind == TokenKind.LeftParen)
                    {
                        Advance();
                        return Checked(new CallNode(name, ParseArguments(), token.Position));
                    }

                    if (string.Equals(name, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        return new LiteralNode(DataValue.True, token.Position);
                    }

                    if (string.Equals(name, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        return new LiteralNode(DataValue.False, token.Position);
                    }

                    if (string.Equals(name, "null", StringComparison.OrdinalIgnoreCase))
                    {
                        return new LiteralNode(DataValue.Null, token.Position);
                    }

                    return new PathNode(new[] { new PathPart(name, false) }, token.Position);
                }

                case TokenKind.AtReference:
                    if (Peek().Kind == TokenKind.LeftParen)
                    {
                        Advance();
                        return Checked(new ExternalCallNode(token.Text!, ParseArguments(), token.Position));
                    }

                    return new PathNode(new[] { new PathPart("@" + token.Text, false) }, token.Position);

                case TokenKind.Dot:
                {
                    // ".day": a path relative to the enclosing With scope (root key ".")
                    Token member = Advance();
                    if (member.Kind != TokenKind.Identifier)
                    {
                        throw new ParseException($"Expected a member name after '.', found {Describe(member)}.", member.Position);
                    }

                    return new PathNode(new[] { new PathPart(RelativeRoot, false), new PathPart(member.Text!, false) }, token.Position);
                }

                case TokenKind.LeftParen:
                {
                    Node inner = ParseExpression(0);
                    Expect(TokenKind.RightParen, "')'");
                    return inner;
                }

                case TokenKind.Bang:
                    return Checked(new UnaryNode(UnaryOperator.Not, ParseExpression(PrefixPower), token.Position));

                case TokenKind.Minus:
                    return Checked(new UnaryNode(UnaryOperator.Negate, ParseExpression(PrefixPower), token.Position));

                default:
                    throw new ParseException($"Unexpected {Describe(token)}; expected a value.", token.Position);
            }
        }

        private Node ParseInfix(Node left, Token token, int power)
        {
            switch (token.Kind)
            {
                case TokenKind.Question:
                {
                    Node whenTrue = ParseExpression(0);
                    Expect(TokenKind.Colon, "':' in conditional");
                    Node whenFalse = ParseExpression(TernaryPower - 1);
                    return new ConditionalNode(left, whenTrue, whenFalse, token.Position);
                }

                case TokenKind.Dot:
                    return ParseMember(left, token);

                case TokenKind.LeftBracket:
                    return ParseIndex(left, token);

                case TokenKind.LeftParen:
                    throw new ParseException("Only named functions can be called (e.g. round(x)); method calls are not supported.", token.Position);

                default:
                    return new BinaryNode(ToBinary(token.Kind), left, ParseExpression(power), token.Position);
            }
        }

        private Node ParseMember(Node left, Token dot)
        {
            Token name = Advance();
            List<string> keys = new(1);
            switch (name.Kind)
            {
                case TokenKind.Identifier:
                    keys.Add(name.Text!);
                    break;

                case TokenKind.Number:
                    // "list.0" is an index; "list.0.1" lexes as the number 0.1, so split it back into steps
                    keys.AddRange(name.Text!.Split('.'));
                    break;

                default:
                    throw new ParseException($"Expected a member name after '.', found {Describe(name)}.", name.Position);
            }

            Node result = left;
            foreach (string key in keys)
            {
                result = result is PathNode path
                    ? path.Append(new PathPart(key, false))
                    : new MemberNode(result, key, false, dot.Position);
            }

            return result;
        }

        private Node ParseIndex(Node left, Token bracket)
        {
            if (Peek().Kind == TokenKind.RawKey)
            {
                string key = Advance().Text!;
                Expect(TokenKind.RightBracket, "']'");
                return left is PathNode rawPath
                    ? rawPath.Append(new PathPart(key, true))
                    : new MemberNode(left, key, true, bracket.Position);
            }

            Node index = ParseExpression(0);
            Expect(TokenKind.RightBracket, "']'");
            if (index is LiteralNode literal)
            {
                string key = literal.Value.AsString();
                return left is PathNode staticPath
                    ? staticPath.Append(new PathPart(key, true))
                    : new MemberNode(left, key, true, bracket.Position);
            }

            return left is PathNode path
                ? path.Append(new PathPart(index))
                : new MemberNode(left, index, bracket.Position);
        }

        private Node[] ParseArguments()
        {
            if (Peek().Kind == TokenKind.RightParen)
            {
                Advance();
                return Array.Empty<Node>();
            }

            List<Node> arguments = new();
            while (true)
            {
                if (arguments.Count >= MaxArguments)
                {
                    throw new ParseException($"More than {MaxArguments} arguments.", Peek().Position);
                }

                arguments.Add(ParseExpression(0));
                Token separator = Advance();
                if (separator.Kind == TokenKind.RightParen)
                {
                    return arguments.ToArray();
                }

                if (separator.Kind != TokenKind.Comma)
                {
                    throw new ParseException($"Expected ',' or ')' in argument list, found {Describe(separator)}.", separator.Position);
                }
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Helpers
        // -------------------------------------------------------------------------------------------------------------

        private static int LeftPower(TokenKind kind)
        {
            return kind switch
            {
                TokenKind.Question => TernaryPower,
                TokenKind.OrOr => 2,
                TokenKind.AndAnd => 3,
                TokenKind.EqualEqual or TokenKind.BangEqual => 4,
                TokenKind.Less or TokenKind.LessEqual or TokenKind.Greater or TokenKind.GreaterEqual => 5,
                TokenKind.Plus or TokenKind.Minus => 6,
                TokenKind.Star or TokenKind.Slash or TokenKind.Percent => 7,
                TokenKind.Dot or TokenKind.LeftBracket or TokenKind.LeftParen => PostfixPower,
                _ => 0
            };
        }

        private static BinaryOperator ToBinary(TokenKind kind)
        {
            return kind switch
            {
                TokenKind.Plus => BinaryOperator.Add,
                TokenKind.Minus => BinaryOperator.Subtract,
                TokenKind.Star => BinaryOperator.Multiply,
                TokenKind.Slash => BinaryOperator.Divide,
                TokenKind.Percent => BinaryOperator.Modulo,
                TokenKind.Less => BinaryOperator.Less,
                TokenKind.LessEqual => BinaryOperator.LessEqual,
                TokenKind.Greater => BinaryOperator.Greater,
                TokenKind.GreaterEqual => BinaryOperator.GreaterEqual,
                TokenKind.EqualEqual => BinaryOperator.Equal,
                TokenKind.BangEqual => BinaryOperator.NotEqual,
                TokenKind.AndAnd => BinaryOperator.And,
                _ => BinaryOperator.Or
            };
        }

        /// <summary>Reject nodes deeper than <see cref="ExpressionLimits.MaxDepth"/> (long operator chains nest without recursing).</summary>
        private static Node Checked(Node node)
        {
            if (node.Depth > ExpressionLimits.MaxDepth)
            {
                throw new ParseException($"Expression is nested more than {ExpressionLimits.MaxDepth} levels deep.", node.Position);
            }

            return node;
        }

        private Token Peek() => tokens[index];

        private Token Advance()
        {
            Token token = tokens[index];
            if (token.Kind != TokenKind.End)
            {
                index++;
            }

            return token;
        }

        private void Expect(TokenKind kind, string what)
        {
            Token token = Advance();
            if (token.Kind != kind)
            {
                throw new ParseException($"Expected {what}, found {Describe(token)}.", token.Position);
            }
        }

        private static string Describe(Token token)
        {
            return token.Kind switch
            {
                TokenKind.End => "end of expression",
                TokenKind.Number => $"number '{token.Text}'",
                TokenKind.String => "string",
                TokenKind.Identifier => $"'{token.Text}'",
                TokenKind.AtReference => $"'@{token.Text}'",
                TokenKind.RawKey => $"'{token.Text}'",
                _ => $"'{Symbol(token.Kind)}'"
            };
        }

        private static string Symbol(TokenKind kind)
        {
            return kind switch
            {
                TokenKind.LeftParen => "(",
                TokenKind.RightParen => ")",
                TokenKind.LeftBracket => "[",
                TokenKind.RightBracket => "]",
                TokenKind.Comma => ",",
                TokenKind.Dot => ".",
                TokenKind.Question => "?",
                TokenKind.Colon => ":",
                TokenKind.Plus => "+",
                TokenKind.Minus => "-",
                TokenKind.Star => "*",
                TokenKind.Slash => "/",
                TokenKind.Percent => "%",
                TokenKind.Bang => "!",
                TokenKind.Less => "<",
                TokenKind.LessEqual => "<=",
                TokenKind.Greater => ">",
                TokenKind.GreaterEqual => ">=",
                TokenKind.EqualEqual => "==",
                TokenKind.BangEqual => "!=",
                TokenKind.AndAnd => "&&",
                TokenKind.OrOr => "||",
                _ => kind.ToString()
            };
        }

        /// <summary>Internal control flow only; always caught in <see cref="TryParse"/>.</summary>
        private sealed class ParseException : Exception
        {
            internal ParseException(string message, int position) : base(message)
            {
                Position = position;
            }

            internal int Position { get; }
        }
    }
}
