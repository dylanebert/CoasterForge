using System;
using System.Collections.Generic;

namespace CoasterForge.UI {
    [Serializable]
    public class SerializedNode {
        public Name Name;
        public NodeType Type;
        public UIPosition Position;
        public bool Render;
        public List<SerializedPort> InputPorts;
        public List<SerializedPort> OutputPorts;

        public PointData Anchor;

        public Duration Duration;
        public List<Keyframe> RollSpeedKeyframes;
        public List<Keyframe> NormalForceKeyframes;
        public List<Keyframe> LateralForceKeyframes;
        public List<Keyframe> PitchSpeedKeyframes;
        public List<Keyframe> YawSpeedKeyframes;
    }
}
