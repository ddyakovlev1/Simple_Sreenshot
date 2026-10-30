using System;
using System.IO;
using System.Media;

namespace SimpleScreenshot
{
    /// <summary>
    /// Звук щелчка затвора. Файл shutter.wav зашит внутрь exe.
    /// </summary>
    internal sealed class ShutterSound : IDisposable
    {
        private readonly MemoryStream _wav;
        private readonly SoundPlayer _player;

        public ShutterSound()
        {
            _wav = new MemoryStream();
            using (Stream res = Resources.Open("shutter.wav"))
                res.CopyTo(_wav);
            _wav.Position = 0;

            _player = new SoundPlayer(_wav);
            _player.Load();
        }

        public void Play()
        {
            try
            {
                _wav.Position = 0;
                _player.Play(); // асинхронно, не тормозит сохранение
            }
            catch (Exception)
            {
                // Нет звуковой карты или звук занят — не страшно.
            }
        }

        public void Dispose()
        {
            _player.Dispose();
            _wav.Dispose();
        }
    }
}
