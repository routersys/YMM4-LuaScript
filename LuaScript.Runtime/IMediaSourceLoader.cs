namespace LuaScript
{
    internal interface IMediaSourceLoader : IDisposable
    {
        byte[] RenderText(string text, string fontFamily, double fontSize, bool bold, bool italic, int colorRgb, out int width, out int height);
        byte[] DecodeImage(string path, out int width, out int height);
        byte[] DecodeMovie(string path, double time, out int width, out int height);
    }
}
