using System.Text;
using Unity.Mathematics;

namespace CoasterForge {
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

        public static PointData Default => new() {
            Position = float3.zero,
            Direction = math.back(),
            Lateral = math.right(),
            Normal = math.down(),
            Roll = 0f,
            Velocity = 0f,
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
