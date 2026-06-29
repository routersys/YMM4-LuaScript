using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Vortice;
using Vortice.D3DCompiler;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace LuaScript.Engine.Kernel
{
    internal sealed class GpuKernelEffect : ID2D1Effect
    {
        private const int ConstantsIndex = 0;
        private const int InitializedIndex = 1;

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct ConstantBuffer
        {
            public fixed float Values[HlslKernelEmitter.ConstantFloatCount];
        }

        [CustomEffect(1, null, null, null, null)]
        private sealed class Impl(Guid guid, string hlsl) : CustomEffectBase, ID2D1DrawTransform
        {
            private ConstantBuffer _constants;
            private ID2D1DrawInfo? _drawInfo;

            [CustomEffectProperty(PropertyType.Blob, ConstantsIndex)]
            public byte[] Constants
            {
                get => ToBytes(_constants);
                set
                {
                    FromBytes(value, ref _constants);
                    _drawInfo?.SetPixelShaderConstantBuffer(in _constants);
                }
            }

            [CustomEffectProperty(PropertyType.Bool, InitializedIndex)]
            public bool Initialized { get; private set; }

            public override void Initialize(ID2D1EffectContext effectContext, ID2D1TransformGraph transformGraph)
            {
                byte[] source = Encoding.ASCII.GetBytes(hlsl);
                var result = Compiler.Compile(source, "main", string.Empty, "ps_4_1", out var bytecode, out var errors);
                if (result.Failure || bytecode is null)
                {
                    Initialized = false;
                    Log.Default.Write("LuaScript: GPU kernel shader compilation failed.\n" + (errors?.AsString() ?? string.Empty));
                    errors?.Dispose();
                    bytecode?.Dispose();
                    return;
                }

                try
                {
                    byte[] shader = bytecode.AsBytes();
                    effectContext.LoadPixelShader(guid, shader, shader.Length);
                    transformGraph.SetSingleTransformNode(this);
                    Initialized = true;
                }
                finally
                {
                    errors?.Dispose();
                    bytecode.Dispose();
                }
            }

            public void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                _drawInfo = drawInfo;
                drawInfo.SetPixelShader(guid, PixelOptions.None);
                drawInfo.SetInputDescription(0, new InputDescription { Filter = (Filter)21, LevelOfDetailCount = 1 });
                drawInfo.SetPixelShaderConstantBuffer(in _constants);
            }

            public int GetInputCount() => 1;

            public void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects) =>
                inputRects[0] = outputRect;

            public void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                outputRect = inputRects[0];
                outputOpaqueSubRect = default;
            }

            public RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect) => invalidInputRect;

            private static unsafe byte[] ToBytes(ConstantBuffer constants)
            {
                byte[] bytes = new byte[sizeof(ConstantBuffer)];
                fixed (byte* destination = bytes)
                    *(ConstantBuffer*)destination = constants;
                return bytes;
            }

            private static unsafe void FromBytes(byte[] bytes, ref ConstantBuffer constants)
            {
                int length = Math.Min(bytes.Length, sizeof(ConstantBuffer));
                fixed (byte* source = bytes)
                fixed (ConstantBuffer* destination = &constants)
                    Buffer.MemoryCopy(source, destination, sizeof(ConstantBuffer), length);
            }
        }

        private sealed class Manager
        {
            private readonly Lock _locker = new();
            private readonly Dictionary<int, Dictionary<string, (Guid Guid, int Count)>> _registered = [];

            public Guid Acquire(IGraphicsDevicesAndContext devices, string hlsl)
            {
                lock (_locker)
                {
                    int device = devices.GetHashCode();
                    if (!_registered.TryGetValue(device, out var byHlsl))
                    {
                        byHlsl = [];
                        _registered[device] = byHlsl;
                    }
                    if (byHlsl.TryGetValue(hlsl, out var entry))
                    {
                        byHlsl[hlsl] = (entry.Guid, entry.Count + 1);
                        return entry.Guid;
                    }

                    var guid = Guid.NewGuid();
                    byHlsl[hlsl] = (guid, 1);
                    ((ID2D1Factory1)devices.D2D.Factory).RegisterEffect<Impl>(() => new Impl(guid, hlsl), guid);
                    return guid;
                }
            }

            public void Release(IGraphicsDevicesAndContext devices, string hlsl)
            {
                lock (_locker)
                {
                    int device = devices.GetHashCode();
                    if (!_registered.TryGetValue(device, out var byHlsl) || !byHlsl.TryGetValue(hlsl, out var entry))
                        return;
                    if (entry.Count > 1)
                    {
                        byHlsl[hlsl] = (entry.Guid, entry.Count - 1);
                        return;
                    }
                    byHlsl.Remove(hlsl);
                    ((ID2D1Factory1)devices.D2D.Factory).UnregisterEffect(entry.Guid);
                }
            }
        }

        private static readonly Manager Effects = new();

        private readonly IGraphicsDevicesAndContext _devices;
        private readonly string _hlsl;

        public GpuKernelEffect(IGraphicsDevicesAndContext devices, string hlsl)
            : base(Create(devices, hlsl))
        {
            _devices = devices;
            _hlsl = hlsl;
        }

        public bool IsReady => NativePointer != nint.Zero && ((ID2D1Properties)this).GetBoolValue(InitializedIndex);

        public void SetConstants(byte[] bytes) => ((ID2D1Properties)this).SetValue(ConstantsIndex, bytes);

        private static nint Create(IGraphicsDevicesAndContext devices, string hlsl)
        {
            try
            {
                var guid = Effects.Acquire(devices, hlsl);
                return ((ID2D1DeviceContext)devices.DeviceContext).CreateEffect(guid);
            }
            catch (Exception ex)
            {
                Log.Default.Write("LuaScript: failed to create GPU kernel effect.", ex);
                return nint.Zero;
            }
        }

        protected override void DisposeCore(nint nativePointer, bool disposing)
        {
            base.DisposeCore(nativePointer, disposing);
            Effects.Release(_devices, _hlsl);
        }
    }
}
