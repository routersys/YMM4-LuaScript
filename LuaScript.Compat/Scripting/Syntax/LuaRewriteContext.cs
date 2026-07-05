using System.Collections.Generic;
using System.Text;

namespace LuaScript.Compat.Syntax
{
    internal sealed class LuaRewriteContext
    {
        private readonly List<LuaSyntaxToken> _input;
        private readonly List<LuaSyntaxToken> _output;
        private List<int>? _changedLines;
        private List<LuaRewriteDiagnostic>? _diagnostics;
        private int _line = 1;
        private int _lastChangedLine;
        private int _temporaries;

        public LuaRewriteContext(List<LuaSyntaxToken> input)
        {
            _input = input;
            _output = new List<LuaSyntaxToken>(input.Count);
        }

        public IReadOnlyList<LuaSyntaxToken> Input => _input;

        public List<LuaSyntaxToken> Output => _output;

        public int Index { get; set; }

        public bool Changed { get; private set; }

        public int[] ChangedLines => _changedLines is null ? [] : _changedLines.ToArray();

        public LuaRewriteDiagnostic[] Diagnostics => _diagnostics is null ? [] : _diagnostics.ToArray();

        public void AddDiagnostic(string message)
        {
            (_diagnostics ??= new List<LuaRewriteDiagnostic>()).Add(new LuaRewriteDiagnostic(_line, message));
            MarkChanged();
        }

        public bool AtEnd => Index >= _input.Count;

        public LuaSyntaxToken Current => _input[Index];

        public void Advance() => Index++;

        public void Emit(LuaSyntaxToken token)
        {
            _output.Add(token);
            if (token.Kind == LuaSyntaxTokenKind.Newline)
                _line++;
        }

        public void EmitRaw(string text)
        {
            _output.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Name, text));
            MarkChanged();
        }

        public void EmitOperator(string text)
        {
            _output.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Operator, text));
            MarkChanged();
        }

        public string NextTemporary() => "__lc" + _temporaries++;

        public int NextSignificant(int from)
        {
            for (int i = from; i < _input.Count; i++)
            {
                if (!_input[i].IsTrivia)
                    return i;
            }
            return -1;
        }

        public bool TryPreviousOutputSignificantSameLine(out LuaSyntaxToken token)
        {
            for (int i = _output.Count - 1; i >= 0; i--)
            {
                if (_output[i].Kind == LuaSyntaxTokenKind.Newline)
                    break;
                if (_output[i].IsTrivia)
                    continue;
                token = _output[i];
                return true;
            }
            token = default;
            return false;
        }

        public int NextSignificantSameLine(int from)
        {
            for (int i = from; i < _input.Count; i++)
            {
                if (_input[i].Kind == LuaSyntaxTokenKind.Newline)
                    return -1;
                if (_input[i].IsTrivia)
                    continue;
                return i;
            }
            return -1;
        }

        public void EmitWord(string word)
        {
            if (_output.Count > 0 && !_output[_output.Count - 1].IsTrivia)
                _output.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Whitespace, " "));
            _output.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Name, word));
            if (Index < _input.Count && !_input[Index].IsTrivia)
                _output.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Whitespace, " "));
            MarkChanged();
        }

        private void MarkChanged()
        {
            Changed = true;
            if (_line == _lastChangedLine)
                return;
            (_changedLines ??= new List<int>()).Add(_line);
            _lastChangedLine = _line;
        }

        public bool TryReadTrailingLvalue(out int start, out string text)
        {
            start = _output.Count;
            text = string.Empty;
            int end = _output.Count;
            while (end > 0 && _output[end - 1].IsTrivia)
                end--;
            if (end == 0)
                return false;

            int depth = 0;
            int begin = end;
            for (int i = end - 1; i >= 0; i--)
            {
                var token = _output[i];
                if (token.Kind == LuaSyntaxTokenKind.Operator)
                {
                    if (token.Text is ")" or "]" or "}")
                    {
                        depth++;
                        begin = i;
                        continue;
                    }
                    if (token.Text is "(" or "[" or "{")
                    {
                        if (depth == 0)
                            break;
                        depth--;
                        begin = i;
                        continue;
                    }
                    if (depth > 0 || token.Text == ".")
                    {
                        begin = i;
                        continue;
                    }
                    break;
                }
                if (token.Kind == LuaSyntaxTokenKind.Name)
                {
                    if (depth == 0 && LuaKeywords.IsReserved(token.Text))
                        break;
                    begin = i;
                    continue;
                }
                if (depth > 0)
                {
                    begin = i;
                    continue;
                }
                break;
            }

            while (begin < end && _output[begin].IsTrivia)
                begin++;
            if (begin >= end)
                return false;

            start = begin;
            text = Concat(begin, end);
            return true;
        }

        public bool TryPopOrOperand(out string text)
        {
            text = string.Empty;
            int end = _output.Count;
            while (end > 0 && _output[end - 1].IsTrivia)
                end--;
            if (end == 0)
                return false;

            int depth = 0;
            int begin = end;
            for (int i = end - 1; i >= 0; i--)
            {
                var token = _output[i];
                if (token.Kind == LuaSyntaxTokenKind.Operator)
                {
                    if (token.Text is ")" or "]" or "}")
                    {
                        depth++;
                        begin = i;
                        continue;
                    }
                    if (token.Text is "(" or "[" or "{")
                    {
                        if (depth == 0)
                            break;
                        depth--;
                        begin = i;
                        continue;
                    }
                    if (depth == 0 && token.Text is "=" or "," or ";")
                        break;
                    begin = i;
                    continue;
                }
                if (token.Kind == LuaSyntaxTokenKind.Newline && depth == 0)
                    break;
                if (token.Kind == LuaSyntaxTokenKind.Name && depth == 0 && LuaKeywords.IsExpressionBoundary(token.Text))
                    break;
                begin = i;
            }

            while (begin < end && _output[begin].IsTrivia)
                begin++;
            if (begin >= end)
                return false;

            text = Concat(begin, end);
            _output.RemoveRange(begin, _output.Count - begin);
            return true;
        }

        public void RemoveOutputFrom(int start) => _output.RemoveRange(start, _output.Count - start);

        public bool LastOutputIsTrivia() => _output.Count == 0 || _output[_output.Count - 1].IsTrivia;

        public string ReadLineExpression()
        {
            int start = Index;
            int depth = 0;
            while (Index < _input.Count)
            {
                var token = _input[Index];
                if (token.Kind == LuaSyntaxTokenKind.Newline && depth == 0)
                    break;
                if (token.Kind == LuaSyntaxTokenKind.Operator)
                {
                    if (token.Text is "(" or "[" or "{")
                        depth++;
                    else if (token.Text is ")" or "]" or "}")
                    {
                        if (depth == 0)
                            break;
                        depth--;
                    }
                    else if (depth == 0 && token.Text == ";")
                        break;
                }
                Index++;
            }
            return TrimmedRange(start, Index);
        }

        public string ReadForwardOrOperand()
        {
            int start = Index;
            int depth = 0;
            while (Index < _input.Count)
            {
                var token = _input[Index];
                if (token.Kind == LuaSyntaxTokenKind.Newline && depth == 0)
                    break;
                if (token.Kind == LuaSyntaxTokenKind.Operator)
                {
                    if (token.Text is "(" or "[" or "{")
                        depth++;
                    else if (token.Text is ")" or "]" or "}")
                    {
                        if (depth == 0)
                            break;
                        depth--;
                    }
                    else if (depth == 0 && token.Text is "," or ";")
                        break;
                }
                else if (token.Kind == LuaSyntaxTokenKind.Name && depth == 0 && LuaKeywords.IsExpressionBoundary(token.Text))
                    break;
                Index++;
            }
            return TrimmedRange(start, Index);
        }

        public string Serialize()
        {
            var builder = new StringBuilder();
            foreach (var token in _output)
                builder.Append(token.Text);
            return builder.ToString();
        }

        private string Concat(int begin, int end)
        {
            var builder = new StringBuilder();
            for (int i = begin; i < end; i++)
                builder.Append(_output[i].Text);
            return builder.ToString();
        }

        private string TrimmedRange(int begin, int end)
        {
            while (begin < end && _input[begin].IsTrivia)
                begin++;
            while (end > begin && _input[end - 1].IsTrivia)
                end--;
            var builder = new StringBuilder();
            for (int i = begin; i < end; i++)
                builder.Append(_input[i].Text);
            return builder.ToString();
        }
    }
}
