namespace CoasterForge {
    [System.Serializable]
    public struct Keyframe {
        public float Time;
        public float Value;
        public InterpolationType InInterpolation;
        public InterpolationType OutInterpolation;

        public float InTangent;
        public float OutTangent;
        public float InWeight;
        public float OutWeight;

        public static Keyframe Default => new() {
            Time = 0f,
            Value = 0f,
            InInterpolation = InterpolationType.Ease,
            OutInterpolation = InterpolationType.Ease,
            InTangent = 0f,
            OutTangent = 0f,
            InWeight = 1 / 3f,
            OutWeight = 1 / 3f,
        };
    }
}
