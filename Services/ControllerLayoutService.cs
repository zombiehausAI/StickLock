using StickLock.Models;

namespace StickLock.Services;

public class ControllerLayoutService : IControllerLayoutService {
    public ControllerLayoutDefinition GetLayoutForDevice(string productName) {
        var clean = productName ?? string.Empty;
        var upper = clean.ToUpperInvariant();

        if (upper.Contains("KEYBOARD") || upper.Contains("MOUSE") || upper.Contains("KB/M") || upper.Contains("KB1") || upper.Contains("MO1")) {
            return CreateKeyboardMouseLayout();
        }

        if (upper.Contains("XBOX") || upper.Contains("GAMEPAD") || upper.Contains("CONTROLLER") || upper.Contains("XINPUT") || upper.Contains("GP1")) {
            return CreateXboxControllerLayout();
        }

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
            CreateXboxControllerLayout(),
            CreateKeyboardMouseLayout(),
            CreateGenericFlightStickLayout("Generic Joystick")
        };
    }

    private static ControllerLayoutDefinition CreateVkbGladiatorRightCompositeLayout(string fullProductName) {
        var detectedModules = ControllerLayoutDefinition.DetectModulesFromProductName(fullProductName);

        var root = new ControllerLayoutDefinition {
            Id = "vkb_gladiator_evo_r",
            Name = "VKB Gladiator EVO Right (Space Combat)",
            Category = "Flight Stick",
            Width = 1000,
            Height = 770,
            ImageUrl = "images/layouts/vkb_gladiator_right.jpg",
            AttachedModules = detectedModules
        };

        // Primary Module: Gladiator NXT / EVO Right Stick (Kosmosima SCG Grip + Base)
        root.Hotspots.AddRange(CreateGladiatorRightHotspots("Grip"));

        // SubModule 1: GNX-SEM (Side Extension Module) if detected
        if (detectedModules.Contains("GNX-SEM") || true) {
            var semModule = new ControllerLayoutDefinition {
                Id = "vkb_gnx_sem",
                Name = "GNX-SEM (Side Extension Module)",
                Category = "Module",
                Width = 1000,
                Height = 770,
                ImageUrl = "images/layouts/vkb_gnx_sem.jpg",
                AttachedModules = new List<string> { "GNX-SEM" },
                Hotspots = CreateGnxSemHotspots()
            };
            root.SubModules.Add(semModule);
        }

        // SubModule 2: GNX-FSM-GA (Front Side Module - General Aviation) if detected
        if (detectedModules.Contains("GNX-FSM-GA") || true) {
            var fsmModule = new ControllerLayoutDefinition {
                Id = "vkb_fsm_ga",
                Name = "GNX-FSM-GA (Front Switchboard Module)",
                Category = "Module",
                Width = 1000,
                Height = 770,
                ImageUrl = "images/layouts/vkb_fsm_ga.jpg",
                AttachedModules = new List<string> { "GNX-FSM-GA" },
                Hotspots = CreateGnxFsmGaHotspots()
            };
            root.SubModules.Add(fsmModule);
        }

        return root;
    }

    private static List<ControllerHotspot> CreateGladiatorRightHotspots(string moduleName) {
        return new List<ControllerHotspot> {
            // Triggers & Grip Buttons (Left Side Callout Boxes)
            new ControllerHotspot { Id = "r_btn1", Label = "Fire 1 (Stage 1 Soft Pull)", InputCode = "button1", X = 32.5, Y = 72.5, Type = "button", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn2", Label = "Fire 2 (Stage 2 Full Pull)", InputCode = "button2", X = 31.3, Y = 75.5, Type = "button", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn3", Label = "A2 Red Weapon Release Button", InputCode = "button3", X = 27.4, Y = 28.0, Type = "button", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn4", Label = "B1 Upper Side Button", InputCode = "button4", X = 38.2, Y = 58.4, Type = "button", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn5", Label = "D1 Pinky Button", InputCode = "button5", X = 41.7, Y = 51.4, Type = "button", ModuleName = moduleName },

            // Center Hat A3
            new ControllerHotspot { Id = "r_btn6", Label = "A3 Center Hat (UP)", InputCode = "button6", X = 62.9, Y = 20.9, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn7", Label = "A3 Center Hat (RIGHT)", InputCode = "button7", X = 71.1, Y = 25.1, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn8", Label = "A3 Center Hat (DOWN)", InputCode = "button8", X = 62.9, Y = 29.1, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn9", Label = "A3 Center Hat (CLICK)", InputCode = "button9", X = 55.0, Y = 24.9, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn10", Label = "A3 Center Hat (LEFT)", InputCode = "button10", X = 62.8, Y = 24.9, Type = "hat", ModuleName = moduleName },

            // Top Hat A4
            new ControllerHotspot { Id = "r_btn11", Label = "A4 Top Hat (UP)", InputCode = "button11", X = 60.7, Y = 5.9, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn12", Label = "A4 Top Hat (RIGHT)", InputCode = "button12", X = 68.5, Y = 10.0, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn13", Label = "A4 Top Hat (DOWN)", InputCode = "button13", X = 60.6, Y = 14.0, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn14", Label = "A4 Top Hat (CLICK)", InputCode = "button14", X = 53.1, Y = 10.0, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn15", Label = "A4 Top Hat (LEFT)", InputCode = "button15", X = 60.7, Y = 10.0, Type = "hat", ModuleName = moduleName },

            // Thumb Hat C1
            new ControllerHotspot { Id = "r_btn16", Label = "C1 Thumb Hat (UP)", InputCode = "button16", X = 64.5, Y = 35.5, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn17", Label = "C1 Thumb Hat (RIGHT)", InputCode = "button17", X = 72.7, Y = 41.0, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn18", Label = "C1 Thumb Hat (DOWN)", InputCode = "button18", X = 64.5, Y = 46.5, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn19", Label = "C1 Thumb Hat (CLICK)", InputCode = "button19", X = 58.5, Y = 40.9, Type = "hat", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn20", Label = "C1 Thumb Hat (LEFT)", InputCode = "button20", X = 64.5, Y = 41.0, Type = "hat", ModuleName = moduleName },

            // SpeedFire Rapid Trigger
            new ControllerHotspot { Id = "r_btn21", Label = "SpeedFire Rapid Trigger (PULL)", InputCode = "button21", X = 37.2, Y = 63.9, Type = "button", ModuleName = moduleName },
            new ControllerHotspot { Id = "r_btn22", Label = "SpeedFire Rapid Trigger (PUSH)", InputCode = "button22", X = 36.1, Y = 68.0, Type = "button", ModuleName = moduleName },

            // Base Encoders & Buttons
            new ControllerHotspot { Id = "r_btn23", Label = "En2 Right Encoder (UP)", InputCode = "button23", X = 72.8, Y = 76.6, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "r_btn24", Label = "En2 Right Encoder (DOWN)", InputCode = "button24", X = 72.1, Y = 82.9, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "r_btn25", Label = "En1 Left Encoder (UP)", InputCode = "button25", X = 63.6, Y = 90.8, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "r_btn26", Label = "En1 Left Encoder (DOWN)", InputCode = "button26", X = 63.2, Y = 95.0, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "r_btn27", Label = "F1 Base Button", InputCode = "button27", X = 65.8, Y = 63.8, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "r_btn28", Label = "F2 Base Button", InputCode = "button28", X = 70.5, Y = 68.6, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "r_btn29", Label = "F3 Base Button", InputCode = "button29", X = 61.8, Y = 59.4, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "r_slider1", Label = "Throttle Slider", InputCode = "slider1", X = 68.2, Y = 86.4, Type = "axis", ModuleName = "Base" }
        };
    }

    private static List<ControllerHotspot> CreateGnxSemHotspots() {
        return new List<ControllerHotspot> {
            // Left Buttons
            new ControllerHotspot { Id = "sem_btn41", Label = "SEM Button A1", InputCode = "button41", X = 8.0, Y = 48.0, Type = "button", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn42", Label = "SEM Button A2", InputCode = "button42", X = 8.0, Y = 53.0, Type = "button", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn52", Label = "SEM Red START Button", InputCode = "button52", X = 8.0, Y = 58.0, Type = "button", ModuleName = "GNX-SEM" },

            // Middle Buttons
            new ControllerHotspot { Id = "sem_btn43", Label = "SEM Button B1", InputCode = "button43", X = 88.0, Y = 50.5, Type = "button", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn44", Label = "SEM Button B2", InputCode = "button44", X = 88.0, Y = 46.5, Type = "button", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn45", Label = "SEM Button B3", InputCode = "button45", X = 88.0, Y = 41.5, Type = "button", ModuleName = "GNX-SEM" },

            // SW1 K-Switch (Left Rocker)
            new ControllerHotspot { Id = "sem_btn46", Label = "SW1 K-Switch (DOWN)", InputCode = "button46", X = 32.0, Y = 26.0, Type = "switch", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn47", Label = "SW1 K-Switch (CENTER)", InputCode = "button47", X = 36.5, Y = 21.0, Type = "switch", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn48", Label = "SW1 K-Switch (UP)", InputCode = "button48", X = 38.0, Y = 16.5, Type = "switch", ModuleName = "GNX-SEM" },

            // SW2 K-Switch (Right Rocker)
            new ControllerHotspot { Id = "sem_btn49", Label = "SW2 K-Switch (DOWN)", InputCode = "button49", X = 49.0, Y = 7.5, Type = "switch", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn50", Label = "SW2 K-Switch (CENTER)", InputCode = "button50", X = 51.5, Y = 12.0, Type = "switch", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn51", Label = "SW2 K-Switch (UP)", InputCode = "button51", X = 53.5, Y = 16.5, Type = "switch", ModuleName = "GNX-SEM" },

            // Rotary Knob Modes & C1
            new ControllerHotspot { Id = "sem_btn53", Label = "Mode Rotary Knob (Position 1)", InputCode = "button53", X = 82.0, Y = 83.5, Type = "encoder", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn54", Label = "Mode Rotary Knob (Position 2)", InputCode = "button54", X = 82.0, Y = 79.5, Type = "encoder", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn55", Label = "Mode Rotary Knob (Position 3)", InputCode = "button55", X = 82.0, Y = 75.5, Type = "encoder", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn56", Label = "Mode Rotary Knob (Position 4)", InputCode = "button56", X = 82.0, Y = 71.5, Type = "encoder", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn57", Label = "SEM Button C1", InputCode = "button57", X = 82.0, Y = 67.5, Type = "button", ModuleName = "GNX-SEM" },

            // Flaps Lever
            new ControllerHotspot { Id = "sem_btn58", Label = "Flaps Lever (DOWN)", InputCode = "button58", X = 75.5, Y = 24.5, Type = "switch", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn59", Label = "Flaps Lever (CENTER)", InputCode = "button59", X = 75.5, Y = 20.5, Type = "switch", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn61", Label = "Flaps Lever (UP)", InputCode = "button61", X = 75.5, Y = 16.5, Type = "switch", ModuleName = "GNX-SEM" },

            // Landing Gear Lever
            new ControllerHotspot { Id = "sem_btn62", Label = "Landing Gear Switch (DOWN)", InputCode = "button62", X = 88.0, Y = 63.5, Type = "switch", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn63", Label = "Landing Gear Switch (CENTER)", InputCode = "button63", X = 88.0, Y = 59.5, Type = "switch", ModuleName = "GNX-SEM" },
            new ControllerHotspot { Id = "sem_btn64", Label = "Landing Gear Switch (UP)", InputCode = "button64", X = 88.0, Y = 55.5, Type = "switch", ModuleName = "GNX-SEM" }
        };
    }

    private static List<ControllerHotspot> CreateGnxFsmGaHotspots() {
        return new List<ControllerHotspot> {
            // Left Column
            new ControllerHotspot { Id = "fsm_btn1", Label = "HDG Button", InputCode = "button1", X = 8.5, Y = 43.8, Type = "button", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn2", Label = "TRK Button", InputCode = "button2", X = 8.5, Y = 51.5, Type = "button", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn3", Label = "NAV Button", InputCode = "button3", X = 8.5, Y = 59.5, Type = "button", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn4", Label = "APR Button", InputCode = "button4", X = 8.5, Y = 66.8, Type = "button", ModuleName = "GNX-FSM-GA" },

            // Right Column
            new ControllerHotspot { Id = "fsm_btn5", Label = "ALT Button", InputCode = "button5", X = 87.5, Y = 33.5, Type = "button", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn6", Label = "LVL Button", InputCode = "button6", X = 87.5, Y = 41.5, Type = "button", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn7", Label = "VNAV Button", InputCode = "button7", X = 87.5, Y = 49.5, Type = "button", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn8", Label = "IAS Button", InputCode = "button8", X = 87.5, Y = 56.5, Type = "button", ModuleName = "GNX-FSM-GA" },

            // Center Autopilot Buttons
            new ControllerHotspot { Id = "fsm_btn9", Label = "AP Autopilot Button", InputCode = "button9", X = 48.5, Y = 14.5, Type = "button", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn10", Label = "YD Yaw Damper Button", InputCode = "button10", X = 53.5, Y = 18.5, Type = "button", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn11", Label = "FD Flight Director Button", InputCode = "button11", X = 53.5, Y = 28.5, Type = "button", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn12", Label = "VS Vertical Speed Button", InputCode = "button12", X = 28.5, Y = 88.5, Type = "button", ModuleName = "GNX-FSM-GA" },

            // Bottom Avionics & Lights
            new ControllerHotspot { Id = "fsm_btn13", Label = "AVIONIC Switch", InputCode = "button13", X = 38.5, Y = 83.5, Type = "switch", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn14", Label = "LANDING LIGHTS Switch", InputCode = "button14", X = 47.5, Y = 88.5, Type = "switch", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn15", Label = "STROBE LIGHTS Switch", InputCode = "button15", X = 57.0, Y = 83.5, Type = "switch", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn16", Label = "NAV LIGHTS Switch", InputCode = "button16", X = 65.5, Y = 88.5, Type = "switch", ModuleName = "GNX-FSM-GA" },

            // HDG & ALT Knobs
            new ControllerHotspot { Id = "fsm_btn17", Label = "HDG/TRK Knob (LEFT)", InputCode = "button17", X = 36.5, Y = 10.5, Type = "encoder", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn18", Label = "HDG/TRK Knob (RIGHT)", InputCode = "button18", X = 36.5, Y = 14.5, Type = "encoder", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn19", Label = "ALT SEL Knob (LEFT)", InputCode = "button19", X = 63.0, Y = 10.5, Type = "encoder", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn20", Label = "ALT SEL Knob (RIGHT)", InputCode = "button20", X = 63.0, Y = 14.5, Type = "encoder", ModuleName = "GNX-FSM-GA" },

            // Thumb Wheel
            new ControllerHotspot { Id = "fsm_btn21", Label = "Center Thumb Wheel (UP)", InputCode = "button21", X = 88.0, Y = 61.5, Type = "encoder", ModuleName = "GNX-FSM-GA" },
            new ControllerHotspot { Id = "fsm_btn22", Label = "Center Thumb Wheel (DOWN)", InputCode = "button22", X = 88.0, Y = 65.5, Type = "encoder", ModuleName = "GNX-FSM-GA" }
        };
    }

    private static ControllerLayoutDefinition CreateVkbGladiatorOmniLeftLayout(string fullProductName) {
        var layout = new ControllerLayoutDefinition {
            Id = "vkb_gladiator_evo_ot_l",
            Name = "VKB Gladiator EVO Omni Throttle Left",
            Category = "Omni Throttle",
            Width = 1000,
            Height = 770,
            ImageUrl = "images/layouts/vkb_gladiator_omni_left.jpg",
            AttachedModules = new List<string> { "Omni-Throttle" }
        };

        layout.Hotspots.AddRange(new[] {
            // Triggers & Buttons (Right Side Callout Boxes)
            new ControllerHotspot { Id = "l_btn1", Label = "Fire 1 (Stage 1 Soft Pull)", InputCode = "button1", X = 68.6, Y = 72.1, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn2", Label = "Fire 2 (Stage 2 Full Pull)", InputCode = "button2", X = 69.2, Y = 75.8, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn3", Label = "A2 Red Weapon Button", InputCode = "button3", X = 73.8, Y = 26.4, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn4", Label = "B1 Upper Side Button", InputCode = "button4", X = 60.7, Y = 57.9, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn5", Label = "D1 Pinky Button", InputCode = "button5", X = 58.5, Y = 51.5, Type = "button", ModuleName = "Grip" },

            // SpeedFire Rapid Trigger
            new ControllerHotspot { Id = "l_btn21", Label = "SpeedFire Rapid Trigger (PULL)", InputCode = "button21", X = 61.9, Y = 62.4, Type = "button", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn22", Label = "SpeedFire Rapid Trigger (PUSH)", InputCode = "button22", X = 64.9, Y = 66.7, Type = "button", ModuleName = "Grip" },

            // Center Hat A3
            new ControllerHotspot { Id = "l_btn6", Label = "A3 Center Hat (UP)", InputCode = "button6", X = 30.0, Y = 18.9, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn7", Label = "A3 Center Hat (RIGHT)", InputCode = "button7", X = 38.1, Y = 23.4, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn8", Label = "A3 Center Hat (DOWN)", InputCode = "button8", X = 30.1, Y = 28.5, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn9", Label = "A3 Center Hat (CLICK)", InputCode = "button9", X = 22.0, Y = 22.9, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn10", Label = "A3 Center Hat (LEFT)", InputCode = "button10", X = 30.2, Y = 23.4, Type = "hat", ModuleName = "Grip" },

            // Top Hat A4
            new ControllerHotspot { Id = "l_btn11", Label = "A4 Top Hat (UP)", InputCode = "button11", X = 34.1, Y = 5.5, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn12", Label = "A4 Top Hat (RIGHT)", InputCode = "button12", X = 42.1, Y = 10.6, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn13", Label = "A4 Top Hat (DOWN)", InputCode = "button13", X = 34.1, Y = 15.0, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn14", Label = "A4 Top Hat (CLICK)", InputCode = "button14", X = 26.0, Y = 11.1, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn15", Label = "A4 Top Hat (LEFT)", InputCode = "button15", X = 34.2, Y = 10.7, Type = "hat", ModuleName = "Grip" },

            // Thumb Hat C1
            new ControllerHotspot { Id = "l_btn16", Label = "C1 Thumb Hat (UP)", InputCode = "button16", X = 21.5, Y = 36.5, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn17", Label = "C1 Thumb Hat (RIGHT)", InputCode = "button17", X = 32.5, Y = 41.5, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn18", Label = "C1 Thumb Hat (DOWN)", InputCode = "button18", X = 21.5, Y = 46.5, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn19", Label = "C1 Thumb Hat (CLICK)", InputCode = "button19", X = 10.0, Y = 41.5, Type = "hat", ModuleName = "Grip" },
            new ControllerHotspot { Id = "l_btn20", Label = "C1 Thumb Hat (LEFT)", InputCode = "button20", X = 21.5, Y = 41.5, Type = "hat", ModuleName = "Grip" },

            // Base Encoders & Buttons
            new ControllerHotspot { Id = "l_btn29", Label = "F3 Base Button", InputCode = "button29", X = 38.1, Y = 58.9, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "l_btn27", Label = "F1 Base Button", InputCode = "button27", X = 33.9, Y = 64.3, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "l_btn28", Label = "F2 Base Button", InputCode = "button28", X = 29.9, Y = 68.4, Type = "button", ModuleName = "Base" },
            new ControllerHotspot { Id = "l_btn23", Label = "En2 Right Encoder (UP)", InputCode = "button23", X = 28.4, Y = 76.1, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "l_btn24", Label = "En2 Right Encoder (DOWN)", InputCode = "button24", X = 28.2, Y = 82.1, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "l_slider1", Label = "Throttle Slider", InputCode = "slider1", X = 31.5, Y = 86.1, Type = "axis", ModuleName = "Base" },
            new ControllerHotspot { Id = "l_btn25", Label = "En1 Left Encoder (UP)", InputCode = "button25", X = 35.4, Y = 89.7, Type = "encoder", ModuleName = "Base" },
            new ControllerHotspot { Id = "l_btn26", Label = "En1 Left Encoder (DOWN)", InputCode = "button26", X = 35.8, Y = 94.5, Type = "encoder", ModuleName = "Base" }
        });

        return layout;
    }

    private static ControllerLayoutDefinition CreateXboxControllerLayout() {
        var layout = new ControllerLayoutDefinition {
            Id = "xbox_controller",
            Name = "Xbox Wireless Controller (Gamepad)",
            Category = "Gamepad",
            Width = 1000,
            Height = 650,
            ImageUrl = "images/layouts/xbox_controller.svg",
            AttachedModules = new List<string> { "Gamepad" }
        };

        layout.Hotspots.AddRange(new[] {
            // Face Buttons
            new ControllerHotspot { Id = "gp_btn_a", Label = "A Button", InputCode = "button_a", X = 68.0, Y = 46.0, Type = "button", ModuleName = "Face" },
            new ControllerHotspot { Id = "gp_btn_b", Label = "B Button", InputCode = "button_b", X = 73.0, Y = 36.9, Type = "button", ModuleName = "Face" },
            new ControllerHotspot { Id = "gp_btn_x", Label = "X Button", InputCode = "button_x", X = 63.0, Y = 36.9, Type = "button", ModuleName = "Face" },
            new ControllerHotspot { Id = "gp_btn_y", Label = "Y Button", InputCode = "button_y", X = 68.0, Y = 28.0, Type = "button", ModuleName = "Face" },

            // Bumpers & Triggers
            new ControllerHotspot { Id = "gp_lb", Label = "Left Bumper (LB)", InputCode = "shoulderl", X = 33.5, Y = 17.5, Type = "button", ModuleName = "Bumpers" },
            new ControllerHotspot { Id = "gp_rb", Label = "Right Bumper (RB)", InputCode = "shoulderr", X = 66.5, Y = 17.5, Type = "button", ModuleName = "Bumpers" },
            new ControllerHotspot { Id = "gp_lt", Label = "Left Trigger (LT)", InputCode = "triggerl", X = 31.0, Y = 12.0, Type = "axis", ModuleName = "Triggers" },
            new ControllerHotspot { Id = "gp_rt", Label = "Right Trigger (RT)", InputCode = "triggerr", X = 69.0, Y = 12.0, Type = "axis", ModuleName = "Triggers" },

            // D-Pad
            new ControllerHotspot { Id = "gp_dpad_up", Label = "D-Pad UP", InputCode = "dpad_up", X = 42.0, Y = 49.0, Type = "hat", ModuleName = "D-Pad" },
            new ControllerHotspot { Id = "gp_dpad_down", Label = "D-Pad DOWN", InputCode = "dpad_down", X = 42.0, Y = 62.0, Type = "hat", ModuleName = "D-Pad" },
            new ControllerHotspot { Id = "gp_dpad_left", Label = "D-Pad LEFT", InputCode = "dpad_left", X = 37.5, Y = 55.4, Type = "hat", ModuleName = "D-Pad" },
            new ControllerHotspot { Id = "gp_dpad_right", Label = "D-Pad RIGHT", InputCode = "dpad_right", X = 46.5, Y = 55.4, Type = "hat", ModuleName = "D-Pad" },

            // Thumbsticks
            new ControllerHotspot { Id = "gp_ls_click", Label = "Left Stick Click (LS)", InputCode = "thumble", X = 33.0, Y = 36.9, Type = "button", ModuleName = "Sticks" },
            new ControllerHotspot { Id = "gp_rs_click", Label = "Right Stick Click (RS)", InputCode = "thumbr", X = 58.0, Y = 58.5, Type = "button", ModuleName = "Sticks" },

            // Center Buttons
            new ControllerHotspot { Id = "gp_view", Label = "View / Back Button", InputCode = "back", X = 43.0, Y = 36.9, Type = "button", ModuleName = "Center" },
            new ControllerHotspot { Id = "gp_menu", Label = "Menu / Start Button", InputCode = "start", X = 57.0, Y = 36.9, Type = "button", ModuleName = "Center" }
        });

        return layout;
    }

    private static ControllerLayoutDefinition CreateKeyboardMouseLayout() {
        var layout = new ControllerLayoutDefinition {
            Id = "keyboard_mouse",
            Name = "Keyboard & Mouse",
            Category = "Keyboard & Mouse",
            Width = 1320,
            Height = 480,
            ImageUrl = "images/layouts/keyboard_mouse.svg",
            AttachedModules = new List<string> { "Keyboard", "Mouse" }
        };

        layout.Hotspots.AddRange(new[] {
            // Function Row (Y = 15.0%)
            new ControllerHotspot { Id = "km_esc", Label = "Escape", InputCode = "escape", X = 5.4, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f1", Label = "F1", InputCode = "f1", X = 11.6, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f2", Label = "F2", InputCode = "f2", X = 14.6, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f3", Label = "F3", InputCode = "f3", X = 17.7, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f4", Label = "F4", InputCode = "f4", X = 20.7, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f5", Label = "F5", InputCode = "f5", X = 25.6, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f6", Label = "F6", InputCode = "f6", X = 28.6, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f7", Label = "F7", InputCode = "f7", X = 31.7, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f8", Label = "F8", InputCode = "f8", X = 34.7, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f9", Label = "F9", InputCode = "f9", X = 39.6, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f10", Label = "F10", InputCode = "f10", X = 42.7, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f11", Label = "F11", InputCode = "f11", X = 45.7, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f12", Label = "F12", InputCode = "f12", X = 48.7, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_prt", Label = "Print Screen", InputCode = "printscreen", X = 55.3, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_scrlk", Label = "Scroll Lock", InputCode = "scrolllock", X = 58.7, Y = 15.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_pause", Label = "Pause / Break", InputCode = "pause", X = 62.1, Y = 15.0, Type = "key", ModuleName = "Keyboard" },

            // Number Row (Y = 24.8%)
            new ControllerHotspot { Id = "km_grave", Label = "~ / ` (Console)", InputCode = "grave", X = 5.4, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_1", Label = "1", InputCode = "1", X = 8.6, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_2", Label = "2", InputCode = "2", X = 12.0, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_3", Label = "3", InputCode = "3", X = 15.3, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_4", Label = "4", InputCode = "4", X = 18.6, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_5", Label = "5", InputCode = "5", X = 22.0, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_6", Label = "6", InputCode = "6", X = 25.3, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_7", Label = "7", InputCode = "7", X = 28.6, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_8", Label = "8", InputCode = "8", X = 32.0, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_9", Label = "9", InputCode = "9", X = 35.3, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_0", Label = "0", InputCode = "0", X = 38.6, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_minus", Label = "- (Minus)", InputCode = "minus", X = 42.0, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_equals", Label = "= (Equals)", InputCode = "equals", X = 45.3, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_backspace", Label = "Backspace", InputCode = "backspace", X = 49.7, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_ins", Label = "Insert", InputCode = "insert", X = 55.3, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_home", Label = "Home", InputCode = "home", X = 58.7, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_pgup", Label = "Page Up", InputCode = "pageup", X = 62.1, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_numlock", Label = "Num Lock", InputCode = "numlock", X = 66.7, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_div", Label = "Num /", InputCode = "np_divide", X = 70.1, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_mul", Label = "Num *", InputCode = "np_multiply", X = 73.5, Y = 24.8, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_sub", Label = "Num -", InputCode = "np_subtract", X = 76.9, Y = 24.8, Type = "key", ModuleName = "Keyboard" },

            // QWERTY Row (Y = 34.0%)
            new ControllerHotspot { Id = "km_tab", Label = "Tab (Scan Mode)", InputCode = "tab", X = 6.1, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_q", Label = "Q (Roll Left)", InputCode = "q", X = 10.0, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_w", Label = "W (Strafe Forward)", InputCode = "w", X = 13.3, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_e", Label = "E (Roll Right)", InputCode = "e", X = 16.7, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_r", Label = "R (Target Nearest)", InputCode = "r", X = 20.0, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_t", Label = "T (Target Ahead)", InputCode = "t", X = 23.3, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_y", Label = "Y", InputCode = "y", X = 26.7, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_u", Label = "U", InputCode = "u", X = 30.0, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_i", Label = "I (Engine Power)", InputCode = "i", X = 33.3, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_o", Label = "O (Shield Power)", InputCode = "o", X = 36.7, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_p", Label = "P (Weapon Power)", InputCode = "p", X = 40.0, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_lbracket", Label = "[ (Bracket Left)", InputCode = "lbracket", X = 43.3, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_rbracket", Label = "] (Bracket Right)", InputCode = "rbracket", X = 46.7, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_backslash", Label = "\\ (Backslash)", InputCode = "backslash", X = 50.4, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_del", Label = "Delete", InputCode = "delete", X = 55.3, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_end", Label = "End", InputCode = "end", X = 58.7, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_pgdn", Label = "Page Down", InputCode = "pagedown", X = 62.1, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_7", Label = "Num 7", InputCode = "np_7", X = 66.7, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_8", Label = "Num 8", InputCode = "np_8", X = 70.1, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_9", Label = "Num 9", InputCode = "np_9", X = 73.5, Y = 34.0, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_add", Label = "Num +", InputCode = "np_add", X = 76.9, Y = 38.5, Type = "key", ModuleName = "Keyboard" },

            // ASDF Row (Y = 43.1%)
            new ControllerHotspot { Id = "km_capslock", Label = "Caps Lock", InputCode = "capslock", X = 6.4, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_a", Label = "A (Strafe Left)", InputCode = "a", X = 10.8, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_s", Label = "S (Strafe Backward)", InputCode = "s", X = 14.1, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_d", Label = "D (Strafe Right)", InputCode = "d", X = 17.4, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_f", Label = "F (Interaction)", InputCode = "f", X = 20.8, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_g", Label = "G (Missile / CM)", InputCode = "g", X = 24.1, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_h", Label = "H (Flare / Chaff)", InputCode = "h", X = 27.4, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_j", Label = "J", InputCode = "j", X = 30.8, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_k", Label = "K", InputCode = "k", X = 34.1, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_l", Label = "L (Ship Lights)", InputCode = "l", X = 37.4, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_semicolon", Label = "; (Semicolon)", InputCode = "semicolon", X = 40.8, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_apostrophe", Label = "' (Apostrophe)", InputCode = "apostrophe", X = 44.1, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_enter", Label = "Enter (Chat / Confirm)", InputCode = "enter", X = 49.1, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_4", Label = "Num 4", InputCode = "np_4", X = 66.7, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_5", Label = "Num 5", InputCode = "np_5", X = 70.1, Y = 43.1, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_6", Label = "Num 6", InputCode = "np_6", X = 73.5, Y = 43.1, Type = "key", ModuleName = "Keyboard" },

            // ZXCV Row (Y = 52.3%)
            new ControllerHotspot { Id = "km_lshift", Label = "Left Shift (Boost / Afterburner)", InputCode = "lshift", X = 7.3, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_z", Label = "Z (Free Look)", InputCode = "z", X = 12.4, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_x", Label = "X (Space Brake)", InputCode = "x", X = 15.8, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_c", Label = "C (Decoupled Mode)", InputCode = "c", X = 19.1, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_v", Label = "V (Scanning / Quantum)", InputCode = "v", X = 22.4, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_b", Label = "B (Quantum Drive)", InputCode = "b", X = 25.8, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_n", Label = "N (Landing Gear)", InputCode = "n", X = 29.1, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_m", Label = "M (Mining Mode / Toggle)", InputCode = "m", X = 32.4, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_comma", Label = ", (Comma)", InputCode = "comma", X = 35.8, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_period", Label = ". (Period)", InputCode = "period", X = 39.1, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_slash", Label = "/ (Slash)", InputCode = "slash", X = 42.4, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_rshift", Label = "Right Shift", InputCode = "rshift", X = 48.3, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_up", Label = "Up Arrow", InputCode = "up", X = 58.7, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_1", Label = "Num 1", InputCode = "np_1", X = 66.7, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_2", Label = "Num 2", InputCode = "np_2", X = 70.1, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_3", Label = "Num 3", InputCode = "np_3", X = 73.5, Y = 52.3, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_enter", Label = "Num Enter", InputCode = "np_enter", X = 76.9, Y = 56.9, Type = "key", ModuleName = "Keyboard" },

            // Bottom Modifier & Arrow Row (Y = 61.5%)
            new ControllerHotspot { Id = "km_lctrl", Label = "Left Ctrl (Strafe Down / Crouch)", InputCode = "lctrl", X = 6.0, Y = 61.5, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_lalt", Label = "Left Alt (Modifier)", InputCode = "lalt", X = 14.0, Y = 61.5, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_space", Label = "Spacebar (Strafe Up / Jump)", InputCode = "space", X = 26.1, Y = 61.5, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_ralt", Label = "Right Alt", InputCode = "ralt", X = 38.1, Y = 61.5, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_rctrl", Label = "Right Ctrl", InputCode = "rctrl", X = 50.0, Y = 61.5, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_left", Label = "Left Arrow", InputCode = "left", X = 55.3, Y = 61.5, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_down", Label = "Down Arrow", InputCode = "down", X = 58.7, Y = 61.5, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_right", Label = "Right Arrow", InputCode = "right", X = 62.1, Y = 61.5, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_0", Label = "Num 0", InputCode = "np_0", X = 68.4, Y = 61.5, Type = "key", ModuleName = "Keyboard" },
            new ControllerHotspot { Id = "km_np_period", Label = "Num . (Period)", InputCode = "np_period", X = 73.5, Y = 61.5, Type = "key", ModuleName = "Keyboard" },

            // Gaming Mouse
            new ControllerHotspot { Id = "km_mouse1", Label = "Left Click (Primary Fire)", InputCode = "mouse1", X = 89.0, Y = 31.3, Type = "mouse", ModuleName = "Mouse" },
            new ControllerHotspot { Id = "km_mouse2", Label = "Right Click (Secondary Fire / Aim)", InputCode = "mouse2", X = 92.8, Y = 31.3, Type = "mouse", ModuleName = "Mouse" },
            new ControllerHotspot { Id = "km_mouse3", Label = "Middle Click (Lock Missile / Ping)", InputCode = "mouse3", X = 90.9, Y = 32.7, Type = "mouse", ModuleName = "Mouse" },
            new ControllerHotspot { Id = "km_wheel_up", Label = "Wheel UP (Speed Limiter UP)", InputCode = "wheel_up", X = 90.9, Y = 28.1, Type = "mouse", ModuleName = "Mouse" },
            new ControllerHotspot { Id = "km_wheel_down", Label = "Wheel DOWN (Speed Limiter DOWN)", InputCode = "wheel_down", X = 90.9, Y = 37.5, Type = "mouse", ModuleName = "Mouse" },
            new ControllerHotspot { Id = "km_mouse4", Label = "Thumb Button 1 (Back)", InputCode = "mouse4", X = 85.5, Y = 47.3, Type = "mouse", ModuleName = "Mouse" },
            new ControllerHotspot { Id = "km_mouse5", Label = "Thumb Button 2 (Forward)", InputCode = "mouse5", X = 85.7, Y = 56.7, Type = "mouse", ModuleName = "Mouse" }
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
