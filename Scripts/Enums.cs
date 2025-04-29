namespace CoasterForge {
    public enum DurationType {
        Time,
        Distance,
    }

    public enum SectionType {
        Geometric,
        Force,
    }

    public enum InterpolationType {
        Constant,
        Linear,
        Ease,
    }

    public enum NodeType {
        ForceSection,
        GeometricSection,
        Anchor,
    }

    public enum PortType {
        Anchor,
        Duration,
        Position,
        Roll,
        Pitch,
        Yaw,
        Velocity,
    }
}
