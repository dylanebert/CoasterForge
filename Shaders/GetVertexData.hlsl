StructuredBuffer<float3> _Vertices;
StructuredBuffer<float3> _Normals;
StructuredBuffer<uint> _Triangles;
StructuredBuffer<float2> _UVs;
uniform uint _UVCount;

void GetVertexData_float(float vertexID, out float3 Position, out float3 Normal, out float2 UV) {
    uint index = _Triangles[(uint)vertexID];
    uint uvIndex = index - (index / _UVCount) * _UVCount;
    Position = _Vertices[index];
    Normal = _Normals[index];
    UV = _UVs[uvIndex];
}
