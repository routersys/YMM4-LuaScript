namespace LuaScript
{
    internal interface IBufferCompositor
    {
        bool TryCompose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command);
    }
}
