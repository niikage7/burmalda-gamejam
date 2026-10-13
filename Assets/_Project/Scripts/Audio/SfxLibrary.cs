using System;
using UnityEngine;

namespace TankBrawl.Audio
{
    /// <summary>
    /// Which clips play for which <see cref="Sfx"/>, plus the music. Loaded from Resources by <see cref="AudioManager"/>.
    /// To replace a placeholder sound, drop the new clip into the entry; several clips in one entry are picked at random.
    /// </summary>
    [CreateAssetMenu(menuName = "TankBrawl/Sfx Library", fileName = "SfxLibrary")]
    public class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public Sfx id;
            public AudioClip[] clips;

            [Range(0f, 1f)] public float volume = 1f;

            [Tooltip("Random pitch range, so repeated sounds do not sound identical. 1 = unchanged.")]
            public Vector2 pitch = new Vector2(0.95f, 1.05f);
        }

        public Entry[] entries;

        [Header("Music (loops)")]
        public AudioClip menuMusic;
        public AudioClip battleMusic;

        [Range(0f, 1f)] public float musicBaseVolume = 0.5f;

        public Entry Find(Sfx id)
        {
            if (entries == null)
                return null;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].id == id)
                    return entries[i];
            }

            return null;
        }
    }
}
