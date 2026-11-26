using System.Windows;
using System.Windows.Forms;

namespace Bloxstrap.UI.Utility
{
    public static class WindowScaling
    {
        public static double ScaleFactor
        {
            get
            {
                Screen? primaryScreen = Screen.PrimaryScreen;
                double logicalWidth = SystemParameters.PrimaryScreenWidth;

                // Screen.PrimaryScreen may be null in headless/remote desktop sessions,
                // and WPF can report zero during early startup. Keep a neutral scale in
                // either case rather than crashing while constructing a WinForms dialog.
                if (primaryScreen is null || logicalWidth <= 0)
                    return 1.0;

                double scale = primaryScreen.Bounds.Width / logicalWidth;
                return Double.IsFinite(scale) && scale > 0 ? scale : 1.0;
            }
        }

        public static int GetScaledNumber(int number)
        {
            return (int)Math.Ceiling(number * ScaleFactor);
        }

        public static System.Drawing.Size GetScaledSize(System.Drawing.Size size)
        {
            return new System.Drawing.Size(GetScaledNumber(size.Width), GetScaledNumber(size.Height));
        }

        public static System.Drawing.Point GetScaledPoint(System.Drawing.Point point)
        {
            return new System.Drawing.Point(GetScaledNumber(point.X), GetScaledNumber(point.Y));
        }

        public static Padding GetScaledPadding(Padding padding)
        {
            return new Padding(GetScaledNumber(padding.Left), GetScaledNumber(padding.Top), GetScaledNumber(padding.Right), GetScaledNumber(padding.Bottom));
        }
    }
}
