using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using static CoasterForge.Constants;

namespace CoasterForge {
    public class SectionAuthoring : MonoBehaviour {
        public List<Keyframe> RollSpeedKeyframes;
        public List<Keyframe> NormalForceKeyframes;
        public List<Keyframe> LateralForceKeyframes;
        public SectionType SectionType;
        public DurationType DurationType;
        public float Duration;
        public bool FixedVelocity;

#if UNITY_EDITOR
        public AnimationCurve RollSpeedCurveEditor = new();
        public AnimationCurve NormalForceCurveEditor = new();
        public AnimationCurve LateralForceCurveEditor = new();
#endif

        private class Baker : Baker<SectionAuthoring> {
            public override void Bake(SectionAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Section {
                    DurationType = authoring.DurationType,
                    Duration = authoring.Duration,
                    FixedVelocity = authoring.FixedVelocity,
                });
                AddBuffer<Node>(entity);
                AddBuffer<RollSpeedKeyframe>(entity);
                for (int i = 0; i < authoring.RollSpeedKeyframes.Count; i++) {
                    AppendToBuffer(entity, new RollSpeedKeyframe { Value = authoring.RollSpeedKeyframes[i] });
                }
                if (authoring.SectionType == SectionType.Force) {
                    AddBuffer<NormalForceKeyframe>(entity);
                    for (int i = 0; i < authoring.NormalForceKeyframes.Count; i++) {
                        AppendToBuffer(entity, new NormalForceKeyframe { Value = authoring.NormalForceKeyframes[i] });
                    }
                    AddBuffer<LateralForceKeyframe>(entity);
                    for (int i = 0; i < authoring.LateralForceKeyframes.Count; i++) {
                        AppendToBuffer(entity, new LateralForceKeyframe { Value = authoring.LateralForceKeyframes[i] });
                    }
                }
                else {
                    AddBuffer<PitchSpeedKeyframe>(entity);
                    for (int i = 0; i < authoring.NormalForceKeyframes.Count; i++) {
                        AppendToBuffer(entity, new PitchSpeedKeyframe { Value = authoring.NormalForceKeyframes[i] });
                    }
                    AddBuffer<YawSpeedKeyframe>(entity);
                    for (int i = 0; i < authoring.LateralForceKeyframes.Count; i++) {
                        AppendToBuffer(entity, new YawSpeedKeyframe { Value = authoring.LateralForceKeyframes[i] });
                    }
                }
            }
        }

#if UNITY_EDITOR
        public void UpdateEditorCurves() {
            RollSpeedCurveEditor.ClearKeys();
            NormalForceCurveEditor.ClearKeys();
            LateralForceCurveEditor.ClearKeys();

            int nodeCount = (int)(HZ * Duration);
            var nodeCountRef = new NativeReference<int>(Allocator.TempJob) { Value = nodeCount };

            var rollSpeedKeyframes = new NativeArray<Keyframe>(RollSpeedKeyframes.Count, Allocator.TempJob);
            var normalForceKeyframes = new NativeArray<Keyframe>(NormalForceKeyframes.Count, Allocator.TempJob);
            var lateralForceKeyframes = new NativeArray<Keyframe>(LateralForceKeyframes.Count, Allocator.TempJob);

            for (int i = 0; i < RollSpeedKeyframes.Count; i++) {
                rollSpeedKeyframes[i] = RollSpeedKeyframes[i];
            }
            for (int i = 0; i < NormalForceKeyframes.Count; i++) {
                normalForceKeyframes[i] = NormalForceKeyframes[i];
            }
            for (int i = 0; i < LateralForceKeyframes.Count; i++) {
                lateralForceKeyframes[i] = LateralForceKeyframes[i];
            }

            for (int i = 0; i < nodeCount; i++) {
                float t = i / HZ;
                float rollSpeed = rollSpeedKeyframes.Evaluate(t);
                float normalForce = normalForceKeyframes.Evaluate(t);
                float lateralForce = lateralForceKeyframes.Evaluate(t);

                var rollKey = new UnityEngine.Keyframe(t, rollSpeed) { weightedMode = WeightedMode.None };
                var normalKey = new UnityEngine.Keyframe(t, normalForce) { weightedMode = WeightedMode.None };
                var lateralKey = new UnityEngine.Keyframe(t, lateralForce) { weightedMode = WeightedMode.None };

                RollSpeedCurveEditor.AddKey(rollKey);
                NormalForceCurveEditor.AddKey(normalKey);
                LateralForceCurveEditor.AddKey(lateralKey);
            }

            rollSpeedKeyframes.Dispose();
            normalForceKeyframes.Dispose();
            lateralForceKeyframes.Dispose();
            nodeCountRef.Dispose();
        }
#endif
    }
}
