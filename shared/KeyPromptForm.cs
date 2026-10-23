using System;
using System.Drawing;
using System.Windows.Forms;

namespace Verity.Shared
{
    /// <summary>
    /// First-run dialog: Verity's avatar, a short description, and API key entry.
    /// </summary>
    public sealed class KeyPromptForm : Form
    {
        private readonly TextBox _keyBox = new();
        private readonly TextBox _exaBox = new();
        private readonly TextBox _modelBox = new();

        public string EnteredKey { get; private set; } = "";
        public string EnteredExaKey { get; private set; } = "";
        public string EnteredModel { get; private set; } = "";

        public KeyPromptForm(string model, string exaApiKey = "")
        {
            EnteredModel = model;

            Brand.Apply(this);
            Text = "verity";
            ClientSize = new Size(540, 440);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgDeep;
            ForeColor = Theme.Ink;
            Font = Theme.Face(10.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;

            var root = new ChunkyPanel
            {
                Dock = DockStyle.Fill,
                Radius = 26,
                Border = 4,
                FillColor = Theme.BgDeep,
                BorderColor = Theme.Outline,
                Padding = new Padding(14)
            };
            Controls.Add(root);

            var logo = new PictureBox
            {
                Image = Brand.Logo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(6, 2),
                Size = new Size(62, 62),
                BackColor = Theme.BgDeep
            };
            root.Controls.Add(logo);

            var header = new Label
            {
                Text = "VERITY",
                Font = Theme.Face(16f, FontStyle.Bold),
                ForeColor = Theme.Cyan,
                AutoSize = true,
                Location = new Point(76, 12),
                BackColor = Color.Transparent
            };
            root.Controls.Add(header);

            var sub = new Label
            {
                Text = "a small desktop chat. ctrl+v pastes an image.",
                Font = Theme.Face(9.5f, FontStyle.Italic),
                ForeColor = Theme.InkDim,
                AutoSize = true,
                Location = new Point(76, 40),
                BackColor = Color.Transparent
            };
            root.Controls.Add(sub);

            int y = 88;

            root.Controls.Add(FieldLabel("OpenRouter API key:", y));
            _keyBox.Location = new Point(0, y + 20);
            _keyBox.Size = new Size(root.ClientSize.Width - 28, 30);
            StyleField(_keyBox, password: true);
            root.Controls.Add(_keyBox);
            y += 58;

            root.Controls.Add(FieldLabel("Exa API key (optional, for web search):", y));
            _exaBox.Location = new Point(0, y + 20);
            _exaBox.Size = new Size(root.ClientSize.Width - 28, 30);
            StyleField(_exaBox, password: true);
            _exaBox.Text = exaApiKey;
            root.Controls.Add(_exaBox);
            y += 58;

            root.Controls.Add(FieldLabel("Model (blank = default):", y));
            _modelBox.Location = new Point(0, y + 20);
            _modelBox.Size = new Size(300, 30);
            StyleField(_modelBox, password: false);
            _modelBox.Text = model;
            root.Controls.Add(_modelBox);
            y += 62;

            var go = new ChunkyButton
            {
                Text = "START",
                Size = new Size(150, 44),
                Radius = 18,
                FillColor = Theme.Yellow,
                TextColor = Color.FromArgb(0x2A, 0x14, 0x50),
                Font = Theme.Face(11f, FontStyle.Bold)
            };
            go.Click += (_, _) => Accept();

            var cancel = new ChunkyButton
            {
                Text = "NOPE",
                Size = new Size(90, 44),
                Radius = 18,
                FillColor = Theme.BgPanel,
                TextColor = Theme.Ink,
                Font = Theme.Face(11f, FontStyle.Bold)
            };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            root.Controls.Add(go);
            root.Controls.Add(cancel);

            // Positioned in a Resize handler rather than against
            // root.ClientSize at construction: the panel has no final width
            // until the form does, which pushed the row off the right edge.
            void PlaceButtons()
            {
                int right = root.ClientSize.Width - 14;
                int gap = 10;
                go.Location = new Point(right - go.Width, y);
                cancel.Location = new Point(go.Left - gap - cancel.Width, y);
            }
            root.Resize += (_, _) => PlaceButtons();
            Shown += (_, _) => PlaceButtons();

            AcceptButton = go;
            CancelButton = cancel;

            Shown += (_, _) => Native.ApplyRoundedCorners(this);
        }

        private static Label FieldLabel(string text, int y) => new()
        {
            Text = text,
            ForeColor = Theme.InkDim,
            Font = Theme.Face(9.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, y),
            BackColor = Color.Transparent
        };

        private static void StyleField(TextBox b, bool password)
        {
            b.BorderStyle = BorderStyle.FixedSingle;
            b.BackColor = Color.FromArgb(0x2E, 0x18, 0x58);
            b.ForeColor = Theme.Ink;
            b.Font = Theme.Face(11f);
            b.UseSystemPasswordChar = password;
        }

        private void Accept()
        {
            if (string.IsNullOrWhiteSpace(_keyBox.Text))
            {
                MessageBox.Show(this, "An API key is required.", "verity",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                _keyBox.Focus();
                return;
            }
            EnteredKey = _keyBox.Text;
            EnteredExaKey = _exaBox.Text.Trim();
            EnteredModel = _modelBox.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
