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
                if (result == DialogResult.OK) {
                    tcs.SetResult(dialog.FileName);
                } else {
                    tcs.SetResult(null);
                }
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        };

        if (mainForm != null && mainForm.InvokeRequired) {
            mainForm.BeginInvoke(showDialog);
        } else {
            showDialog();
        }

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
                if (result == DialogResult.OK) {
                    tcs.SetResult(dialog.SelectedPath);
                } else {
                    tcs.SetResult(null);
                }
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        };

        if (mainForm != null && mainForm.InvokeRequired) {
            mainForm.BeginInvoke(showDialog);
        } else {
            showDialog();
        }

        return tcs.Task;
    }
}

