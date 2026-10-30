using System;
using System.IO;
using System.Media;

namespace SimpleScreenshot
{
    /// <summary>
    /// Звук щелчка затвора. Генерируется в памяти: отдельный файл со звуком не нужен.
    /// </summary>
    internal sealed class ShutterSound : IDisposable
    {
        private const int SampleRate = 44100;
        private readonly MemoryStream _wav;
        private readonly SoundPlayer _player;

        public ShutterSound()
        {
            _wav = new MemoryStream(BuildWav());
            _player = new SoundPlayer(_wav);
            _player.Load();
        }

        public void Play()
        {
            try
            {
                _player.Play(); // асинхронно, не тормозит сохранение
            }
            catch (Exception)
            {
                // Нет звуковой карты или звук занят — не страшно.
            }
        }

        // Два коротких щелчка, как у зеркалки: шторка открылась и закрылась.
        private static byte[] BuildWav()
        {
            int length = SampleRate * 160 / 1000; // 160 мс
            var samples = new float[length];
            var rnd = new Random(12345);

            AddClick(samples, rnd, startMs: 0, durationMs: 45, loudness: 0.9f, tone: 2200);
            AddClick(samples, rnd, startMs: 70, durationMs: 60, loudness: 0.7f, tone: 1500);

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int dataSize = length * 2;
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + dataSize);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);                // размер блока fmt
                w.Write((short)1);          // PCM
                w.Write((short)1);          // моно
                w.Write(SampleRate);
                w.Write(SampleRate * 2);    // байт в секунду
                w.Write((short)2);          // байт на отсчёт
                w.Write((short)16);         // бит на отсчёт
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(dataSize);

                foreach (float s in samples)
                {
                    float v = Math.Max(-1f, Math.Min(1f, s));
                    w.Write((short)(v * 30000));
                }

                w.Flush();
                return ms.ToArray();
            }
        }

        // Щелчок = резкий всплеск шума + короткий «металлический» тон, всё быстро затухает.
        private static void AddClick(float[] buf, Random rnd, int startMs, int durationMs, float loudness, double tone)
        {
            int start = SampleRate * startMs / 1000;
            int count = SampleRate * durationMs / 1000;
            float prev = 0;

            for (int i = 0; i < count && start + i < buf.Length; i++)
            {
                double t = (double)i / SampleRate;
                double envelope = Math.Exp(-t * 90);
                float noise = (float)(rnd.NextDouble() * 2 - 1);
                // Простой фильтр: убираем самые высокие частоты, чтобы не шипело.
                prev = prev * 0.55f + noise * 0.45f;
                double ring = Math.Sin(2 * Math.PI * tone * t) * 0.35;
                buf[start + i] += (float)((prev + ring) * envelope * loudness);
            }
        }

        public void Dispose()
        {
            _player.Dispose();
            _wav.Dispose();
        }
    }
}
