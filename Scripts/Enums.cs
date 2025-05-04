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
        PathSection,
        Anchor,
        Reverse,
        ReversePath,
    }

    public enum PortType {
        Anchor,
        Path,
        Duration,
        Position,
        Roll,
        Pitch,
        Yaw,
        Velocity,
    }
}
