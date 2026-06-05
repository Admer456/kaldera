
// Must match the struct in your C# code!
struct TrianglePushConstant
{
    float4 A;
    float4 B;
    float4 C;
};

// SV_Position -> the float4 outputted by VertexMain will be fed to the rasteriser as a vertex position
// SV_VertexId -> the int parametre is the ID of this vertex
// uniform -> marks a push constant if used on a function param like this
[shader("vertex")]
float4 VertexMain( uniform TrianglePushConstant pushConstant, int vertexId : SV_VertexID ) : SV_Position
{
    if ( vertexId == 0 )
    {
        return pushConstant.A;
    }
    else if ( vertexId == 1 )
    {
        return pushConstant.B;
    }

    return pushConstant.C;
}

// SV_Target -> the float4 outputted by PixelMain will end up on the screen
[shader("pixel")]
float4 PixelMain() : SV_Target
{
    return float4( float3( 0.5 ), 1.0 );
}
