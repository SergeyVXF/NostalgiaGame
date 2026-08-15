using UnityEngine;

namespace MiniVanGame
{
    public static class MiniVanHornAudio
    {
        private static AudioClip hornClip;

        public static void PlayHorn(Vector3 position)
        {
            if (hornClip == null)
            {
                hornClip = CreateHornClip();
            }

            AudioSource.PlayClipAtPoint(hornClip, position, 0.8f);
        }

        private static AudioClip CreateHornClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.42f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(t / 0.035f) * Mathf.Clamp01((duration - t) / 0.08f);
                float waveA = Mathf.Sin(2f * Mathf.PI * 410f * t);
                float waveB = Mathf.Sin(2f * Mathf.PI * 515f * t);
                samples[i] = (waveA * 0.62f + waveB * 0.38f) * envelope * 0.55f;
            }

            AudioClip clip = AudioClip.Create("MiniVan Horn", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
