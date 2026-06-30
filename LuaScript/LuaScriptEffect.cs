using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using LuaScript.Compat;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace LuaScript
{
    [PluginDetails(AuthorName = "routersys", ContentId = "nc487743")]

    [VideoEffect(nameof(Texts.LuaScript), [VideoEffectCategories.Filtering], ["lua", "script", "スクリプト", "lua script", "アニメーション効果", "animation"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class LuaScriptEffect : VideoEffectBase, IScriptProvider
    {
        internal const string DefaultScript =
            """
            obj.alpha = math.min(time * 255, 255)
            """;

        public override string Label => Texts.LuaScript;

        [Display(GroupName = nameof(Texts.ScriptGroup), Name = nameof(Texts.ScriptCode), Description = nameof(Texts.ScriptCodeDesc), ResourceType = typeof(Texts))]
        [LuaScriptToolBar]
        public object? ToolBar => null;

        [Display(GroupName = nameof(Texts.ScriptGroup), ResourceType = typeof(Texts))]
        [CodeEditor(Language = "pack://application:,,,/LuaScript;component/Resources/SyntaxDefinitions/Lua-{theme}.xshd", FoldingStrategyType = typeof(LuaFoldingStrategy), AutoCompletionStrategyType = typeof(LuaAutoCompletionStrategy), ToolBarStrategyType = typeof(EmptyToolBarStrategy), PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public string Script
        {
            get => _script;
            set { if (Set(ref _script, value)) UpdateLayout(); }
        }
        string _script = DefaultScript;

        string IScriptProvider.DefaultScript => DefaultScript;

        internal AviUtlParameterLayout Layout { get; private set; } = AviUtlParameterLayout.Parse(DefaultScript);

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Track0), Description = nameof(Texts.TrackDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", -100, 100)]
        [ShowPropertyEditorWhen(nameof(IsTrack0Visible), true)]
        public Animation Track0 { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Track1), Description = nameof(Texts.TrackDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", -100, 100)]
        [ShowPropertyEditorWhen(nameof(IsTrack1Visible), true)]
        public Animation Track1 { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Track2), Description = nameof(Texts.TrackDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", -100, 100)]
        [ShowPropertyEditorWhen(nameof(IsTrack2Visible), true)]
        public Animation Track2 { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Track3), Description = nameof(Texts.TrackDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", -100, 100)]
        [ShowPropertyEditorWhen(nameof(IsTrack3Visible), true)]
        public Animation Track3 { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Slider0), Description = nameof(Texts.SliderDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 1, 10)]
        [DefaultValue(1d)]
        [ShowPropertyEditorWhen(nameof(IsSlider0Visible), true)]
        public double Slider0 { get => _slider0; set => Set(ref _slider0, value); }
        double _slider0 = 1d;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Slider1), Description = nameof(Texts.SliderDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "", 0, 100)]
        [DefaultValue(0d)]
        [ShowPropertyEditorWhen(nameof(IsSlider1Visible), true)]
        public double Slider1 { get => _slider1; set => Set(ref _slider1, value); }
        double _slider1;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Slider2), Description = nameof(Texts.SliderDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "", 0, 1000)]
        [DefaultValue(0d)]
        [ShowPropertyEditorWhen(nameof(IsSlider2Visible), true)]
        public double Slider2 { get => _slider2; set => Set(ref _slider2, value); }
        double _slider2;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Slider3), Description = nameof(Texts.SliderDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "", -1000, 1000)]
        [DefaultValue(0d)]
        [ShowPropertyEditorWhen(nameof(IsSlider3Visible), true)]
        public double Slider3 { get => _slider3; set => Set(ref _slider3, value); }
        double _slider3;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Check0), Description = nameof(Texts.CheckDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(IsCheck0Visible), true)]
        public bool Check0 { get => _check0; set => Set(ref _check0, value); }
        bool _check0;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Check1), Description = nameof(Texts.CheckDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(IsCheck1Visible), true)]
        public bool Check1 { get => _check1; set => Set(ref _check1, value); }
        bool _check1;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Check2), Description = nameof(Texts.CheckDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(IsCheck2Visible), true)]
        public bool Check2 { get => _check2; set => Set(ref _check2, value); }
        bool _check2;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Check3), Description = nameof(Texts.CheckDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(IsCheck3Visible), true)]
        public bool Check3 { get => _check3; set => Set(ref _check3, value); }
        bool _check3;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Color), Description = nameof(Texts.ColorDesc), ResourceType = typeof(Texts))]
        [ColorPicker]
        [ShowPropertyEditorWhen(nameof(IsColorVisible), true)]
        public Color Color { get => _color; set => Set(ref _color, value); }
        Color _color = Colors.White;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Text), Description = nameof(Texts.TextDesc), ResourceType = typeof(Texts))]
        [TextEditor(AcceptsReturn = true, PropertyEditorSize = PropertyEditorSize.FullWidth)]
        [ShowPropertyEditorWhen(nameof(IsTextVisible), true)]
        public string Text { get => _text; set => Set(ref _text, value); }
        string _text = string.Empty;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Font), Description = nameof(Texts.FontDesc), ResourceType = typeof(Texts))]
        [FontComboBox]
        [ShowPropertyEditorWhen(nameof(IsFontVisible), true)]
        public string Font { get => _font; set => Set(ref _font, value); }
        string _font = "メイリオ";

        [JsonIgnore] public bool IsSlider0Visible { get => _isSlider0Visible; private set => Set(ref _isSlider0Visible, value); }
        [JsonIgnore] public bool IsSlider1Visible { get => _isSlider1Visible; private set => Set(ref _isSlider1Visible, value); }
        [JsonIgnore] public bool IsSlider2Visible { get => _isSlider2Visible; private set => Set(ref _isSlider2Visible, value); }
        [JsonIgnore] public bool IsSlider3Visible { get => _isSlider3Visible; private set => Set(ref _isSlider3Visible, value); }
        [JsonIgnore] public bool IsTrack0Visible { get => _isTrack0Visible; private set => Set(ref _isTrack0Visible, value); }
        [JsonIgnore] public bool IsTrack1Visible { get => _isTrack1Visible; private set => Set(ref _isTrack1Visible, value); }
        [JsonIgnore] public bool IsTrack2Visible { get => _isTrack2Visible; private set => Set(ref _isTrack2Visible, value); }
        [JsonIgnore] public bool IsTrack3Visible { get => _isTrack3Visible; private set => Set(ref _isTrack3Visible, value); }
        [JsonIgnore] public bool IsCheck0Visible { get => _isCheck0Visible; private set => Set(ref _isCheck0Visible, value); }
        [JsonIgnore] public bool IsCheck1Visible { get => _isCheck1Visible; private set => Set(ref _isCheck1Visible, value); }
        [JsonIgnore] public bool IsCheck2Visible { get => _isCheck2Visible; private set => Set(ref _isCheck2Visible, value); }
        [JsonIgnore] public bool IsCheck3Visible { get => _isCheck3Visible; private set => Set(ref _isCheck3Visible, value); }
        [JsonIgnore] public bool IsColorVisible { get => _isColorVisible; private set => Set(ref _isColorVisible, value); }
        [JsonIgnore] public bool IsTextVisible { get => _isTextVisible; private set => Set(ref _isTextVisible, value); }
        [JsonIgnore] public bool IsFontVisible { get => _isFontVisible; private set => Set(ref _isFontVisible, value); }

        bool _isSlider0Visible;
        bool _isSlider1Visible;
        bool _isSlider2Visible;
        bool _isSlider3Visible;
        bool _isTrack0Visible = true;
        bool _isTrack1Visible = true;
        bool _isTrack2Visible = true;
        bool _isTrack3Visible = true;
        bool _isCheck0Visible;
        bool _isCheck1Visible;
        bool _isCheck2Visible;
        bool _isCheck3Visible;
        bool _isColorVisible;
        bool _isTextVisible;
        bool _isFontVisible;

        private void UpdateLayout()
        {
            var layout = AviUtlParameterLayout.Parse(_script);
            Layout = layout;

            bool hasAny = layout.HasAny;
            var usage = ScriptParameterUsage.Detect(_script);

            IsSlider0Visible = usage.Slider(0);
            IsSlider1Visible = usage.Slider(1);
            IsSlider2Visible = usage.Slider(2);
            IsSlider3Visible = usage.Slider(3);
            IsTrack0Visible = !hasAny || layout.HasTrack(0);
            IsTrack1Visible = !hasAny || layout.HasTrack(1);
            IsTrack2Visible = !hasAny || layout.HasTrack(2);
            IsTrack3Visible = !hasAny || layout.HasTrack(3);
            IsCheck0Visible = layout.HasCheck(0) || (!hasAny && usage.Check0);
            IsCheck1Visible = layout.HasCheck(1) || (!hasAny && usage.Check1);
            IsCheck2Visible = layout.HasCheck(2) || (!hasAny && usage.Check2);
            IsCheck3Visible = layout.HasCheck(3) || (!hasAny && usage.Check3);
            IsColorVisible = layout.HasColor || (!hasAny && usage.Color);
            IsTextVisible = usage.Uses("text");
            IsFontVisible = usage.Uses("font");

            SeedDefaults(layout);
        }

        private void SeedDefaults(AviUtlParameterLayout layout)
        {
            if (layout.GetCheck(0) is { Default: true } && !_check0) Check0 = true;
            if (layout.GetCheck(1) is { Default: true } && !_check1) Check1 = true;
            if (layout.GetCheck(2) is { Default: true } && !_check2) Check2 = true;
            if (layout.GetCheck(3) is { Default: true } && !_check3) Check3 = true;

            if (layout.Color is { } color && _color == Colors.White)
                Color = ToMediaColor(color.Default);
        }

        private static Color ToMediaColor(int rgb)
        {
            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);
            return Color.FromRgb(r, g, b);
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new LuaScriptEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [
            Track0, Track1, Track2, Track3,
        ];
    }
}
