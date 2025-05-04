using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class OrbitCameraController : MonoBehaviour {
        public CinemachineCamera CinemachineCamera;
        public UIDocument UIDocument;
        public float OrbitSpeed = 100f;
        public float ZoomSpeed = 1f;
        public float MinDistance = 1f;
        public float MaxDistance = 100f;

        private VisualElement _gameView;
        private Camera _camera;
        private Transform _target;
        private float _distance;
        private float _pitch;
        private float _yaw;
        private bool _isOverGameView;

        public static OrbitCameraController Instance { get; private set; }

        private void Awake() {
            Instance = this;
        }

        private void Start() {
            var root = UIDocument.rootVisualElement;
            _gameView = root.Q<VisualElement>("GameView");

            _gameView.RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            _gameView.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);

            _camera = Camera.main;
            _target = new GameObject("Dummy").transform;

            _distance = CinemachineCamera.transform.position.magnitude;
            _pitch = CinemachineCamera.transform.rotation.eulerAngles.x;
            _yaw = CinemachineCamera.transform.rotation.eulerAngles.y;

            UpdateCamera();
        }

        private void Update() {
            HandleInput();
            UpdateCamera();
        }

        private void OnMouseEnter(MouseEnterEvent e) {
            _isOverGameView = true;
        }

        private void OnMouseLeave(MouseLeaveEvent e) {
            _isOverGameView = false;
        }

        private void HandleInput() {
            if (!_isOverGameView) return;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            Vector2 mousePos = mouse.position.ReadValue();
            Vector2 mouseDelta = mouse.delta.ReadValue();
            Vector2 screenSize = new(Screen.width, Screen.height);
            float screenScale = screenSize.magnitude;

            if (mouse.rightButton.isPressed && !keyboard.leftAltKey.isPressed) {
                _yaw += mouseDelta.x / screenScale * OrbitSpeed;
                _pitch -= mouseDelta.y / screenScale * OrbitSpeed;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            }

            if (mouse.middleButton.isPressed || (mouse.rightButton.isPressed && keyboard.leftAltKey.isPressed)) {
                Plane plane = new(CinemachineCamera.transform.forward, _target.position);
                Vector2 prevPos = mousePos - mouseDelta;
                Ray prevRay = _camera.ScreenPointToRay(prevPos);
                Ray ray = _camera.ScreenPointToRay(mousePos);
                if (plane.Raycast(prevRay, out float prevDist) && plane.Raycast(ray, out float dist)) {
                    Vector3 prevWorld = prevRay.GetPoint(prevDist);
                    Vector3 world = ray.GetPoint(dist);
                    Vector3 delta = world - prevWorld;
                    _target.position -= delta;
                }
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) {
                float zoomAmount = scroll * ZoomSpeed * _distance;
                _distance -= zoomAmount;
                _distance = Mathf.Clamp(_distance, MinDistance, MaxDistance);
            }
        }

        private void UpdateCamera() {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 dir = rotation * Vector3.back;
            Vector3 pos = _target.position + dir * _distance;

            CinemachineCamera.transform.SetPositionAndRotation(pos, rotation);
        }

        private void FocusInternal(Bounds bounds) {
            _target.position = bounds.center;

            float fov = _camera.fieldOfView;
            float aspect = _camera.aspect;

            const float padding = 5f;
            Vector3 extents = bounds.extents + new Vector3(padding, padding, padding);
            float radius = extents.magnitude;

            float verticalFovRad = Mathf.Deg2Rad * fov * 0.5f;
            float horizontalFovRad = Mathf.Atan(Mathf.Tan(verticalFovRad) * aspect);

            float distanceV = radius / Mathf.Sin(verticalFovRad);
            float distanceH = radius / Mathf.Sin(horizontalFovRad);

            _distance = Mathf.Max(distanceV, distanceH) - radius;
            _distance = Mathf.Clamp(_distance, MinDistance, MaxDistance);

            UpdateCamera();
        }

        public static void Focus(Bounds bounds) {
            Instance.FocusInternal(bounds);
        }
    }
}
