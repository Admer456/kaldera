
struct VertexData
{
    float3 Position : POSITION;
    float2 Uv : TEXCOORD0;
}

struct PixelInput
{
    float2 Uv;
}

struct VertexOutput
{
    PixelInput Pixel : PIXEL_INPUT;
    float4 OutPosition : SV_Position;
}

[shader("vertex")]
VertexOutput VertexMain( VertexData in )
{
    VertexOutput result;
    result.OutPosition = float4( in.Position, 1.0 );
    result.Pixel.Uv = in.Uv;
    return result;
}

[shader("pixel")]
float4 PixelMain( PixelInput in : PIXEL_INPUT ) : SV_Target
{
    // This is Elegy Engine's modified "missing texture" pattern
    float px2rad = 3.14159;
    float targetRes = 32.0;
    float tiling = targetRes / 2.0;

    float x = in.Uv.x * targetRes;
    float y = in.Uv.y * targetRes;

    float3 col = float3(
        0.3 + 0.15 * sin( px2rad * (x + y) / tiling ),
        0.33 + 0.2 * sin( px2rad * y / tiling ),
        0.33 + 0.2 * sin( px2rad * x / tiling )
    );

    float2 iuv = fmod( float2( x, y ), 8.0 );
    if ( iuv.x < 1.0 || iuv.y < 1.0 )
    {
        col += 0.2;
    }

    return float4( col, 1.0 );
}
