using System.Collections.Generic;

namespace LuaScript.Engine.Kernel
{
    internal sealed class LuaParser
    {
        private readonly List<LuaToken> _tokens;
        private int _pos;

        private LuaParser(List<LuaToken> tokens) => _tokens = tokens;

        public static IReadOnlyList<LuaStmt> Parse(string source)
        {
            var parser = new LuaParser(LuaLexer.Tokenize(source));
            var block = parser.ParseBlock();
            parser.Expect(LuaTokenKind.Eof);
            return block;
        }

        private LuaToken Current => _tokens[_pos];

        private LuaToken Advance() => _tokens[_pos++];

        private bool IsBlockEnd()
        {
            var t = Current;
            return t.Kind == LuaTokenKind.Eof ||
                t.IsKeyword("end") || t.IsKeyword("else") || t.IsKeyword("elseif") || t.IsKeyword("until");
        }

        private IReadOnlyList<LuaStmt> ParseBlock()
        {
            var statements = new List<LuaStmt>();
            while (!IsBlockEnd())
            {
                if (Current.IsSymbol(";"))
                {
                    Advance();
                    continue;
                }
                statements.Add(ParseStatement());
            }
            return statements;
        }

        private LuaStmt ParseStatement()
        {
            var t = Current;
            if (t.IsKeyword("local"))
                return ParseLocal();
            if (t.IsKeyword("if"))
                return ParseIf();
            if (t.IsKeyword("for"))
                return ParseFor();
            if (t.Kind == LuaTokenKind.Keyword)
                throw new KernelUnsupportedException($"Unsupported statement '{t.Text}'.");
            return ParseExpressionStatement();
        }

        private LuaStmt ParseLocal()
        {
            Advance();
            if (Current.IsKeyword("function"))
                throw new KernelUnsupportedException("Local functions are not supported.");

            var names = new List<string> { ExpectName() };
            while (Current.IsSymbol(","))
            {
                Advance();
                names.Add(ExpectName());
            }

            IReadOnlyList<LuaExpr> values = [];
            if (Current.IsSymbol("="))
            {
                Advance();
                values = ParseExpressionList();
            }
            return new LocalStmt(names, values);
        }

        private LuaStmt ParseIf()
        {
            Advance();
            var clauses = new List<IfClause>();

            var condition = ParseExpression();
            ExpectKeyword("then");
            clauses.Add(new IfClause(condition, ParseBlock()));

            while (Current.IsKeyword("elseif"))
            {
                Advance();
                var elseifCondition = ParseExpression();
                ExpectKeyword("then");
                clauses.Add(new IfClause(elseifCondition, ParseBlock()));
            }

            IReadOnlyList<LuaStmt>? elseBody = null;
            if (Current.IsKeyword("else"))
            {
                Advance();
                elseBody = ParseBlock();
            }

            ExpectKeyword("end");
            return new IfStmt(clauses, elseBody);
        }

        private LuaStmt ParseFor()
        {
            Advance();
            string variable = ExpectName();
            if (!Current.IsSymbol("="))
                throw new KernelUnsupportedException("Generic for loops are not supported.");

            Advance();
            var start = ParseExpression();
            ExpectSymbol(",");
            var stop = ParseExpression();

            LuaExpr? step = null;
            if (Current.IsSymbol(","))
            {
                Advance();
                step = ParseExpression();
            }

            ExpectKeyword("do");
            var body = ParseBlock();
            ExpectKeyword("end");
            return new NumericForStmt(variable, start, stop, step, body);
        }

        private LuaStmt ParseExpressionStatement()
        {
            var first = ParseSuffixedExpression();

            if (Current.IsSymbol("=") || Current.IsSymbol(","))
            {
                var targets = new List<LuaExpr> { first };
                while (Current.IsSymbol(","))
                {
                    Advance();
                    targets.Add(ParseSuffixedExpression());
                }
                ExpectSymbol("=");
                var values = ParseExpressionList();
                return new AssignStmt(targets, values);
            }

            if (first is CallExpr call)
                return new CallStmt(call);

            throw new KernelUnsupportedException("Expression is not a valid statement.");
        }

        private List<LuaExpr> ParseExpressionList()
        {
            var values = new List<LuaExpr> { ParseExpression() };
            while (Current.IsSymbol(","))
            {
                Advance();
                values.Add(ParseExpression());
            }
            return values;
        }

        private LuaExpr ParseExpression(int minPrecedence = 0)
        {
            var left = ParseUnary();
            while (true)
            {
                var (op, precedence, rightAssociative) = PeekBinaryOperator();
                if (op is null || precedence < minPrecedence)
                    break;
                Advance();
                int nextMinimum = rightAssociative ? precedence : precedence + 1;
                var right = ParseExpression(nextMinimum);
                left = new BinaryExpr(op, left, right);
            }
            return left;
        }

        private LuaExpr ParseUnary()
        {
            var t = Current;
            if (t.IsKeyword("not") || t.IsSymbol("-") || t.IsSymbol("#"))
            {
                Advance();
                return new UnaryExpr(t.Text, ParseExpression(7));
            }
            return ParseValue();
        }

        private (string? Operator, int Precedence, bool RightAssociative) PeekBinaryOperator()
        {
            var t = Current;
            if (t.IsKeyword("or")) return ("or", 1, false);
            if (t.IsKeyword("and")) return ("and", 2, false);
            if (t.Kind == LuaTokenKind.Symbol)
            {
                switch (t.Text)
                {
                    case "<":
                    case ">":
                    case "<=":
                    case ">=":
                    case "~=":
                    case "==":
                        return (t.Text, 3, false);
                    case "..":
                        return ("..", 4, true);
                    case "+":
                    case "-":
                        return (t.Text, 5, false);
                    case "*":
                    case "/":
                    case "%":
                        return (t.Text, 6, false);
                    case "^":
                        return ("^", 8, true);
                }
            }
            return (null, 0, false);
        }

        private LuaExpr ParseValue()
        {
            var t = Current;
            switch (t.Kind)
            {
                case LuaTokenKind.Number:
                    Advance();
                    return new NumberExpr(t.Number);
                case LuaTokenKind.String:
                    Advance();
                    return new StringExpr(t.Text);
                case LuaTokenKind.Keyword when t.Text == "true":
                    Advance();
                    return new BoolExpr(true);
                case LuaTokenKind.Keyword when t.Text == "false":
                    Advance();
                    return new BoolExpr(false);
                case LuaTokenKind.Keyword when t.Text == "nil":
                    Advance();
                    return new NilExpr();
            }

            if (t.IsKeyword("function") || t.IsSymbol("{") || t.IsSymbol("..."))
                throw new KernelUnsupportedException($"Unsupported expression near '{t.Text}'.");

            return ParseSuffixedExpression();
        }

        private LuaExpr ParseSuffixedExpression()
        {
            var expression = ParsePrimaryExpression();
            while (true)
            {
                var t = Current;
                if (t.IsSymbol("."))
                {
                    Advance();
                    expression = new MemberExpr(expression, ExpectName());
                }
                else if (t.IsSymbol("["))
                {
                    Advance();
                    var key = ParseExpression();
                    ExpectSymbol("]");
                    expression = new IndexExpr(expression, key);
                }
                else if (t.IsSymbol("("))
                {
                    expression = new CallExpr(expression, ParseArguments());
                }
                else
                {
                    break;
                }
            }
            return expression;
        }

        private LuaExpr ParsePrimaryExpression()
        {
            var t = Current;
            if (t.IsSymbol("("))
            {
                Advance();
                var inner = ParseExpression();
                ExpectSymbol(")");
                return inner;
            }
            if (t.Kind == LuaTokenKind.Name)
            {
                Advance();
                return new NameExpr(t.Text);
            }
            throw new KernelUnsupportedException($"Unexpected token '{t.Text}'.");
        }

        private List<LuaExpr> ParseArguments()
        {
            ExpectSymbol("(");
            var arguments = new List<LuaExpr>();
            if (!Current.IsSymbol(")"))
            {
                arguments.Add(ParseExpression());
                while (Current.IsSymbol(","))
                {
                    Advance();
                    arguments.Add(ParseExpression());
                }
            }
            ExpectSymbol(")");
            return arguments;
        }

        private string ExpectName()
        {
            if (Current.Kind != LuaTokenKind.Name)
                throw new KernelUnsupportedException($"Expected a name but found '{Current.Text}'.");
            return Advance().Text;
        }

        private void Expect(LuaTokenKind kind)
        {
            if (Current.Kind != kind)
                throw new KernelUnsupportedException($"Expected {kind} but found '{Current.Text}'.");
            Advance();
        }

        private void ExpectSymbol(string symbol)
        {
            if (!Current.IsSymbol(symbol))
                throw new KernelUnsupportedException($"Expected '{symbol}' but found '{Current.Text}'.");
            Advance();
        }

        private void ExpectKeyword(string keyword)
        {
            if (!Current.IsKeyword(keyword))
                throw new KernelUnsupportedException($"Expected '{keyword}' but found '{Current.Text}'.");
            Advance();
        }
    }
}
