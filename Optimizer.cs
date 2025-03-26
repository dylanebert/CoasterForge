using Unity.Mathematics;
using UnityEngine;

namespace CoasterForge {
    public class Optimizer : MonoBehaviour {
        public Track Track;
        public Transform ControlPoint;
        public float LearningRate = 0.001f;

        private void Update() {
            if (Track.SolvedResolution <= 0) return;

            Track.MarkDirty();
            Track.Build();
            Track.Sync();
            var nodes = Track.Nodes;

            var keyframe = Track.NormalForceCurve[1];
            float N1 = keyframe.Value;

            float3 endPos = ControlPoint.position;
            float loss = math.lengthsq(nodes[nodes.Length / 2].Position - endPos);

            var keyframeN1 = keyframe;
            keyframeN1.Value += 0.1f;
            Track.NormalForceCurve[1] = keyframeN1;

            Track.MarkDirty();
            Track.Build();
            Track.Sync();
            nodes = Track.Nodes;

            float lossN1 = math.lengthsq(nodes[nodes.Length / 2].Position - endPos);
            float gradN1 = lossN1 - loss;

            N1 -= LearningRate * gradN1;
            keyframe.Value = N1;
            Track.NormalForceCurve[1] = keyframe;
        }
    }
}
