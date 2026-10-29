using System.Diagnostics;

namespace StickLock.Services;

public class BrowserService : IBrowserService {
    public void OpenUrl(string url) {
        try {
            Process.Start(new ProcessStartInfo(url) {
                UseShellExecute = true
            });
        } catch { }
    }
}
