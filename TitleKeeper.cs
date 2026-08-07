using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace EchoBootstrapper
{
    /// <summary>
    /// Forces the player's window caption to the product name.
    ///
    /// The client is a VMProtect-packed build with no plaintext "Roblox" string left to patch -
    /// the caption is produced by the packed code at runtime, so the only reliable way to change
    /// it is from outside, after the window exists. This renames every top-level window the
    /// player process owns and keeps doing it for as long as the process runs, because the
    /// caption is set again when the 3D game window is created a few seconds into the join.
    /// </summary>
    internal static class TitleKeeper
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowTextW(IntPtr hWnd, string text);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// Runs until the process exits (or the token trips). Non-blocking to start: call from a
        /// background thread. Safe if the process is already gone - it simply returns.
        /// </summary>
        public static void Run(Process player, string title, CancellationToken ct)
        {
            if (player == null) return;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (player.HasExited) return;
                }
                catch
                {
                    return;
                }

                var pid = (uint)player.Id;
                EnumWindows((hWnd, _) =>
                {
                    GetWindowThreadProcessId(hWnd, out var owner);
                    if (owner != pid) return true;
                    if (!IsWindowVisible(hWnd)) return true;

                    // Skip tiny tool/message windows: the caption we care about is on a real
                    // top-level window with a title of its own.
                    var len = GetWindowTextLength(hWnd);
                    if (len == 0) return true;

                    var sb = new StringBuilder(len + 2);
                    GetWindowTextW(hWnd, sb, sb.Capacity);
                    if (sb.ToString() == title) return true;   // already ours, leave it

                    SetWindowTextW(hWnd, title);
                    return true;
                }, IntPtr.Zero);

                // Long enough not to fight the app if it re-titles, short enough that the rename
                // is not visible as a flicker to the player.
                if (ct.WaitHandle.WaitOne(600)) return;
            }
        }
    }
}
