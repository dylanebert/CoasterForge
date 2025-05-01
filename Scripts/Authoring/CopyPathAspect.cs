using Unity.Entities;

namespace CoasterForge {
    public readonly partial struct CopyPathAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Anchor> AnchorRO;
        private readonly RefRO<CopyPathTag> CopyPathTagRO;

        private readonly RefRW<Dirty> DirtyRW;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public PointData Anchor => AnchorRO.ValueRO;

        public bool Dirty {
            get => DirtyRW.ValueRO.Value;
            set => DirtyRW.ValueRW.Value = value;
        }

        public CopyPathTag CopyPathTag => CopyPathTagRO.ValueRO;
    }
}
