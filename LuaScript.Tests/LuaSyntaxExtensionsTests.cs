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

        [Fact]
        public void Ternary_Rewrites()
        {
            Assert.Equal(
                "x = (function() if c then return (1) else return (2) end end)()",
                Rewrite("x = c ? 1 : 2"));
        }

        [Fact]
        public void Ternary_Evaluates()
        {
            Assert.Equal(1d, Eval("result = true ? 1 : 2").Number);
            Assert.Equal(2d, Eval("result = false ? 1 : 2").Number);
        }

        [Fact]
        public void Ternary_ReturnsFalsyBranch()
        {
            var value = Eval("result = true ? false : 1");
            Assert.Equal(DataType.Boolean, value.Type);
            Assert.False(value.Boolean);
        }

        [Fact]
        public void Ternary_InsideCallArgument()
        {
            Assert.Equal(3d, Eval("function f(v, w) return v + w end\nresult = f(true ? 1 : 2, 2)").Number);
        }

        [Fact]
        public void Ternary_NestedInElse()
        {
            Assert.Equal(3d, Eval("a = 3\nresult = a == 1 ? 1 : a == 2 ? 2 : 3").Number);
        }

        [Theory]
        [InlineData("x = a and b or c")]
        [InlineData("t = { x = 1, y = 2 }")]
        [InlineData("obj:draw()")]
        [InlineData("::label::")]
        public void Ternary_DoesNotTouchOtherCode(string source)
        {
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void SafeNavigation_ReturnsNilForNilTarget()
        {
            Assert.True(Eval("t = nil\nresult = t?.x == nil").Boolean);
        }

        [Fact]
        public void SafeNavigation_ReadsMember()
        {
            Assert.Equal(5d, Eval("t = { x = 5 }\nresult = t?.x").Number);
        }

        [Fact]
        public void SafeNavigation_Chains()
        {
            Assert.True(Eval("t = { a = nil }\nresult = t?.a?.b == nil").Boolean);
            Assert.Equal(7d, Eval("t = { a = { b = 7 } }\nresult = t?.a?.b").Number);
        }

        [Fact]
        public void SafeNavigation_Index()
        {
            Assert.True(Eval("t = nil\nresult = t?[1] == nil").Boolean);
            Assert.Equal(9d, Eval("t = { 9 }\nresult = t?[1]").Number);
        }

        [Fact]
        public void SafeNavigation_EvaluatesTargetOnce()
        {
            const string source = "calls = 0\nfunction f() calls = calls + 1 return { x = 3 } end\nresult = f()?.x";
            var script = new Script(CoreModules.Preset_SoftSandbox);
            script.DoString(AviUtlScript.Transform(source));
            Assert.Equal(3d, script.Globals.Get("result").Number);
            Assert.Equal(1d, script.Globals.Get("calls").Number);
        }

        [Fact]
        public void SafeNavigation_CombinesWithNilCoalesce()
        {
            Assert.Equal(4d, Eval("t = nil\nresult = t?.x ?? 4").Number);
        }

        [Fact]
        public void Lambda_Rewrites()
        {
            Assert.Equal(
                "f = (function(a, b) return a + b end)",
                Rewrite("f = (a, b) => a + b"));
        }

        [Fact]
        public void Lambda_SingleParameterWithoutParens()
        {
            Assert.Equal(9d, Eval("f = x => x * 3\nresult = f(3)").Number);
        }

        [Fact]
        public void Lambda_NoParameters()
        {
            Assert.Equal(1d, Eval("f = () => 1\nresult = f()").Number);
        }

        [Fact]
        public void Lambda_AsSortComparer()
        {
            const string source = "t = { 3, 1, 2 }\ntable.sort(t, (a, b) => a < b)\nresult = t[1]";
            Assert.Equal(1d, Eval(source).Number);
        }

        [Fact]
        public void Lambda_DoesNotTouchComparisons()
        {
            Assert.Equal("x = a >= b", Rewrite("x = a >= b"));
            Assert.Equal("x = a <= b", Rewrite("x = a <= b"));
        }

        [Fact]
        public void InterpolatedString_Rewrites()
        {
            Assert.Equal(
                "s = (\"x=\" .. tostring(a))",
                Rewrite("s = `x={a}`"));
        }

        [Fact]
        public void InterpolatedString_Evaluates()
        {
            Assert.Equal("x=5 y=6", Eval("a = 5\nb = 6\nresult = `x={a} y={b}`").String);
        }

        [Fact]
        public void InterpolatedString_PlainLiteral()
        {
            Assert.Equal("hello", Eval("result = `hello`").String);
        }

        [Fact]
        public void InterpolatedString_ExpressionOnly()
        {
            Assert.Equal("7", Eval("result = `{3 + 4}`").String);
        }

        [Fact]
        public void InterpolatedString_NestedExtensionSyntax()
        {
            Assert.Equal("4", Eval("t = nil\nresult = `{t ?? 4}`").String);
        }

        [Fact]
        public void InterpolatedString_Empty()
        {
            Assert.Equal("", Eval("result = ``").String);
        }

        [Fact]
        public void Backtick_InsideStringOrComment_IsPreserved()
        {
            const string source = "x = \"a ` b\"";
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void Pipe_Rewrites()
        {
            Assert.Equal("x = f((v))", Rewrite("x = v |> f"));
        }

        [Fact]
        public void Pipe_Evaluates()
        {
            Assert.Equal(4d, Eval("function double(v) return v * 2 end\nresult = 2 |> double").Number);
        }

        [Fact]
        public void Pipe_InsertsAsFirstArgument()
        {
            Assert.Equal(
                "x = clamp((v), 0, 255)",
                Rewrite("x = v |> clamp(0, 255)"));
            Assert.Equal(5d, Eval("function clamp(v, lo, hi) return math.min(math.max(v, lo), hi) end\nresult = 5 |> clamp(0, 255)").Number);
        }

        [Fact]
        public void Pipe_Chains()
        {
            const string source = "function inc(v) return v + 1 end\nfunction double(v) return v * 2 end\nresult = 3 |> inc |> double";
            Assert.Equal(8d, Eval(source).Number);
        }

        [Fact]
        public void Pipe_MemberFunction()
        {
            Assert.Equal(2d, Eval("result = 2.9 |> math.floor").Number);
        }

        [Fact]
        public void RangeMembership_Rewrites()
        {
            Assert.Equal(
                "x = (function() local __lc0 = (v) return __lc0 >= (1) and __lc0 <= (5) end)()",
                Rewrite("x = v in <1..5>"));
        }

        [Fact]
        public void RangeMembership_Evaluates()
        {
            Assert.True(Eval("result = 3 in <1..5>").Boolean);
            Assert.True(Eval("result = 5 in <1..5>").Boolean);
            Assert.False(Eval("result = 6 in <1..5>").Boolean);
        }

        [Fact]
        public void RangeMembership_ExclusiveEvaluates()
        {
            Assert.False(Eval("result = 5 in <1..<5>").Boolean);
            Assert.True(Eval("result = 4 in <1..<5>").Boolean);
        }

        [Fact]
        public void RangeMembership_InsideIf()
        {
            Assert.Equal(1d, Eval("v = 40\nresult = 0\nif v in <30..60> then result = 1 end").Number);
        }

        [Fact]
        public void RangeMembership_EvaluatesLeftOnce()
        {
            const string source = "calls = 0\nfunction f() calls = calls + 1 return 3 end\nresult = f() in <1..5>";
            var script = new Script(CoreModules.Preset_SoftSandbox);
            script.DoString(AviUtlScript.Transform(source));
            Assert.True(script.Globals.Get("result").Boolean);
            Assert.Equal(1d, script.Globals.Get("calls").Number);
        }

        [Fact]
        public void RangeMembership_DoesNotTouchForHeaders()
        {
            Assert.Equal("for i = 0, 10 do end", Rewrite("for i in <0..10> do end"));
        }

        [Theory]
        [InlineData("[fast] obj.fill(0, 0, 0, 255)", " __fast_fill(0, 0, 0, 255)")]
        [InlineData("[fast] obj.convolve(k, 3)", " __fast_convolve(k, 3)")]
        [InlineData("[fast] obj.resize(100, 100)", " __fast_resize(100, 100)")]
        [InlineData("[ fast ] obj.resize(100, 100)", " __fast_resize(100, 100)")]
        [InlineData("[fast]obj.fill()", "__fast_fill()")]
        [InlineData("    [fast] obj.fill()", "     __fast_fill()")]
        public void Fast_RewritesAcceleratedPrimitive(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Theory]
        [InlineData("[fast]\nobj.fill(0, 0, 0, 255)", "\n__fast_fill(0, 0, 0, 255)")]
        [InlineData("[fast]\nobj.convolve(k, 3)", "\n__fast_convolve(k, 3)")]
        [InlineData("[fast]\nobj.resize(64, 64)", "\n__fast_resize(64, 64)")]
        [InlineData("obj.x = 1\n[fast]\nobj.resize(64, 64)", "obj.x = 1\n\n__fast_resize(64, 64)")]
        public void Fast_MarkerOnOwnLine_RewritesNextStatement(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Fact]
        public void Fast_MarkerOnOwnLine_ReportsDiagnosticOnMarkerLine()
        {
            var (code, _, diagnostics) = LuaSyntaxExtensions.RewriteWithMap("obj.x = 1\n[fast]\nobj.draw(0)");
            Assert.Equal("obj.x = 1\n\nobj.draw(0)", code);
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal(2, diagnostic.Line);
        }

        [Fact]
        public void Fast_RewritesInsideBlock()
        {
            Assert.Equal(
                "for i = 1, 3 do  __fast_fill(i, i, i, 255) end",
                Rewrite("for i in <1..3> do [fast] obj.fill(i, i, i, 255) end"));
        }

        [Theory]
        [InlineData("x = t[fast]")]
        [InlineData("x = obj.data[fast]")]
        [InlineData("x = f()[fast]")]
        [InlineData("t = { [fast] = 1 }")]
        [InlineData("x = \"[fast] obj.fill()\"")]
        [InlineData("-- [fast] obj.fill()")]
        public void Fast_DoesNotTouchNonMarkerBrackets(string source)
        {
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void Fast_OnUnsupportedTargetStripsMarkerAndReportsDiagnostic()
        {
            var (code, _, diagnostics) = LuaSyntaxExtensions.RewriteWithMap("[fast] obj.draw(0)");
            Assert.Equal(" obj.draw(0)", code);
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal(1, diagnostic.Line);
            Assert.Contains("fast", diagnostic.Message);
        }

        [Fact]
        public void Fast_OnNonObjTargetReportsDiagnostic()
        {
            var (code, _, diagnostics) = LuaSyntaxExtensions.RewriteWithMap("[fast] local x = 1");
            Assert.Equal(" local x = 1", code);
            Assert.Single(diagnostics);
        }

        [Fact]
        public void Fast_SupportedTargetEmitsNoDiagnostic()
        {
            var (_, _, diagnostics) = LuaSyntaxExtensions.RewriteWithMap("[fast] obj.fill(0, 0, 0, 255)");
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void Fast_ReportsDiagnosticLineWithinBlock()
        {
            var (_, _, diagnostics) = LuaSyntaxExtensions.RewriteWithMap("obj.x = 1\n[fast] obj.draw(0)");
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal(2, diagnostic.Line);
        }
    }
}
