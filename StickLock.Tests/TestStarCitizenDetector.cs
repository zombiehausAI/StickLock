using StickLock.Services;

namespace StickLock.Tests;

public class TestStarCitizenDetector : IStarCitizenDetector {
    public bool Running { get; set; } = false;

    public bool IsGameRunning() {
        return Running;
    }
}

