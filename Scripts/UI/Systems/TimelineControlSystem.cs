using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class TimelineControlSystem : SystemBase {
        private NodeGraphView _view;
        private Timeline _timeline;

        protected override void OnStartRunning() {
            var root = UIService.Instance.UIDocument.rootVisualElement;
            _view = root.Q<NodeGraphView>();
            _timeline = root.Q<Timeline>();
        }

        protected override void OnUpdate() {
            bool hasSelectedSection = TryGetSelectedSection(out var data);

            if (hasSelectedSection) {
                _timeline.SetSelectedSection(data);
                if (Keyboard.current.fKey.wasPressedThisFrame) {
                    OrbitCameraController.Focus(data.Bounds);
                }
            }
            else {
                _timeline.ClearSelectedSection();
            }
        }

        private bool TryGetSelectedSection(out SectionData data) {
            data = default;

            var selectedNodes = _view.SelectedNodes;
            if (selectedNodes.Count != 1) return false;

            var node = selectedNodes[0];
            bool isSection = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection
                or NodeType.PathSection => true,
                _ => false
            };
            if (!isSection || !EntityManager.Exists(node.Entity)) return false;

            var pointBuffer = SystemAPI.GetBuffer<Point>(node.Entity);
            if (pointBuffer.Length < 2) return false;

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;

            var points = new NativeArray<PointData>(2, Allocator.Temp);
            points[0] = pointBuffer[0];
            points[1] = pointBuffer[^1];
            foreach (var point in points) {
                if (point.Position.x < minX) minX = point.Position.x;
                if (point.Position.x > maxX) maxX = point.Position.x;
                if (point.Position.y < minY) minY = point.Position.y;
                if (point.Position.y > maxY) maxY = point.Position.y;
                if (point.Position.z < minZ) minZ = point.Position.z;
                if (point.Position.z > maxZ) maxZ = point.Position.z;
            }
            points.Dispose();

            Vector3 min = new(minX, minY, minZ);
            Vector3 max = new(maxX, maxY, maxZ);
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;
            data.Bounds = new Bounds(center, size);

            return true;
        }
    }
}
