namespace StickLock.Services;

public class FileDialogService : IFileDialogService {
    public Task<string?> PickActionMapsXmlFileAsync(string? initialDirectory = null) {
        var tcs = new TaskCompletionSource<string?>();
        var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;

        Action showDialog = () => {
            try {
                using var dialog = new OpenFileDialog {
                    Title = "Select Star Citizen actionmaps.xml",
                    Filter = "Star Citizen ActionMaps (actionmaps.xml)|actionmaps.xml|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory)) {
                    dialog.InitialDirectory = initialDirectory;
                }

                var result = dialog.ShowDialog(mainForm);
                tcs.SetResult(result == DialogResult.OK ? dialog.FileName : null);
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        };

        Dispatch(mainForm, showDialog);
        return tcs.Task;
    }

    public Task<string?> PickStarCitizenFolderAsync(string? initialDirectory = null) {
        var tcs = new TaskCompletionSource<string?>();
        var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;

        Action showDialog = () => {
            try {
                using var dialog = new FolderBrowserDialog {
                    Description = "Select Star Citizen Directory (containing LIVE or PTU)",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false
                };

                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory)) {
                    dialog.InitialDirectory = initialDirectory;
                }

                var result = dialog.ShowDialog(mainForm);
                tcs.SetResult(result == DialogResult.OK ? dialog.SelectedPath : null);
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        };

        Dispatch(mainForm, showDialog);
        return tcs.Task;
    }

    public Task<string?> SaveActionMapsXmlFileAsync(string defaultFileName, string? initialDirectory = null) {
        var tcs = new TaskCompletionSource<string?>();
        var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;

        Action showDialog = () => {
            try {
                using var dialog = new SaveFileDialog {
                    Title = "Export Star Citizen Control Profile XML",
                    Filter = "Star Citizen Profile (*.xml)|*.xml|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                    DefaultExt = "xml",
                    FileName = defaultFileName,
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory)) {
                    dialog.InitialDirectory = initialDirectory;
                }

                var result = dialog.ShowDialog(mainForm);
                tcs.SetResult(result == DialogResult.OK ? dialog.FileName : null);
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        };

        Dispatch(mainForm, showDialog);
        return tcs.Task;
    }

    public Task<string?> PickXmlFileToImportAsync(string? initialDirectory = null) {
        var tcs = new TaskCompletionSource<string?>();
        var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;

        Action showDialog = () => {
            try {
                using var dialog = new OpenFileDialog {
                    Title = "Select Star Citizen Control Profile XML to Import",
                    Filter = "Star Citizen Profile (*.xml)|*.xml|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory)) {
                    dialog.InitialDirectory = initialDirectory;
                }

                var result = dialog.ShowDialog(mainForm);
                tcs.SetResult(result == DialogResult.OK ? dialog.FileName : null);
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        };

        Dispatch(mainForm, showDialog);
        return tcs.Task;
    }

    public Task<string?> SaveJsonConfigFileAsync(string defaultFileName, string? initialDirectory = null) {
        var tcs = new TaskCompletionSource<string?>();
        var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;

        Action showDialog = () => {
            try {
                using var dialog = new SaveFileDialog {
                    Title = "Export StickLock Settings JSON",
                    Filter = "StickLock Settings (*.json)|*.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = defaultFileName,
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory)) {
                    dialog.InitialDirectory = initialDirectory;
                }

                var result = dialog.ShowDialog(mainForm);
                tcs.SetResult(result == DialogResult.OK ? dialog.FileName : null);
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        };

        Dispatch(mainForm, showDialog);
        return tcs.Task;
    }

    public Task<string?> PickJsonConfigFileAsync(string? initialDirectory = null) {
        var tcs = new TaskCompletionSource<string?>();
        var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;

        Action showDialog = () => {
            try {
                using var dialog = new OpenFileDialog {
                    Title = "Select StickLock Settings JSON to Import",
                    Filter = "StickLock Settings (*.json)|*.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory)) {
                    dialog.InitialDirectory = initialDirectory;
                }

                var result = dialog.ShowDialog(mainForm);
                tcs.SetResult(result == DialogResult.OK ? dialog.FileName : null);
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        };

        Dispatch(mainForm, showDialog);
        return tcs.Task;
    }

    private static void Dispatch(Form? form, Action action) {
        if (form != null && form.InvokeRequired) {
            form.BeginInvoke(action);
        } else {
            action();
        }
    }
}
