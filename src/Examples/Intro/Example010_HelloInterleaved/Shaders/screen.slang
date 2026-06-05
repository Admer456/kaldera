
struct VertexOut
{
    float4 Position : SV_Position;
    float2 Uv : PIXEL_INPUT;
}

Texture2D Framebuffer;
SamplerState Sampler;

static float2 BuiltinCoordinates[] = {
    float2( 0.0, 3.0 ),
    float2( 3.0, -1.0 ),
    float2( -3.0, -1.0 )
};

[shader("vertex")]
VertexOut VertexMain( int vertexId : SV_VertexID )
{
    float2 pos = BuiltinCoordinates[vertexId];
    VertexOut result;
    result.Position = float4( pos, 0.0, 1.0 );
    result.Uv = pos;
    return result;
}

[shader("pixel")]
float4 PixelMain( float2 Uv : PIXEL_INPUT ) : SV_Target
{
    return Framebuffer.Sample( Sampler, (Uv + 1.0) * 0.5 );
}
