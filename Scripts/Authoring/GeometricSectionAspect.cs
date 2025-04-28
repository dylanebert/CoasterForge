using Unity.Entities;

namespace CoasterForge {
    public readonly partial struct GeometricSectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Anchor> AnchorRO;

        private readonly RefRW<Duration> DurationRW;
        private readonly RefRW<FixedVelocity> FixedVelocityRW;
        private readonly RefRW<Dirty> DirtyRW;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public readonly DynamicBuffer<RollSpeedKeyframe> RollSpeedKeyframes;
        public readonly DynamicBuffer<PitchSpeedKeyframe> PitchSpeedKeyframes;
        public readonly DynamicBuffer<YawSpeedKeyframe> YawSpeedKeyframes;

        public PointData Anchor => AnchorRO.ValueRO;

        public DurationType DurationType {
            get => DurationRW.ValueRO.Type;
            set => DurationRW.ValueRW.Type = value;
        }

        public float Duration {
            get => DurationRW.ValueRO.Value;
            set => DurationRW.ValueRW.Value = value;
        }

        public bool FixedVelocity {
            get => FixedVelocityRW.ValueRO.Value;
            set => FixedVelocityRW.ValueRW.Value = value;
        }

        public bool Dirty {
            get => DirtyRW.ValueRO.Value;
            set => DirtyRW.ValueRW.Value = value;
        }
    }
}
