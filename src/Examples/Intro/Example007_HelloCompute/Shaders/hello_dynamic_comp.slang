
// You can stuff plenty of information into a push constant. Vulkan 1.4
// guarantees 256 bytes. We're using 64 here, though this could've been a lot less
struct UserInputs
{
    float4 Data;
    int4 Data2;

    float2 GetMouse() { return Data.xy; }
    float GetDt() { return Data.z; }
    float GetTime() { return Data.w; }

    int GetWidth() { return Data2.x; }
    int GetHeight() { return Data2.y; }
    float GetHeld() { return Data2.z; }
}

StructuredBuffer<float4> Original;
StructuredBuffer<float4> Previous;
RWStructuredBuffer<float4> Modified;

float distanceSquared( float2 a, float2 b )
{
    float2 c = a - b;
    return (c.x*c.x) + (c.y*c.y);
}

void TransformGeometry( int vertexId, UserInputs i )
{
    float3 originalPos = Original[vertexId].xyz;
    float3 pos = Previous[vertexId].xyz;
    float time = i.GetTime();

    if ( i.GetHeld() > 0.0 )
    {
        float distance = max( 1.0, distanceSquared( pos.xy, i.GetMouse() ) );
        float falloff = 1.0 / distance;

        pos += float3(
            sin( (time * 3.0) + pos.y ) * 0.1 * falloff,
            cos( (time * 3.0) + pos.x ) * 0.1 * falloff,
            (sin( (time * 6.0) + pos.y ) + cos( (time * 6.0) + pos.x )) * 0.033 * falloff
        );
    }

    // Really stupid "bounce back" thing
    {
        float dist = distance( pos, originalPos );
        pos += (originalPos - pos) * i.GetDt() * dist;
    }
    Modified[vertexId].xyz = pos;
}

[shader("compute")]
[numthreads(16,1,1)]
void CompMain( uint3 id : SV_DispatchThreadID, uniform UserInputs inputs )
{
    TransformGeometry( id.x, inputs );
}
