using System;

namespace LuaScript.Engine.Processing
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal sealed class GpuPixelOperationShaderAttribute(string methodName, string fileName, string entryPoint) : Attribute
    {
        public string MethodName { get; } = methodName;

        public string FileName { get; } = fileName;

        public string EntryPoint { get; } = entryPoint;
    }
}
