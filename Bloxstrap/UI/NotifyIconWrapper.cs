using Bloxstrap.Integrations;
using Bloxstrap.UI.Elements.About;
using Bloxstrap.UI.Elements.ContextMenu;

namespace Bloxstrap.UI
{
    public class NotifyIconWrapper : IDisposable
    {
        private volatile bool _disposing;

        private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
        private readonly MenuContainer _menuContainer;
        private readonly Watcher _watcher;

        private ActivityWatcher? _activityWatcher => _watcher.ActivityWatcher;

        private EventHandler? _alertClickHandler;

        public NotifyIconWrapper(Watcher watcher)
        {
            App.Logger.WriteLine("NotifyIconWrapper::NotifyIconWrapper", "Initializing notification area icon");

            _watcher = watcher;

            _notifyIcon = new(new System.ComponentModel.Container())
            {
                Icon = Properties.Resources.IconBloxstrap,
                Text = App.ProjectName,
                Visible = true
            };

            _notifyIcon.MouseClick += MouseClickEventHandler;

            _menuContainer = new(_watcher);
            _menuContainer.Show();

            if (_activityWatcher is not null && App.Settings.Prop.ShowServerDetails)
                _activityWatcher.OnGameJoin += OnGameJoin;
        }

        #region Context menu
        public void MouseClickEventHandler(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_disposing || e.Button != System.Windows.Forms.MouseButtons.Right)
                return;

            _menuContainer.Activate();
            _menuContainer.ContextMenu.IsOpen = true;
        }
        #endregion

        #region Activity handlers
        public async void OnGameJoin(object? sender, EventArgs e)
        {
            ActivityWatcher? activityWatcher = _activityWatcher;
            if (activityWatcher is null || _disposing)
                return;

            // Capture the activity before awaiting network I/O; the log reader may
            // update Data while a location lookup is in flight.
            var activity = activityWatcher.Data;

            string? serverLocation;
            try
            {
                serverLocation = await activity.QueryServerLocation();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("NotifyIconWrapper::OnGameJoin", "Could not resolve the server location.");
                App.Logger.WriteException("NotifyIconWrapper::OnGameJoin", ex);
                serverLocation = null;
            }

            if (_disposing)
                return;

            if (String.IsNullOrEmpty(serverLocation))
            {
                ShowAlert(
                    String.Format(Strings.Dialog_Connectivity_UnableToConnect, "ipinfo.io"),
                    Strings.ActivityWatcher_LocationQueryFailed,
                    10,
                    null
                );

                return;
            }

            string title = activity.ServerType switch
            {
                ServerType.Public => Strings.ContextMenu_ServerInformation_Notification_Title_Public,
                ServerType.Private => Strings.ContextMenu_ServerInformation_Notification_Title_Private,
                ServerType.Reserved => Strings.ContextMenu_ServerInformation_Notification_Title_Reserved,
                _ => ""
            };

            ShowAlert(
                title,
                String.Format(Strings.ContextMenu_ServerInformation_Notification_Text, serverLocation),
                10,
                (_, _) => _menuContainer.ShowServerInformationWindow()
            );
        }
        #endregion

        public void ShowAlert(string caption, string message, int duration, EventHandler? clickHandler)
        {
            if (_disposing || duration <= 0)
                return;

            // NotifyIcon.ShowBalloonTip expects milliseconds. The user-facing callers
            // specify seconds, so clamp and convert once for both display and cleanup.
            int durationSeconds = Math.Clamp(duration, 1, 30);

            if (!_menuContainer.Dispatcher.CheckAccess())
            {
                try
                {
                    _menuContainer.Dispatcher.BeginInvoke(new Action(
                        () => ShowAlert(caption, message, durationSeconds, clickHandler)
                    ));
                }
                catch (InvalidOperationException)
                {
                    // The UI dispatcher is already shutting down.
                }

                return;
            }

            if (_disposing)
                return;

            string id = Guid.NewGuid().ToString("N")[..8];
            string logIdent = $"NotifyIconWrapper::ShowAlert.{id}";

            App.Logger.WriteLine(logIdent, $"Showing alert for {durationSeconds} seconds (clickHandler={clickHandler is not null})");
            App.Logger.WriteLine(logIdent, $"{caption}: {message.Replace("\n", "\\n")}");

            _notifyIcon.BalloonTipTitle = caption;
            _notifyIcon.BalloonTipText = message;

            if (_alertClickHandler is not null)
            {
                App.Logger.WriteLine(logIdent, "Previous alert still present, erasing click handler");
                _notifyIcon.BalloonTipClicked -= _alertClickHandler;
            }

            _alertClickHandler = clickHandler;
            _notifyIcon.BalloonTipClicked += clickHandler;

            _notifyIcon.ShowBalloonTip(durationSeconds * 1000);

            _ = ClearAlertClickHandlerAsync(clickHandler, logIdent, durationSeconds);
        }

        private async Task ClearAlertClickHandlerAsync(EventHandler? clickHandler, string logIdent, int durationSeconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(durationSeconds)).ConfigureAwait(false);

            try
            {
                _menuContainer.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_disposing)
                        return;

                    _notifyIcon.BalloonTipClicked -= clickHandler;

                    App.Logger.WriteLine(logIdent, "Duration over, erasing current click handler");

                    if (_alertClickHandler == clickHandler)
                        _alertClickHandler = null;
                    else
                        App.Logger.WriteLine(logIdent, "Click handler has been overridden by another alert");
                }));
            }
            catch (InvalidOperationException)
            {
                // The UI dispatcher was shut down while the balloon timeout was pending.
            }
            catch (ObjectDisposedException)
            {
                // The notification icon was disposed while the timeout was pending.
            }
        }

        public void Dispose()
        {
            if (_disposing)
                return;

            _disposing = true;

            App.Logger.WriteLine("NotifyIconWrapper::Dispose", "Disposing NotifyIcon");

            if (_activityWatcher is not null)
                _activityWatcher.OnGameJoin -= OnGameJoin;

            _menuContainer.Dispatcher.Invoke(_menuContainer.Close);
            _notifyIcon.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
