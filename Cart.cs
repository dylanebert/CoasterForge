using Unity.Mathematics;
using static CoasterForge.Constants;

namespace CoasterForge {
    public class Cart : UnityEngine.MonoBehaviour {
        private const float FRONT_WHEEL_OFFSET = 0.75f;
        private const float REAR_WHEEL_OFFSET = -0.75f;

        public Section Track;

        private float _indexFloat = 1f;

        private void Update() {
            ApplyPosition();
        }

        private void FixedUpdate() {
            _indexFloat += UnityEngine.Time.fixedDeltaTime * HZ;
            if (_indexFloat >= Track.NodeCount - 2) {
                _indexFloat = 1f;
            }
        }

        private void ApplyPosition() {
            int nodeCount = Track.NodeCount;
            var nodes = Track.Nodes;
            if (nodeCount < 3) return;

            _indexFloat = math.clamp(_indexFloat, 1, nodeCount - 2);
            int index = (int)math.floor(_indexFloat);
            float t = _indexFloat - index;

            int GetIndex(float offset) {
                int currentIndex = index;
                float startLength = nodes[index].TotalLength;
                float targetLength = startLength + offset;
                int direction = offset > 0f ? 1 : -1;
                while (currentIndex > 0 && currentIndex < nodeCount - 2) {
                    if ((direction > 0 && nodes[currentIndex + 1].TotalLength >= targetLength) ||
                        (direction < 0 && nodes[currentIndex - 1].TotalLength <= targetLength)) {
                        break;
                    }
                    currentIndex += direction;
                }
                return currentIndex;
            }

            float3 GetSmoothPosition(int index) {
                return math.lerp(
                    nodes[index].GetHeartPosition(HEART),
                    nodes[index + 1].GetHeartPosition(HEART),
                    t
                );
            }

            float3 GetSmoothHeartDirection(int index) {
                return math.normalize(math.lerp(
                    nodes[index].GetHeartDirection(HEART),
                    nodes[index + 1].GetHeartDirection(HEART),
                    t
                ));
            }

            float3 GetSmoothHeartLateral(int index) {
                return math.normalize(math.lerp(
                    nodes[index].GetHeartLateral(HEART),
                    nodes[index + 1].GetHeartLateral(HEART),
                    t
                ));
            }

            int frontWheelIndex = GetIndex(FRONT_WHEEL_OFFSET);
            int rearWheelIndex = GetIndex(REAR_WHEEL_OFFSET);
            float3 frontWheelPosition = GetSmoothPosition(frontWheelIndex);
            float3 rearWheelPosition = GetSmoothPosition(rearWheelIndex);
            float3 position = math.lerp(frontWheelPosition, rearWheelPosition, 0.5f);

            if (math.any(math.isnan(position))) {
                UnityEngine.Debug.Log($"{index}: {nodes[frontWheelIndex]}, {nodes[rearWheelIndex]}");
            }

            float3 direction = GetSmoothHeartDirection(index);
            float3 lateral = GetSmoothHeartLateral(index);
            float3 normal = math.normalize(math.cross(direction, lateral));
            quaternion rotation = quaternion.LookRotation(direction, -normal);

            transform.position = position;
            transform.rotation = rotation;
        }
    }
}
