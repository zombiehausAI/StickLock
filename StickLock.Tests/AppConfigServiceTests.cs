using StickLock.Models;
using StickLock.Services;

namespace StickLock.Tests;

public class AppConfigServiceTests {
    [Fact]
    public void ResolveActionMapsPath_SwitchesLiveToPtuAndBack() {
        var service = new AppConfigService();
        var livePath = @"C:\Program Files\Roberts Space Industries\StarCitizen\LIVE\USER\Client\0\Profiles\default\actionmaps.xml";

        var ptuPath = service.ResolveActionMapsPath(livePath, ScEnvironment.PTU);
        Assert.Contains(@"\PTU\", ptuPath);
        Assert.DoesNotContain(@"\LIVE\", ptuPath);

        var eptuPath = service.ResolveActionMapsPath(ptuPath, ScEnvironment.EPTU);
        Assert.Contains(@"\EPTU\", eptuPath);
        Assert.DoesNotContain(@"\PTU\", eptuPath);

        var backToLive = service.ResolveActionMapsPath(eptuPath, ScEnvironment.LIVE);
        Assert.Contains(@"\LIVE\", backToLive);
    }

    [Fact]
    public async Task CanonicalStick_AssignmentAndRemoval_WorksCorrectly() {
        var service = new AppConfigService();
        var stickGuid = Guid.NewGuid();

        await service.SetCanonicalStickAsync(1, new DirectInputDevice {
            SlotIndex = 1,
            InstanceGuid = stickGuid,
            ProductGuid = Guid.NewGuid(),
            ProductName = "Test Gladiator"
        }, "Right Stick");

        Assert.Single(service.Config.CanonicalSticks);
        Assert.Equal(1, service.Config.CanonicalSticks[0].RoleSlot);
        Assert.Equal("Right Stick", service.Config.CanonicalSticks[0].FriendlyRoleName);
        Assert.Equal(stickGuid, service.Config.CanonicalSticks[0].InstanceGuid);

        await service.RemoveCanonicalStickAsync(1);
        Assert.Empty(service.Config.CanonicalSticks);
    }
}
