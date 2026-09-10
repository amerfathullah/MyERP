using System;
using System.Collections.Generic;
using System.Globalization;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Evaluates Quality Inspection acceptance formulas — arithmetic/comparison/logical
/// expressions over named reading variables (e.g. "reading_1 &gt;= 10 and reading_1 &lt;= 20").
/// Per ERPNext quality_inspection.py set_status_based_on_acceptance_formula, but implemented
/// as a hand-rolled recursive-descent parser instead of frappe.safe_eval: formula text is
/// user-authored template config (possibly from a lower-trust admin role), so there is
/// deliberately no function-call, reflection, or looping support — only +, -, *, /,
/// comparisons, and/or/not, parentheses, numbers and known variable names. Parser recursion
/// is bounded by formula length, so it cannot be used for a DoS.
/// </summary>
public static class AcceptanceFormulaEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="formula"/> against <paramref name="variables"/> and returns
    /// whether the result is truthy (non-zero). Throws <see cref="FormatException"/> for any
    /// syntax error, unknown variable, or division by zero.
    /// </summary>
    public static bool EvaluateBoolean(string formula, IReadOnlyDictionary<string, decimal> variables)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new FormatException("Formula is empty.");

        var parser = new Parser(formula, variables);
        var result = parser.ParseExpression();
        parser.ExpectEnd();
        return result != 0m;
    }

    private sealed class Parser
    {
        private readonly string _text;
        private readonly IReadOnlyDictionary<string, decimal> _variables;
        private int _pos;

        public Parser(string text, IReadOnlyDictionary<string, decimal> variables)
        {
            _text = text;
            _variables = variables;
            _pos = 0;
        }

        public void ExpectEnd()
        {
            SkipWhitespace();
            if (_pos != _text.Length)
                throw new FormatException($"Unexpected character at position {_pos} in formula '{_text}'.");
        }

        public decimal ParseExpression() => ParseOr();

        private decimal ParseOr()
        {
            var left = ParseAnd();
            while (TryConsumeWord("or"))
            {
                var right = ParseAnd();
                left = (left != 0m || right != 0m) ? 1m : 0m;
            }
            return left;
        }

        private decimal ParseAnd()
        {
            var left = ParseComparison();
            while (TryConsumeWord("and"))
            {
                var right = ParseComparison();
                left = (left != 0m && right != 0m) ? 1m : 0m;
            }
            return left;
        }

        private decimal ParseComparison()
        {
            var left = ParseAdditive();
            var op = TryConsumeOperator(">=", "<=", "==", "!=", ">", "<");
            if (op == null) return left;

            var right = ParseAdditive();
            return op switch
            {
                ">=" => left >= right ? 1m : 0m,
                "<=" => left <= right ? 1m : 0m,
                ">" => left > right ? 1m : 0m,
                "<" => left < right ? 1m : 0m,
                "==" => left == right ? 1m : 0m,
                "!=" => left != right ? 1m : 0m,
                _ => throw new FormatException("Unknown comparison operator."),
            };
        }

        private decimal ParseAdditive()
        {
            var left = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Peek() == '+') { _pos++; left += ParseTerm(); }
                else if (Peek() == '-') { _pos++; left -= ParseTerm(); }
                else break;
            }
            return left;
        }

        private decimal ParseTerm()
        {
            var left = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (Peek() == '*') { _pos++; left *= ParseUnary(); }
                else if (Peek() == '/')
                {
                    _pos++;
                    var divisor = ParseUnary();
                    if (divisor == 0m)
                        throw new FormatException("Division by zero in formula.");
                    left /= divisor;
                }
                else break;
            }
            return left;
        }

        private decimal ParseUnary()
        {
            SkipWhitespace();
            if (Peek() == '-') { _pos++; return -ParseUnary(); }
            if (Peek() == '+') { _pos++; return ParseUnary(); }
            if (TryConsumeWord("not")) return ParseUnary() == 0m ? 1m : 0m;
            return ParsePrimary();
        }

        private decimal ParsePrimary()
        {
            SkipWhitespace();
            if (Peek() == '(')
            {
                _pos++;
                var value = ParseExpression();
                SkipWhitespace();
                if (Peek() != ')')
                    throw new FormatException("Expected closing parenthesis in formula.");
                _pos++;
                return value;
            }

            if (char.IsDigit(Peek()) || Peek() == '.')
                return ParseNumber();

            if (char.IsLetter(Peek()) || Peek() == '_')
                return ParseIdentifier();

            throw new FormatException($"Unexpected character '{Peek()}' at position {_pos} in formula '{_text}'.");
        }

        private decimal ParseNumber()
        {
            var start = _pos;
            while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
                _pos++;
            var token = _text[start.._pos];
            if (!decimal.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"Invalid number '{token}' in formula.");
            return value;
        }

        private decimal ParseIdentifier()
        {
            var start = _pos;
            while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
                _pos++;
            var token = _text[start.._pos];
            if (!_variables.TryGetValue(token, out var value))
                throw new FormatException($"Unknown variable '{token}' in formula.");
            return value;
        }

        private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

        private void SkipWhitespace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
        }

        /// <summary>Consumes a keyword ("and"/"or"/"not") only on a word boundary, case-insensitively.</summary>
        private bool TryConsumeWord(string word)
        {
            SkipWhitespace();
            if (_pos + word.Length > _text.Length ||
                string.Compare(_text, _pos, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0)
                return false;

            var nextPos = _pos + word.Length;
            var boundaryOk = nextPos == _text.Length ||
                !(char.IsLetterOrDigit(_text[nextPos]) || _text[nextPos] == '_');
            if (!boundaryOk) return false;

            _pos = nextPos;
            return true;
        }

        private string? TryConsumeOperator(params string[] operators)
        {
            SkipWhitespace();
            foreach (var op in operators)
            {
                if (_pos + op.Length <= _text.Length &&
                    string.CompareOrdinal(_text, _pos, op, 0, op.Length) == 0)
                {
                    _pos += op.Length;
                    return op;
                }
            }
            return null;
        }
    }
}
