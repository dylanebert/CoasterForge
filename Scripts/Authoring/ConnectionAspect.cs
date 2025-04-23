using Unity.Entities;

namespace CoasterForge {
    public readonly partial struct ConnectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Connection> ConnectionRO;
        private readonly RefRW<Dirty> DirtyRW;

        public bool Dirty {
            get => DirtyRW.ValueRO.Value;
            set => DirtyRW.ValueRW.Value = value;
        }

        public Entity SourcePort => ConnectionRO.ValueRO.SourcePort;
        public Entity TargetPort => ConnectionRO.ValueRO.TargetPort;
    }
}
