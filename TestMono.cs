using Unity.Mathematics;

namespace CoasterForge {
    public class TestMono : UnityEngine.MonoBehaviour {
        public UnityEngine.Transform ControlPoint0;
        public UnityEngine.Transform ControlPoint1;
        public int NodeCount = 100;

        private Node[] _nodes;
        private Random _random;

        private void Start() {
            _random = new Random(1);
            _nodes = new Node[NodeCount];

            for (int i = 0; i < NodeCount; i++) {
                _nodes[i] = RandomNode();
            }

            var n_p0 = _nodes[0];
            var n_p1 = _nodes[1];
            n_p0.Position = ControlPoint0.position;
            n_p1.Position = ControlPoint1.position;
            _nodes[0] = n_p0;
            _nodes[1] = n_p1;
        }

        private Node RandomNode() {
            return new Node {
                Position = new float3(
                    _random.NextFloat(-100f, 100f),
                    _random.NextFloat(-100f, 100f),
                    _random.NextFloat(-100f, 100f)
                ),
                Direction = math.normalize(new float3(
                    _random.NextFloat(-1f, 1f),
                    _random.NextFloat(-1f, 1f),
                    _random.NextFloat(-1f, 1f)
                )),
                Energy = 0
            };
        }

        private void OrawGizmos() {
            if (_nodes == null) return;

            for (int i = 0; i < _nodes.Length; i++) {
                var node = _nodes[i];
                UnityEngine.Gizmos.color = UnityEngine.Color.Lerp(UnityEngine.Color.yellow, UnityEngine.Color.red, node.Energy);
                UnityEngine.Gizmos.DrawSphere(node.Position, 0.1f);
                UnityEngine.Gizmos.DrawLine(node.Position, node.Position + node.Direction);
            }
        }
    }
}
