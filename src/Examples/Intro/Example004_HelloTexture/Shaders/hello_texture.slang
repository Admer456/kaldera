
// Must match the struct in your C# code!
struct VertexInput
{
    float3 Position : POSITION;
    float2 Uv : TEXTURE0;
}

// This time we use a struct as the vertex output
struct VertexOutput
{
    // Thanks to the SV_Position semantic, the GPU knows what to do with Position here
    float4 Position : SV_Position;
    // PIXEL_INPUT is not defined by HLSL or Slang, it is actually our own, custom
    // semantic, defined on-the-fly. This way, the pixel shader knows what to receive
    float2 Uv : PIXEL_INPUT;
}

// It also makes the vertex entry point a tad cleaner
[shader("vertex")]
VertexOutput VertexMain( VertexInput in )
{
    VertexOutput result;
    result.Position = float4( in.Position, 1.0 );

    // Pass the UV to the pixel shader
    result.Uv = in.Uv;
    // You may also apply additional scaling, rotation etc. via push constants or whatever,
    // and then you're one step closer to a functioning game engine :3
    return result;
}

Texture2D DiffuseMap; // Set 0, binding 0
SamplerState Sampler; // Set 0, binding 1

[shader("pixel")]
float4 PixelMain( float2 Uv : PIXEL_INPUT ) : SV_Target
{
    // Send it riiiiiight to the blender, no processing of any kind
    return float4( DiffuseMap.Sample( Sampler, Uv ).rgb + float3( Uv, 0.0 ), 1.0 );
}
