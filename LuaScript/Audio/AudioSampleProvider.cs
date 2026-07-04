using System.Reflection;
using LuaScript.Compat;
using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.FileSource;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.Settings;

namespace LuaScript
{
    internal sealed class AudioSampleProvider : IDisposable
    {
        private static readonly object s_ctorLock = new();
        private static ConstructorInfo? s_effectedSourceCtor;
        private static bool s_ctorResolved;

        private readonly AviUtlAudioConverter _converter = new();
        private readonly double[] _data = new double[AviUtlAudioConverter.MaxSamples];
        private float[] _readBuffer = [];

        private IAudioFileSource? _fileSource;
        private string _fileSourcePath = string.Empty;

        private IAudioStream? _sceneSource;
        private Scene? _sceneSourceScene;

        private IAudioStream? _itemSource;
        private IAudioItem? _itemSourceItem;
        private Scene? _itemSourceScene;
        private string? _itemSourceFile;

        public double[] Data => _data;

        public (int Count, int Rate) ReadFile(string path, double time, string type, int size)
        {
            try
            {
                if (_fileSource is null || !string.Equals(path, _fileSourcePath, StringComparison.Ordinal))
                {
                    _fileSource?.Dispose();
                    _fileSource = null;
                    _fileSource = AudioFileSourceFactory.Create(path, 0);
                    _fileSourcePath = path;
                }

                var source = _fileSource;
                if (source is null || source.Hz <= 0)
                    return (0, 0);

                int floats = EnsureReadBuffer(type, size);
                if (floats <= 0)
                    return (0, source.Hz);

                source.Seek(TimeSpan.FromSeconds(Math.Max(0d, time)));
                int total = 0;
                while (total < floats)
                {
                    int read = source.Read(_readBuffer, total, floats - total);
                    if (read <= 0)
                        break;
                    total += read;
                }

                return (_converter.Convert(type, _readBuffer, total / 2, size, _data), source.Hz);
            }
            catch
            {
                return (0, 0);
            }
        }

        public (int Count, int Rate) ReadScene(Scene? scene, double time, string type, int size)
        {
            try
            {
                if (scene is null)
                    return (0, 0);

                if (_sceneSource is null || !ReferenceEquals(scene, _sceneSourceScene))
                {
                    _sceneSource?.Dispose();
                    _sceneSource = null;
                    scene.TryCreateAudioSource(out _sceneSource);
                    _sceneSourceScene = scene;
                }

                return ReadStream(_sceneSource, time, type, size);
            }
            catch
            {
                return (0, 0);
            }
        }

        public (int Count, int Rate) ReadItem(IAudioItem? item, Scene? scene, double time, int fps, string type, int size)
        {
            try
            {
                if (item is null || scene is null)
                    return (0, 0);

                string? file = item switch
                {
                    AudioItem audio => audio.FilePath,
                    VideoItem video => video.FilePath,
                    _ => null,
                };
                if (_itemSource is null ||
                    !ReferenceEquals(item, _itemSourceItem) ||
                    !ReferenceEquals(scene, _itemSourceScene) ||
                    !string.Equals(file, _itemSourceFile, StringComparison.Ordinal))
                {
                    _itemSource?.Dispose();
                    _itemSource = null;
                    _itemSource = CreateEffectedSource(item, scene, fps) ?? CreateRawItemSource(item, scene);
                    _itemSourceItem = item;
                    _itemSourceScene = scene;
                    _itemSourceFile = file;
                }

                return ReadStream(_itemSource, time, type, size);
            }
            catch
            {
                return (0, 0);
            }
        }

        private (int Count, int Rate) ReadStream(IAudioStream? source, double time, string type, int size)
        {
            if (source is null || source.Hz <= 0)
                return (0, 0);

            int floats = EnsureReadBuffer(type, size);
            if (floats <= 0)
                return (0, source.Hz);

            source.Seek(TimeSpan.FromSeconds(Math.Max(0d, time)));
            int total = 0;
            while (total < floats)
            {
                int read = source.Read(_readBuffer, total, floats - total);
                if (read <= 0)
                    break;
                total += read;
            }

            return (_converter.Convert(type, _readBuffer, total / 2, size, _data), source.Hz);
        }

        private int EnsureReadBuffer(string type, int size)
        {
            int frames = AviUtlAudioConverter.RequiredFrames(type, size);
            if (frames <= 0)
                return 0;
            int floats = frames * 2;
            if (_readBuffer.Length < floats)
                _readBuffer = new float[floats];
            return floats;
        }

        private static IAudioStream? CreateRawItemSource(IAudioItem item, Scene scene)
        {
            try
            {
                return item.CreateAudioSource(scene);
            }
            catch
            {
                return null;
            }
        }

        private static IAudioStream? CreateEffectedSource(IAudioItem item, Scene scene, int fps)
        {
            var ctor = ResolveEffectedSourceCtor();
            if (ctor is null)
                return null;
            try
            {
                return ctor.Invoke([item, scene, 0, fps, ResamplerMode.Linear, false, false, false]) as IAudioStream;
            }
            catch
            {
                return null;
            }
        }

        private static ConstructorInfo? ResolveEffectedSourceCtor()
        {
            if (s_ctorResolved)
                return s_effectedSourceCtor;
            lock (s_ctorLock)
            {
                if (!s_ctorResolved)
                {
                    try
                    {
                        s_effectedSourceCtor = typeof(Scene).Assembly
                            .GetType("YukkuriMovieMaker.Player.Audio.EffectedItemSource")?
                            .GetConstructor([
                                typeof(IAudioItem), typeof(Scene), typeof(int), typeof(int),
                                typeof(ResamplerMode), typeof(bool), typeof(bool), typeof(bool)]);
                    }
                    catch
                    {
                        s_effectedSourceCtor = null;
                    }
                    s_ctorResolved = true;
                }
            }
            return s_effectedSourceCtor;
        }

        public void Dispose()
        {
            _fileSource?.Dispose();
            _fileSource = null;
            _sceneSource?.Dispose();
            _sceneSource = null;
            _sceneSourceScene = null;
            _itemSource?.Dispose();
            _itemSource = null;
            _itemSourceItem = null;
            _itemSourceScene = null;
            _itemSourceFile = null;
        }
    }
}
