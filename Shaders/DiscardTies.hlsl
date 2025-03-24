StructuredBuffer<uint> _Ties;

void DiscardTies_float(float instanceID, float2 uv, out float Out) {
    float isTieNode = step((float)_Ties[instanceID], 0.5);
    float isTieUV = step(uv.y, 0.5);
    Out = max(isTieNode, (1.0 - isTieUV));
}
