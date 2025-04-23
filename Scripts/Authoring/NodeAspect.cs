using Unity.Entities;

namespace CoasterForge {
    public readonly partial struct NodeAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Node> NodeRO;

        private readonly RefRW<Dirty> DirtyRW;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public bool Dirty {
            get => DirtyRW.ValueRO.Value;
            set => DirtyRW.ValueRW.Value = value;
        }

        public Node Node => NodeRO.ValueRO;
    }
}
