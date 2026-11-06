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
            Width = 1000,
            Height = 600,
            AttachedModules = detectedModules
        };

        // 1. Grip Controls (Space Combat Grip - Kosmosima)
        // Center stick is centered at X=50%, Head is Y=10% to 28%
        layout.Hotspots.AddRange(new[] {
            // A1 Analog Mini-Stick / Hat (Top-Left of Head)
            new ControllerHotspot { Id = "hat_a1_click", Label = "A1 Hat / Mini-Stick (Click)", InputCode = "button10", X = 46.5, Y = 18, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a1_up", Label = "A1 Hat (Up)", InputCode = "hat1_up", X = 46.5, Y = 15, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a1_down", Label = "A1 Hat (Down)", InputCode = "hat1_down", X = 46.5, Y = 21, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a1_left", Label = "A1 Hat (Left)", InputCode = "hat1_left", X = 44.5, Y = 18, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "hat_a1_right", Label = "A1 Hat (Right)", InputCode = "hat1_right", X = 48.5, Y = 18, Type = "hat", ModuleName = "Grip" },

            // A4 Top Hat (Top Center Edge of Head)
            new ControllerHotspot { Id = "hat_a4_click", Label = "A4 Top Hat (Click)", InputCode = "button14", X = 50, Y = 13.5, Type = "hat", ModuleName = "Grip" },

            // A2 Red Weapon Release Button (Top-Right of Head)
            new ControllerHotspot { Id = "red_weapon_button", Label = "A2 Red Weapon Button", InputCode = "button5", X = 53.5, Y = 16, Type = "button", ModuleName = "Grip" },

            // A3 Center Hat (Middle Faceplate)
            new ControllerHotspot { Id = "hat_a3_click", Label = "A3 Center Hat (Click)", InputCode = "button16", X = 50, Y = 23, Type = "hat", ModuleName = "Grip" },

            // C1 Thumb Shelf Hat / Button (Left Ergonomic Shelf)
            new ControllerHotspot { Id = "thumb_c1", Label = "C1 Thumb Shelf Switch", InputCode = "button18", X = 41.5, Y = 32, Type = "button", ModuleName = "Grip" },

            // Triggers (Front of Grip)
            new ControllerHotspot { Id = "trigger_rapid_push", Label = "Rapid Fire Trigger (Push)", InputCode = "button4", X = 50, Y = 33, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "trigger_rapid_pull", Label = "Rapid Fire Trigger (Pull)", InputCode = "button3", X = 50, Y = 37, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "trigger_primary_1", Label = "Primary Trigger (Stage 1)", InputCode = "button1", X = 50, Y = 42, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "trigger_primary_2", Label = "Primary Trigger (Stage 2)", InputCode = "button2", X = 50, Y = 46, Type = "button", ModuleName = "Grip" },

            // Pinky Button (Lower Grip Right)
            new ControllerHotspot { Id = "pinky_button", Label = "Pinky Button (D1)", InputCode = "button22", X = 44, Y = 56, Type = "button", ModuleName = "Grip" },

            // Primary Axes
            new ControllerHotspot { Id = "axis_pitch", Label = "Y Axis (Pitch)", InputCode = "y", X = 50, Y = 62, Type = "axis", ModuleName = "Grip" },
            new ControllerHotspot { Id = "axis_roll", Label = "X Axis (Roll / Yaw)", InputCode = "x", X = 54, Y = 62, Type = "axis", ModuleName = "Grip" },
            new ControllerHotspot { Id = "axis_twist", Label = "Z Twist Axis (Yaw / Roll)", InputCode = "rotz", X = 46, Y = 62, Type = "axis", ModuleName = "Grip" }
        });

        // 2. Base Controls (EVO Base X: 35% to 65%, Y: 70% to 92%)
        layout.Hotspots.AddRange(new[] {
            new ControllerHotspot { Id = "base_enc1_cw", Label = "Base Encoder 1 (CW)", InputCode = "button28", X = 39, Y = 75, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_enc1_ccw", Label = "Base Encoder 1 (CCW)", InputCode = "button29", X = 39, Y = 80, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_enc2_cw", Label = "Base Encoder 2 (CW)", InputCode = "button30", X = 61, Y = 75, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_enc2_ccw", Label = "Base Encoder 2 (CCW)", InputCode = "button31", X = 61, Y = 80, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_b1", Label = "Base Button B1", InputCode = "button25", X = 46, Y = 78, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_b2", Label = "Base Button B2", InputCode = "button26", X = 50, Y = 78, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_b3", Label = "Base Button B3", InputCode = "button27", X = 54, Y = 78, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "base_throttle", Label = "Base Throttle Lever", InputCode = "slider1", X = 50, Y = 87, Type = "axis", ModuleName = "Base" }
        });

        // 3. Side Extension Module (GNX-SEM - Left Panel X: 3% to 28%)
        if (detectedModules.Contains("GNX-SEM") || true) {
            layout.Hotspots.AddRange(new[] {
                // Landing Gear Lever (Left Slot X: 8.5%)
                new ControllerHotspot { Id = "sem_gear_up", Label = "Landing Gear Switch (UP)", InputCode = "button33", X = 8.5, Y = 22, Type = "switch", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_gear_down", Label = "Landing Gear Switch (DOWN)", InputCode = "button34", X = 8.5, Y = 38, Type = "switch", ModuleName = "GNX-SEM" },

                // Flaps Lever (Right Slot X: 22.5%)
                new ControllerHotspot { Id = "sem_flaps_up", Label = "Flaps Lever (UP)", InputCode = "button35", X = 22.5, Y = 20, Type = "switch", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_flaps_mid", Label = "Flaps Lever (MID)", InputCode = "button36", X = 22.5, Y = 30, Type = "switch", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_flaps_down", Label = "Flaps Lever (DOWN)", InputCode = "button37", X = 22.5, Y = 40, Type = "switch", ModuleName = "GNX-SEM" },

                // Mode Push Buttons Row (Y: 52%)
                new ControllerHotspot { Id = "sem_mode_b1", Label = "Mode Button B1 (START)", InputCode = "button38", X = 7, Y = 52, Type = "button", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_mode_b2", Label = "Mode Button B2", InputCode = "button39", X = 12.5, Y = 52, Type = "button", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_mode_b3", Label = "Mode Button B3", InputCode = "button40", X = 18, Y = 52, Type = "button", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_mode_b4", Label = "Mode Button B4", InputCode = "button41", X = 23.5, Y = 52, Type = "button", ModuleName = "GNX-SEM" },

                // K-Switch Rocker (Y: 67%)
                new ControllerHotspot { Id = "sem_ksw_up", Label = "K-Switch (UP)", InputCode = "button42", X = 10, Y = 66, Type = "switch", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_ksw_down", Label = "K-Switch (DOWN)", InputCode = "button43", X = 10, Y = 74, Type = "switch", ModuleName = "GNX-SEM" },

                // Rotary Selector (Y: 78%)
                new ControllerHotspot { Id = "sem_rot_cw", Label = "Rotary Mode (CW)", InputCode = "button44", X = 20, Y = 66, Type = "encoder", ModuleName = "GNX-SEM" },
                new ControllerHotspot { Id = "sem_rot_ccw", Label = "Rotary Mode (CCW)", InputCode = "button45", X = 20, Y = 74, Type = "encoder", ModuleName = "GNX-SEM" }
            });
        }

        // 4. Front Side Module - General Aviation (GNX-FSM-GA - Right Panel X: 72% to 97%)
        if (detectedModules.Contains("GNX-FSM-GA") || true) {
            layout.Hotspots.AddRange(new[] {
                // HDG Knob (Left X: 78%, Y: 24%)
                new ControllerHotspot { Id = "fsm_hdg_click", Label = "HDG Heading Knob (Click)", InputCode = "button45", X = 78, Y = 22, Type = "encoder", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_hdg_cw", Label = "HDG Heading Knob (CW)", InputCode = "button46", X = 81, Y = 25, Type = "encoder", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_hdg_ccw", Label = "HDG Heading Knob (CCW)", InputCode = "button47", X = 75, Y = 25, Type = "encoder", ModuleName = "GNX-FSM-GA" },

                // ALT Knob (Right X: 91%, Y: 24%)
                new ControllerHotspot { Id = "fsm_alt_click", Label = "ALT Altitude Knob (Click)", InputCode = "button48", X = 91, Y = 22, Type = "encoder", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_alt_cw", Label = "ALT Altitude Knob (CW)", InputCode = "button49", X = 94, Y = 25, Type = "encoder", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_alt_ccw", Label = "ALT Altitude Knob (CCW)", InputCode = "button50", X = 88, Y = 25, Type = "encoder", ModuleName = "GNX-FSM-GA" },

                // Autopilot Buttons Row (Y: 44%)
                new ControllerHotspot { Id = "fsm_ap_btn", Label = "AP Autopilot Button", InputCode = "button51", X = 76, Y = 44, Type = "button", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_fd_btn", Label = "FD Flight Director Button", InputCode = "button52", X = 81.5, Y = 44, Type = "button", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_nav_btn", Label = "NAV Navigation Button", InputCode = "button53", X = 87.5, Y = 44, Type = "button", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_apr_btn", Label = "APR Approach Button", InputCode = "button54", X = 93, Y = 44, Type = "button", ModuleName = "GNX-FSM-GA" },

                // Light Toggle Switches Row (Y: 68%)
                new ControllerHotspot { Id = "fsm_strobe_switch", Label = "Strobe Light Switch", InputCode = "button55", X = 76, Y = 68, Type = "switch", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_beacon_switch", Label = "Beacon Light Switch", InputCode = "button56", X = 81.5, Y = 68, Type = "switch", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_nav_switch", Label = "Nav Light Switch", InputCode = "button57", X = 87.5, Y = 68, Type = "switch", ModuleName = "GNX-FSM-GA" },
                new ControllerHotspot { Id = "fsm_taxi_switch", Label = "Taxi Light Switch", InputCode = "button58", X = 93, Y = 68, Type = "switch", ModuleName = "GNX-FSM-GA" }
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

