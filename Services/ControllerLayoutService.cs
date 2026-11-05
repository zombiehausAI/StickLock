using StickLock.Models;

namespace StickLock.Services;

public class ControllerLayoutService : IControllerLayoutService {
    public ControllerLayoutDefinition GetLayoutForDevice(string productName) {
        var clean = productName ?? string.Empty;
        var upper = clean.ToUpperInvariant();

        var isVkb = upper.Contains("VKB") || upper.Contains("GLADIATOR");
        var isLeft = upper.Contains(" L ") || upper.EndsWith(" L") || upper.Contains("LEFT") || upper.Contains("OT L");
        var isOmni = upper.Contains("OT") || upper.Contains("OMNI");

        if (isVkb) {
            if (isLeft || isOmni) {
                return CreateVkbGladiatorOmniLeftLayout(clean);
            }
            return CreateVkbGladiatorRightCompositeLayout(clean);
        }

        // Generic fallback flight stick
        return CreateGenericFlightStickLayout(clean);
    }

    public IReadOnlyList<ControllerLayoutDefinition> GetAllLayouts() {
        return new List<ControllerLayoutDefinition> {
            CreateVkbGladiatorRightCompositeLayout("VKBsim Gladiator EVO R SEM FSM.GA"),
            CreateVkbGladiatorOmniLeftLayout("VKBsim Gladiator EVO OT L"),
            CreateGenericFlightStickLayout("Generic Joystick")
        };
    }

    private static ControllerLayoutDefinition CreateVkbGladiatorRightCompositeLayout(string fullProductName) {
        var detectedModules = ControllerLayoutDefinition.DetectModulesFromProductName(fullProductName);

        var layout = new ControllerLayoutDefinition {
            Id = "vkb_gladiator_evo_r",
            Name = "VKB Gladiator EVO Right",
            Category = "Flight Stick",
            Width = 850,
            Height = 650,
            AttachedModules = detectedModules
        };

        // 1. Grip Controls (Space Combat Grip)
        layout.Hotspots.AddRange(new[] {
            new ControllerHotspot { Id = "trigger_primary_1", Label = "Primary Trigger (Stage 1)", InputCode = "button1", X = 48, Y = 32, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "trigger_primary_2", Label = "Primary Trigger (Stage 2)", InputCode = "button2", X = 48, Y = 36, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "trigger_rapid_pull", Label = "Rapid Fire Trigger (Pull)", InputCode = "button3", X = 43, Y = 30, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "trigger_rapid_push", Label = "Rapid Fire Trigger (Push)", InputCode = "button4", X = 43, Y = 26, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "red_weapon_button", Label = "Red Weapon Release Button", InputCode = "button5", X = 54, Y = 20, Type = "button", ModuleName = "Grip" },

            // Center Hat A1
            new ControllerHotspot { Id = "hat_a1_click", Label = "A1 Center Hat (Click)", InputCode = "button10", X = 49, Y = 24, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a1_up", Label = "A1 Center Hat (Up)", InputCode = "hat1_up", X = 49, Y = 21, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a1_down", Label = "A1 Center Hat (Down)", InputCode = "hat1_down", X = 49, Y = 27, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a1_left", Label = "A1 Center Hat (Left)", InputCode = "hat1_left", X = 46, Y = 24, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a1_right", Label = "A1 Center Hat (Right)", InputCode = "hat1_right", X = 52, Y = 24, Type = "hat", ModuleName = "Grip" },

            // Top-Left Hat A4 (POV/Mini-stick)
            new ControllerHotspot { Id = "hat_a4_click", Label = "A4 Hat / Mini-Stick (Click)", InputCode = "button14", X = 39, Y = 22, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a4_up", Label = "A4 Hat (Up)", InputCode = "hat2_up", X = 39, Y = 19, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a4_down", Label = "A4 Hat (Down)", InputCode = "hat2_down", X = 39, Y = 25, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a4_left", Label = "A4 Hat (Left)", InputCode = "hat2_left", X = 36, Y = 22, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a4_right", Label = "A4 Hat (Right)", InputCode = "hat2_right", X = 42, Y = 22, Type = "hat", ModuleName = "Grip" },

            // Thumb Hat A2
            new ControllerHotspot { Id = "hat_a2_click", Label = "A2 Thumb Hat (Click)", InputCode = "button18", X = 60, Y = 32, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "pinky_button", Label = "Pinky Button", InputCode = "button22", X = 36, Y = 48, Type = "button", ModuleName = "Grip" },

            // Primary Axes
            new ControllerHotspot { Id = "axis_pitch", Label = "Y Axis (Pitch)", InputCode = "y", X = 48, Y = 56, Type = "axis", ModuleName = "Grip" },
            new ControllerHotspot { Id = "axis_roll", Label = "X Axis (Roll / Yaw)", InputCode = "x", X = 55, Y = 56, Type = "axis", ModuleName = "Grip" },
            new ControllerHotspot { Id = "axis_twist", Label = "Z Twist Axis (Yaw / Roll)", InputCode = "rotz", X = 48, Y = 50, Type = "axis", ModuleName = "Grip" }
        });

        // 2. Base Controls
        layout.Hotspots.AddRange(new[] {
            new ControllerHotspot { Id = "base_b1", Label = "Base Button B1", InputCode = "button25", X = 40, Y = 78, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_b2", Label = "Base Button B2", InputCode = "button26", X = 48, Y = 78, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_b3", Label = "Base Button B3", InputCode = "button27", X = 56, Y = 78, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_enc1_cw", Label = "Base Encoder 1 (CW)", InputCode = "button28", X = 36, Y = 72, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_enc1_ccw", Label = "Base Encoder 1 (CCW)", InputCode = "button29", X = 36, Y = 75, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_enc2_cw", Label = "Base Encoder 2 (CW)", InputCode = "button30", X = 60, Y = 72, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_enc2_ccw", Label = "Base Encoder 2 (CCW)", InputCode = "button31", X = 60, Y = 75, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_throttle", Label = "Base Throttle Lever", InputCode = "slider1", X = 48, Y = 86, Type = "axis", ModuleName = "Base" }
        });

        // 3. Side Extension Module (GNX-SEM) if present
        if (detectedModules.Contains("GNX-SEM") || true) {
            layout.Hotspots.AddRange(new[] {
                new ControllerHotspot { Id = "sem_gear_up", Label = "Landing Gear Switch (UP)", InputCode = "button33", X = 12, Y = 28, Type = "switch", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_gear_down", Label = "Landing Gear Switch (DOWN)", InputCode = "button34", X = 12, Y = 40, Type = "switch", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_flaps_up", Label = "Flaps Lever (UP)", InputCode = "button35", X = 24, Y = 28, Type = "switch", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_flaps_mid", Label = "Flaps Lever (MID)", InputCode = "button36", X = 24, Y = 35, Type = "switch", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_flaps_down", Label = "Flaps Lever (DOWN)", InputCode = "button37", X = 24, Y = 42, Type = "switch", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_mode_b1", Label = "Mode Button B1 (START)", InputCode = "button38", X = 12, Y = 56, Type = "button", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_mode_b2", Label = "Mode Button B2", InputCode = "button39", X = 18, Y = 56, Type = "button", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_mode_b3", Label = "Mode Button B3", InputCode = "button40", X = 24, Y = 56, Type = "button", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_rot_cw", Label = "Rotary Selector (CW)", InputCode = "button42", X = 14, Y = 70, Type = "encoder", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_rot_ccw", Label = "Rotary Selector (CCW)", InputCode = "button43", X = 22, Y = 70, Type = "encoder", ModuleName = "GNX-SEM" }
            });
        }

        // 4. Front Side Module - General Aviation (GNX-FSM-GA) if present
        if (detectedModules.Contains("GNX-FSM-GA") || true) {
            layout.Hotspots.AddRange(new[] {
                new ControllerHotspot { Id = "fsm_hdg_knob", Label = "HDG Heading Knob (CW)", InputCode = "button46", X = 74, Y = 28, Type = "encoder", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_alt_knob", Label = "ALT Altitude Knob (CW)", InputCode = "button49", X = 86, Y = 28, Type = "encoder", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_ap_btn", Label = "AP Autopilot Button", InputCode = "button51", X = 74, Y = 48, Type = "button", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_fd_btn", Label = "FD Flight Director Button", InputCode = "button52", X = 80, Y = 48, Type = "button", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_nav_btn", Label = "NAV Navigation Button", InputCode = "button53", X = 86, Y = 48, Type = "button", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_strobe_switch", Label = "Strobe Light Switch", InputCode = "button55", X = 74, Y = 68, Type = "switch", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_beacon_switch", Label = "Beacon Light Switch", InputCode = "button56", X = 80, Y = 68, Type = "switch", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_nav_switch", Label = "Nav Light Switch", InputCode = "button57", X = 86, Y = 68, Type = "switch", ModuleName = "GNX-FSM-GA" }
            });
        }

        return layout;
    }

    private static ControllerLayoutDefinition CreateVkbGladiatorOmniLeftLayout(string fullProductName) {
        var layout = new ControllerLayoutDefinition {
            Id = "vkb_gladiator_evo_ot_l",
            Name = "VKB Gladiator EVO Omni Throttle Left",
            Category = "Omni Throttle",
            Width = 600,
            Height = 650,
            AttachedModules = new List<string> { "Omni-Throttle" }
        };

        layout.Hotspots.AddRange(new[] {
            new ControllerHotspot { Id = "ot_trigger_1", Label = "Primary Trigger (Stage 1)", InputCode = "button1", X = 50, Y = 32, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "ot_trigger_rapid", Label = "Rapid Fire Trigger (Pull)", InputCode = "button3", X = 45, Y = 28, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "ot_thumb_hat_click", Label = "A1 Center Hat (Click)", InputCode = "button10", X = 48, Y = 24, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "ot_thumb_hat_up", Label = "A1 Center Hat (Up)", InputCode = "hat1_up", X = 48, Y = 20, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "ot_thumb_hat_down", Label = "A1 Center Hat (Down)", InputCode = "hat1_down", X = 48, Y = 28, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "ot_pinky_button", Label = "Pinky Button", InputCode = "button22", X = 40, Y = 46, Type = "button", ModuleName = "Grip" },

            // Omni Throttle Strafe Axes
            new ControllerHotspot { Id = "ot_axis_forward", Label = "Y Axis (Strafe Forward / Backward)", InputCode = "y", X = 48, Y = 56, Type = "axis", ModuleName = "Grip" },
            new ControllerHotspot { Id = "ot_axis_lateral", Label = "X Axis (Strafe Left / Right)", InputCode = "x", X = 56, Y = 56, Type = "axis", ModuleName = "Grip" },
            new ControllerHotspot { Id = "ot_axis_vertical", Label = "Z Twist Axis (Strafe Up / Down)", InputCode = "rotz", X = 48, Y = 50, Type = "axis", ModuleName = "Grip" },

            // Base Buttons & Encoders
            new ControllerHotspot { Id = "ot_base_b1", Label = "Base Button B1", InputCode = "button25", X = 40, Y = 78, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "ot_base_b2", Label = "Base Button B2", InputCode = "button26", X = 48, Y = 78, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "ot_base_b3", Label = "Base Button B3", InputCode = "button27", X = 56, Y = 78, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "ot_base_enc1_cw", Label = "Encoder 1 (CW)", InputCode = "button28", X = 36, Y = 74, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "ot_base_enc2_cw", Label = "Encoder 2 (CW)", InputCode = "button30", X = 60, Y = 74, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "ot_base_slider", Label = "Base Throttle Lever", InputCode = "slider1", X = 48, Y = 86, Type = "axis", ModuleName = "Base" }
        });

        return layout;
    }

    private static ControllerLayoutDefinition CreateGenericFlightStickLayout(string productName) {
        var layout = new ControllerLayoutDefinition {
            Id = "generic_stick",
            Name = string.IsNullOrWhiteSpace(productName) ? "Generic Flight Stick" : productName,
            Category = "Flight Stick",
            Width = 500,
            Height = 600
        };

        layout.Hotspots.AddRange(new[] {
            new ControllerHotspot { Id = "gen_trigger", Label = "Primary Trigger", InputCode = "button1", X = 50, Y = 30, Type = "button" },
            new ControllerHotspot { Id = "gen_b2", Label = "Button 2", InputCode = "button2", X = 45, Y = 25, Type = "button" },
            new ControllerHotspot { Id = "gen_b3", Label = "Button 3", InputCode = "button3", X = 55, Y = 25, Type = "button" },
            new ControllerHotspot { Id = "gen_b4", Label = "Button 4", InputCode = "button4", X = 50, Y = 20, Type = "button" },
            new ControllerHotspot { Id = "gen_axis_y", Label = "Y Axis (Pitch)", InputCode = "y", X = 48, Y = 55, Type = "axis" },
            new ControllerHotspot { Id = "gen_axis_x", Label = "X Axis (Roll / Yaw)", InputCode = "x", X = 55, Y = 55, Type = "axis" },
            new ControllerHotspot { Id = "gen_axis_twist", Label = "Twist Axis (Yaw / Roll)", InputCode = "rotz", X = 50, Y = 48, Type = "axis" }
        });

        return layout;
    }
}
