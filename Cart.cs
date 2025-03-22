using Unity.Mathematics;
using static CoasterForge.Constants;

namespace CoasterForge {
    public class Cart : UnityEngine.MonoBehaviour {
        private const float FRONT_WHEEL_OFFSET = 0.75f;
        private const float REAR_WHEEL_OFFSET = -0.75f;

        public Track Track;

        private float _indexFloat = 1f;

        private void Update() {
            ApplyPosition();
        }

        private void FixedUpdate() {
            _indexFloat += UnityEngine.Time.fixedDeltaTime * HZ;
            if (_indexFloat >= Track.Nodes.Length - 2) {
                _indexFloat = 1f;
            }
        }

        private void ApplyPosition() {
            var nodes = Track.Nodes;
            if (nodes.Length < 3) return;

            _indexFloat = math.clamp(_indexFloat, 1, nodes.Length - 2);
            int index = (int)math.floor(_indexFloat);
            float t = _indexFloat - index;

            int GetIndex(float offset) {
                float start = nodes[index].TotalLength;
                int increment = offset > 0f ? 1 : -1;
                while (math.abs(nodes[index].TotalLength - start) < math.abs(offset) && index > 0 && index < nodes.Length - 2) {
                    index += increment;
                }
                return index;
            }

            float3 GetSmoothPosition(int index) {
                return math.lerp(
                    nodes[index].GetHeartPosition(HEART),
                    nodes[index + 1].GetHeartPosition(HEART),
                    t
                );
            }

            int frontWheelIndex = GetIndex(FRONT_WHEEL_OFFSET);
            int rearWheelIndex = GetIndex(REAR_WHEEL_OFFSET);
            float3 frontWheelPosition = GetSmoothPosition(frontWheelIndex);
            float3 rearWheelPosition = GetSmoothPosition(rearWheelIndex);

            float3 normal = math.lerp(nodes[index].Normal, nodes[index + 1].Normal, t);
            float3 direction = math.normalize(frontWheelPosition - rearWheelPosition);

            float3 position = math.lerp(frontWheelPosition, rearWheelPosition, 0.5f);
            quaternion rotation = quaternion.LookRotation(direction, -normal);

            transform.position = position;
            transform.rotation = rotation;
        }
    }
}
