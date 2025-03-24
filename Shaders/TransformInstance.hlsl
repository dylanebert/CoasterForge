StructuredBuffer<float4x4> _Matrices;

void TransformInstance_float(float instanceID, float3 pos, out float3 Out) {
    uint id = (uint)instanceID;
    float4x4 mat = _Matrices[id];
    float4 worldPos = mul(mat, float4(pos, 1.0));
    float4 objectPos = mul(unity_WorldToObject, worldPos);
    Out = objectPos.xyz;
}
