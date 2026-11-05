using StickLock.Models;
using StickLock.Services;
using Xunit;

namespace StickLock.Tests;

public class ControllerLayoutServiceTests {
    [Fact]
    public void DetectModules_FindsSemAndFsmGaInVkbDeviceString() {
        var devName = "VKBsim Gladiator EVO R SEM FSM.GA";
        var modules = ControllerLayoutDefinition.DetectModulesFromProductName(devName);

        Assert.Contains("GNX-SEM", modules);
        Assert.Contains("GNX-FSM-GA", modules);
    }

    [Fact]
    public void GetLayoutForDevice_ReturnsCompositeLayoutWithHotspotsForSemAndFsm() {
        var service = new ControllerLayoutService();
        var layout = service.GetLayoutForDevice("VKBsim Gladiator EVO R SEM FSM.GA");

        Assert.Equal("vkb_gladiator_evo_r", layout.Id);
        Assert.Contains("GNX-SEM", layout.AttachedModules);
        Assert.Contains("GNX-FSM-GA", layout.AttachedModules);

        // Verify SEM hotspots exist
        Assert.Contains(layout.Hotspots, h => h.ModuleName == "GNX-SEM" && h.InputCode == "button33"); // Gear UP
        Assert.Contains(layout.Hotspots, h => h.ModuleName == "GNX-SEM" && h.InputCode == "button35"); // Flaps UP

        // Verify FSM-GA hotspots exist
        Assert.Contains(layout.Hotspots, h => h.ModuleName == "GNX-FSM-GA" && h.InputCode == "button51"); // AP
        Assert.Contains(layout.Hotspots, h => h.ModuleName == "GNX-FSM-GA" && h.InputCode == "button55"); // Strobe
    }

    [Fact]
    public void GetLayoutForDevice_OmniThrottleLeft_ReturnsOmniLayout() {
        var service = new ControllerLayoutService();
        var layout = service.GetLayoutForDevice("VKBsim Gladiator EVO OT L");

        Assert.Equal("vkb_gladiator_evo_ot_l", layout.Id);
        Assert.Contains("Omni-Throttle", layout.AttachedModules);
        Assert.Contains(layout.Hotspots, h => h.Label.Contains("Strafe Forward"));
    }
}
