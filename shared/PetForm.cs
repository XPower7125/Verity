using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Verity.Shared
{
    /// <summary>
    /// The floating Verity face: transparent, borderless, always on top, draggable.
    /// Clicking it opens the chat bubble. Right-click opens a small menu.
    /// </summary>
    public sealed class PetForm : Form
    {
        private readonly SettingsData _settings;
        private readonly Action<SettingsData> _onSettingsChanged;
        private readonly PictureBox _face = new();
        private readonly Timer _idle = new() { Interval = 240 };
        private readonly Timer _bob = new() { Interval = 2600 };

        private bool _dragging;
        private int _dragOffsetX, _dragOffsetY;
        private int _bobPhase;
        private int _bobBaseY;

        public PetForm(SettingsData settings, Action<SettingsData> onSettingsChanged)
        {
            _settings = settings;
            _onSettingsChanged = onSettingsChanged;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Magenta;          // keyed out below
            TransparencyKey = Color.Magenta;
            ClientSize = new Size(124, 124);
            Text = "verity";
            Icon = Brand.Icon;

            _face.Image = Brand.RoundFace(112);
            _face.SizeMode = PictureBoxSizeMode.Zoom;
            _face.BackColor = Color.Magenta;
            _face.Location = new Point(6, 6);
            _face.Size = new Size(112, 112);
            _face.Cursor = Cursors.Hand;
            _face.MouseDown += FaceMouseDown;
            _face.MouseMove += FaceMouseMove;
            _face.MouseUp += FaceMouseUp;
            Controls.Add(_face);

            PlaceFromSettings();

            _idle.Tick += (_, _) => { if (!_dragging) _face.Invalidate(); };
            _idle.Start();

            _bob.Tick += (_, _) =>
            {
                if (_dragging) return;
                // Oscillate around the resting Y rather than nudging relative to
                // the current one, otherwise the drift compounds each tick.
                _bobPhase = (_bobPhase + 1) % 4;
                int offset = _bobPhase switch { 0 or 1 => 0, 2 => 4, _ => 8 };
                var target = _bobBaseY + offset;
                if (Top != target) Location = new Point(Left, target);
            };
            _bob.Start();
        }

        /// <summary>
        /// The corner shadow idea is deliberately absent: a TransparencyKey
        /// window is masked by exact colour, not alpha, so a semi-transparent
        /// black fill blends with the key colour and turns into a visible smudge.
        /// </summary>
        private void PlaceFromSettings()
        {
            var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            if (_settings.PetX >= 0 && _settings.PetY >= 0 &&
                _settings.PetX < screen.Right && _settings.PetY < screen.Bottom)
            {
                Location = new Point(_settings.PetX, _settings.PetY);
            }
            else
            {
                Location = new Point(screen.Right - 180, screen.Bottom - 190);
            }
            _bobBaseY = Top;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // WS_EX_TOOLWINDOW keeps it out of Alt+Tab.
                cp.ExStyle |= 0x80;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Visible = true;
        }

        private void FaceMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowMenu();
                return;
            }
            _dragging = true;
            _bob.Stop();
            _dragOffsetX = e.X;
            _dragOffsetY = e.Y;
            BringToFront();
        }

        private void FaceMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            var screen = Screen.FromControl(this).WorkingArea;
            var x = Cursor.Position.X - _dragOffsetX;
            var y = Cursor.Position.Y - _dragOffsetY;
            Location = new Point(
                Math.Clamp(x, screen.Left - Width + 40, screen.Right - 40),
                Math.Clamp(y, screen.Top, screen.Bottom - 40));
        }

        private void FaceMouseUp(object? sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;

            // A click, not a drag: open the chat.
            if (Math.Abs(Cursor.Position.X - (_dragOffsetX + Left)) < 4 &&
                Math.Abs(Cursor.Position.Y - (_dragOffsetY + Top)) < 4)
            {
                ChatRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            _settings.PetX = Left;
            _settings.PetY = Top;
            _onSettingsChanged(_settings);
            _bobBaseY = Top;
            _bobPhase = 0;
            _bob.Start();
        }

        public event EventHandler? ChatRequested;

        private void ShowMenu()
        {
            var menu = new ContextMenuStrip
            {
                BackColor = Theme.BgPanel,
                ForeColor = Theme.Ink,
                Font = Theme.Face(10f),
                ShowImageMargin = false
            };

            var chat = new ToolStripMenuItem("chat") { ForeColor = Theme.Ink, BackColor = Theme.BgPanel };
            chat.Click += (_, _) => ChatRequested?.Invoke(this, EventArgs.Empty);

            var settings = new ToolStripMenuItem("api key + model...") { ForeColor = Theme.Ink, BackColor = Theme.BgPanel };
            settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

            var quit = new ToolStripMenuItem("quit") { ForeColor = Theme.Ink, BackColor = Theme.BgPanel };
            quit.Click += (_, _) => Application.Exit();

            menu.Items.Add(chat);
            menu.Items.Add(settings);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(quit);

            menu.Show(Cursor.Position);
        }

        public event EventHandler? SettingsRequested;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _settings.PetX = Left;
            _settings.PetY = Top;
            _onSettingsChanged(_settings);
            _idle.Stop();
            _bob.Stop();
            base.OnFormClosing(e);
        }
    }
}