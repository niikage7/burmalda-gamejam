using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// Speed lines of the nitro: short thin white streaks that appear behind the hull while the nitro is on and fade within a
    /// fraction of a second, staying where they were born so they trail behind the tank. A fixed pool of sprites, nothing is
    /// allocated while playing. Added to the tank by <see cref="TankNitro"/>. Streaks of a tank hidden in a bush fade with it.
    /// </summary>
    public class NitroTrail : MonoBehaviour
    {
        const int PoolSize = 22;
        const float SpawnRate = 48f;
        const float Life = 0.22f;
        const float BehindDistance = 0.3f;
        const float SideSpread = 0.4f;
        const float MinLength = 0.45f;
        const float MaxLength = 0.95f;
        const float Thickness = 0.045f;
        const float PeakAlpha = 0.85f;
        const int SortingOrder = 8;

        static Sprite whiteSprite;

        sealed class Streak
        {
            public Transform Root;
            public SpriteRenderer Renderer;
            public float Born = -10f;
            public float Length;
        }

        readonly Streak[] pool = new Streak[PoolSize];
        TankNitro nitro;
        TankController tank;
        TankConcealment concealment;
        Transform holder;
        float spawnDebt;
        int next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            whiteSprite = null;
        }

        void Awake()
        {
            nitro = GetComponent<TankNitro>();
            tank = GetComponent<TankController>();
            TryGetComponent(out concealment);

            // Not a child of the tank: the streaks must stay in the world while the tank drives on.
            holder = new GameObject(name + " NitroStreaks").transform;
            for (int i = 0; i < PoolSize; i++)
            {
                var root = new GameObject("Streak");
                root.transform.SetParent(holder, false);
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = WhiteSprite();
                renderer.sortingOrder = SortingOrder;
                renderer.enabled = false;
                pool[i] = new Streak { Root = root.transform, Renderer = renderer };
            }
        }

        void OnDestroy()
        {
            if (holder != null)
                Destroy(holder.gameObject);
        }

        void LateUpdate()
        {
            float now = Time.time;
            float visibility = concealment != null ? concealment.VisibilityAlpha : 1f;

            if (nitro != null && nitro.IsActive && tank != null && tank.IsDriving && visibility > 0f)
            {
                spawnDebt += SpawnRate * Time.deltaTime;
                while (spawnDebt >= 1f)
                {
                    spawnDebt -= 1f;
                    Spawn(now);
                }
            }
            else
            {
                spawnDebt = 0f;
            }

            for (int i = 0; i < PoolSize; i++)
            {
                Streak streak = pool[i];
                if (!streak.Renderer.enabled)
                    continue;

                float t = (now - streak.Born) / Life;
                if (t >= 1f)
                {
                    streak.Renderer.enabled = false;
                    continue;
                }

                streak.Root.localScale = new Vector3(streak.Length * (1f - 0.5f * t), Thickness * (1f - 0.4f * t), 1f);
                streak.Renderer.color = new Color(1f, 1f, 1f, PeakAlpha * (1f - t) * visibility);
            }
        }

        void Spawn(float now)
        {
            Streak streak = pool[next];
            next = (next + 1) % PoolSize;

            Vector2 forward = tank.Forward;
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 position = (Vector2)transform.position - forward * BehindDistance + right * Random.Range(-SideSpread, SideSpread);
            float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

            streak.Born = now;
            streak.Length = Random.Range(MinLength, MaxLength);
            streak.Root.SetPositionAndRotation(new Vector3(position.x, position.y, 0f), Quaternion.Euler(0f, 0f, angle));
            streak.Root.localScale = new Vector3(streak.Length, Thickness, 1f);
            streak.Renderer.color = new Color(1f, 1f, 1f, PeakAlpha * visibilityNow());
            streak.Renderer.enabled = true;
        }

        float visibilityNow() => concealment != null ? concealment.VisibilityAlpha : 1f;

        static Sprite WhiteSprite()
        {
            if (whiteSprite != null)
                return whiteSprite;

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            whiteSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return whiteSprite;
        }
    }
}
