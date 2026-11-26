using System.Windows;
using System.Windows.Input;
using Bloxstrap.UI.Elements.About;
using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class MainWindowViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand OpenAboutCommand => new RelayCommand(OpenAbout);
        
        public ICommand SaveSettingsCommand => new RelayCommand(SaveSettings);
        
        public ICommand CloseWindowCommand => new RelayCommand(CloseWindow);

        public ICommand CheckForUpdatesCommand => new AsyncRelayCommand(() => CheckForUpdatesAsync(manual: true));

        private bool _isCheckingForUpdates;

        public EventHandler? RequestSaveNoticeEvent;
        
        public EventHandler? RequestCloseWindowEvent;

        public bool TestModeEnabled
        {
            get => App.LaunchSettings.TestModeFlag.Active;
            set
            {
                if (value)
                {
                    var result = Frontend.ShowMessageBox(Strings.Menu_TestMode_Prompt, MessageBoxImage.Information, MessageBoxButton.YesNo);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                App.LaunchSettings.TestModeFlag.Active = value;
            }
        }

        private void OpenAbout() => new MainWindow().ShowDialog();

        private void CloseWindow() => RequestCloseWindowEvent?.Invoke(this, EventArgs.Empty);

        public async Task CheckForUpdatesAsync(bool manual)
        {
            const string LOG_IDENT = "MainWindowViewModel::CheckForUpdatesAsync";

            if (_isCheckingForUpdates)
                return;

            _isCheckingForUpdates = true;
            try
            {
                App.Logger.WriteLine(LOG_IDENT, "Checking the latest published ClaudeTrap release");
                UpdateCheckResult check = await UpdateManager.CheckLatestReleaseAsync();

                if (check.NoPublishedRelease)
                {
                    if (manual)
                        Frontend.ShowMessageBox(Strings.Update_NoPublishedRelease, MessageBoxImage.Information);
                    return;
                }

                GithubRelease release = check.Release
                    ?? throw new InvalidDataException("GitHub returned no release data.");

                if (!UpdateManager.IsNewerRelease(release))
                {
                    if (manual)
                        Frontend.ShowMessageBox(String.Format(Strings.Update_UpToDate, App.Version), MessageBoxImage.Information);
                    return;
                }

                if (!UpdateManager.HasRequiredAssets(release))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Release {release.TagName} is missing the expected update assets.");
                    Frontend.ShowMessageBox(Strings.Update_InvalidRelease, MessageBoxImage.Warning);
                    return;
                }

                string releaseNotes = String.IsNullOrWhiteSpace(release.Body)
                    ? "No release notes were provided."
                    : release.Body.Trim();
                if (releaseNotes.Length > 1200)
                    releaseNotes = releaseNotes[..1200] + "…";

                MessageBoxResult result = Frontend.ShowMessageBox(
                    String.Format(Strings.Update_Available, release.TagName, releaseNotes),
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo
                );
                if (result != MessageBoxResult.Yes)
                    return;

                try
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Downloading release {release.TagName}");
                    string updatePath = await UpdateManager.DownloadReleaseAsync(release);
                    SaveSettings();

                    Process? updateProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = updatePath,
                        Arguments = "-upgrade -settings",
                        WorkingDirectory = Path.GetDirectoryName(updatePath)!,
                        UseShellExecute = true
                    });
                    if (updateProcess is null)
                        throw new InvalidOperationException("Windows did not start the update process.");

                    App.Logger.WriteLine(LOG_IDENT, "Verified update started; closing the current settings process.");
                    RequestCloseWindowEvent?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to download, verify, or start the update.");
                    App.Logger.WriteException(LOG_IDENT, ex);
                    Frontend.ShowMessageBox(Strings.Update_DownloadFailed, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to check for GitHub updates.");
                App.Logger.WriteException(LOG_IDENT, ex);
                if (manual)
                    Frontend.ShowMessageBox(Strings.Update_CheckFailed, MessageBoxImage.Warning);
            }
            finally
            {
                _isCheckingForUpdates = false;
            }
        }

        private void SaveSettings()
        {
            const string LOG_IDENT = "MainWindowViewModel::SaveSettings";

            App.Settings.Save();
            App.State.Save();
            App.FastFlags.Save();

            foreach (var pair in App.PendingSettingTasks)
            {
                var task = pair.Value;

                if (task.Changed)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Executing pending task '{task}'");
                    task.Execute();
                }
            }

            App.PendingSettingTasks.Clear();

            RequestSaveNoticeEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
