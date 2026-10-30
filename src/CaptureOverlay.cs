using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace SimpleScreenshot
{
    /// <summary>
    /// Полноэкранное окно поверх всех мониторов: показывает застывший снимок экрана,
    /// даёт выделить область мышью.
    /// </summary>
    internal sealed class CaptureOverlay : Form
    {
        private readonly Bitmap _shot;      // снимок всех мониторов
        private readonly Bitmap _dimmed;    // тот же снимок, затемнённый
        private readonly Rectangle _virtual; // координаты всех мониторов вместе

        private bool _dragging;
        private Point _start;
        private Rectangle _selection = Rectangle.Empty;
        private bool _finished;

        /// <summary>Готовый кадр. null — пользователь отменил.</summary>
        public Bitmap Result { get; private set; }

        public CaptureOverlay(Bitmap shot, Rectangle virtualScreen)
        {
            _shot = shot;
            _virtual = virtualScreen;
            _dimmed = MakeDimmed(shot);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            Bounds = virtualScreen;
            Text = "Simple Screenshot";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW — не показывать в Alt+Tab
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Ставим окно точно на все мониторы в реальных пикселях.
            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST,
                _virtual.X, _virtual.Y, _virtual.Width, _virtual.Height, NativeMethods.SWP_SHOWWINDOW);
            Activate();
            NativeMethods.SetForegroundWindow(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            // Окно растянуто на мониторы с разным масштабом: не даём Windows менять его размер.
            if (m.Msg == NativeMethods.WM_DPICHANGED)
            {
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        // ---------- Действия ----------

        /// <summary>Снимок всего экрана (монитора, на котором сейчас курсор).</summary>
        public void CaptureFullScreen()
        {
            if (_finished)
                return;

            Rectangle screen = Screen.FromPoint(Cursor.Position).Bounds;
            screen.Offset(-_virtual.X, -_virtual.Y);
            screen.Intersect(new Rectangle(Point.Empty, _shot.Size));
            Finish(screen);
        }

        public void Cancel()
        {
            if (_finished)
                return;
            _finished = true;
            Close();
        }

        private void Finish(Rectangle area)
        {
            if (_finished)
                return;
            _finished = true;

            if (area.Width > 0 && area.Height > 0)
                Result = _shot.Clone(area, _shot.PixelFormat);

            Close();
        }

        // ---------- Мышь и клавиши ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _start = e.Location;
                SetSelection(new Rectangle(e.Location, Size.Empty));
            }
            else if (e.Button == MouseButtons.Right)
            {
                Cancel();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
                SetSelection(Normalize(_start, e.Location));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !_dragging)
                return;

            _dragging = false;
            Rectangle area = Normalize(_start, e.Location);

            // Случайный клик без протягивания — остаёмся в режиме выбора.
            if (area.Width < 3 || area.Height < 3)
            {
                SetSelection(Rectangle.Empty);
                return;
            }

            area.Intersect(new Rectangle(Point.Empty, _shot.Size));
            Finish(area);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            // Запасной путь: обычно Shift и Esc ловит глобальный перехват клавиатуры.
            if (e.KeyCode == Keys.Escape)
                Cancel();
            else if (e.KeyCode == Keys.ShiftKey && !_dragging)
                CaptureFullScreen();
        }

        private static Rectangle Normalize(Point a, Point b)
        {
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                                      Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        // ---------- Отрисовка ----------

        private void SetSelection(Rectangle next)
        {
            Rectangle a = Expand(_selection);
            Rectangle b = Expand(next);
            _selection = next;

            if (a.IsEmpty && b.IsEmpty)
                return;
            Invalidate(a.IsEmpty ? b : b.IsEmpty ? a : Rectangle.Union(a, b));
        }

        // Область перерисовки: рамка плюс запас под подпись с размером.
        private static Rectangle Expand(Rectangle r)
        {
            if (r == Rectangle.Empty)
                return Rectangle.Empty;
            return Rectangle.FromLTRB(r.Left - 4, r.Top - 40, r.Right + 160, r.Bottom + 40);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Фон рисуем сами в OnPaint — так нет мерцания.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            Rectangle clip = e.ClipRectangle;
            g.DrawImage(_dimmed, clip, clip, GraphicsUnit.Pixel);

            if (_selection.Width > 0 && _selection.Height > 0)
            {
                g.DrawImage(_shot, _selection, _selection, GraphicsUnit.Pixel);

                g.PixelOffsetMode = PixelOffsetMode.Default;
                using (var pen = new Pen(Color.FromArgb(0, 162, 255), 1))
                    g.DrawRectangle(pen, _selection.X, _selection.Y, _selection.Width - 1, _selection.Height - 1);

                DrawSizeLabel(g);
            }
        }

        private void DrawSizeLabel(Graphics g)
        {
            string text = _selection.Width + " × " + _selection.Height;
            using (var font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point))
            {
                SizeF size = g.MeasureString(text, font);
                float x = _selection.X;
                float y = _selection.Y - size.Height - 6;
                if (y < 0)
                    y = _selection.Y + 6;

                var box = new RectangleF(x, y, size.Width + 8, size.Height + 2);
                using (var bg = new SolidBrush(Color.FromArgb(200, 20, 20, 20)))
                    g.FillRectangle(bg, box);
                g.DrawString(text, font, Brushes.White, x + 4, y + 1);
            }
        }

        private static Bitmap MakeDimmed(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(result))
            using (var shade = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
            {
                g.DrawImage(source, new Rectangle(Point.Empty, source.Size));
                g.FillRectangle(shade, new Rectangle(Point.Empty, source.Size));
            }
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _dimmed.Dispose();
            base.Dispose(disposing);
        }
    }
}
