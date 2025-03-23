using Unity.Mathematics;

namespace CoasterForge {
    [System.Serializable]
    public struct Node {
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

        public static Node Default => new() {
            Position = float3.zero,
            Direction = math.forward(),
            Lateral = math.left(),
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
        };
    }
}
