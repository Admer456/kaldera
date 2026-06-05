
// Must match the struct in your C# code!
struct VertexInput
{
    float3 Position : POSITION;
}

// "VertexInput in" is implied to be vertex buffer 0
[shader("vertex")]
float4 VertexMain( VertexInput in ) : SV_Position
{
    return float4( in.Position, 1.0 );
}

[shader("pixel")]
float4 PixelMain() : SV_Target
{
    return float4( float3( 0.5 ), 1.0 );
}
