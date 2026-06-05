
// Must match the struct in your C# code!
struct VertexInput0
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
}

struct VertexInput1
{
    float4 Colour : COLOR;
}

struct PixelInput
{
    float3 Normal;
    float4 Colour;
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

ConstantBuffer<CameraData> Camera;

[shader("vertex")]
VertexOutput VertexMain( VertexInput0 in0, VertexInput1 in1 )
{
    VertexOutput result;
    result.Position = mul( Camera.ViewProj, float4( in0.Position, 1.0 ) );
    result.Pixel.Normal = in0.Normal;
    result.Pixel.Colour = in1.Colour;
    return result;
}

[shader("pixel")]
float4 PixelMain( PixelInput in : PIXEL_INPUT ) : SV_Target
{
    float3 lightDirection = normalize( float3( 1.0, 1.0, -0.25 ) );
    float lambert = max( 0.0, dot( in.Normal, -lightDirection ) );

    return float4( in.Colour.rgb * lambert, in.Colour.a );
}
