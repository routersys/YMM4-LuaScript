namespace LuaScript
{
    internal interface IBufferCompositor
    {
        void Compose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command);
    }
}
