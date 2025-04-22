namespace CoasterForge {
    public static class Constants {
        public const float M = 1f; // Mass in KG
        public const float G = 9.80665f; // Gravity in m/s^2
        public const float HZ = 100f; // Simulation rate in Hz
        public const float EPSILON = 1.192092896e-07f; // Epsilon for floating point comparisons
        public const float HEART = 1.1f; // Distance from track to rider heart in meters
        public const float CENTER = HEART * 0.9f; // Distance from track to rider center of mass in meters
        public const float FRICTION = 0.021f; // Friction coefficient
        public const float RESISTANCE = 2e-5f; // Air resistance coefficient
        public const float TIE_SPACING = 0.8f; // Distance between track ties in meters
        public const float FRONT_WHEEL_OFFSET = 0.75f;
        public const float REAR_WHEEL_OFFSET = -0.75f;
    }
}
