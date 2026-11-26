using System.Windows;
using System.Windows.Input;
using Bloxstrap.Integrations;
using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.ContextMenu
{
    internal class ServerInformationViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ActivityWatcher _activityWatcher;

        public string InstanceId => _activityWatcher.Data.JobId;

        public string ServerType => _activityWatcher.Data.ServerType.ToTranslatedString();

        public string ServerLocation { get; private set; } = Strings.Common_Loading;

        public Visibility ServerLocationVisibility => App.Settings.Prop.ShowServerDetails ? Visibility.Visible : Visibility.Collapsed;

        public ICommand CopyInstanceIdCommand => new RelayCommand(CopyInstanceId);

        public ServerInformationViewModel(Watcher watcher)
        {
            _activityWatcher = watcher.ActivityWatcher
                ?? throw new InvalidOperationException("Server information requires an active activity watcher.");

            if (ServerLocationVisibility == Visibility.Visible)
                _ = QueryServerLocationAsync();
        }

        private async Task QueryServerLocationAsync()
        {
            try
            {
                string? location = await _activityWatcher.Data.QueryServerLocation();
                ServerLocation = String.IsNullOrEmpty(location)
                    ? Strings.Common_NotAvailable
                    : location;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ServerInformationViewModel::QueryServerLocationAsync", "Failed to query server location.");
                App.Logger.WriteException("ServerInformationViewModel::QueryServerLocationAsync", ex);
                ServerLocation = Strings.Common_NotAvailable;
            }

            OnPropertyChanged(nameof(ServerLocation));
        }

        private void CopyInstanceId() => Clipboard.SetDataObject(InstanceId);
    }
}
