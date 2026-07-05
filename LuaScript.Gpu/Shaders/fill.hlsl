Texture2D source : register(t0);

cbuffer Parameters : register(b0)
{
    float4 color;
    float4 rect;
};

float4 fill_full(float4 pos : SV_Position) : SV_Target
{
    return color;
}

float4 fill_partial(float4 pos : SV_Position) : SV_Target
{
    uint2 p = (uint2)floor(pos.xy);
    uint4 r = (uint4)rect;
    if (p.x >= r.x && p.y >= r.y && p.x < r.z && p.y < r.w)
        return color;
    return source[p];
}
