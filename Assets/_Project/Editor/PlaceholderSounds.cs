using System;
using System.Collections.Generic;
using System.IO;
using TankBrawl.Audio;
using UnityEditor;
using UnityEngine;

namespace TankBrawl.EditorTools
{
    /// <summary>
    /// Writes simple synthetic .wav files for every <see cref="Sfx"/> (so the game is audible while testing) and fills the
    /// <see cref="SfxLibrary"/> asset. Run from the menu TankBrawl / Generate Placeholder Sounds. Existing entries that already have
    /// real clips (not from the Placeholders folder) are left alone, so re-running never overwrites final sounds.
    /// </summary>
    public static class PlaceholderSounds
    {
        const string Folder = "Assets/_Project/Audio/Placeholders";
        const string LibraryPath = "Assets/_Project/ScriptableObjects/Resources/SfxLibrary.asset";
        const int SampleRate = 44100;

        struct Spec
        {
            public Sfx Id;
            public float Length;
            public float Volume;
            public Func<float, float, System.Random, float> Voice; // (time, length, rng) -> sample
            public int Variants;
        }

        [MenuItem("TankBrawl/Generate Placeholder Sounds")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects/Resources"))
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Resources");

            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var existing = new Dictionary<Sfx, SfxLibrary.Entry>();
            if (library.entries != null)
            {
                foreach (SfxLibrary.Entry entry in library.entries)
                {
                    if (entry != null)
                        existing[entry.id] = entry;
                }
            }

            var entries = new List<SfxLibrary.Entry>();
            foreach (Spec spec in Specs())
            {
                SfxLibrary.Entry entry;
                if (!existing.TryGetValue(spec.Id, out entry))
                {
                    entry = new SfxLibrary.Entry { id = spec.Id, volume = spec.Volume, pitch = new Vector2(0.95f, 1.05f) };
                }
                else if (HasRealClips(entry))
                {
                    entries.Add(entry);
                    continue;
                }

                var clips = new List<AudioClip>();
                for (int v = 0; v < spec.Variants; v++)
                {
                    string path = $"{Folder}/sfx_{spec.Id}_{v}.wav";
                    WriteWav(path, Render(spec, 1000 + v * 77 + (int)spec.Id));
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
                }

                entry.clips = clips.ToArray();
                entries.Add(entry);
            }

            library.entries = entries.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"Placeholder sounds: {entries.Count} entries in {LibraryPath}");
        }

        static bool HasRealClips(SfxLibrary.Entry entry)
        {
            if (entry.clips == null || entry.clips.Length == 0)
                return false;

            foreach (AudioClip clip in entry.clips)
            {
                if (clip == null)
                    continue;

                if (!AssetDatabase.GetAssetPath(clip).StartsWith(Folder))
                    return true;
            }

            return false;
        }

        // ---- Synthesis ----

        static float[] Render(Spec spec, int seed)
        {
            var rng = new System.Random(seed);
            int count = Mathf.CeilToInt(spec.Length * SampleRate);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float fadeIn = Mathf.Clamp01(t / 0.003f);
                float fadeOut = Mathf.Clamp01((spec.Length - t) / 0.02f);
                samples[i] = Mathf.Clamp(spec.Voice(t, spec.Length, rng) * fadeIn * fadeOut, -1f, 1f);
            }

            return samples;
        }

        static void WriteWav(string path, float[] samples)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                foreach (float sample in samples)
                    writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }

        // ---- Voices: building blocks ----

        // A sine whose pitch glides from `from` to `to` (Hz), with an exponential decay.
        static Func<float, float, System.Random, float> Sweep(float from, float to, float decay, float noise = 0f, float noiseDecay = 20f, bool square = false)
        {
            float phase = 0f;
            float last = -1f;
            float filtered = 0f;
            return (t, length, rng) =>
            {
                if (t < last)
                {
                    phase = 0f;
                    filtered = 0f;
                }

                float dt = last < 0f ? 0f : t - last;
                last = t;
                float k = Mathf.Clamp01(t / length);
                float frequency = Mathf.Lerp(from, to, k);
                phase += frequency * dt;
                float tone = Mathf.Sin(phase * Mathf.PI * 2f);
                if (square)
                    tone = tone >= 0f ? 0.6f : -0.6f;
                float envelope = Mathf.Exp(-t * decay);

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                filtered += (white - filtered) * 0.35f;
                float grit = filtered * Mathf.Exp(-t * noiseDecay) * noise;
                return tone * envelope * 0.8f + grit;
            };
        }

        // A burst of low-passed noise.
        static Func<float, float, System.Random, float> Burst(float decay, float smooth = 0.3f, float rumble = 0f, float rumbleHz = 60f)
        {
            float filtered = 0f;
            float last = -1f;
            return (t, length, rng) =>
            {
                if (t < last)
                    filtered = 0f;
                last = t;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                filtered += (white - filtered) * smooth;
                float envelope = Mathf.Exp(-t * decay);
                float low = Mathf.Sin(t * rumbleHz * Mathf.PI * 2f) * rumble * Mathf.Exp(-t * decay * 0.6f);
                return filtered * envelope * 1.2f + low;
            };
        }

        // Two inharmonic tones: a metallic ring.
        static Func<float, float, System.Random, float> Ring(float baseHz, float decay, float noise = 0.15f)
        {
            return (t, length, rng) =>
            {
                float envelope = Mathf.Exp(-t * decay);
                float tone = Mathf.Sin(t * baseHz * Mathf.PI * 2f) + 0.6f * Mathf.Sin(t * baseHz * 2.76f * Mathf.PI * 2f) + 0.3f * Mathf.Sin(t * baseHz * 5.4f * Mathf.PI * 2f);
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                return (tone * 0.4f + white * noise * Mathf.Exp(-t * 60f)) * envelope;
            };
        }

        // A little melody: each note lasts `noteLength` seconds, a clean tone with its own decay.
        static Func<float, float, System.Random, float> Melody(float[] hz, float noteLength, bool square = false)
        {
            return (t, length, rng) =>
            {
                int index = Mathf.Min((int)(t / noteLength), hz.Length - 1);
                float local = t - index * noteLength;
                float envelope = Mathf.Exp(-local * 7f) * Mathf.Clamp01(local / 0.004f);
                float wave = Mathf.Sin(t * hz[index] * Mathf.PI * 2f);
                if (square)
                    wave = wave >= 0f ? 0.5f : -0.5f;
                return wave * envelope * 0.6f;
            };
        }

        static Func<float, float, System.Random, float> Mix(Func<float, float, System.Random, float> a, Func<float, float, System.Random, float> b, float weightB = 1f)
        {
            return (t, length, rng) => a(t, length, rng) + b(t, length, rng) * weightB;
        }

        static IEnumerable<Spec> Specs()
        {
            yield return S(Sfx.ShotArmorPiercing, 0.38f, 0.8f, Mix(Sweep(190, 55, 11, 0.5f, 28), Burst(26, 0.5f)), 3);
            yield return S(Sfx.ShotFragment, 0.4f, 0.8f, Mix(Sweep(150, 45, 9, 0.7f, 20), Burst(18, 0.2f, 0.2f)), 2);
            yield return S(Sfx.ShotExplosive, 0.5f, 0.85f, Mix(Sweep(120, 35, 7, 0.6f, 14), Burst(12, 0.25f, 0.35f, 45)), 2);
            yield return S(Sfx.ShotBot, 0.28f, 0.6f, Mix(Sweep(260, 90, 14, 0.4f, 30), Burst(34, 0.6f)), 2);
            yield return S(Sfx.HitTank, 0.25f, 0.75f, Mix(Ring(420, 18), Burst(45, 0.5f)), 3);
            yield return S(Sfx.HitSelf, 0.32f, 0.9f, Mix(Ring(260, 14), Sweep(110, 60, 14, 0.5f, 30)), 3);
            yield return S(Sfx.ShieldBlock, 0.3f, 0.7f, Sweep(900, 1700, 9, 0.2f, 40, true), 2);
            yield return S(Sfx.TankExplode, 1.0f, 0.95f, Mix(Burst(4.5f, 0.12f, 0.8f, 48), Sweep(90, 28, 4f, 0.4f, 8)), 2);
            yield return S(Sfx.ExplosionBlast, 0.8f, 0.9f, Mix(Burst(6f, 0.18f, 0.55f, 55), Sweep(110, 30, 6f, 0.5f, 12)), 2);
            yield return S(Sfx.HitWall, 0.2f, 0.7f, Mix(Burst(40, 0.4f), Sweep(180, 90, 30, 0f)), 3);
            yield return S(Sfx.Ricochet, 0.35f, 0.7f, Ring(1450, 11, 0.25f), 3);
            yield return S(Sfx.BrickBreak, 0.4f, 0.8f, Burst(10, 0.55f, 0.15f, 90), 3);
            yield return S(Sfx.BrickRestore, 0.1f, 0.35f, Sweep(500, 800, 28), 1);
            yield return S(Sfx.ShellClash, 0.16f, 0.7f, Mix(Ring(1900, 40, 0.6f), Burst(60, 0.8f)), 2);
            yield return S(Sfx.RamHit, 0.45f, 0.9f, Mix(Ring(190, 12, 0.5f), Burst(16, 0.35f, 0.5f, 70)), 2);

            yield return S(Sfx.NitroStart, 0.5f, 0.6f, Mix(Burst(4f, 0.07f), Sweep(180, 520, 5f)), 1);
            yield return S(Sfx.LaserStart, 0.65f, 0.6f, Sweep(180, 1400, 3f, 0.1f, 8, true), 1);
            yield return S(Sfx.AmmoSwitch, 0.18f, 0.5f, Mix(Ring(900, 30, 0.2f), Burst(80, 0.6f)), 1);
            yield return S(Sfx.Respawn, 0.7f, 0.6f, Sweep(250, 900, 3.5f), 1);
            yield return S(Sfx.PowerUpSpawn, 0.55f, 0.55f, Melody(new[] { 660f, 880f, 1100f }, 0.14f), 1);
            yield return S(Sfx.PowerUpPickup, 0.4f, 0.7f, Melody(new[] { 660f, 990f, 1320f }, 0.09f, true), 1);
            yield return S(Sfx.ShieldUp, 0.55f, 0.6f, Sweep(300, 1200, 4f, 0.1f, 10), 1);

            yield return S(Sfx.RoundStart, 1.0f, 0.8f, Melody(new[] { 440f, 440f, 660f }, 0.28f, true), 1);
            yield return S(Sfx.CountdownTick, 0.1f, 0.5f, Sweep(900, 900, 40), 1);
            yield return S(Sfx.RoundWin, 1.2f, 0.8f, Melody(new[] { 523f, 659f, 784f, 1047f }, 0.22f, true), 1);
            yield return S(Sfx.RoundLose, 1.2f, 0.8f, Melody(new[] { 392f, 349f, 311f, 262f }, 0.26f, true), 1);
            yield return S(Sfx.MatchWin, 2.2f, 0.9f, Melody(new[] { 523f, 659f, 784f, 659f, 784f, 1047f }, 0.32f), 1);
            yield return S(Sfx.MatchLose, 2.0f, 0.85f, Melody(new[] { 392f, 330f, 294f, 262f, 196f }, 0.38f), 1);
            yield return S(Sfx.KillConfirm, 0.28f, 0.7f, Melody(new[] { 880f, 1320f }, 0.1f), 1);
            yield return S(Sfx.GearGain, 0.32f, 0.55f, Melody(new[] { 1500f, 2000f, 2400f }, 0.07f), 1);
            yield return S(Sfx.ZoneCapture, 0.45f, 0.55f, Sweep(400, 760, 5f), 1);

            yield return S(Sfx.UiClick, 0.08f, 0.6f, Sweep(1100, 900, 40), 1);
            yield return S(Sfx.UiBack, 0.1f, 0.55f, Sweep(700, 500, 32), 1);
            yield return S(Sfx.UiBuy, 0.45f, 0.7f, Melody(new[] { 1000f, 1500f, 2000f, 2600f }, 0.08f, true), 1);
            yield return S(Sfx.UiDenied, 0.22f, 0.55f, Sweep(220, 150, 12, 0f, 20, true), 1);
            yield return S(Sfx.UiOpen, 0.2f, 0.45f, Sweep(500, 900, 12), 1);
        }

        static Spec S(Sfx id, float length, float volume, Func<float, float, System.Random, float> voice, int variants)
        {
            return new Spec { Id = id, Length = length, Volume = volume, Voice = voice, Variants = variants };
        }
    }
}
