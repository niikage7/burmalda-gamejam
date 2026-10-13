using System;
using TankBrawl.Audio;
using TankBrawl.Tank;
using TankBrawl.Weapons;
using UnityEngine;

namespace TankBrawl.PowerUps
{
    /// <summary>
    /// The effects that are on a tank right now. Pick-ups call <see cref="Apply"/>; a repeated pick-up of the same kind
    /// refills its timer instead of adding to it. Effects of different kinds work together. The instant kinds (repair,
    /// nitro refill) act at once through <see cref="TankHealth.Heal"/> / <see cref="TankNitro.Refill"/> and keep no timer.
    /// While an effect is on, a ring in the colour of the latest one pulses under the tank (everyone sees it; an enemy in a
    /// bush hides it).
    /// The effects themselves live in the tank's own components: <see cref="TankWeapon"/> (fire rate, projectile type,
    /// muting the gun), <see cref="TankLaser"/>, <see cref="TankShield"/> and <see cref="TankController.SpeedMultiplier"/>.
    /// </summary>
    [RequireComponent(typeof(TankWeapon), typeof(TankController), typeof(TankShield))]
    public class TankPowerUps : MonoBehaviour
    {
        static readonly int KindCount = Enum.GetValues(typeof(PowerUpKind)).Length;

        // The ring lies under the tank's own sprites (sorting orders 9-14).
        const int MarkerOrder = 8;
        const float MarkerDiameter = 1.15f;
        const int MarkerTextureSize = 64;

        static Sprite ringSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ringSprite = null;
        }

        TankWeapon weapon;
        TankController tank;
        TankShield shield;
        TankLaser laser; // optional: a tank without the laser component just ignores the laser power-up
        TankHealth health;
        TankNitro nitro;
        TankConcealment concealment;
        SpriteRenderer marker;

        // Indexed by PowerUpKind. The shield keeps its own timer (it can also end by absorbing a hit).
        PowerUpConfig[] configs;
        float[] endTimes;
        float[] appliedAt;

        /// <summary>Raised when an effect starts or ends.</summary>
        public event Action Changed;

        public bool IsActive(PowerUpKind kind)
        {
            int i = (int)kind;
            if (configs == null || configs[i] == null)
                return false;

            return kind == PowerUpKind.Shield ? shield.IsActive : Time.time < endTimes[i];
        }

        /// <summary>
        /// For the HUD: the effect picked up last among those still on. Returns false when there is none.
        /// </summary>
        public bool TryGetLatest(out string effectName, out float secondsLeft)
        {
            effectName = null;
            secondsLeft = 0f;
            if (configs == null)
                return false;

            int latest = LatestActive();
            if (latest < 0)
                return false;

            effectName = configs[latest].displayName;
            secondsLeft = latest == (int)PowerUpKind.Shield ? shield.TimeLeft : endTimes[latest] - Time.time;
            return true;
        }

        public void Apply(PowerUpConfig config)
        {
            if (config == null)
                return;

            if (config.IsInstant)
            {
                ApplyInstant(config);
                return;
            }

            int i = (int)config.kind;
            configs[i] = config;
            endTimes[i] = Time.time + config.duration;
            appliedAt[i] = Time.time;

            if (config.kind == PowerUpKind.Shield)
            {
                shield.Activate(config.duration, config.shieldBlinkTime);
                AudioManager.PlayAt(Sfx.ShieldUp, transform.position);
            }
            else if (config.kind == PowerUpKind.Laser)
            {
                AudioManager.PlayAt(Sfx.LaserStart, transform.position);
            }

#if UNITY_EDITOR
            Debug.Log($"{name}: power-up {config.displayName} for {config.duration} s", this);
#endif

            Refresh();
        }

        void Awake()
        {
            weapon = GetComponent<TankWeapon>();
            tank = GetComponent<TankController>();
            shield = GetComponent<TankShield>();
            TryGetComponent(out laser);
            TryGetComponent(out health);
            TryGetComponent(out nitro);
            TryGetComponent(out concealment);

            configs = new PowerUpConfig[KindCount];
            endTimes = new float[KindCount];
            appliedAt = new float[KindCount];
        }

        void ApplyInstant(PowerUpConfig config)
        {
            if (config.kind == PowerUpKind.Repair && health != null)
                health.Heal(config.repairAmount);
            else if (config.kind == PowerUpKind.NitroRefill && nitro != null)
                nitro.Refill();

#if UNITY_EDITOR
            Debug.Log($"{name}: power-up {config.displayName}", this);
#endif
        }

        // Start, not Awake: TankHealth and TankConcealment collect the tank's sprites in Awake, and the ring must not be one
        // of them (it would blink with hits and take the bush alpha).
        void Start()
        {
            var host = new GameObject("PowerUpMarker");
            host.transform.SetParent(transform, false);
            host.transform.localScale = Vector3.one * MarkerDiameter;
            marker = host.AddComponent<SpriteRenderer>();
            marker.sprite = RingSprite();
            marker.sortingOrder = MarkerOrder;
            marker.enabled = false;
        }

        void OnDisable()
        {
            // The components the effects act on reset themselves when the tank is deactivated.
            Array.Clear(configs, 0, configs.Length);
            if (marker != null)
                marker.enabled = false;
        }

        void Update()
        {
            bool ended = false;
            for (int i = 0; i < KindCount; i++)
            {
                if (configs[i] == null || IsActive((PowerUpKind)i))
                    continue;

                configs[i] = null;
                ended = true;
            }

            if (ended)
                Refresh();

            UpdateMarker();
        }

        int LatestActive()
        {
            int latest = -1;
            for (int i = 0; i < KindCount; i++)
            {
                if (IsActive((PowerUpKind)i) && (latest < 0 || appliedAt[i] > appliedAt[latest]))
                    latest = i;
            }

            return latest;
        }

        void UpdateMarker()
        {
            if (marker == null)
                return;

            int latest = LatestActive();
            float visibility = concealment != null ? concealment.VisibilityAlpha : 1f;
            bool shown = latest >= 0 && visibility > 0.001f;
            if (marker.enabled != shown)
                marker.enabled = shown;
            if (!shown)
                return;

            Color color = configs[latest].pickupColor;
            color.a = (0.6f + 0.25f * Mathf.Sin(Time.time * 6f)) * visibility;
            marker.color = color;
        }

        // A thin soft ring generated once, so the marker needs no art file (placeholder until real art).
        static Sprite RingSprite()
        {
            if (ringSprite != null)
                return ringSprite;

            const int size = MarkerTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f) + (y - half + 0.5f) * (y - half + 0.5f)) / half;
                    float alpha = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.85f) / 0.12f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return ringSprite;
        }

        void Refresh()
        {
            PowerUpConfig rapid = IsActive(PowerUpKind.RapidFire) ? configs[(int)PowerUpKind.RapidFire] : null;
            PowerUpConfig explosive = IsActive(PowerUpKind.ExplosiveShells) ? configs[(int)PowerUpKind.ExplosiveShells] : null;
            PowerUpConfig beam = IsActive(PowerUpKind.Laser) ? configs[(int)PowerUpKind.Laser] : null;

            weapon.FireIntervalScale = rapid != null ? rapid.fireIntervalScale : 1f;
            weapon.SetOverride(explosive != null ? explosive.explosiveWeapon : null);
            weapon.Suppressed = beam != null;
            tank.SpeedMultiplier = beam != null ? beam.laserSpeedMultiplier : 1f;

            if (laser != null)
            {
                if (beam != null)
                    laser.Begin(beam.laserDamage, beam.laserInterval, beam.laserRange, beam.laserBeamRadius);
                else if (laser.IsOn)
                    laser.End();
            }

            Changed?.Invoke();
        }
    }
}
