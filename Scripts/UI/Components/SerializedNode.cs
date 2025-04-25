using System;
using System.Collections.Generic;

namespace CoasterForge.UI {
    [Serializable]
    public class SerializedNode {
        public Section Section;
        public UIPosition Position;
        public List<Keyframe> RollSpeedKeyframes;
        public List<Keyframe> NormalForceKeyframes;
        public List<Keyframe> LateralForceKeyframes;
        public List<SerializedPort> InputPorts;
        public List<SerializedPort> OutputPorts;
    }
}
