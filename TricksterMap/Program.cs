using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TricksterMap
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--verify-map")
            {
                Environment.ExitCode = MapEditorVerification.Run(args);
                return;
            }
            if (Environment.OSVersion.Version.Major <= 6)
            {
                SetProcessDPIAware();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var main = new MDIParent();
            if (args.Length == 1)
                try { main.OpenMap(args[0]); }
                catch (Exception ex) { MessageBox.Show(main, ex.Message, "無法開啟地圖", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            main.Show();
            Application.Run();
        }
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}
