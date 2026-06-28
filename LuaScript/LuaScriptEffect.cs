using System.Collections.Generic;
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

        [JsonIgnore] public bool IsTrack0Visible { get => _isTrack0Visible; private set => Set(ref _isTrack0Visible, value); }
        [JsonIgnore] public bool IsTrack1Visible { get => _isTrack1Visible; private set => Set(ref _isTrack1Visible, value); }
        [JsonIgnore] public bool IsTrack2Visible { get => _isTrack2Visible; private set => Set(ref _isTrack2Visible, value); }
        [JsonIgnore] public bool IsTrack3Visible { get => _isTrack3Visible; private set => Set(ref _isTrack3Visible, value); }
        [JsonIgnore] public bool IsCheck0Visible { get => _isCheck0Visible; private set => Set(ref _isCheck0Visible, value); }
        [JsonIgnore] public bool IsCheck1Visible { get => _isCheck1Visible; private set => Set(ref _isCheck1Visible, value); }
        [JsonIgnore] public bool IsCheck2Visible { get => _isCheck2Visible; private set => Set(ref _isCheck2Visible, value); }
        [JsonIgnore] public bool IsCheck3Visible { get => _isCheck3Visible; private set => Set(ref _isCheck3Visible, value); }
        [JsonIgnore] public bool IsColorVisible { get => _isColorVisible; private set => Set(ref _isColorVisible, value); }

        bool _isTrack0Visible = true;
        bool _isTrack1Visible = true;
        bool _isTrack2Visible = true;
        bool _isTrack3Visible = true;
        bool _isCheck0Visible;
        bool _isCheck1Visible;
        bool _isCheck2Visible;
        bool _isCheck3Visible;
        bool _isColorVisible;

        private void UpdateLayout()
        {
            var layout = AviUtlParameterLayout.Parse(_script);
            Layout = layout;

            bool hasAny = layout.HasAny;

            IsTrack0Visible = !hasAny || layout.HasTrack(0);
            IsTrack1Visible = !hasAny || layout.HasTrack(1);
            IsTrack2Visible = !hasAny || layout.HasTrack(2);
            IsTrack3Visible = !hasAny || layout.HasTrack(3);
            IsCheck0Visible = layout.HasCheck(0);
            IsCheck1Visible = layout.HasCheck(1);
            IsCheck2Visible = layout.HasCheck(2);
            IsCheck3Visible = layout.HasCheck(3);
            IsColorVisible = layout.HasColor;

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
