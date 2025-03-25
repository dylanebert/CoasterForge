StructuredBuffer<uint> _DuplicationNodes;

void Discard_float(float instanceID, out float Out) {
    Out = _DuplicationNodes[instanceID];
}
