using System;
using UnityEngine;

namespace CoasterForge {
    public class ControlPoint : MonoBehaviour {
        public ConstraintMode Mode = ConstraintMode.Position;
        public float TargetDuration = 1f;

        [SerializeField] private ConstraintData _normalForce = new() { TargetValue = 1f };
        [SerializeField] private ConstraintData _roll = new();
        [SerializeField] private ConstraintData _pitch = new();
        [SerializeField] private ConstraintData _yaw = new();

        private ConstraintMode _previousMode;
        private Vector3 _previousPosition;
        private float _previousDuration;
        private ConstraintData _previousNormalForce;
        private ConstraintData _previousRoll;
        private ConstraintData _previousPitch;
        private ConstraintData _previousYaw;

        public bool ConstrainNormalForce => _normalForce.IsConstrained;
        public bool ConstrainRoll => _roll.IsConstrained;
        public bool ConstrainPitch => _pitch.IsConstrained;
        public bool ConstrainYaw => _yaw.IsConstrained;

        public float NormalForce => _normalForce.TargetValue;
        public float Roll => _roll.TargetValue;
        public float Pitch => _pitch.TargetValue;
        public float Yaw => _yaw.TargetValue;

        public bool Dirty { get; set; }

        private void Update() {
            CheckForChanges();
        }

        private void CheckForChanges() {
            if (_previousMode != Mode) {
                _previousMode = Mode;
                Dirty = true;
            }

            if (Mode == ConstraintMode.Position) {
                if (_previousPosition != transform.position) {
                    _previousPosition = transform.position;
                    Dirty = true;
                }
            }
            else if (Mode == ConstraintMode.Duration) {
                if (!Mathf.Approximately(_previousDuration, TargetDuration)) {
                    _previousDuration = TargetDuration;
                    Dirty = true;
                }
            }

            Dirty |= _normalForce.HasChanged(_previousNormalForce);
            Dirty |= _roll.HasChanged(_previousRoll);
            Dirty |= _pitch.HasChanged(_previousPitch);
            Dirty |= _yaw.HasChanged(_previousYaw);

            _previousNormalForce = _normalForce;
            _previousRoll = _roll;
            _previousPitch = _pitch;
            _previousYaw = _yaw;
        }

        public enum ConstraintMode {
            Position,
            Duration,
            Free
        }

        [Serializable]
        private struct ConstraintData {
            public bool IsConstrained;
            public float TargetValue;

            public bool HasChanged(ConstraintData other) {
                return IsConstrained != other.IsConstrained ||
                    (IsConstrained && !Mathf.Approximately(TargetValue, other.TargetValue));
            }
        }
    }
}
