using System;
using System.Collections.Generic;

/// <summary>
/// Parses and evaluates boolean expressions with variables A/B/C.
/// Supports:
/// - OR: +
/// - AND: ·, *, &, or adjacency like (A+B)(B+C)
/// - NOT: prefix !, postfix ', or combining overline (A̅)
/// - Parentheses
/// </summary>
public static class BooleanExpressionEvaluator
{
    private enum TokenType
    {
        Variable,
        Or,
        And,
        Not,
        LeftParen,
        RightParen
    }

    private struct Token
    {
        public TokenType type;
        public char variableName;

        public Token(TokenType t, char v = '\0')
        {
            type = t;
            variableName = v;
        }
    }

    public static bool Evaluate(string expression, Dictionary<char, bool> values)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        List<Token> tokens = Tokenize(expression);
        List<Token> rpn = ToRpn(tokens);
        return EvaluateRpn(rpn, values);
    }

    public static bool AreEquivalent(string lhs, string rhs, char[] variables = null)
    {
        char[] vars = (variables != null && variables.Length > 0)
            ? variables
            : new[] { 'A', 'B', 'C' };

        int combinations = 1 << vars.Length;
        for (int mask = 0; mask < combinations; mask++)
        {
            var values = new Dictionary<char, bool>(vars.Length);
            for (int i = 0; i < vars.Length; i++)
                values[vars[i]] = (mask & (1 << i)) != 0;

            bool left = Evaluate(lhs, values);
            bool right = Evaluate(rhs, values);
            if (left != right) return false;
        }

        return true;
    }

    private static List<Token> Tokenize(string expression)
    {
        List<Token> raw = new List<Token>();

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];
            if (char.IsWhiteSpace(c)) continue;

            if (c == '(') { raw.Add(new Token(TokenType.LeftParen)); continue; }
            if (c == ')') { raw.Add(new Token(TokenType.RightParen)); continue; }
            if (c == '+') { raw.Add(new Token(TokenType.Or)); continue; }
            if (c == '·' || c == '*' || c == '&') { raw.Add(new Token(TokenType.And)); continue; }
            if (c == '!' || c == '~') { raw.Add(new Token(TokenType.Not)); continue; }

            if (char.IsLetter(c))
            {
                char upper = char.ToUpperInvariant(c);
                raw.Add(new Token(TokenType.Variable, upper));
                continue;
            }

            if (c == '\'')
            {
                // Postfix NOT marker; converted in a second pass.
                raw.Add(new Token(TokenType.Not));
                continue;
            }

            if (c == '\u0305')
            {
                // Combining overline: applies NOT to previous variable/group.
                raw.Add(new Token(TokenType.Not));
            }
        }

        return InsertImplicitAnd(raw);
    }

    private static List<Token> InsertImplicitAnd(List<Token> raw)
    {
        var result = new List<Token>();
        for (int i = 0; i < raw.Count; i++)
        {
            Token current = raw[i];
            result.Add(current);
            if (i == raw.Count - 1) continue;

            Token next = raw[i + 1];
            bool currentIsOperandEnd =
                current.type == TokenType.Variable || current.type == TokenType.RightParen;
            bool nextStartsOperand =
                next.type == TokenType.Variable || next.type == TokenType.LeftParen || next.type == TokenType.Not;

            if (currentIsOperandEnd && nextStartsOperand)
                result.Add(new Token(TokenType.And));
        }
        return result;
    }

    private static int Precedence(TokenType t)
    {
        switch (t)
        {
            case TokenType.Not: return 3;
            case TokenType.And: return 2;
            case TokenType.Or:  return 1;
            default:            return 0;
        }
    }

    private static bool IsRightAssociative(TokenType t) => t == TokenType.Not;

    private static List<Token> ToRpn(List<Token> tokens)
    {
        var output = new List<Token>();
        var ops = new Stack<Token>();

        foreach (Token token in tokens)
        {
            if (token.type == TokenType.Variable)
            {
                output.Add(token);
            }
            else if (token.type == TokenType.Not || token.type == TokenType.And || token.type == TokenType.Or)
            {
                while (ops.Count > 0)
                {
                    Token top = ops.Peek();
                    if (top.type == TokenType.LeftParen) break;

                    int pTop = Precedence(top.type);
                    int pTok = Precedence(token.type);
                    if (pTop > pTok || (pTop == pTok && !IsRightAssociative(token.type)))
                        output.Add(ops.Pop());
                    else
                        break;
                }
                ops.Push(token);
            }
            else if (token.type == TokenType.LeftParen)
            {
                ops.Push(token);
            }
            else if (token.type == TokenType.RightParen)
            {
                while (ops.Count > 0 && ops.Peek().type != TokenType.LeftParen)
                    output.Add(ops.Pop());

                if (ops.Count > 0 && ops.Peek().type == TokenType.LeftParen)
                    ops.Pop();
            }
        }

        while (ops.Count > 0)
            output.Add(ops.Pop());

        return output;
    }

    private static bool EvaluateRpn(List<Token> rpn, Dictionary<char, bool> values)
    {
        var stack = new Stack<bool>();

        foreach (Token token in rpn)
        {
            if (token.type == TokenType.Variable)
            {
                bool v = values != null && values.TryGetValue(token.variableName, out bool val) ? val : false;
                stack.Push(v);
            }
            else if (token.type == TokenType.Not)
            {
                if (stack.Count < 1) return false;
                stack.Push(!stack.Pop());
            }
            else if (token.type == TokenType.And)
            {
                if (stack.Count < 2) return false;
                bool b = stack.Pop();
                bool a = stack.Pop();
                stack.Push(a && b);
            }
            else if (token.type == TokenType.Or)
            {
                if (stack.Count < 2) return false;
                bool b = stack.Pop();
                bool a = stack.Pop();
                stack.Push(a || b);
            }
        }

        return stack.Count == 1 && stack.Pop();
    }
}
