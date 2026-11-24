using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Services;

namespace Bloxstrap.UI.Elements.Base
{
    public abstract class WpfUiWindow : UiWindow
    {
        private readonly IThemeService _themeService = new ThemeService();

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowThemeOptions
        {
            public uint Flags;
            public uint Mask;
        }

        // Wpf.Ui currently calls this HRESULT-returning API through a PreserveSig=false
        // declaration. On some Windows installations it can return E_NOTIMPL, which
        // then becomes a fatal NotImplementedException while the window is loading.
        // Probe it with PreserveSig=true before asking Wpf.Ui to apply an optional backdrop.
        [DllImport("uxtheme.dll", EntryPoint = "SetWindowThemeAttribute", ExactSpelling = true, PreserveSig = true)]
        private static extern int TrySetWindowThemeAttribute(
            IntPtr windowHandle,
            uint attribute,
            ref WindowThemeOptions options,
            uint optionsSize
        );

        public WpfUiWindow()
        {
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            const int customThemeIndex = 2; // index for CustomTheme merged dictionary

            _themeService.SetTheme(App.Settings.Prop.Theme.GetFinal() == Enums.Theme.Dark ? ThemeType.Dark : ThemeType.Light);
            _themeService.SetAccent(System.Windows.Media.Color.FromRgb(0xD3, 0x74, 0x53));

            // there doesn't seem to be a way to query the name for merged dictionaries
            var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Style/{Enum.GetName(App.Settings.Prop.Theme.GetFinal())}.xaml") };
            Application.Current.Resources.MergedDictionaries[customThemeIndex] = dict;

#if QA_BUILD
            this.BorderBrush = System.Windows.Media.Brushes.Red;
            this.BorderThickness = new Thickness(4);
#endif
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            if (App.Settings.Prop.WPFSoftwareRender || App.LaunchSettings.NoGPUFlag.Active)
            {
                if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                    hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            // A backdrop is decorative. If the OS does not implement the caption API
            // that Wpf.Ui invokes internally, disable the backdrop and retain an opaque
            // window instead of letting an optional visual effect crash application startup.
            if (WindowBackdropType != BackgroundType.None
                && WindowBackdropType != BackgroundType.Unknown
                && Background.IsSupported(WindowBackdropType))
            {
                IntPtr windowHandle = new WindowInteropHelper(this).Handle;
                var options = new WindowThemeOptions
                {
                    Flags = 0x00000001, // WTNCA_NODRAWCAPTION
                    Mask = 0x0000000F  // WTNCA_VALIDBITS
                };

                try
                {
                    int hresult = TrySetWindowThemeAttribute(
                        windowHandle,
                        1, // WTA_NONCLIENT
                        ref options,
                        (uint)Marshal.SizeOf<WindowThemeOptions>()
                    );

                    if (hresult < 0)
                    {
                        App.Logger.WriteLine(
                            "WpfUiWindow::OnSourceInitialized",
                            $"SetWindowThemeAttribute is unavailable (HRESULT 0x{hresult:X8}); using the standard window background."
                        );
                        WindowBackdropType = BackgroundType.None;
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(
                        "WpfUiWindow::OnSourceInitialized",
                        "Could not probe SetWindowThemeAttribute; using the standard window background."
                    );
                    App.Logger.WriteException("WpfUiWindow::OnSourceInitialized", ex);
                    WindowBackdropType = BackgroundType.None;
                }
            }

            base.OnSourceInitialized(e);
        }
    }
}
