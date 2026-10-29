using System.Diagnostics;

namespace StickLock.Services;

public class StarCitizenDetector : IStarCitizenDetector {
    public bool IsGameRunning() {
        try {
            var processes = Process.GetProcessesByName("StarCitizen");
            var running = processes.Length > 0;
            foreach (var proc in processes) {
                proc.Dispose();
            }
            return running;
        } catch {
            return false;
        }
    }
}

