using System;
using Microsoft.Win32;

namespace SimpleScreenshot
{
    /// <summary>
    /// Настройки в реестре текущего пользователя: HKCU\Software\SimpleScreenshot.
    /// </summary>
    internal static class AppSettings
    {
        private const string Key = @"Software\SimpleScreenshot";

        /// <summary>Папка для скриншотов по умолчанию — рабочий стол.</summary>
        public static string DefaultFolder
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); }
        }

        public static string SaveFolder
        {
            get
            {
                string value = ReadString("SaveFolder");
                return string.IsNullOrWhiteSpace(value) ? DefaultFolder : value;
            }
            set { WriteString("SaveFolder", value); }
        }

        public static bool SoundEnabled
        {
            get { return ReadBool("Sound", true); }
            set { WriteBool("Sound", value); }
        }

        // ---------- Чтение и запись ----------

        private static string ReadString(string name)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Key))
                    return key == null ? null : key.GetValue(name) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteString(string name, string value)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(Key))
                key.SetValue(name, value ?? "", RegistryValueKind.String);
        }

        internal static bool ReadBool(string name, bool defaultValue)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Key))
                {
                    object value = key == null ? null : key.GetValue(name);
                    return value is int ? (int)value != 0 : defaultValue;
                }
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        internal static void WriteBool(string name, bool value)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(Key))
                key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
