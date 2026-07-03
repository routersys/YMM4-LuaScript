namespace LuaScript
{
    internal sealed class MediaSourceLoader : IMediaSourceLoader
    {
        private TextRenderer? _textRenderer;
        private ImageDecoder? _imageDecoder;
        private MovieDecoder? _movieDecoder;

        public byte[] RenderText(string text, string fontFamily, double fontSize, bool bold, bool italic, int colorRgb, out int width, out int height)
        {
            _textRenderer ??= new TextRenderer();
            return _textRenderer.Render(text, fontFamily, fontSize, bold, italic, colorRgb, out width, out height);
        }

        public byte[] DecodeImage(string path, out int width, out int height)
        {
            _imageDecoder ??= new ImageDecoder();
            return _imageDecoder.Decode(path, out width, out height);
        }

        public byte[] DecodeMovie(string path, double time, out int width, out int height)
        {
            _movieDecoder ??= new MovieDecoder();
            return _movieDecoder.Decode(path, time, out width, out height);
        }

        public void Dispose()
        {
            _textRenderer?.Dispose();
            _imageDecoder?.Dispose();
            _movieDecoder?.Dispose();
        }
    }
}
