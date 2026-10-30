using System;
using System.Threading;
using System.Windows.Forms;

namespace SimpleScreenshot
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "SimpleScreenshot_SingleInstance", out createdNew))
            {
                // Приложение уже запущено — второй экземпляр не нужен.
                if (!createdNew)
                    return;

                // Реальные пиксели на экранах с масштабом 125%, 150% и т. д.
                NativeMethods.EnableDpiAwareness();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
