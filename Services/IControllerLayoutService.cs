using StickLock.Models;

namespace StickLock.Services;

public interface IControllerLayoutService {
    ControllerLayoutDefinition GetLayoutForDevice(string productName);
    IReadOnlyList<ControllerLayoutDefinition> GetAllLayouts();
}
