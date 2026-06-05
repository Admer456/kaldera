
// Must match the struct in your C# code!
struct VertexInput
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
}

struct VertexOutput
{
    float4 Position : SV_Position;
    float3 Normal : PIXEL_INPUT;
}

struct CameraData
{
    float4x4 ViewProj;
}

ConstantBuffer<CameraData> Camera;
StructuredBuffer<float4x4> Instances;

[shader("vertex")]
VertexOutput VertexMain( VertexInput in, int instanceId : SV_InstanceID )
{
    float4x4 instanceMatrix = Instances[instanceId];
    float4 instancedPosition = mul( instanceMatrix, float4( in.Position, 1.0 ) );

    VertexOutput result;
    result.Position = mul( Camera.ViewProj, instancedPosition );
    // In case the instances are rotated, you'd have to rotate the normal too!
    result.Normal = mul( instanceMatrix, float4( in.Normal, 0.0 ) ).xyz;
    return result;
}

[shader("pixel")]
float4 PixelMain( float3 Normal : PIXEL_INPUT ) : SV_Target
{
    float3 colour = float3( 0.7 );
    float3 lightDirection = normalize( float3( 1.0, 1.0, -10.0 ) );
    float lambert = max( 0.0, dot( Normal, -lightDirection ) );

    return float4( colour * lambert, 1.0 );
}
