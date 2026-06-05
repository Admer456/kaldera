
// Must match the struct in your C# code!
struct VertexInput
{
    float3 Position;
    float4 Normal;
    float4 Tangent;
    float4 Colour;
    float2 Uv;
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

struct EntityData
{
    float4x4 World;
    float4 Params1;
    float4 Params2;
    float4 Params3;
    float4 Params4;
}

ConstantBuffer<CameraData> Camera;
SamplerState Sampler;
#ifdef INSTANCED
StructuredBuffer<EntityData> Entities;
#else
ConstantBuffer<EntityData> Entity;
#endif
Texture2D DiffuseTexture;

float4x4 GetEntityMatrix( int instanceId )
{
#ifdef INSTANCED
    return Entities[instanceId].World;
#else
    return Entity.World;
#endif
}

[shader("vertex")]
VertexOutput VertexMain( VertexInput in, int instanceId : SV_InstanceID )
{
    float4x4 entityMatrix = GetEntityMatrix( instanceId );
    float4 position = mul( entityMatrix, float4( in.Position, 1.0 ) );

    VertexOutput result;
    result.Position = mul( Camera.ViewProj, position );
    // In case the model is rotated, you have to rotate the normal too!
    result.Pixel.Normal = mul( entityMatrix, float4( in.Normal.xyz, 0.0 ) ).xyz;
    result.Pixel.Uv = in.Uv;
    return result;
}

// Adapted from:
// https://bgolus.medium.com/anti-aliased-alpha-test-the-esoteric-alpha-to-coverage-8b177335ae4f
float AlphaTest( float cutoff, float baseAlpha )
{
    // We're dealing with chonky pixels, so pre-filter them a bit
    baseAlpha = baseAlpha > cutoff ? 1.0 : 0.0;

    // If you're not using MSAA, you can just do this instead:
    // if ( baseAlpha < cutoff )
    //     discard;

    float changeInAlpha = fwidth( baseAlpha ) * 3.0;
    return (baseAlpha - (1.0 - cutoff)) / fmax( changeInAlpha, 0.0001 ) + cutoff;
}

[shader("pixel")]
float4 PixelMain( PixelInput in : PIXEL_INPUT ) : SV_Target
{
    float4 colour = DiffuseTexture.Sample( Sampler, in.Uv );

#ifdef ALPHA_TEST
    // Back in the OpenGL days, you'd do:
    // glEnable( GL_ALPHA_TEST );
    // glAlphaFunc( GL_LESS_THAN, 0.1 );
    // Nowadays, this is done in shaders... yeah
    // Homework: do this with specialisation constants instead of ifdefs!
    colour.a = AlphaTest( 0.5, colour.a );
#endif

    float3 lightDirection = normalize( float3( 1.0, 1.0, -10.0 ) );
    float halfLambert = dot( in.Normal, -lightDirection ) * 0.5 + 0.5;
    return float4( colour.rgb * halfLambert, colour.a );
}
