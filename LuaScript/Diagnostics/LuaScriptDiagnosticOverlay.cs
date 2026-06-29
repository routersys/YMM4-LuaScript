using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Rendering;

namespace LuaScript.Diagnostics
{
    internal sealed class LuaScriptDiagnosticOverlay : IBackgroundRenderer, ILuaScriptDiagnosticsListener
    {
        private static readonly FieldInfo? s_textViewsField = ResolveTextViewsField();

        private static readonly Pen s_squigglePen = CreateSquigglePen();

        private sealed class Marker : ISegment
        {
            public required int Offset { get; init; }
            public required int Length { get; init; }
            public int EndOffset => Offset + Length;
            public required LuaScriptDiagnostic Diagnostic { get; init; }
        }

        private readonly TextView _textView;
        private readonly ToolTip _toolTip = new() { Placement = PlacementMode.Mouse };
        private readonly List<Marker> _markers = [];
        private object? _renderedText;
        private bool _isDirty = true;

        public static void Attach(FoldingManager manager)
        {
            var textView = ResolveTextView(manager);
            if (textView is null)
                return;

            foreach (var renderer in textView.BackgroundRenderers)
            {
                if (renderer is LuaScriptDiagnosticOverlay)
                    return;
            }

            _ = new LuaScriptDiagnosticOverlay(textView);
        }

        private LuaScriptDiagnosticOverlay(TextView textView)
        {
            _textView = textView;
            textView.BackgroundRenderers.Add(this);
            textView.MouseHover += OnMouseHover;
            textView.MouseHoverStopped += OnMouseHoverStopped;
            LuaScriptDiagnostics.Instance.Subscribe(this);
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void OnDiagnosticsChanged()
        {
            _textView.Dispatcher.BeginInvoke(() =>
            {
                _isDirty = true;
                _textView.InvalidateLayer(KnownLayer.Selection);
            });
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var document = textView.Document;
            if (document is null)
                return;

            if (_isDirty || !ReferenceEquals(document.Text, _renderedText))
                Rebuild(document);

            if (_markers.Count == 0 || !textView.VisualLinesValid)
                return;

            foreach (var marker in _markers)
            {
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker, false))
                    DrawSquiggle(drawingContext, rect);
            }
        }

        private void Rebuild(TextDocument document)
        {
            _isDirty = false;
            _renderedText = document.Text;
            _markers.Clear();

            foreach (var diagnostic in LuaScriptDiagnostics.Instance.Get(document.Text))
            {
                if (TryCreateMarker(document, diagnostic, out var marker))
                    _markers.Add(marker);
            }
        }

        private static bool TryCreateMarker(TextDocument document, LuaScriptDiagnostic diagnostic, out Marker marker)
        {
            marker = null!;

            int lineNumber = diagnostic.Line > 0 ? diagnostic.Line : 1;
            if (lineNumber > document.LineCount)
                return false;

            var line = document.GetLineByNumber(lineNumber);

            int offset;
            int length;
            if (diagnostic.Line > 0 && diagnostic.Column > 0 && diagnostic.Length > 0)
            {
                offset = Math.Min(line.Offset + diagnostic.Column - 1, line.EndOffset);
                length = Math.Min(diagnostic.Length, line.EndOffset - offset);
            }
            else
            {
                offset = SkipLeadingWhitespace(document, line);
                length = line.EndOffset - offset;
            }

            if (length <= 0)
            {
                if (line.Length == 0)
                    return false;
                offset = line.Offset;
                length = line.Length;
            }

            marker = new Marker { Offset = offset, Length = length, Diagnostic = diagnostic };
            return true;
        }

        private static int SkipLeadingWhitespace(TextDocument document, DocumentLine line)
        {
            int offset = line.Offset;
            while (offset < line.EndOffset)
            {
                char c = document.GetCharAt(offset);
                if (c != ' ' && c != '\t')
                    break;
                offset++;
            }
            return offset;
        }

        private void OnMouseHover(object? sender, MouseEventArgs e)
        {
            var document = _textView.Document;
            if (document is null)
                return;

            var position = _textView.GetPositionFloor(e.GetPosition(_textView) + _textView.ScrollOffset);
            if (position is null)
                return;

            int offset = document.GetOffset(position.Value.Location);
            foreach (var marker in _markers)
            {
                if (offset < marker.Offset || offset > marker.EndOffset)
                    continue;

                _toolTip.Content = BuildToolTipContent(marker.Diagnostic);
                _toolTip.IsOpen = true;
                e.Handled = true;
                return;
            }
        }

        private void OnMouseHoverStopped(object? sender, MouseEventArgs e)
        {
            _toolTip.IsOpen = false;
        }

        private static object BuildToolTipContent(LuaScriptDiagnostic diagnostic)
        {
            var panel = new StackPanel { MaxWidth = 480 };
            panel.Children.Add(new TextBlock { Text = KindLabel(diagnostic.Kind), FontWeight = FontWeights.Bold });
            if (!string.IsNullOrEmpty(diagnostic.Message))
                panel.Children.Add(new TextBlock { Text = diagnostic.Message, TextWrapping = TextWrapping.Wrap });
            return panel;
        }

        private static string KindLabel(LuaScriptDiagnosticKind kind) => kind switch
        {
            LuaScriptDiagnosticKind.Compile => Texts.DiagnosticCompile,
            LuaScriptDiagnosticKind.Timeout => Texts.DiagnosticTimeout,
            _ => Texts.DiagnosticRuntime,
        };

        private static void DrawSquiggle(DrawingContext drawingContext, Rect rect)
        {
            if (rect.Width <= 0)
                return;

            const double step = 3.0;
            double baseline = rect.Bottom - 0.5;

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(rect.Left, baseline), false, false);

                var points = new List<Point>();
                int index = 0;
                for (double x = rect.Left; x <= rect.Right; x += step)
                {
                    points.Add(new Point(x, baseline - (index % 2 == 0 ? 0.0 : step)));
                    index++;
                }
                context.PolyLineTo(points, true, false);
            }
            geometry.Freeze();

            drawingContext.DrawGeometry(null, s_squigglePen, geometry);
        }

        private static Pen CreateSquigglePen()
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE5, 0x1A, 0x1A)), 1.0);
            pen.Brush.Freeze();
            pen.Freeze();
            return pen;
        }

        private static FieldInfo? ResolveTextViewsField()
        {
            foreach (var field in typeof(FoldingManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (typeof(IEnumerable<TextView>).IsAssignableFrom(field.FieldType))
                    return field;
            }
            return null;
        }

        private static TextView? ResolveTextView(FoldingManager? manager)
        {
            if (manager is null || s_textViewsField is null)
                return null;

            try
            {
                if (s_textViewsField.GetValue(manager) is IEnumerable<TextView> views)
                {
                    foreach (var view in views)
                    {
                        if (view is not null)
                            return view;
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}
