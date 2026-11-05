# Button Mapping & Visual Controller Schematics

StickLock includes a comprehensive controller mapping inspector and visual remapping suite designed to view, edit, and rebind buttons, switches, and axes directly inside your game's configuration files without needing to open the game.

---

## 1. Overview & Features

* **Dual-View Interface**:
  * **List View**: A high-density MFD table displaying all detected controller inputs, action maps, and active bindings with instant search and per-stick filtering.
  * **Visual Schematic View**: Interactive vector schematics of your physical controllers with glowing callout pins for every button, switch, hat, dial, and axis.
* **Modular Multi-Panel Hardware Detection**:
  * Automatically detects modular side expansion panels from DirectInput product strings (e.g., `VKBsim Gladiator EVO R SEM FSM.GA`).
  * Seamlessly mounts the **GNX-SEM** (Side Extension Module) and **GNX-FSM-GA** (Front Side Module - General Aviation) alongside the primary Space Combat Grip and EVO Base.
  * Supports **Omni Throttle** angled adapter configurations for dual-stick (HOSAS) space flight setups.
* **Direct Game Rebinding**:
  * Click **REBIND** on any action or click any visual pin on the stick schematic.
  * Choose your target controller slot and input switch or enter a direct input code.
  * StickLock writes the rebind directly into `actionmaps.xml` (creating automatic timestamped backups before writing) and mirrors changes to your custom mappings folder.
* **Clear Bindings**:
  * Remove unused or accidental bindings with one click.

---

## 2. Supported Controller Hardware Schematics

| Controller Layout | Category | Modules Supported |
| :--- | :--- | :--- |
| **VKB Gladiator EVO Right** | Flight Stick | Space Combat Grip (SCG), EVO Base, GNX-SEM, GNX-FSM-GA, GNX-THQ |
| **VKB Gladiator EVO Omni Throttle Left** | Omni Throttle | Angled SCG Grip, EVO Base, Strafe / Throttle Axes |
| **Generic Flight Stick** | Joystick | Fallback layout for standard DirectInput sticks |

---

## 3. How to Rebind

1. Open StickLock and select the **BUTTON MAPPINGS** tab.
2. Switch between **List View** and **Visual Schematic**.
3. In **Visual Schematic**:
   * Green pins indicate inputs that are actively mapped to a game action.
   * Blue pins indicate unmapped physical controls.
   * Hover over any pin to see the control label and its currently mapped action.
   * Click any pin to open the **Map / Rebind Action** dialog.
4. Select your target controller and new input, then click **SAVE BINDING**.
