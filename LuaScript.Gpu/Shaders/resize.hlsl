Texture2D source : register(t0);

cbuffer Parameters : register(b0)
{
    float4 size;
};

float channel(float value)
{
    return floor(saturate(value) * 255.0) / 255.0;
}

float4 resize_pack(float4 value)
{
    return float4(channel(value.r), channel(value.g), channel(value.b), channel(value.a));
}

float4 resize_nearest(float4 pos : SV_Position) : SV_Target
{
    uint2 p = (uint2)floor(pos.xy);
    uint sourceWidth = (uint)size.x;
    uint sourceHeight = (uint)size.y;
    uint targetWidth = (uint)size.z;
    uint targetHeight = (uint)size.w;
    uint sx = min(((p.x * 2u + 1u) * sourceWidth) / (targetWidth * 2u), sourceWidth - 1u);
    uint sy = min(((p.y * 2u + 1u) * sourceHeight) / (targetHeight * 2u), sourceHeight - 1u);
    return source[uint2(sx, sy)];
}

float4 resize_linear(float4 pos : SV_Position) : SV_Target
{
    uint2 p = (uint2)floor(pos.xy);
    float sourceWidth = size.x;
    float sourceHeight = size.y;
    float targetWidth = size.z;
    float targetHeight = size.w;
    float u = (p.x + 0.5) * sourceWidth / targetWidth - 0.5;
    float v = (p.y + 0.5) * sourceHeight / targetHeight - 0.5;
    int x0 = (int)floor(u);
    int y0 = (int)floor(v);
    float tx = u - x0;
    float ty = v - y0;
    int maxX = (int)sourceWidth - 1;
    int maxY = (int)sourceHeight - 1;
    uint xA = (uint)clamp(x0, 0, maxX);
    uint yA = (uint)clamp(y0, 0, maxY);
    uint xB = (uint)clamp(x0 + 1, 0, maxX);
    uint yB = (uint)clamp(y0 + 1, 0, maxY);
    float4 c00 = source[uint2(xA, yA)];
    float4 c10 = source[uint2(xB, yA)];
    float4 c01 = source[uint2(xA, yB)];
    float4 c11 = source[uint2(xB, yB)];
    float4 top = c00 * (1.0 - tx) + c10 * tx;
    float4 bottom = c01 * (1.0 - tx) + c11 * tx;
    return resize_pack(top * (1.0 - ty) + bottom * ty);
}
