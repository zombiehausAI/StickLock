using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using StickLock.Host;
using StickLock.Services;

namespace StickLock;

static class Program {
    [STAThread]
    static async Task Main(string[] args) {
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        var configService = serviceProvider.GetRequiredService<IAppConfigService>();
        await configService.LoadConfigAsync();

        var launchMinimized = args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(a, "-m", StringComparison.OrdinalIgnoreCase));

        var mainForm = new MainForm(
            serviceProvider,
            configService,
            serviceProvider.GetRequiredService<IActionMapsXmlService>(),
            serviceProvider.GetRequiredService<IUsbWatcherService>(),
            launchMinimized);

        Application.Run(mainForm);
    }

    private static void ConfigureServices(IServiceCollection services) {
        services.AddWindowsFormsBlazorWebView();
        services.AddMudServices();

        // Core singletons
        services.AddSingleton<IAppConfigService, AppConfigService>();
        services.AddSingleton<IDirectInputService, DirectInputService>();
        services.AddSingleton<IStarCitizenDetector, StarCitizenDetector>();
        services.AddSingleton<IActivityLogService, ActivityLogService>();
        services.AddSingleton<IActionMapsXmlService, ActionMapsXmlService>();
        services.AddSingleton<IUsbWatcherService, UsbWatcherService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
    }
}
