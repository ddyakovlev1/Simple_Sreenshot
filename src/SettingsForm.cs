using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SimpleScreenshot
{
    /// <summary>Окно «Настройки».</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly CheckBox _autoStart;
        private readonly TextBox _folder;
        private readonly CheckBox _sound;

        public SettingsForm(Icon icon)
        {
            Text = "Simple Screenshot — настройки";
            Icon = icon;
            Font = new Font("Segoe UI", 9f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(12, 12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _autoStart = new CheckBox
            {
                Text = "Запускать при запуске Windows",
                AutoSize = true,
                Checked = AutoStart.Wanted,
                Margin = new Padding(3, 3, 3, 12)
            };
            layout.Controls.Add(_autoStart, 0, 0);
            layout.SetColumnSpan(_autoStart, 2);

            var folderLabel = new Label
            {
                Text = "Папка для сохранения скриншотов:",
                AutoSize = true,
                Margin = new Padding(3, 3, 3, 3)
            };
            layout.Controls.Add(folderLabel, 0, 1);
            layout.SetColumnSpan(folderLabel, 2);

            _folder = new TextBox
            {
                Text = AppSettings.SaveFolder,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 3, 3, 12)
            };
            layout.Controls.Add(_folder, 0, 2);

            var browse = new Button
            {
                Text = "Обзор…",
                AutoSize = true,
                Margin = new Padding(3, 2, 3, 12)
            };
            browse.Click += delegate { ChooseFolder(); };
            layout.Controls.Add(browse, 1, 2);

            _sound = new CheckBox
            {
                Text = "Включить звук скриншота",
                AutoSize = true,
                Checked = AppSettings.SoundEnabled,
                Margin = new Padding(3, 3, 3, 16)
            };
            layout.Controls.Add(_sound, 0, 3);
            layout.SetColumnSpan(_sound, 2);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            // Окно открыто через Show(), а не ShowDialog(): DialogResult его не закрывает, закрываем сами.
            var cancel = new Button { Text = "Закрыть", AutoSize = true, MinimumSize = new Size(90, 0) };
            cancel.Click += delegate { Close(); };
            var save = new Button { Text = "Сохранить", AutoSize = true, MinimumSize = new Size(90, 0) };
            save.Click += delegate { SaveAndClose(); };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            layout.Controls.Add(buttons, 0, 4);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void ChooseFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Куда сохранять скриншоты";
                dialog.ShowNewFolderButton = true;
                if (Directory.Exists(_folder.Text))
                    dialog.SelectedPath = _folder.Text;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _folder.Text = dialog.SelectedPath;
            }
        }

        private void SaveAndClose()
        {
            string folder = _folder.Text.Trim();
            if (folder.Length == 0)
                folder = AppSettings.DefaultFolder;

            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не получается использовать эту папку:\n" + ex.Message,
                    "Simple Screenshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                AppSettings.SaveFolder = folder;
                AppSettings.SoundEnabled = _sound.Checked;
                AutoStart.Apply(_autoStart.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось сохранить настройки:\n" + ex.Message,
                    "Simple Screenshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
