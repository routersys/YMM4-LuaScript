using System;

namespace LuaScript.Engine.Processing
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class GpuPixelOperationShaderAttribute(string apiName, string fileName) : Attribute
    {
        public string ApiName { get; } = apiName;

        public string FileName { get; } = fileName;
    }
}
