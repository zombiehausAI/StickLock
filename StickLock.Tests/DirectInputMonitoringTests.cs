using StickLock.Services;
using Xunit;

namespace StickLock.Tests;

public class DirectInputMonitoringTests {
    [Fact]
    public void ButtonInputDetected_FiresWhenButtonTriggered() {
        var diService = new TestDirectInputService();
        int receivedSlot = 0;
        string receivedButton = string.Empty;

        diService.ButtonInputDetected += (slot, btn) => {
            receivedSlot = slot;
            receivedButton = btn;
        };

        diService.TriggerButtonInput(1, "button3");

        Assert.Equal(1, receivedSlot);
        Assert.Equal("button3", receivedButton);
    }
}

