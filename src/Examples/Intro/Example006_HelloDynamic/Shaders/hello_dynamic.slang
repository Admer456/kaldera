
// Must match the struct in your C# code!
struct VertexInput
{
    float3 Position : POSITION;
}

struct VertexOutput
{
    float4 Position : SV_Position;
}

struct CameraData
{
    float4x4 ViewProj;
}

ConstantBuffer<CameraData> Camera; // Set 0, binding 0

[shader("vertex")]
VertexOutput VertexMain( VertexInput in )
{
    VertexOutput result;
    result.Position = mul( Camera.ViewProj, float4( in.Position, 1.0 ) );
    return result;
}

[shader("pixel")]
float4 PixelMain() : SV_Target
{
    return float4( float3( 0.75 ), 1.0 );
}
