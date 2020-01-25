using System;
using System.IO;
using System.Text;

namespace WinService
{
    public static class Extensions
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern uint GetModuleFileName(IntPtr hModule, System.Text.StringBuilder lpFilename, int nSize);

        static readonly int MAX_PATH = 255;

        public static string GetExecutablePath() {
            var sb = new StringBuilder(MAX_PATH);
            GetModuleFileName(IntPtr.Zero, sb, MAX_PATH);
            return sb.ToString();
        }

        public static void Log(string txt) {
            string dir = Path.GetDirectoryName(GetExecutablePath());
            txt = DateTime.Now.ToString() + ": " + txt + "\r\n";
            string file = Path.Combine(dir, "Log.txt");
            CheckFileReset(file);
            try {
                File.AppendAllText(file, txt);
            }
            catch (Exception) { }
        }

        private static void CheckFileReset(string file) {
            try {
                if (new FileInfo(file).Length > 10 * 1024 * 1024) {
                    File.Delete(file);
                }
            }
            catch (Exception) { }
        }
    }
}
