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
    private readonly IBrowserService _browserService;

    private readonly BlazorWebView _blazorWebView;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly ToolStripMenuItem _autoSyncMenuItem;

    private bool _allowClose = false;
    private bool _startMinimized = false;
    private bool _initialShown = false;
    private bool _hasShownTrayTip = false;

    public MainForm(
        IServiceProvider serviceProvider,
        IAppConfigService configService,
        IActionMapsXmlService xmlService,
        IUsbWatcherService usbWatcherService,
        IBrowserService browserService,
        bool launchMinimized = false) {
        _serviceProvider = serviceProvider;
        _configService = configService;
        _xmlService = xmlService;
        _usbWatcherService = usbWatcherService;
        _browserService = browserService;
        _startMinimized = launchMinimized || _configService.Config.StartMinimizedToTray;
        _hasShownTrayTip = _configService.Config.HasShownTrayMinimizeTip;

        Text = "StickLock - Star Citizen Flight Controller Sync";
        Size = new Size(1100, 780);
        MinimumSize = new Size(880, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(11, 15, 25);

        ApplySavedWindowBounds();
        ResizeEnd += (_, _) => SaveWindowBounds();

        // Generate custom app icon
        Icon = CreateAppIcon();

        // Ensure WebView2 uses a writable user data directory inside ~/.sticklock
        var webViewDataDir = Path.Combine(_configService.BaseDirectory, "WebView2");
        Directory.CreateDirectory(webViewDataDir);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewDataDir);

        // Setup Blazor WebView
        _blazorWebView = new BlazorWebView {
            Dock = DockStyle.Fill,
            HostPage = @"wwwroot\index.html"
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

        var kofiItem = new ToolStripMenuItem("Support on Ko-fi", null, (_, _) => _browserService.OpenUrl("https://ko-fi.com/zombiehaus"));
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        _trayMenu.Items.Add(openItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(syncNowItem);
        _trayMenu.Items.Add(_autoSyncMenuItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(kofiItem);
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
        _notifyIcon.BalloonTipClicked += (_, _) => RestoreWindow();

        // Listen for USB device changes and alert on misalignment if not auto-synced
        _usbWatcherService.UsbDevicesChanged += async (_) => {
            try {
                var alignment = await _xmlService.CheckAlignmentAsync();
                if (alignment.IsMisaligned && !_configService.Config.AutoSyncOnUsbChange) {
                    _notifyIcon.ShowBalloonTip(
                        6000,
                        "StickLock: Joystick Order Mismatch!",
                        "Windows DirectInput order does not match Star Citizen mappings. Click here to fix and back up.",
                        ToolTipIcon.Warning);
                }
            } catch { }
        };

        // Start USB Watcher
        _usbWatcherService.Start();

        if (_configService.Config.AutoSyncOnUsbChange) {
            _ = Task.Run(() => _usbWatcherService.TriggerManualScanAsync());
        }
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
        SaveWindowBounds();

        if (e.CloseReason == CloseReason.UserClosing && !_allowClose) {
            e.Cancel = true;
            Hide();
            if (!_hasShownTrayTip && !_configService.Config.HasShownTrayMinimizeTip) {
                _hasShownTrayTip = true;
                _configService.Config.HasShownTrayMinimizeTip = true;
                _ = _configService.SaveConfigAsync();
                _notifyIcon.ShowBalloonTip(
                    2000,
                    "StickLock",
                    "StickLock is running in the system tray. Joysticks are monitored in the background.",
                    ToolTipIcon.Info);
            }
            return;
        }

        base.OnFormClosing(e);
    }

    private void ApplySavedWindowBounds() {
        var cfg = _configService.Config;
        if (cfg.WindowWidth.HasValue && cfg.WindowHeight.HasValue &&
            cfg.WindowWidth.Value >= MinimumSize.Width && cfg.WindowHeight.Value >= MinimumSize.Height) {
            Size = new Size(cfg.WindowWidth.Value, cfg.WindowHeight.Value);
        }

        if (cfg.WindowX.HasValue && cfg.WindowY.HasValue) {
            var candidateRect = new Rectangle(cfg.WindowX.Value, cfg.WindowY.Value, Size.Width, Size.Height);
            var isOnScreen = Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(candidateRect));
            if (isOnScreen) {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(cfg.WindowX.Value, cfg.WindowY.Value);
            }
        }

        if (cfg.WindowMaximized && !_startMinimized) {
            WindowState = FormWindowState.Maximized;
        }
    }

    private void SaveWindowBounds() {
        try {
            var cfg = _configService.Config;
            var rect = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (rect.Width >= MinimumSize.Width && rect.Height >= MinimumSize.Height) {
                cfg.WindowX = rect.X;
                cfg.WindowY = rect.Y;
                cfg.WindowWidth = rect.Width;
                cfg.WindowHeight = rect.Height;
            }
            if (WindowState != FormWindowState.Minimized) {
                cfg.WindowMaximized = WindowState == FormWindowState.Maximized;
            }
            _ = _configService.SaveConfigAsync();
        } catch { }
    }

    private void RestoreWindow() {
        _startMinimized = false;
        _initialShown = true;
        Show();
        WindowState = _configService.Config.WindowMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
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
        SaveWindowBounds();
        _allowClose = true;
        _usbWatcherService.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }

    private static Icon CreateAppIcon() {
        try {
            var icoPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "icon.ico");
            if (File.Exists(icoPath)) {
                return new Icon(icoPath);
            }
            var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null) {
                return exeIcon;
            }
        } catch { }

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
