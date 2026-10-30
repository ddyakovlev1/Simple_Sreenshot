using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace SimpleScreenshot
{
    /// <summary>Файлы, зашитые внутрь exe: значок трея и звук.</summary>
    internal static class Resources
    {
        public static Stream Open(string name)
        {
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (s == null)
                throw new InvalidOperationException("Нет встроенного файла " + name);
            return s;
        }

        /// <summary>Значок нужного размера из tray.ico.</summary>
        public static Icon TrayIcon(Size size)
        {
            using (Stream s = Open("tray.ico"))
                return new Icon(s, size);
        }

        /// <summary>Значок со всеми размерами — для окна настроек.</summary>
        public static Icon AppIcon()
        {
            using (Stream s = Open("app.ico"))
                return new Icon(s);
        }
    }
}
