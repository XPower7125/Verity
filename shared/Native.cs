using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Verity.Shared
{
    internal static class Native
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(int processId);

        /// <summary>
        /// Asks DWM for rounded window corners. Windows 11 honours this;
        /// Windows 10 ignores it and keeps the square frame.
        /// </summary>
        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attribute, ref int value, int size);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        internal static void ApplyRoundedCorners(Form form)
        {
            if (form.IsDisposed) return;
            int pref = DWMWCP_ROUND;
            try { DwmSetWindowAttribute(form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int)); }
            catch { /* pre-11, or no DWM. Harmless. */ }
        }
    }
}