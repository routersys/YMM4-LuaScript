using LuaScript.Compat;
using MoonSharp.Interpreter;

namespace LuaScript.Tests
{
    public class LuaSyntaxExtensionsTests
    {
        private static string Rewrite(string source) => LuaSyntaxExtensions.Rewrite(source);

        private static DynValue Eval(string source)
        {
            var script = new Script(CoreModules.Preset_SoftSandbox);
            script.DoString(AviUtlScript.Transform(source));
            return script.Globals.Get("result");
        }

        [Theory]
        [InlineData("a is not b", "a ~= b")]
        [InlineData("if x is not nil then end", "if x ~= nil then end")]
        [InlineData("a is  not b", "a ~= b")]
        [InlineData("a is\tnot b", "a ~= b")]
        [InlineData("a is\t  not b", "a ~= b")]
        [InlineData("a is not b and c is not d", "a ~= b and c ~= d")]
        [InlineData("f() is not nil", "f() ~= nil")]
        public void IsNot_RewritesToNotEqual(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Theory]
        [InlineData("a is b", "a == b")]
        [InlineData("x = a is b", "x = a == b")]
        [InlineData("if a is b then end", "if a == b then end")]
        [InlineData("a is nothing", "a == nothing")]
        [InlineData("f() is g()", "f() == g()")]
        public void Is_RewritesToEqual(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Theory]
        [InlineData("local is = 1\nx = is + 1")]
        [InlineData("is = 1")]
        [InlineData("obj.is = 1")]
        [InlineData("return is")]
        [InlineData("f(is)")]
        [InlineData("x = is")]
        [InlineData("print(x)\nis(y)")]
        [InlineData("island")]
        [InlineData("basis not x")]
        [InlineData("a isnot b")]
        public void Is_DoesNotClobberIdentifier(string source)
        {
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void Newline_BetweenIsAndNot_IsNotAnOperator()
        {
            const string source = "a is\nnot b";
            Assert.Equal(source, Rewrite(source));
        }

        [Theory]
        [InlineData("a.is not b")]
        [InlineData("a:is not b")]
        public void MemberAccess_IsNotRewritten(string source)
        {
            Assert.Equal(source, Rewrite(source));
        }

        [Theory]
        [InlineData("a != b", "a ~= b")]
        [InlineData("x = a != b", "x = a ~= b")]
        public void NotEqual_RewritesToTildeEqual(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Theory]
        [InlineData("x = !y", "x = not y")]
        [InlineData("!flag", "not flag")]
        [InlineData("a = !b and !c", "a = not b and not c")]
        public void Bang_RewritesToNot(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Theory]
        [InlineData("a && b", "a and b")]
        [InlineData("a&&b", "a and b")]
        [InlineData("a || b", "a or b")]
        [InlineData("a||b", "a or b")]
        [InlineData("a && b || c", "a and b or c")]
        public void LogicalAliases_Rewrite(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Theory]
        [InlineData("x += 1", "x = x + (1)")]
        [InlineData("x -= 1", "x = x - (1)")]
        [InlineData("x *= 2", "x = x * (2)")]
        [InlineData("x /= 2", "x = x / (2)")]
        [InlineData("x %= 2", "x = x % (2)")]
        [InlineData("x ^= 2", "x = x ^ (2)")]
        [InlineData("s ..= \"b\"", "s = s .. (\"b\")")]
        [InlineData("x ||= y", "x = x or (y)")]
        [InlineData("x &&= y", "x = x and (y)")]
        [InlineData("x *= a + b", "x = x * (a + b)")]
        [InlineData("t[i] += 1", "t[i] = t[i] + (1)")]
        [InlineData("obj.count += 1", "obj.count = obj.count + (1)")]
        [InlineData("x += f(a, b)", "x = x + (f(a, b))")]
        public void AugmentedAssignment_Rewrites(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Theory]
        [InlineData("x ??= y", "if x == nil then x = (y) end")]
        [InlineData("obj.field ??= 5", "if obj.field == nil then obj.field = (5) end")]
        [InlineData("  x ??= y", "  if x == nil then x = (y) end")]
        public void NilCoalesceAssignment_Rewrites(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Theory]
        [InlineData("i++", "i = i + 1")]
        [InlineData("count ++", "count = count + 1")]
        [InlineData("t[k]++", "t[k] = t[k] + 1")]
        public void Increment_Rewrites(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Fact]
        public void NilCoalesce_LowersToInlineFunction()
        {
            Assert.Equal(
                "x = (function() local __lc0 = (a) if __lc0 ~= nil then return __lc0 else return (b) end end)()",
                Rewrite("x = a ?? b"));
        }

        [Fact]
        public void NilCoalesce_CapturesFullOrOperand()
        {
            Assert.Equal(
                "x = (function() local __lc0 = (foo(a)) if __lc0 ~= nil then return __lc0 else return (b) end end)()",
                Rewrite("x = foo(a) ?? b"));
        }

        [Theory]
        [InlineData("for i in <0..10> do end", "for i = 0, 10 do end")]
        [InlineData("for i in <0..<10> do end", "for i = 0, (10) - 1 do end")]
        [InlineData("for i in <0..10, 2> do end", "for i = 0, 10, 2 do end")]
        [InlineData("for i in <0..<10, 2> do end", "for i = 0, (10) - 1, 2 do end")]
        [InlineData("for i in <a..b> do end", "for i = a, b do end")]
        public void NumericRange_Rewrites(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Theory]
        [InlineData("for k, v in pairs(t) do end")]
        [InlineData("for i, x in ipairs(list) do end")]
        [InlineData("for i in <0> do end")]
        public void NumericRange_DoesNotTouchGenericFor(string source)
        {
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void Comment_ContentIsPreserved()
        {
            const string source = "-- a is not b\nx = 1";
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void LongComment_ContentIsPreserved()
        {
            const string source = "--[[ a is not b ]]\nx = 1";
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void ShortString_ContentIsPreserved()
        {
            const string source = "x = \"a is not b ?? c += 1\"";
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void LongString_ContentIsPreserved()
        {
            const string source = "x = [==[ a is not b ]==]";
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void RewrittenCode_PreservesLineCount()
        {
            const string source = "a = 1\nb = a is not 2\nc = 3";
            string rewritten = Rewrite(source);
            Assert.Equal(source.Split('\n').Length, rewritten.Split('\n').Length);
            Assert.Contains("b = a ~= 2", rewritten);
        }

        [Fact]
        public void NoTrigger_ReturnsSameReference()
        {
            const string source = "obj.x = 1\nobj.y = 2";
            Assert.Same(source, Rewrite(source));
        }

        [Theory]
        [InlineData("", "")]
        public void Empty_IsUnchanged(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Fact]
        public void Is_EvaluatesEquality()
        {
            Assert.True(Eval("result = (1 is 1)").Boolean);
            Assert.False(Eval("result = (1 is 2)").Boolean);
        }

        [Fact]
        public void IsNot_EvaluatesInequality()
        {
            Assert.True(Eval("result = (1 is not 2)").Boolean);
            Assert.False(Eval("result = (2 is not 2)").Boolean);
        }

        [Fact]
        public void NotEqual_Evaluates()
        {
            Assert.True(Eval("result = (1 != 2)").Boolean);
        }

        [Fact]
        public void Bang_Evaluates()
        {
            Assert.True(Eval("result = (!false)").Boolean);
            Assert.False(Eval("result = (!true)").Boolean);
        }

        [Fact]
        public void LogicalAliases_Evaluate()
        {
            Assert.False(Eval("result = (true && false)").Boolean);
            Assert.Equal(4d, Eval("result = (false || 4)").Number);
        }

        [Fact]
        public void NilCoalesce_FallsBackOnlyOnNil()
        {
            Assert.Equal(7d, Eval("result = nil ?? 7").Number);
            Assert.Equal(3d, Eval("result = 3 ?? 7").Number);
            var falseResult = Eval("result = false ?? 7");
            Assert.Equal(DataType.Boolean, falseResult.Type);
            Assert.False(falseResult.Boolean);
        }

        [Fact]
        public void NilCoalesce_EvaluatesLeftOnce()
        {
            const string source = "calls = 0\nfunction f() calls = calls + 1 return 42 end\nresult = f() ?? 0\nassert(calls == 1)";
            var script = new Script(CoreModules.Preset_SoftSandbox);
            script.DoString(AviUtlScript.Transform(source));
            Assert.Equal(42d, script.Globals.Get("result").Number);
            Assert.Equal(1d, script.Globals.Get("calls").Number);
        }

        [Fact]
        public void NilCoalesceAssignment_Evaluates()
        {
            Assert.Equal(9d, Eval("x = nil\nx ??= 9\nresult = x").Number);
            Assert.Equal(4d, Eval("x = 4\nx ??= 9\nresult = x").Number);
        }

        [Fact]
        public void AugmentedAssignment_Evaluates()
        {
            Assert.Equal(8d, Eval("x = 5\nx += 3\nresult = x").Number);
            Assert.Equal(2d, Eval("x = 5\nx -= 3\nresult = x").Number);
        }

        [Fact]
        public void AugmentedAssignment_PreservesPrecedence()
        {
            Assert.Equal(14d, Eval("a = 5\nb = 2\nx = 2\nx *= a + b\nresult = x").Number);
        }

        [Fact]
        public void ConcatAssignment_Evaluates()
        {
            Assert.Equal("ab", Eval("s = \"a\"\ns ..= \"b\"\nresult = s").String);
        }

        [Fact]
        public void LogicalAssignment_Evaluates()
        {
            Assert.Equal(10d, Eval("x = false\nx ||= 10\nresult = x").Number);
            Assert.Equal(2d, Eval("x = 2\nx ||= 10\nresult = x").Number);
            Assert.Equal(5d, Eval("x = 1\nx &&= 5\nresult = x").Number);
            Assert.False(Eval("x = false\nx &&= 5\nresult = x").Boolean);
        }

        [Fact]
        public void Increment_Evaluates()
        {
            Assert.Equal(2d, Eval("x = 1\nx++\nresult = x").Number);
        }

        [Fact]
        public void NumericRange_InclusiveEvaluates()
        {
            Assert.Equal(6d, Eval("s = 0\nfor i in <1..3> do s = s + i end\nresult = s").Number);
        }

        [Fact]
        public void NumericRange_ExclusiveEvaluates()
        {
            Assert.Equal(6d, Eval("s = 0\nfor i in <1..<4> do s = s + i end\nresult = s").Number);
        }

        [Fact]
        public void NumericRange_SteppedEvaluates()
        {
            Assert.Equal(30d, Eval("s = 0\nfor i in <0..10, 2> do s = s + i end\nresult = s").Number);
        }
    }
}
