using Unity.Entities;

namespace CoasterForge {
    public readonly partial struct GeometricSectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRW<Section> SectionRW;

        public readonly DynamicBuffer<Node> Nodes;

        public readonly DynamicBuffer<RollSpeedKeyframe> RollSpeedKeyframes;
        public readonly DynamicBuffer<PitchSpeedKeyframe> PitchSpeedKeyframes;
        public readonly DynamicBuffer<YawSpeedKeyframe> YawSpeedKeyframes;

        public DurationType DurationType {
            get => SectionRW.ValueRO.DurationType;
            set => SectionRW.ValueRW.DurationType = value;
        }

        public float Duration {
            get => SectionRW.ValueRO.Duration;
            set => SectionRW.ValueRW.Duration = value;
        }

        public bool FixedVelocity {
            get => SectionRW.ValueRO.FixedVelocity;
            set => SectionRW.ValueRW.FixedVelocity = value;
        }
    }
}
