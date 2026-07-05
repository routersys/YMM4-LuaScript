Texture2D source : register(t0);

cbuffer Parameters : register(b0)
{
    float4 values[256];
};

float get_value(uint index)
{
    uint row = index >> 2;
    uint column = index & 3u;
    float4 value = values[row];
    if (column == 0u)
        return value.x;
    if (column == 1u)
        return value.y;
    if (column == 2u)
        return value.z;
    return value.w;
}

float convolve_channel(float value)
{
    return floor(saturate(value) * 255.0) / 255.0;
}

float4 convolve(float4 pos : SV_Position) : SV_Target
{
    uint2 p = (uint2)floor(pos.xy);
    int width = (int)get_value(0u);
    int height = (int)get_value(1u);
    float divisor = get_value(2u);
    float offset = get_value(3u) / 255.0;
    int kernelSize = (int)get_value(4u);
    int radius = (int)get_value(5u);
    float4 sum = 0.0;

    for (int ky = 0; ky < kernelSize; ky++)
    {
        int sy = clamp((int)p.y + ky - radius, 0, height - 1);
        for (int kx = 0; kx < kernelSize; kx++)
        {
            int sx = clamp((int)p.x + kx - radius, 0, width - 1);
            float kv = get_value((uint)(8 + ky * kernelSize + kx));
            float4 sourceValue = source[uint2((uint)sx, (uint)sy)];
            float3 rgb = sourceValue.a <= 0.0 ? 0.0 : saturate(sourceValue.rgb / sourceValue.a);
            sum += float4(rgb, sourceValue.a) * kv;
        }
    }

    float3 rgbOut = saturate(sum.rgb / divisor + offset);
    float alpha = saturate(sum.a / divisor + offset);
    return float4(
        convolve_channel(rgbOut.r * alpha),
        convolve_channel(rgbOut.g * alpha),
        convolve_channel(rgbOut.b * alpha),
        convolve_channel(alpha));
}
