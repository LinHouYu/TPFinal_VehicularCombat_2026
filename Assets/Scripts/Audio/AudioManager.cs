using UnityEngine;
using VehicularCombat.Combat;

namespace VehicularCombat.Audio
{
    /// <summary>
    /// Zero-dependency procedural 8-bit retro SFX synthesizer using AudioClip.Create.
    /// Plays overlapping crisp arcade audio for all weapons, hits, explosions, pickups, and UI clicks.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Master Volume")]
        [Range(0f, 1f)] public float masterVolume = 0.65f;

        private const int SampleRate = 22050;
        private const int PoolSize = 10;

        private AudioSource[] sourcePool;
        private int poolCursor;

        private AudioClip clipShootDefault;
        private AudioClip clipShootShotgun;
        private AudioClip clipShootMachineGun;
        private AudioClip clipEnemyHit;
        private AudioClip clipEnemyExplosion;
        private AudioClip clipPlayerHit;
        private AudioClip clipPickup;
        private AudioClip clipUiClick;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            sourcePool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; // Crisp 2D arcade mix
                sourcePool[i] = src;
            }

            GenerateAllRetroClips();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PlayShoot(WeaponType weapon)
        {
            switch (weapon)
            {
                case WeaponType.Shotgun:
                    PlayClip(clipShootShotgun, 0.95f, Random.Range(0.92f, 1.04f));
                    break;
                case WeaponType.MachineGun:
                    PlayClip(clipShootMachineGun, 0.55f, Random.Range(0.94f, 1.12f));
                    break;
                default:
                    PlayClip(clipShootDefault, 0.75f, Random.Range(0.95f, 1.06f));
                    break;
            }
        }

        public void PlayEnemyHit() => PlayClip(clipEnemyHit, 0.65f, Random.Range(0.95f, 1.12f));
        public void PlayEnemyExplosion() => PlayClip(clipEnemyExplosion, 0.95f, Random.Range(0.88f, 1.05f));
        public void PlayPlayerHit() => PlayClip(clipPlayerHit, 0.90f, Random.Range(0.92f, 1.04f));
        public void PlayPickup() => PlayClip(clipPickup, 0.85f, 1.0f);
        public void PlayUIClick() => PlayClip(clipUiClick, 0.70f, 1.0f);

        private void PlayClip(AudioClip clip, float volumeScale, float pitch)
        {
            if (clip == null || sourcePool == null || sourcePool.Length == 0)
                return;

            var src = sourcePool[poolCursor];
            poolCursor = (poolCursor + 1) % sourcePool.Length;
            src.pitch = pitch;
            src.volume = masterVolume * volumeScale;
            src.PlayOneShot(clip);
        }

        private void GenerateAllRetroClips()
        {
            clipShootDefault = SynthesizeSweep("SFX_ShootDefault", 0.11f, 520f, 130f, 0.25f, waveType: 0);
            clipShootShotgun = SynthesizeShotgun("SFX_ShootShotgun", 0.24f);
            clipShootMachineGun = SynthesizeSweep("SFX_ShootMG", 0.055f, 760f, 260f, 0.30f, waveType: 0);
            clipEnemyHit = SynthesizeSweep("SFX_EnemyHit", 0.065f, 920f, 480f, 0.15f, waveType: 0);
            clipEnemyExplosion = SynthesizeExplosion("SFX_EnemyExplosion", 0.38f);
            clipPlayerHit = SynthesizeSweep("SFX_PlayerHit", 0.22f, 220f, 65f, 0.40f, waveType: 1);
            clipPickup = SynthesizeArpeggio("SFX_Pickup", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.065f);
            clipUiClick = SynthesizeArpeggio("SFX_UIClick", new[] { 680f, 980f }, 0.032f);
        }

        private static AudioClip SynthesizeSweep(string name, float duration, float startFreq, float endFreq, float noiseMix, int waveType)
        {
            int samples = Mathf.Max(64, Mathf.RoundToInt(SampleRate * duration));
            float[] data = new float[samples];
            var rng = new System.Random(name.GetHashCode());
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = Mathf.Pow(1f - t, 1.5f);
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                phase += (2f * Mathf.PI * freq) / SampleRate;

                float wave = waveType == 0
                    ? (Mathf.Sin(phase) >= 0f ? 1f : -1f) // 8-bit square wave
                    : (2f * ((phase / (2f * Mathf.PI)) % 1f) - 1f); // 8-bit sawtooth

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float val = Mathf.Lerp(wave, noise, noiseMix) * env * 0.55f;
                // Quantize slightly for authentic 8-bit crunch
                data[i] = Mathf.Round(val * 16f) / 16f;
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeShotgun(string name, float duration)
        {
            int samples = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[samples];
            var rng = new System.Random(1337);
            float phase = 0f;
            float heldNoise = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = Mathf.Pow(1f - t, 1.8f);
                float bassFreq = Mathf.Lerp(170f, 38f, t);
                phase += (2f * Mathf.PI * bassFreq) / SampleRate;
                float bass = Mathf.Sin(phase) >= 0f ? 1f : -1f;

                // Stepped 8-bit LFSR-style crunchy noise
                if (i % 4 == 0)
                    heldNoise = (float)(rng.NextDouble() * 2.0 - 1.0);

                float val = (bass * 0.55f + heldNoise * 0.65f) * env * 0.7f;
                data[i] = Mathf.Clamp(Mathf.Round(val * 14f) / 14f, -1f, 1f);
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeExplosion(string name, float duration)
        {
            int samples = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[samples];
            var rng = new System.Random(4096);
            float heldNoise = 0f;
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = Mathf.Pow(1f - t, 1.45f);
                int step = 2 + Mathf.FloorToInt(t * 12f);
                if (i % step == 0)
                    heldNoise = (float)(rng.NextDouble() * 2.0 - 1.0);

                float rumbleFreq = Mathf.Lerp(110f, 28f, t);
                phase += (2f * Mathf.PI * rumbleFreq) / SampleRate;
                float rumble = Mathf.Sin(phase) >= 0f ? 0.6f : -0.6f;

                float val = (heldNoise * 0.75f + rumble * 0.45f) * env * 0.72f;
                data[i] = Mathf.Clamp(Mathf.Round(val * 12f) / 12f, -1f, 1f);
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeArpeggio(string name, float[] notes, float noteDuration)
        {
            int perNote = Mathf.RoundToInt(SampleRate * noteDuration);
            int total = perNote * notes.Length;
            float[] data = new float[total];
            float phase = 0f;

            for (int n = 0; n < notes.Length; n++)
            {
                float freq = notes[n];
                for (int i = 0; i < perNote; i++)
                {
                    float localT = (float)i / perNote;
                    float env = localT < 0.1f ? (localT / 0.1f) : (1f - (localT - 0.1f) * 0.55f);
                    phase += (2f * Mathf.PI * freq) / SampleRate;
                    float sq = Mathf.Sin(phase) >= 0f ? 1f : -1f;
                    data[n * perNote + i] = sq * env * 0.42f;
                }
            }

            var clip = AudioClip.Create(name, total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
