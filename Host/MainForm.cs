using System.Drawing.Drawing2D;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using StickLock.Components;
using StickLock.Services;

namespace StickLock.Host;

public class MainForm : Form {
    private readonly IServiceProvider _serviceProvider;
    private readonly IAppConfigService _configService;
    private readonly IActionMapsXmlService _xmlService;
    private readonly IUsbWatcherService _usbWatcherService;

    private readonly BlazorWebView _blazorWebView;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly ToolStripMenuItem _autoSyncMenuItem;

    private bool _allowClose = false;
    private bool _startMinimized = false;
    private bool _initialShown = false;

    public MainForm(
        IServiceProvider serviceProvider,
        IAppConfigService configService,
        IActionMapsXmlService xmlService,
        IUsbWatcherService usbWatcherService,
        bool launchMinimized = false) {
        _serviceProvider = serviceProvider;
        _configService = configService;
        _xmlService = xmlService;
        _usbWatcherService = usbWatcherService;
        _startMinimized = launchMinimized || _configService.Config.StartMinimizedToTray;

        Text = "StickLock - Star Citizen Flight Controller Sync";
        Size = new Size(1100, 780);
        MinimumSize = new Size(880, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(11, 15, 25);

        // Generate custom app icon
        Icon = CreateAppIcon();

        // Setup Blazor WebView
        _blazorWebView = new BlazorWebView {
            Dock = DockStyle.Fill,
            HostPage = @"wwwroot/index.html"
        };
        _blazorWebView.Services = _serviceProvider;
        _blazorWebView.RootComponents.Add<App>("#app");
        Controls.Add(_blazorWebView);

        // Setup Tray Context Menu
        _trayMenu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Open StickLock", null, (_, _) => RestoreWindow());
        openItem.Font = new Font(openItem.Font, FontStyle.Bold);

        var syncNowItem = new ToolStripMenuItem("Sync Sticks Now", null, async (_, _) => await TriggerTraySyncAsync());

        _autoSyncMenuItem = new ToolStripMenuItem("Auto-Sync Enabled", null, async (sender, _) => {
            var newState = !_configService.Config.AutoSyncOnUsbChange;
            if (sender is ToolStripMenuItem item) {
                item.Checked = newState;
            }
            await _configService.SetAutoSyncAsync(newState);
        }) {
            CheckOnClick = true,
            Checked = _configService.Config.AutoSyncOnUsbChange
        };

        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        _trayMenu.Items.Add(openItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(syncNowItem);
        _trayMenu.Items.Add(_autoSyncMenuItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(exitItem);

        _trayMenu.Opening += (_, _) => {
            _autoSyncMenuItem.Checked = _configService.Config.AutoSyncOnUsbChange;
        };

        // Setup NotifyIcon
        _notifyIcon = new NotifyIcon {
            Icon = Icon,
            ContextMenuStrip = _trayMenu,
            Text = "StickLock - Star Citizen Sync",
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreWindow();

        // Start USB Watcher
        _usbWatcherService.Start();
    }

    protected override void SetVisibleCore(bool value) {
        if (_startMinimized && !_initialShown) {
            value = false;
            if (!IsHandleCreated) {
                CreateHandle();
            }
        }
        base.SetVisibleCore(value);
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
        if (e.CloseReason == CloseReason.UserClosing && !_allowClose) {
            e.Cancel = true;
            Hide();
            _notifyIcon.ShowBalloonTip(
                2000,
                "StickLock",
                "StickLock is running in the system tray. Joysticks are monitored in the background.",
                ToolTipIcon.Info);
            return;
        }

        base.OnFormClosing(e);
    }

    private void RestoreWindow() {
        _startMinimized = false;
        _initialShown = true;
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }

    private async Task TriggerTraySyncAsync() {
        try {
            var result = await _xmlService.SyncAsync();
            var icon = result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning;
            _notifyIcon.ShowBalloonTip(3000, "StickLock Sync", result.Message, icon);
        } catch (Exception ex) {
            _notifyIcon.ShowBalloonTip(3000, "StickLock Sync Error", ex.Message, ToolTipIcon.Error);
        }
    }

    private void ExitApplication() {
        _allowClose = true;
        _usbWatcherService.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }

    private static Icon CreateAppIcon() {
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Dark background circle
        using var bgBrush = new SolidBrush(Color.FromArgb(11, 15, 25));
        g.FillEllipse(bgBrush, 1, 1, 30, 30);

        // Cyan ring
        using var cyanPen = new Pen(Color.FromArgb(0, 229, 255), 2f);
        g.DrawEllipse(cyanPen, 2, 2, 28, 28);

        // Amber crosshair / stick symbol
        using var amberPen = new Pen(Color.FromArgb(255, 171, 0), 2f);
        g.DrawLine(amberPen, 16, 6, 16, 26);
        g.DrawLine(amberPen, 6, 16, 26, 16);

        // Center dot
        using var dotBrush = new SolidBrush(Color.FromArgb(0, 229, 255));
        g.FillEllipse(dotBrush, 13, 13, 6, 6);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _notifyIcon.Dispose();
            _trayMenu.Dispose();
            _blazorWebView.Dispose();
        }
        base.Dispose(disposing);
    }
}
