
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

// This could've just been a float4x4: ConstantBuffer<float4x4>
// But, if you add more stuff here, it's convenient to have a struct early on :)
struct CameraData
{
    float4x4 ViewProj;
}

// In Slang, we use ConstantBuffer for uniform buffers like this
ConstantBuffer<CameraData> Camera; // Set 0, binding 0

[shader("vertex")]
VertexOutput VertexMain( VertexInput in )
{
    VertexOutput result;

    // Matrix multiplication!!!
    // float4(pos, 1.0) makes the matrix affect the position properly.
    // float4(pos, 0.0) would only make it rotate the vector around (0,0,0)
    result.Position = mul( Camera.ViewProj, float4( in.Position, 1.0 ) );

    // Just pass the normal directly to the pixel shader. Note that this normal is basically in world space
    result.Normal = in.Normal;
    return result;
}

[shader("pixel")]
float4 PixelMain( float3 Normal : PIXEL_INPUT ) : SV_Target
{
    // Here we'll do some hardcoded Lambertian shading for funsies
    float3 colour = float3( 0.7 );
    float3 lightDirection = normalize( float3( 1.0, 1.0, -10.0 ) );
    // The dot product can go negative, so... yeah, just clamping it here
    float lambert = max( 0.0, dot( Normal, -lightDirection ) );

    return float4( colour * lambert, 1.0 );
}
