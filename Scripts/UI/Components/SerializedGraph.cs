using System;
using System.Collections.Generic;

namespace CoasterForge.UI {
    [Serializable]
    public class SerializedGraph {
        public List<SerializedNode> Nodes;
        public List<SerializedEdge> Edges;
    }
}
