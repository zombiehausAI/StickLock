// StickLock JavaScript Game Plugin Sample
// Evaluated using Jint ECMAScript engine with sandboxed sticklock bridge
({
    id: "falcon-bms",
    name: "Falcon BMS",
    description: "Falcon BMS flight stick and throttle slot synchronization.",
    processName: "Falcon BMS.exe",
    iconSvg: "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5",
    supportsFlightAxisMode: false,

    // Returns whether the game is installed on this PC
    isInstalled: function() {
        var baseDir = sticklock.env.expand("%USERPROFILE%\\Falcon BMS");
        return sticklock.fs.dirExists(baseDir);
    },

    // Detects the active bindings/config file path
    detectActiveBindingsPath: function() {
        var configPath = sticklock.env.expand("%USERPROFILE%\\Falcon BMS\\User\\Config\\DeviceSort.txt");
        if (sticklock.fs.fileExists(configPath)) {
            return configPath;
        }
        return null;
    },

    // Evaluates controller alignment
    checkAlignment: function(context) {
        // context contains:
        // context.liveDevices     -> Array of currently connected DirectInput devices
        // context.canonicalSticks -> Array of locked slots in StickLock
        // context.isGameRunning   -> boolean

        if (context.canonicalSticks.length < 2) {
            return {
                status: "Aligned",
                statusMessage: "Falcon BMS ready. Awaiting canonical slot lock."
            };
        }

        return {
            status: context.isGameRunning ? "StarCitizenActive" : "Aligned",
            statusMessage: "Falcon BMS flight controllers aligned with DirectInput."
        };
    },

    // Performs synchronization & reordering
    sync: function(context) {
        // context contains:
        // context.force      -> boolean (true if user clicked Force Sync)
        // context.activePath -> detected config path
        // context.backupPath -> path where StickLock automatically saved a backup

        sticklock.log.info("Synchronizing Falcon BMS controller slots...");

        return {
            success: true,
            message: "Falcon BMS controller order synchronized successfully."
        };
    }
})
