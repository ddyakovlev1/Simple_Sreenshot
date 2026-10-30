using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace SimpleScreenshot
{
    /// <summary>
    /// Фоновое приложение: значок в трее, перехват PrtSc, сохранение снимков.
    /// </summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly Icon _icon;
        private readonly Control _invoker; // чтобы выполнять действия в UI-потоке
        private readonly ToolStripMenuItem _autoStartItem;
        private KeyboardHook _hook;
        private CaptureOverlay _overlay;

        public TrayApp()
        {
            _invoker = new Control();
            _invoker.CreateControl();

            _icon = CreateTrayIcon();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Сделать скриншот (PrtSc)", null, delegate { StartCaptureDelayed(); });
            menu.Items.Add("Открыть рабочий стол", null, delegate { OpenDesktop(); });
            menu.Items.Add(new ToolStripSeparator());
            _autoStartItem = new ToolStripMenuItem("Запускать вместе с Windows");
            _autoStartItem.Click += delegate { ToggleAutoStart(); };
            menu.Items.Add(_autoStartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Выход", null, delegate { ExitThread(); });

            _tray = new NotifyIcon
            {
                Icon = _icon,
                Text = "Simple Screenshot — нажми PrtSc",
                ContextMenuStrip = menu,
                Visible = true
            };
            _tray.MouseDoubleClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    StartCaptureDelayed();
            };

            try
            {
                AutoStart.Apply(AutoStart.Wanted);
            }
            catch (Exception)
            {
                // Нет доступа к реестру — приложение всё равно работает.
            }
            _autoStartItem.Checked = AutoStart.Wanted;

            try
            {
                _hook = new KeyboardHook(OnKey);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Simple Screenshot", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- Клавиатура ----------

        // Вызывается из глобального перехвата. Должен работать быстро: тяжёлое — через BeginInvoke.
        private bool OnKey(int vk, bool isDown)
        {
            if (vk == NativeMethods.VK_SNAPSHOT)
            {
                // Alt+PrtSc и Win+PrtSc оставляем Windows.
                if (NativeMethods.IsKeyDown(NativeMethods.VK_MENU) ||
                    NativeMethods.IsKeyDown(NativeMethods.VK_LWIN) ||
                    NativeMethods.IsKeyDown(NativeMethods.VK_RWIN))
                    return false;

                if (isDown && _overlay == null)
                    _invoker.BeginInvoke(new Action(StartCapture));
                return true; // PrtSc дальше не пускаем
            }

            if (_overlay == null || !isDown)
                return false;

            if (vk == NativeMethods.VK_SHIFT || vk == NativeMethods.VK_LSHIFT || vk == NativeMethods.VK_RSHIFT)
            {
                CaptureOverlay overlay = _overlay;
                _invoker.BeginInvoke(new Action(overlay.CaptureFullScreen));
                return false;
            }

            if (vk == NativeMethods.VK_ESCAPE)
            {
                CaptureOverlay overlay = _overlay;
                _invoker.BeginInvoke(new Action(overlay.Cancel));
                return true;
            }

            return false;
        }

        // ---------- Снимок ----------

        // Из меню трея: ждём, пока меню исчезнет с экрана.
        private void StartCaptureDelayed()
        {
            var timer = new Timer { Interval = 250 };
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                StartCapture();
            };
            timer.Start();
        }

        private void StartCapture()
        {
            if (_overlay != null)
                return;

            Rectangle virt = SystemInformation.VirtualScreen;
            Bitmap shot;
            try
            {
                shot = new Bitmap(virt.Width, virt.Height, PixelFormat.Format32bppRgb);
                using (var g = Graphics.FromImage(shot))
                    g.CopyFromScreen(virt.Left, virt.Top, 0, 0, virt.Size, CopyPixelOperation.SourceCopy);
            }
            catch (Exception ex)
            {
                ShowError("Не удалось сделать снимок экрана: " + ex.Message);
                return;
            }

            _overlay = new CaptureOverlay(shot, virt);
            _overlay.FormClosed += delegate
            {
                // Окно, открытое через Show(), освобождается само при закрытии.
                Bitmap result = _overlay.Result;
                _overlay = null;
                shot.Dispose();

                if (result != null)
                {
                    using (result)
                        SaveToDesktop(result);
                }
            };
            _overlay.Show();
        }

        private void SaveToDesktop(Bitmap image)
        {
            try
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string name = "Скриншот " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
                string path = Path.Combine(dir, name + ".png");
                for (int i = 2; File.Exists(path); i++)
                    path = Path.Combine(dir, name + " (" + i + ").png");

                image.Save(path, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                ShowError("Не удалось сохранить скриншот: " + ex.Message);
            }
        }

        // ---------- Меню ----------

        private void ToggleAutoStart()
        {
            bool enable = !_autoStartItem.Checked;
            try
            {
                AutoStart.Apply(enable);
                _autoStartItem.Checked = enable;
            }
            catch (Exception ex)
            {
                ShowError("Не удалось изменить автозапуск: " + ex.Message);
            }
        }

        private static void OpenDesktop()
        {
            try
            {
                Process.Start("explorer.exe",
                    "\"" + Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\"");
            }
            catch (Exception)
            {
            }
        }

        private void ShowError(string message)
        {
            _tray.ShowBalloonTip(5000, "Simple Screenshot", message, ToolTipIcon.Error);
        }

        // ---------- Значок ----------

        // Рисуем значок кодом: синий квадрат с белыми уголками рамки выделения.
        private static Icon CreateTrayIcon()
        {
            using (var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (var path = RoundedRect(new Rectangle(1, 1, 30, 30), 7))
                    using (var bg = new SolidBrush(Color.FromArgb(0, 120, 215)))
                        g.FillPath(bg, path);

                    using (var pen = new Pen(Color.White, 3f))
                    {
                        pen.StartCap = LineCap.Square;
                        pen.EndCap = LineCap.Square;
                        const int a = 8, b = 23, l = 6;
                        g.DrawLines(pen, new[] { new Point(a, a + l), new Point(a, a), new Point(a + l, a) });
                        g.DrawLines(pen, new[] { new Point(b - l, a), new Point(b, a), new Point(b, a + l) });
                        g.DrawLines(pen, new[] { new Point(a, b - l), new Point(a, b), new Point(a + l, b) });
                        g.DrawLines(pen, new[] { new Point(b - l, b), new Point(b, b), new Point(b, b - l) });
                    }
                }

                // Значок создаётся один раз за всё время работы, освобождать его не нужно.
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ---------- Выход ----------

        protected override void ExitThreadCore()
        {
            if (_hook != null)
            {
                _hook.Dispose();
                _hook = null;
            }
            if (_overlay != null)
                _overlay.Cancel();

            _tray.Visible = false;
            _tray.Dispose();
            _invoker.Dispose();
            base.ExitThreadCore();
        }
    }
}
