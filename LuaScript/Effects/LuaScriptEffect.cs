using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using LuaScript.Anchor;
using LuaScript.Compat;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Settings;

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
        [DefaultValue(1)]
        [ShowPropertyEditorWhen(nameof(IsSlider0Visible), true)]
        public int Slider0 { get => _slider0; set => Set(ref _slider0, value); }
        int _slider0 = 1;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Slider1), Description = nameof(Texts.SliderDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 0, 100)]
        [DefaultValue(0)]
        [ShowPropertyEditorWhen(nameof(IsSlider1Visible), true)]
        public int Slider1 { get => _slider1; set => Set(ref _slider1, value); }
        int _slider1;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Slider2), Description = nameof(Texts.SliderDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 0, 1000)]
        [DefaultValue(0)]
        [ShowPropertyEditorWhen(nameof(IsSlider2Visible), true)]
        public int Slider2 { get => _slider2; set => Set(ref _slider2, value); }
        int _slider2;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Slider3), Description = nameof(Texts.SliderDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", -1000, 1000)]
        [DefaultValue(0)]
        [ShowPropertyEditorWhen(nameof(IsSlider3Visible), true)]
        public int Slider3 { get => _slider3; set => Set(ref _slider3, value); }
        int _slider3;

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

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Directory), Description = nameof(Texts.DirectoryDesc), ResourceType = typeof(Texts))]
        [DirectorySelector(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        [ShowPropertyEditorWhen(nameof(IsDirectoryVisible), true)]
        public string Directory { get => _directory; set => Set(ref _directory, value); }
        string _directory = string.Empty;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.FileVideo), Description = nameof(Texts.FileDesc), ResourceType = typeof(Texts))]
        [FileSelector(FileGroupType.VideoItem)]
        [ShowPropertyEditorWhen(nameof(IsFileVideoVisible), true)]
        public string FileVideo { get => _fileVideo; set => Set(ref _fileVideo, value); }
        string _fileVideo = string.Empty;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.FileAudio), Description = nameof(Texts.FileDesc), ResourceType = typeof(Texts))]
        [FileSelector(FileGroupType.AudioItem)]
        [ShowPropertyEditorWhen(nameof(IsFileAudioVisible), true)]
        public string FileAudio { get => _fileAudio; set => Set(ref _fileAudio, value); }
        string _fileAudio = string.Empty;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.FileImage), Description = nameof(Texts.FileDesc), ResourceType = typeof(Texts))]
        [FileSelector(FileGroupType.ImageItem)]
        [ShowPropertyEditorWhen(nameof(IsFileImageVisible), true)]
        public string FileImage { get => _fileImage; set => Set(ref _fileImage, value); }
        string _fileImage = string.Empty;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.FileProject), Description = nameof(Texts.FileDesc), ResourceType = typeof(Texts))]
        [FileSelector(FileGroupType.Project)]
        [ShowPropertyEditorWhen(nameof(IsFileProjectVisible), true)]
        public string FileProject { get => _fileProject; set => Set(ref _fileProject, value); }
        string _fileProject = string.Empty;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.FileMp4), Description = nameof(Texts.FileDesc), ResourceType = typeof(Texts))]
        [FileSelector(FileGroupType.MP4)]
        [ShowPropertyEditorWhen(nameof(IsFileMp4Visible), true)]
        public string FileMp4 { get => _fileMp4; set => Set(ref _fileMp4, value); }
        string _fileMp4 = string.Empty;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.FileExo), Description = nameof(Texts.FileDesc), ResourceType = typeof(Texts))]
        [FileSelector(FileGroupType.Exo)]
        [ShowPropertyEditorWhen(nameof(IsFileExoVisible), true)]
        public string FileExo { get => _fileExo; set => Set(ref _fileExo, value); }
        string _fileExo = string.Empty;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.FileSubtitle), Description = nameof(Texts.FileDesc), ResourceType = typeof(Texts))]
        [FileSelector(FileGroupType.Subtitle)]
        [ShowPropertyEditorWhen(nameof(IsFileSubtitleVisible), true)]
        public string FileSubtitle { get => _fileSubtitle; set => Set(ref _fileSubtitle, value); }
        string _fileSubtitle = string.Empty;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.FileShader), Description = nameof(Texts.FileDesc), ResourceType = typeof(Texts))]
        [FileSelector(FileGroupType.PixelShaderSource)]
        [ShowPropertyEditorWhen(nameof(IsFileShaderVisible), true)]
        public string FileShader { get => _fileShader; set => Set(ref _fileShader, value); }
        string _fileShader = string.Empty;

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
        [JsonIgnore] public bool IsDirectoryVisible { get => _isDirectoryVisible; private set => Set(ref _isDirectoryVisible, value); }
        [JsonIgnore] public bool IsFileVideoVisible { get => _isFileVideoVisible; private set => Set(ref _isFileVideoVisible, value); }
        [JsonIgnore] public bool IsFileAudioVisible { get => _isFileAudioVisible; private set => Set(ref _isFileAudioVisible, value); }
        [JsonIgnore] public bool IsFileImageVisible { get => _isFileImageVisible; private set => Set(ref _isFileImageVisible, value); }
        [JsonIgnore] public bool IsFileProjectVisible { get => _isFileProjectVisible; private set => Set(ref _isFileProjectVisible, value); }
        [JsonIgnore] public bool IsFileMp4Visible { get => _isFileMp4Visible; private set => Set(ref _isFileMp4Visible, value); }
        [JsonIgnore] public bool IsFileExoVisible { get => _isFileExoVisible; private set => Set(ref _isFileExoVisible, value); }
        [JsonIgnore] public bool IsFileSubtitleVisible { get => _isFileSubtitleVisible; private set => Set(ref _isFileSubtitleVisible, value); }
        [JsonIgnore] public bool IsFileShaderVisible { get => _isFileShaderVisible; private set => Set(ref _isFileShaderVisible, value); }

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
        bool _isDirectoryVisible;
        bool _isFileVideoVisible;
        bool _isFileAudioVisible;
        bool _isFileImageVisible;
        bool _isFileProjectVisible;
        bool _isFileMp4Visible;
        bool _isFileExoVisible;
        bool _isFileSubtitleVisible;
        bool _isFileShaderVisible;

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
            IsDirectoryVisible = usage.Uses("dir");
            IsFileVideoVisible = usage.Uses("file_video");
            IsFileAudioVisible = usage.Uses("file_audio");
            IsFileImageVisible = usage.Uses("file_image");
            IsFileProjectVisible = usage.Uses("file_project");
            IsFileMp4Visible = usage.Uses("file_mp4");
            IsFileExoVisible = usage.Uses("file_exo");
            IsFileSubtitleVisible = usage.Uses("file_subtitle");
            IsFileShaderVisible = usage.Uses("file_shader");

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

        public ImmutableList<LuaAnchorPoint> Anchors
        {
            get => _anchors;
            set { if (Set(ref _anchors, value ?? ImmutableList<LuaAnchorPoint>.Empty)) AnchorVersion++; }
        }
        ImmutableList<LuaAnchorPoint> _anchors = ImmutableList<LuaAnchorPoint>.Empty;

        [JsonIgnore]
        internal int AnchorVersion { get; private set; }

        internal void ApplyAnchorDrag(string group, int index, double dx, double dy, double dz)
            => Anchors = AnchorSupport.ApplyDrag(Anchors, group, index, dx, dy, dz);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new LuaScriptEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [
            Track0, Track1, Track2, Track3,
        ];
    }
}
