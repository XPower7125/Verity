using System;
using System.Drawing;
using System.Windows.Forms;

namespace Verity.Shared
{
    /// <summary>
    /// Hosts the pet on a message loop. Clicking the face opens the chat bubble;
    /// closing the bubble leaves the pet behind. Owns settings load/save.
    /// </summary>
    public sealed class VerityHost : ApplicationContext
    {
        private SettingsData _settings;
        private PetForm _pet = null!;
        private BubbleForm? _bubble;

        public VerityHost(SettingsData settings)
        {
            _settings = settings;
            EnsureKey();
            StartPet();
        }

        private void Save() => Settings.Save(_settings);

        private void EnsureKey()
        {
            if (!string.IsNullOrWhiteSpace(_settings.ApiKey)) return;

            using var prompt = new KeyPromptForm(_settings.Model, _settings.ExaApiKey);
            if (prompt.ShowDialog() == DialogResult.OK)
            {
                _settings.ApiKey = prompt.EnteredKey;
                _settings.ExaApiKey = prompt.EnteredExaKey;
                _settings.Model = prompt.EnteredModel;
                Save();
            }
            else
            {
                ExitThread();
            }
        }

        private void StartPet()
        {
            _pet = new PetForm(_settings, _ => Save())
            {
                Visible = false
            };
            _pet.ChatRequested += (_, _) => OpenBubble();
            _pet.SettingsRequested += (_, _) => EditSettings();
            _pet.Shown += (_, _) => _pet.Visible = true;
            MainForm = _pet;
        }

        private void OpenBubble()
        {
            if (_bubble != null && !_bubble.IsDisposed)
            {
                _bubble.Activate();
                _bubble.BringToFront();
                return;
            }

            _bubble = new BubbleForm(_settings);
            _bubble.BubbleClosed += (_, _) =>
            {
                _bubble?.Dispose();
                _bubble = null;
                _pet.BringToFront();
            };

            _bubble.FormClosed += (_, _) => _pet.BringToFront();

            // Position relative to where the face currently is.
            var bounds = new Rectangle(_pet.Left, _pet.Top, _pet.Width, _pet.Height);
            _bubble.PositionBesidePet(bounds);
            _bubble.Show();
            _bubble.BringToFront();
        }

        private void EditSettings()
        {
            using var prompt = new KeyPromptForm(_settings.Model, _settings.ExaApiKey);
            if (prompt.ShowDialog() == DialogResult.OK)
            {
                _settings.ApiKey = prompt.EnteredKey;
                _settings.ExaApiKey = prompt.EnteredExaKey;
                _settings.Model = prompt.EnteredModel;
                Save();

                // Force the next message to rebuild the client with the new key.
                if (_bubble != null && !_bubble.IsDisposed)
                {
                    _bubble.Close();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Save();
                _pet?.Dispose();
                _bubble?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
