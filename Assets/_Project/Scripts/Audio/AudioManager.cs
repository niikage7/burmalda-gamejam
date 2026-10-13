using TankBrawl.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TankBrawl.Audio
{
    /// <summary>
    /// Plays sounds and music. Creates itself once per run from <c>Resources/SfxLibrary</c> (no scene needs it).
    /// Static facade: <see cref="Play"/> for interface and global sounds, <see cref="PlayAt"/> for sounds in the world
    /// (quieter the farther they are from the camera). Everything is silent and harmless if the library or a clip is missing.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const string LibraryResource = "SfxLibrary";
        const int PoolSize = 20;

        // Within this distance of the camera a world sound is at full volume, beyond FarDistance at the minimum.
        const float NearDistance = 6f;
        const float FarDistance = 22f;
        const float FarVolume = 0.15f;

        const float MusicFadeSpeed = 2f;

        static AudioManager instance;

        SfxLibrary library;
        AudioSource[] pool;
        int nextSource;
        AudioSource music;
        AudioClip wantedMusic;
        float musicLevel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance != null)
                return;

            var host = new GameObject(nameof(AudioManager));
            DontDestroyOnLoad(host);
            instance = host.AddComponent<AudioManager>();
        }

        /// <summary>A sound that is not tied to a place (interface, match events).</summary>
        public static void Play(Sfx id)
        {
            if (instance != null)
                instance.PlayInternal(id, 1f);
        }

        /// <summary>A sound in the world: its volume falls off with the distance to the camera.</summary>
        public static void PlayAt(Sfx id, Vector2 position)
        {
            if (instance == null)
                return;

            Camera camera = Camera.main;
            float falloff = 1f;
            if (camera != null)
            {
                float distance = Vector2.Distance(camera.transform.position, position);
                falloff = Mathf.Lerp(1f, FarVolume, Mathf.InverseLerp(NearDistance, FarDistance, distance));
            }

            instance.PlayInternal(id, falloff);
        }

        void Awake()
        {
            library = Resources.Load<SfxLibrary>(LibraryResource);
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                pool[i] = source;
            }

            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = 0f;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this)
                instance = null;
        }

        void Start()
        {
            ChooseMusic(SceneManager.GetActiveScene());
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ChooseMusic(scene);
        }

        // The menu has its own track; every other scene is a battle.
        void ChooseMusic(Scene scene)
        {
            if (library == null)
                return;

            wantedMusic = scene.name == "Menu" ? library.menuMusic : library.battleMusic;
        }

        // The manager lives for the whole run, so it also writes the postponed settings to disk.
        void OnApplicationPause(bool paused)
        {
            if (paused)
                PlayerProgress.FlushIfDue(true);
        }

        void OnApplicationQuit()
        {
            PlayerProgress.FlushIfDue(true);
        }

        void Update()
        {
            PlayerProgress.FlushIfDue();

            if (library == null)
                return;

            // Fade out the old track, switch, fade in the new one.
            float target = wantedMusic != null ? library.musicBaseVolume * PlayerProgress.MusicVolume : 0f;
            if (music.clip != wantedMusic)
            {
                musicLevel = Mathf.MoveTowards(musicLevel, 0f, MusicFadeSpeed * Time.unscaledDeltaTime);
                if (musicLevel <= 0.001f)
                {
                    music.clip = wantedMusic;
                    if (wantedMusic != null)
                        music.Play();
                    else
                        music.Stop();
                }
            }
            else
            {
                musicLevel = Mathf.MoveTowards(musicLevel, target, MusicFadeSpeed * Time.unscaledDeltaTime);
            }

            music.volume = music.clip != null ? Mathf.Min(musicLevel, target) : 0f;
        }

        void PlayInternal(Sfx id, float scale)
        {
            if (library == null || id == Sfx.None)
                return;

            SfxLibrary.Entry entry = library.Find(id);
            if (entry == null || entry.clips == null || entry.clips.Length == 0)
                return;

            AudioClip clip = entry.clips[entry.clips.Length == 1 ? 0 : Random.Range(0, entry.clips.Length)];
            if (clip == null)
                return;

            AudioSource source = pool[nextSource];
            nextSource = (nextSource + 1) % PoolSize;
            source.pitch = Random.Range(entry.pitch.x, entry.pitch.y);
            source.PlayOneShot(clip, entry.volume * scale * PlayerProgress.SfxVolume);
        }
    }
}
