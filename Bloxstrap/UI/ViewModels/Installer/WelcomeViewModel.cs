namespace Bloxstrap.UI.ViewModels.Installer
{
    public class WelcomeViewModel : NotifyPropertyChangedViewModel
    {
        // formatting is done here instead of in xaml, it's just a bit easier
        public string MainText => String.Format(
            Strings.Installer_Welcome_MainText,
            $"[ClaudeTrap source repository](https://github.com/{App.ProjectRepository})",
            $"[ClaudeTrap releases]({App.ProjectDownloadLink})"
        );

        public string VersionNotice { get; private set; } = "";

        public bool CanContinue { get; set; } = false;

        public event EventHandler? CanContinueEvent;

        // called by codebehind on page load
        public void DoChecks()
        {
            // Checking GitHub must not block the installer when the API is slow or unavailable.
            CanContinue = true;
            OnPropertyChanged(nameof(CanContinue));
            CanContinueEvent?.Invoke(this, EventArgs.Empty);

            _ = CheckForUpdateAsync();
        }

        private async Task CheckForUpdateAsync()
        {
            const string LOG_IDENT = "WelcomeViewModel::CheckForUpdateAsync";

            try
            {
                var releaseInfo = await App.GetLatestRelease();

                if (releaseInfo is null || String.IsNullOrWhiteSpace(releaseInfo.TagName))
                    return;

                string releaseTag = releaseInfo.TagName.Trim();
                string releaseVersion = releaseTag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? releaseTag[1..]
                    : releaseTag;

                if (!Version.TryParse(releaseVersion, out _))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Ignoring invalid release tag '{releaseTag}'.");
                    return;
                }

                if (Utilities.CompareVersions(App.Version, releaseTag) == VersionComparison.LessThan)
                {
                    VersionNotice = String.Format(
                        Strings.Installer_Welcome_UpdateNotice,
                        App.Version,
                        releaseVersion
                    );
                    OnPropertyChanged(nameof(VersionNotice));
                }
            }
            catch (Exception ex)
            {
                // An update notice is optional; a failed check must never break installation.
                App.Logger.WriteLine(LOG_IDENT, "Failed to check for a newer ClaudeTrap release.");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
    }
}
