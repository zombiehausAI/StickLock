namespace StickLock.Models;

public class ControllerHotspot {
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string InputCode { get; set; } = string.Empty; // e.g. "button1", "button33", "x", "y", "hat_up"
    public double X { get; set; } // Percentage (0.0 to 100.0)
    public double Y { get; set; } // Percentage (0.0 to 100.0)
    public string Type { get; set; } = "button"; // "button", "axis", "hat", "encoder", "switch"
    public string ModuleName { get; set; } = "Grip"; // "Grip", "Base", "GNX-SEM", "GNX-FSM-GA"
    public string? Description { get; set; }
}

public class ControllerLayoutDefinition {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "Stick", "Module", "Throttle"
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 600;
    public string? ImageUrl { get; set; }
    public List<ControllerHotspot> Hotspots { get; set; } = new();
    public List<string> AttachedModules { get; set; } = new();
    public List<ControllerLayoutDefinition> SubModules { get; set; } = new();

    public static List<string> DetectModulesFromProductName(string productName) {
        var modules = new List<string>();
        if (string.IsNullOrWhiteSpace(productName)) {
            return modules;
        }

        var upper = productName.ToUpperInvariant();
        if (upper.Contains("SEM")) {
            modules.Add("GNX-SEM");
        }
        if (upper.Contains("FSM") || upper.Contains("FSM.GA") || upper.Contains("FSM-GA")) {
            modules.Add("GNX-FSM-GA");
        }
        if (upper.Contains("THQ")) {
            modules.Add("GNX-THQ");
        }
        if (upper.Contains("OT") || upper.Contains("OMNI")) {
            modules.Add("Omni-Throttle");
        }

        return modules;
    }
}

