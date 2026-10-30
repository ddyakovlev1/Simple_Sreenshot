using Microsoft.Win32;
using System.Windows.Forms;

namespace SimpleScreenshot
{
    /// <summary>
    /// Автозапуск через реестр текущего пользователя (права администратора не нужны).
    /// </summary>
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SimpleScreenshot";

        /// <summary>Хочет ли пользователь автозапуск. По умолчанию — да.</summary>
        public static bool Wanted
        {
            get { return AppSettings.ReadBool("Autostart", true); }
        }

        /// <summary>
        /// Включает или выключает автозапуск. При включении путь к exe обновляется,
        /// так что после переноса файла автозапуск продолжит работать.
        /// </summary>
        public static void Apply(bool enabled)
        {
            AppSettings.WriteBool("Autostart", enabled);

            using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                    run.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else
                    run.DeleteValue(ValueName, false);
            }
        }
    }
}
