using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EchoBootstrapper
{
    internal class ProgressStrip : Control
    {
        private readonly Timer _timer;
        private int _offset;

        public ProgressStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _timer = new Timer { Interval = 16 };
            _timer.Tick += (s, e) =>
            {
                _offset += Step;

                if (_offset > Width) _offset = -BlockWidth;
                Invalidate();
            };
            _offset = -BlockWidth;
        }

        public int Step { get; set; } = 4;

        public int BlockWidth { get; set; } = 150;

        public Color TrackColor { get; set; } = Color.FromArgb(230, 230, 230);
        public Color TrackBorderColor { get; set; } = Color.FromArgb(200, 200, 200);

        public Color FillTopColor { get; set; } = ColorTranslator.FromHtml("#06B025");

        public Color FillBottomColor { get; set; } = ColorTranslator.FromHtml("#55C96A");

        public void Freeze()
        {
            _timer.Stop();
            Invalidate();
        }

        protected override void OnHandleCreated(System.EventArgs e)
        {
            base.OnHandleCreated(e);
            _offset = -BlockWidth;
            _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var full = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var track = new SolidBrush(TrackColor)) g.FillRectangle(track, full);
            using (var border = new Pen(TrackBorderColor)) g.DrawRectangle(border, full);

            if (!_timer.Enabled) return;

            var left = _offset < 1 ? 1 : _offset;
            var right = _offset + BlockWidth;
            if (right > Width - 1) right = Width - 1;
            if (right <= left) return;

            var block = new Rectangle(left, 1, right - left, Height - 2);

            using (var fill = new LinearGradientBrush(
                       new Rectangle(0, 1, Width, Height - 2), FillTopColor, FillBottomColor, LinearGradientMode.Vertical))
                g.FillRectangle(fill, block);
        }
    }
}
