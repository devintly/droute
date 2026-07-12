using Droute.Installer.Forms;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Droute.Installer
{
    internal static class Program
    {
        private static bool createdNew;
        private static Mutex mtx;

        [STAThread]
        static void Main(string[] args)
        {
            mtx = new Mutex(true, "snowluwu.droute", out createdNew);

            // droute is already running
            if (!createdNew)
                return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FrmMain());
        }
    }
}
