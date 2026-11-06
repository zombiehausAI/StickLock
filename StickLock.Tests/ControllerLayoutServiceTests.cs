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

        // Verify Grip hotspots exist
        Assert.Contains(layout.Hotspots, h => h.InputCode == "button1");

        // Verify SEM submodule exists and has official VKB diagram button numbers
        var semModule = layout.SubModules.FirstOrDefault(m => m.Id == "vkb_gnx_sem");
        Assert.NotNull(semModule);
        Assert.Contains(semModule.Hotspots, h => h.InputCode == "button61"); // Flaps UP
        Assert.Contains(semModule.Hotspots, h => h.InputCode == "button64"); // Gear UP

        // Verify FSM-GA submodule exists and has official VKB diagram button numbers
        var fsmModule = layout.SubModules.FirstOrDefault(m => m.Id == "vkb_fsm_ga");
        Assert.NotNull(fsmModule);
        Assert.Contains(fsmModule.Hotspots, h => h.InputCode == "button9");  // AP
        Assert.Contains(fsmModule.Hotspots, h => h.InputCode == "button13"); // Avionics BUS 1
    }

    [Fact]
    public void GetLayoutForDevice_OmniThrottleLeft_ReturnsOmniLayout() {
        var service = new ControllerLayoutService();
        var layout = service.GetLayoutForDevice("VKBsim Gladiator EVO OT L");

        Assert.Equal("vkb_gladiator_evo_ot_l", layout.Id);
        Assert.Contains("Omni-Throttle", layout.AttachedModules);
        Assert.Contains(layout.Hotspots, h => h.InputCode == "button1");
        Assert.Equal("images/layouts/vkb_gladiator_omni_left.jpg", layout.ImageUrl);
    }
}

