
// Must match the struct in your C# code!
struct VertexInput
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float2 Uv : TEXTURE0;
}

struct PixelInput
{
    float3 Normal;
    float2 Uv;
}

struct VertexOutput
{
    float4 Position : SV_Position;
    PixelInput Pixel : PIXEL_INPUT;
}

struct CameraData
{
    float4x4 ViewProj;
}

// My Slang compiler copy currently suffers from this:
// https://github.com/shader-slang/slang/issues/10520
// So, we're gonna work around it...

// Slang lets us use "parameter blocks" to delimit sets
// ParameterBlock<ConstantBuffer<CameraData>> Camera; // Set 0
// ParameterBlock<ConstantBuffer<float4x4>> EntityMatrix; // Set 1
// ParameterBlock<SurfaceData> Surface; // Set 2

ConstantBuffer<CameraData> Camera;
SamplerState Sampler;
ConstantBuffer<float4x4> EntityMatrix;
Texture2D DiffuseTexture;

[shader("vertex")]
VertexOutput VertexMain( VertexInput in, int instanceId : SV_InstanceID )
{
    float4 position = mul( EntityMatrix, float4( in.Position, 1.0 ) );

    VertexOutput result;
    result.Position = mul( Camera.ViewProj, position );
    // In case the model is rotated, you have to rotate the normal too!
    result.Pixel.Normal = mul( EntityMatrix, float4( in.Normal, 0.0 ) ).xyz;
    result.Pixel.Uv = in.Uv;
    return result;
}

[shader("pixel")]
float4 PixelMain( PixelInput in : PIXEL_INPUT ) : SV_Target
{
    float3 lightDirection = normalize( float3( 1.0, 1.0, -10.0 ) );
    float halfLambert = dot( in.Normal, -lightDirection ) * 0.5 + 0.5;

    float3 colour = DiffuseTexture.Sample( Sampler, in.Uv ).rgb;
    return float4( colour * halfLambert, 1.0 );
}
