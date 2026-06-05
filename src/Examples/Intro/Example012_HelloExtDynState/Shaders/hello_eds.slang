
// Must match the struct in your C# code!
struct VertexInput
{
    float3 Position : POSITION;
}

[shader("vertex")]
float4 VertexMain( VertexInput in ) : SV_Position
{
    return float4( in.Position, 1.0 );
}

[shader("pixel")]
float4 PixelMain() : SV_Target
{
    // Orange this time :3
    return float4( 1.0, 0.75, 0.15, 1.0 );
}
