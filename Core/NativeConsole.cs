using System.Runtime.InteropServices;

namespace MT5TradingBot.Core
{
    internal static class NativeConsole
    {
        private const int AttachParentProcess = -1;

        public static void TryAttachParent()
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                AttachConsole(AttachParentProcess);
            }
            catch
            {
                // Best-effort only; CLI routing still returns the correct exit code.
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);
    }
}
