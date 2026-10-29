namespace StickLock.Models;

public enum FlightAxisMode {
    /// <summary>
    /// Space Sim Standard (Pitch on Y, Yaw on X / Left-Right, Roll on Twist / RotZ).
    /// </summary>
    SpaceSim = 0,

    /// <summary>
    /// Atmospheric Flight (Pitch on Y, Roll on X / Left-Right, Yaw on Twist / RotZ).
    /// </summary>
    Atmospheric = 1,

    /// <summary>
    /// Only synchronize axes that are already explicitly defined in the XML.
    /// </summary>
    PreserveOnly = 2
}

