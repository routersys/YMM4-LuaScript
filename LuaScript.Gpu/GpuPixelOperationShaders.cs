using LuaScript.Engine.Processing;

[assembly: GpuPixelOperationShader("fill", "Shaders/fill.hlsl")]
[assembly: GpuPixelOperationShader("convolve", "Shaders/convolve.hlsl")]
[assembly: GpuPixelOperationShader("resize", "Shaders/resize.hlsl")]
