using System;
using System.Drawing;
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
        private readonly Icon _icon;     // значок в трее
        private readonly Icon _appIcon;  // значок окна настроек
        private readonly Control _invoker; // чтобы выполнять действия в UI-потоке
        private readonly ShutterSound _shutter;
        private KeyboardHook _hook;
        private CaptureOverlay _overlay;
        private SettingsForm _settings;

        public TrayApp()
        {
            _invoker = new Control();
            _invoker.CreateControl();

            _icon = Resources.TrayIcon(SystemInformation.SmallIconSize);
            _appIcon = Resources.AppIcon();
            _shutter = new ShutterSound();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Настройки", null, delegate { OpenSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Закрыть", null, delegate { ExitThread(); });

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
                    OpenSettings();
            };

            try
            {
                AutoStart.Apply(AutoStart.Wanted);
            }
            catch (Exception)
            {
                // Нет доступа к реестру — приложение всё равно работает.
            }

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
                    if (AppSettings.SoundEnabled)
                        _shutter.Play();

                    using (result)
                        Save(result);
                }
            };
            _overlay.Show();
        }

        private void Save(Bitmap image)
        {
            try
            {
                string dir = AppSettings.SaveFolder;
                Directory.CreateDirectory(dir);
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

        private void OpenSettings()
        {
            // Окно уже открыто — просто выводим его наверх.
            if (_settings != null)
            {
                if (_settings.WindowState == FormWindowState.Minimized)
                    _settings.WindowState = FormWindowState.Normal;
                _settings.Activate();
                return;
            }

            _settings = new SettingsForm(_appIcon);
            _settings.FormClosed += delegate { _settings = null; };
            _settings.Show();
            _settings.Activate();
        }

        private void ShowError(string message)
        {
            _tray.ShowBalloonTip(5000, "Simple Screenshot", message, ToolTipIcon.Error);
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
            if (_settings != null)
                _settings.Close();

            _shutter.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _invoker.Dispose();
            base.ExitThreadCore();
        }
    }
}
