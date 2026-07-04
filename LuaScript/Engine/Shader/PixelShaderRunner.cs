using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace LuaScript.Engine.Shader
{
    internal sealed unsafe class PixelShaderRunner : IPixelShaderRunner, IDisposable
    {
        private const int MaxResources = 8;
        private const int ConstantBufferSize = 4096;
        private const int RandomSeed = 20260704;

        private const string VertexShaderSource = """
            struct VsOutput { float4 pos : SV_Position; float2 uv : TEXCOORD0; };
            VsOutput vsmain(uint id : SV_VertexID)
            {
                VsOutput output;
                float2 t = float2((id << 1) & 2, id & 2);
                output.uv = t;
                output.pos = float4(t.x * 2.0 - 1.0, 1.0 - t.y * 2.0, 0.0, 1.0);
                return output;
            }
            """;

        private static readonly FeatureLevel[] s_featureLevels = [FeatureLevel.Level_11_0];

        private sealed record CompiledShader(ID3D11PixelShader? Shader, string? Error);

        private struct InputSlot
        {
            public ID3D11Texture2D? Texture;
            public ID3D11ShaderResourceView? View;
            public int Width;
            public int Height;
        }

        private readonly Lock _locker = new();
        private readonly Dictionary<(string Hlsl, string Entry), CompiledShader> _shaders = [];
        private readonly InputSlot[] _inputs = new InputSlot[MaxResources];
        private readonly ID3D11ShaderResourceView[] _boundViews = new ID3D11ShaderResourceView[MaxResources];
        private readonly ID3D11ShaderResourceView[] _nullViews = new ID3D11ShaderResourceView[MaxResources];

        private byte[] _compositeBuffer = [];

        public IBufferCompositor Compositor { get; set; } = SoftwareCompositor.Instance;

        private ID3D11Device? _device;
        private ID3D11DeviceContext? _context;
        private ID3D11VertexShader? _vertexShader;
        private ID3D11Buffer? _constantBuffer;
        private ID3D11RasterizerState? _rasterizer;
        private readonly ID3D11BlendState?[] _blendStates = new ID3D11BlendState?[4];
        private readonly ID3D11SamplerState?[] _samplerStates = new ID3D11SamplerState?[6];
        private ID3D11Texture2D? _randomTexture;
        private ID3D11ShaderResourceView? _randomView;
        private ID3D11Texture2D? _targetTexture;
        private ID3D11RenderTargetView? _targetView;
        private int _targetWidth;
        private int _targetHeight;
        private ID3D11Texture2D? _stagingTexture;
        private int _stagingWidth;
        private int _stagingHeight;

        private bool _disposed;
        private bool _unavailable;
        private bool _failureLogged;
        private int _recreateBudget = 3;

        public PixelShaderRunStatus TryRun(
            string hlsl,
            string entryPoint,
            ReadOnlySpan<PixelShaderInput> resources,
            ReadOnlySpan<float> constants,
            PixelShaderBlend blend,
            PixelShaderSampler sampler,
            byte[] target,
            int targetWidth,
            int targetHeight,
            out string? error)
        {
            error = null;
            lock (_locker)
            {
                if (_disposed || _unavailable)
                    return PixelShaderRunStatus.Unavailable;
                if (targetWidth <= 0 || targetHeight <= 0 || (long)target.Length < (long)targetWidth * targetHeight * 4)
                    return PixelShaderRunStatus.Unavailable;

                try
                {
                    if (!EnsureDevice())
                        return PixelShaderRunStatus.Unavailable;

                    var compiled = GetOrCompile(hlsl, entryPoint);
                    if (compiled.Shader is null)
                    {
                        error = compiled.Error;
                        return PixelShaderRunStatus.CompileError;
                    }

                    Execute(compiled.Shader, resources, constants, blend, sampler, target, targetWidth, targetHeight);
                    return PixelShaderRunStatus.Success;
                }
                catch (Exception ex)
                {
                    HandleFailure(ex);
                    return PixelShaderRunStatus.Unavailable;
                }
            }
        }

        public void Invalidate()
        {
            lock (_locker)
            {
                ReleaseShaders();
            }
        }

        public void Dispose()
        {
            lock (_locker)
            {
                if (_disposed)
                    return;
                _disposed = true;
                ReleaseDeviceObjects();
            }
        }

        private bool EnsureDevice()
        {
            if (_device is not null)
                return true;
            if (_recreateBudget <= 0)
            {
                MarkUnavailable(null);
                return false;
            }
            _recreateBudget--;

            if (!TryCreateDevice(DriverType.Hardware, out var device, out var context) &&
                !TryCreateDevice(DriverType.Warp, out device, out context))
            {
                MarkUnavailable(null);
                return false;
            }

            try
            {
                var vsBytecode = CompileOrThrow(VertexShaderSource, "vsmain", "vs_4_0");
                var vertexShader = device!.CreateVertexShader(vsBytecode);
                _device = device;
                _context = context;
                _vertexShader = vertexShader;
                _constantBuffer = device.CreateBuffer(new BufferDescription(
                    ConstantBufferSize, BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write));
                _rasterizer = device.CreateRasterizerState(RasterizerDescription.CullNone);
                CreateBlendStates(device);
                CreateSamplerStates(device);
                return true;
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(_device, device))
                {
                    ReleaseDeviceObjects();
                }
                else
                {
                    context?.Dispose();
                    device?.Dispose();
                }
                MarkUnavailable(ex);
                return false;
            }
        }

        private static bool TryCreateDevice(DriverType driverType, out ID3D11Device? device, out ID3D11DeviceContext? context)
        {
            device = null;
            context = null;
            try
            {
                var result = D3D11.D3D11CreateDevice(
                    null, driverType, DeviceCreationFlags.BgraSupport, s_featureLevels,
                    out device, out context);
                if (result.Success && device is not null && context is not null)
                    return true;
            }
            catch
            {
            }
            context?.Dispose();
            device?.Dispose();
            device = null;
            context = null;
            return false;
        }

        private void CreateBlendStates(ID3D11Device device)
        {
            _blendStates[(int)PixelShaderBlendMode.Copy] = null;
            _blendStates[(int)PixelShaderBlendMode.Mask] = device.CreateBlendState(new BlendDescription(
                Blend.Zero, Blend.One, Blend.Zero, Blend.SourceAlpha));
            _blendStates[(int)PixelShaderBlendMode.Draw] = device.CreateBlendState(new BlendDescription(
                Blend.One, Blend.InverseSourceAlpha, Blend.One, Blend.InverseSourceAlpha));
            _blendStates[(int)PixelShaderBlendMode.Add] = device.CreateBlendState(new BlendDescription(
                Blend.One, Blend.One, Blend.One, Blend.One));
        }

        private void CreateSamplerStates(ID3D11Device device)
        {
            _samplerStates[(int)PixelShaderSampler.None] = null;
            _samplerStates[(int)PixelShaderSampler.Clip] = CreateSampler(device, Filter.MinMagMipLinear, TextureAddressMode.Border);
            _samplerStates[(int)PixelShaderSampler.Clamp] = CreateSampler(device, Filter.MinMagMipLinear, TextureAddressMode.Clamp);
            _samplerStates[(int)PixelShaderSampler.Loop] = CreateSampler(device, Filter.MinMagMipLinear, TextureAddressMode.Wrap);
            _samplerStates[(int)PixelShaderSampler.Mirror] = CreateSampler(device, Filter.MinMagMipLinear, TextureAddressMode.Mirror);
            _samplerStates[(int)PixelShaderSampler.Dot] = CreateSampler(device, Filter.MinMagMipPoint, TextureAddressMode.Border);
        }

        private static ID3D11SamplerState CreateSampler(ID3D11Device device, Filter filter, TextureAddressMode address)
        {
            var description = new SamplerDescription(filter, address, address, address)
            {
                BorderColor = new Color4(0f, 0f, 0f, 0f),
            };
            return device.CreateSamplerState(description);
        }

        private CompiledShader GetOrCompile(string hlsl, string entryPoint)
        {
            var key = (hlsl, entryPoint);
            if (_shaders.TryGetValue(key, out var cached))
                return cached;

            CompiledShader compiled;
            byte[] source = Encoding.UTF8.GetBytes(hlsl);
            var result = Compiler.Compile(source, entryPoint, string.Empty, "ps_5_0", out var bytecode, out var errors);
            if (result.Failure || bytecode is null)
            {
                compiled = new CompiledShader(null, errors?.AsString()?.Trim() ?? result.ToString());
                errors?.Dispose();
                bytecode?.Dispose();
            }
            else
            {
                try
                {
                    compiled = new CompiledShader(_device!.CreatePixelShader(bytecode.AsSpan()), null);
                }
                finally
                {
                    errors?.Dispose();
                    bytecode.Dispose();
                }
            }

            _shaders[key] = compiled;
            return compiled;
        }

        private static byte[] CompileOrThrow(string source, string entryPoint, string profile)
        {
            var result = Compiler.Compile(Encoding.UTF8.GetBytes(source), entryPoint, string.Empty, profile, out var bytecode, out var errors);
            try
            {
                if (result.Failure || bytecode is null)
                    throw new InvalidOperationException(errors?.AsString() ?? result.ToString());
                return bytecode.AsBytes();
            }
            finally
            {
                errors?.Dispose();
                bytecode?.Dispose();
            }
        }

        private void Execute(
            ID3D11PixelShader shader,
            ReadOnlySpan<PixelShaderInput> resources,
            ReadOnlySpan<float> constants,
            PixelShaderBlend blend,
            PixelShaderSampler sampler,
            byte[] target,
            int width,
            int height)
        {
            var context = _context!;

            bool composite = blend.Mode == PixelShaderBlendMode.Composite;
            EnsureTarget(width, height);
            if (!composite && blend.Mode != PixelShaderBlendMode.Copy)
                UploadPixels(_targetTexture!, target, width, height);

            UploadConstants(constants);
            BindResources(resources);

            context.OMSetRenderTargets(_targetView!);
            context.OMSetBlendState(composite ? null : _blendStates[(int)blend.Mode]);
            context.RSSetViewport(0f, 0f, width, height);
            context.RSSetState(_rasterizer);
            context.IASetInputLayout(null);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.VSSetShader(_vertexShader);
            context.PSSetShader(shader);
            context.PSSetConstantBuffer(0, _constantBuffer);
            context.PSSetSampler(0, _samplerStates[(int)sampler]);
            context.PSSetShaderResources(0, _boundViews);
            context.Draw(3, 0);
            context.PSSetShaderResources(0, _nullViews);
            context.UnsetRenderTargets();

            if (composite)
            {
                int length = width * height * 4;
                if (_compositeBuffer.Length < length)
                    _compositeBuffer = new byte[length];
                ReadBack(_compositeBuffer, width, height);
                Compositor.TryCompose(target, width, height, _compositeBuffer, width, height,
                    new DrawCommand(width * 0.5, height * 0.5, 0d, 1d, 1d, 0d, null, 0d, blend.CompositeBlend));
            }
            else
            {
                ReadBack(target, width, height);
            }
        }

        private void EnsureTarget(int width, int height)
        {
            if (_targetTexture is null || _targetWidth != width || _targetHeight != height)
            {
                _targetView?.Dispose();
                _targetTexture?.Dispose();
                _targetTexture = _device!.CreateTexture2D(new Texture2DDescription
                {
                    Width = width,
                    Height = height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.RenderTarget,
                });
                _targetView = _device.CreateRenderTargetView(_targetTexture);
                _targetWidth = width;
                _targetHeight = height;
            }

            if (_stagingTexture is null || _stagingWidth != width || _stagingHeight != height)
            {
                _stagingTexture?.Dispose();
                _stagingTexture = _device!.CreateTexture2D(new Texture2DDescription
                {
                    Width = width,
                    Height = height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging,
                    CPUAccessFlags = CpuAccessFlags.Read,
                });
                _stagingWidth = width;
                _stagingHeight = height;
            }
        }

        private void UploadPixels(ID3D11Texture2D texture, byte[] pixels, int width, int height)
        {
            fixed (byte* source = pixels)
                _context!.UpdateSubresource(texture, 0, null, (nint)source, width * 4, 0);
        }

        private void UploadConstants(ReadOnlySpan<float> constants)
        {
            var mapped = _context!.Map(_constantBuffer!, MapMode.WriteDiscard);
            try
            {
                var destination = new Span<float>((void*)mapped.DataPointer, ConstantBufferSize / sizeof(float));
                constants.CopyTo(destination);
                destination[constants.Length..].Clear();
            }
            finally
            {
                _context.Unmap(_constantBuffer!);
            }
        }

        private void BindResources(ReadOnlySpan<PixelShaderInput> resources)
        {
            for (int i = 0; i < MaxResources; i++)
            {
                if (i >= resources.Length)
                {
                    _boundViews[i] = null!;
                    continue;
                }

                var resource = resources[i];
                if (resource.IsRandom)
                {
                    _boundViews[i] = EnsureRandomView();
                    continue;
                }

                ref var slot = ref _inputs[i];
                if (slot.Texture is null || slot.Width != resource.Width || slot.Height != resource.Height)
                {
                    slot.View?.Dispose();
                    slot.Texture?.Dispose();
                    slot.Texture = _device!.CreateTexture2D(new Texture2DDescription
                    {
                        Width = resource.Width,
                        Height = resource.Height,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = Format.B8G8R8A8_UNorm,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.ShaderResource,
                    });
                    slot.View = _device.CreateShaderResourceView(slot.Texture);
                    slot.Width = resource.Width;
                    slot.Height = resource.Height;
                }

                UploadPixels(slot.Texture, resource.Data!, resource.Width, resource.Height);
                _boundViews[i] = slot.View!;
            }
        }

        private ID3D11ShaderResourceView EnsureRandomView()
        {
            if (_randomView is not null)
                return _randomView;

            int size = PixelShaderInput.RandomSize;
            var values = new float[size * size];
            var random = new Random(RandomSeed);
            for (int i = 0; i < values.Length; i++)
                values[i] = (float)random.NextDouble();

            fixed (float* source = values)
            {
                _randomTexture = _device!.CreateTexture2D(new Texture2DDescription
                {
                    Width = size,
                    Height = size,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.R32_Float,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Immutable,
                    BindFlags = BindFlags.ShaderResource,
                }, [new SubresourceData((nint)source, size * sizeof(float))]);
            }
            _randomView = _device!.CreateShaderResourceView(_randomTexture);
            return _randomView;
        }

        private void ReadBack(byte[] target, int width, int height)
        {
            var context = _context!;
            context.CopyResource(_stagingTexture!, _targetTexture!);
            var mapped = context.Map(_stagingTexture!, 0, MapMode.Read);
            try
            {
                int rowBytes = width * 4;
                byte* source = (byte*)mapped.DataPointer;
                fixed (byte* destination = target)
                {
                    if (mapped.RowPitch == rowBytes)
                    {
                        Buffer.MemoryCopy(source, destination, target.Length, (long)rowBytes * height);
                    }
                    else
                    {
                        for (int row = 0; row < height; row++)
                            Buffer.MemoryCopy(
                                source + (long)mapped.RowPitch * row,
                                destination + (long)rowBytes * row,
                                rowBytes, rowBytes);
                    }
                }
            }
            finally
            {
                context.Unmap(_stagingTexture!, 0);
            }
        }

        private void HandleFailure(Exception ex)
        {
            if (!_failureLogged)
            {
                _failureLogged = true;
                Log.Default.Write("LuaScript: obj.pixelshader execution failed; the call is skipped.", ex);
            }
            ReleaseDeviceObjects();
            if (_recreateBudget <= 0)
                _unavailable = true;
        }

        private void MarkUnavailable(Exception? ex)
        {
            _unavailable = true;
            if (_failureLogged)
                return;
            _failureLogged = true;
            if (ex is null)
                Log.Default.Write("LuaScript: Direct3D 11 is unavailable; obj.pixelshader is disabled.");
            else
                Log.Default.Write("LuaScript: Direct3D 11 initialization failed; obj.pixelshader is disabled.", ex);
        }

        private void ReleaseShaders()
        {
            foreach (var compiled in _shaders.Values)
                compiled.Shader?.Dispose();
            _shaders.Clear();
        }

        private void ReleaseDeviceObjects()
        {
            ReleaseShaders();
            for (int i = 0; i < _inputs.Length; i++)
            {
                _inputs[i].View?.Dispose();
                _inputs[i].Texture?.Dispose();
                _inputs[i] = default;
                _boundViews[i] = null!;
            }
            for (int i = 0; i < _blendStates.Length; i++)
            {
                _blendStates[i]?.Dispose();
                _blendStates[i] = null;
            }
            for (int i = 0; i < _samplerStates.Length; i++)
            {
                _samplerStates[i]?.Dispose();
                _samplerStates[i] = null;
            }
            _randomView?.Dispose();
            _randomView = null;
            _randomTexture?.Dispose();
            _randomTexture = null;
            _targetView?.Dispose();
            _targetView = null;
            _targetTexture?.Dispose();
            _targetTexture = null;
            _targetWidth = 0;
            _targetHeight = 0;
            _stagingTexture?.Dispose();
            _stagingTexture = null;
            _stagingWidth = 0;
            _stagingHeight = 0;
            _constantBuffer?.Dispose();
            _constantBuffer = null;
            _rasterizer?.Dispose();
            _rasterizer = null;
            _vertexShader?.Dispose();
            _vertexShader = null;
            _context?.Dispose();
            _context = null;
            _device?.Dispose();
            _device = null;
        }
    }
}
