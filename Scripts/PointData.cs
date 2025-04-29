using System.Text;
using Unity.Mathematics;
using static CoasterForge.Constants;

namespace CoasterForge {
    [System.Serializable]
    public struct PointData {
        public float3 Position;
        public float3 Direction;
        public float3 Lateral;
        public float3 Normal;
        public float Roll;
        public float Velocity;
        public float Energy;
        public float NormalForce;
        public float LateralForce;
        public float DistanceFromLast;
        public float HeartDistanceFromLast;
        public float AngleFromLast;
        public float PitchFromLast;
        public float YawFromLast;
        public float RollSpeed;
        public float TotalLength;
        public float TotalHeartLength;
        public float TieDistance;

        public static PointData Create(float velocity = 10f) {
            return Create(float3.zero, velocity);
        }

        public static PointData Create(float3 position, float velocity = 10f) {
            PointData point = new() {
                Position = position,
                Direction = math.back(),
                Lateral = math.right(),
                Normal = math.down(),
                Roll = 0f,
                Velocity = velocity,
                Energy = 0f,
                NormalForce = 1f,
                LateralForce = 0f,
                DistanceFromLast = 0f,
                HeartDistanceFromLast = 0f,
                AngleFromLast = 0f,
                PitchFromLast = 0f,
                YawFromLast = 0f,
                RollSpeed = 0f,
                TotalLength = 0f,
                TotalHeartLength = 0f,
                TieDistance = 0f,
            };
            point.Energy = point.ComputeEnergy();
            return point;
        }

        public void SetPosition(float3 position) {
            Position = position;
            Energy = this.ComputeEnergy();
        }

        public void SetRoll(float degrees) {
            Roll = degrees;
            float currentPitch = this.GetPitch();
            float currentYaw = this.GetYaw();

            Direction = math.normalize(math.mul(
                quaternion.Euler(math.radians(currentPitch), math.radians(currentYaw), 0f),
                math.back()
            ));
            Lateral = math.mul(quaternion.Euler(0f, math.radians(currentYaw), 0f), math.right());

            quaternion rollQuat = quaternion.AxisAngle(Direction, math.radians(-degrees));
            Lateral = math.normalize(math.mul(rollQuat, Lateral));
            Normal = math.normalize(math.cross(Direction, Lateral));

            Energy = this.ComputeEnergy();
        }

        public void SetPitch(float degrees) {
            float currentYaw = this.GetYaw();

            Direction = math.normalize(math.mul(
                quaternion.Euler(math.radians(degrees), math.radians(currentYaw), 0f),
                math.back()
            ));

            Lateral = math.mul(quaternion.Euler(0f, math.radians(currentYaw), 0f), math.right());
            Normal = math.normalize(math.cross(Direction, Lateral));

            SetRoll(Roll);
        }

        public void SetYaw(float degrees) {
            float currentPitch = this.GetPitch();

            Direction = math.normalize(math.mul(
                quaternion.Euler(math.radians(currentPitch), math.radians(degrees), 0f),
                math.back()
            ));

            Lateral = math.mul(quaternion.Euler(0f, math.radians(degrees), 0f), math.right());
            Normal = math.normalize(math.cross(Direction, Lateral));

            SetRoll(Roll);
        }

        public void SetVelocity(float velocity) {
            if (velocity < EPSILON) {
                velocity = EPSILON;
            }
            Velocity = velocity;
            Energy = this.ComputeEnergy();
        }

        public override string ToString() {
            StringBuilder sb = new();
            sb.AppendLine($"Position: {Position}");
            sb.AppendLine($"Direction: {Direction}");
            sb.AppendLine($"Lateral: {Lateral}");
            sb.AppendLine($"Normal: {Normal}");
            sb.AppendLine($"Roll: {Roll}");
            sb.AppendLine($"Velocity: {Velocity}");
            sb.AppendLine($"Energy: {Energy}");
            sb.AppendLine($"NormalForce: {NormalForce}");
            sb.AppendLine($"LateralForce: {LateralForce}");
            sb.AppendLine($"DistanceFromLast: {DistanceFromLast}");
            sb.AppendLine($"HeartDistanceFromLast: {HeartDistanceFromLast}");
            sb.AppendLine($"AngleFromLast: {AngleFromLast}");
            sb.AppendLine($"PitchFromLast: {PitchFromLast}");
            sb.AppendLine($"YawFromLast: {YawFromLast}");
            sb.AppendLine($"RollSpeed: {RollSpeed}");
            sb.AppendLine($"TotalLength: {TotalLength}");
            sb.AppendLine($"TotalHeartLength: {TotalHeartLength}");
            sb.AppendLine($"TieDistance: {TieDistance}");
            return sb.ToString();
        }
    }
}
