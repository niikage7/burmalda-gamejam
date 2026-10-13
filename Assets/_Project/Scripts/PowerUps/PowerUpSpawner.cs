using System.Collections.Generic;
using TankBrawl.Audio;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TankBrawl.PowerUps
{
    /// <summary>
    /// Puts power-ups on the map (balance v0.2, section 10): at most <see cref="maxOnMap"/> at a time, a new one every
    /// <c>clamp(min, max, base * sqrt(refArea / mapArea))</c> seconds, the first one 8-12 s after the start, at a free
    /// point out of <see cref="spawnPoints"/>. While the limit is reached the timer stands still. A pick-up nobody takes
    /// disappears after <see cref="pickupLifetime"/> and frees its place.
    /// </summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        [SerializeField] PowerUpPickup pickupPrefab;

        [Tooltip("What can appear. A kind that is already on the map is skipped while there is another to choose.")]
        [SerializeField] PowerUpConfig[] powerUps;

        [Tooltip("Also spawn repair and nitro refill (balance v0.2 §7). A kind without an asset in the list above uses the code defaults; put an asset of that kind in the list to tune it.")]
        [SerializeField] bool addInstantPowerUps = true;

        [Tooltip("Predefined safe places: not inside a wall, not on water.")]
        [SerializeField] Transform[] spawnPoints;

        [Header("Timing")]
        [Tooltip("Delay before the first power-up, s (random in the range). Balance v0.2: 8-12")]
        [SerializeField] Vector2 firstDelay = new Vector2(8f, 12f);

        [SerializeField, Min(1)] int maxOnMap = 2;

        [Tooltip("A pick-up nobody takes disappears after this long, s. 0 = stays until taken. Balance v0.2 §10 mentions disappearing without a number; proposal: 20")]
        [SerializeField, Min(0f)] float pickupLifetime = 20f;

        [Tooltip("Interval on the reference map, s. Balance v0.2: 25")]
        [SerializeField, Min(1f)] float baseInterval = 25f;

        [SerializeField, Min(1f)] float minInterval = 10f;
        [SerializeField, Min(1f)] float maxInterval = 40f;

        [Tooltip("Area of the reference map, units². Balance v0.2: 27 x 27")]
        [SerializeField, Min(1f)] float referenceArea = 729f;

        [Tooltip("Tilemap whose bounds give the map area (the border). Empty = the reference area, so the interval is the base one.")]
        [SerializeField] Tilemap areaSource;

        [Header("Placing")]
        [Tooltip("A point is free when no tank is closer than this, units.")]
        [SerializeField, Min(0f)] float clearRadius = 1.2f;

        [SerializeField] LayerMask tankLayers;

        [Tooltip("When every point is busy, try again after this many seconds.")]
        [SerializeField, Min(0.1f)] float retryDelay = 1f;

        readonly List<PowerUpPickup> onMap = new List<PowerUpPickup>();
        readonly List<Transform> freePoints = new List<Transform>();
        readonly List<PowerUpConfig> candidates = new List<PowerUpConfig>();
        readonly List<PowerUpConfig> pool = new List<PowerUpConfig>();
        float timeLeft;

        /// <summary>Seconds between power-ups on this map.</summary>
        public float Interval
        {
            get
            {
                float area = referenceArea;
                if (areaSource != null)
                {
                    BoundsInt bounds = areaSource.cellBounds;
                    Vector3 cell = areaSource.cellSize;
                    area = Mathf.Max(bounds.size.x * cell.x * bounds.size.y * cell.y, 1f);
                }

                return Mathf.Clamp(baseInterval * Mathf.Sqrt(referenceArea / area), minInterval, maxInterval);
            }
        }

        void Start()
        {
            timeLeft = Random.Range(firstDelay.x, firstDelay.y);
            BuildPool();
        }

        // The configured list plus default instant kinds that the list does not have.
        void BuildPool()
        {
            pool.Clear();
            if (powerUps != null)
            {
                for (int i = 0; i < powerUps.Length; i++)
                {
                    if (powerUps[i] != null)
                        pool.Add(powerUps[i]);
                }
            }

            if (!addInstantPowerUps)
                return;

            AddDefaultIfMissing(PowerUpKind.Repair);
            AddDefaultIfMissing(PowerUpKind.NitroRefill);
        }

        void AddDefaultIfMissing(PowerUpKind kind)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].kind == kind)
                    return;
            }

            pool.Add(PowerUpConfig.CreateDefault(kind));
        }

        void Update()
        {
            for (int i = onMap.Count - 1; i >= 0; i--)
            {
                if (onMap[i] == null)
                    onMap.RemoveAt(i);
            }

            // At the limit the countdown waits for a pick-up.
            if (onMap.Count >= maxOnMap)
                return;

            timeLeft -= Time.deltaTime;
            if (timeLeft > 0f)
                return;

            timeLeft = TrySpawn() ? Interval : retryDelay;
        }

        bool TrySpawn()
        {
            if (pickupPrefab == null || pool.Count == 0 || spawnPoints == null)
                return false;

            freePoints.Clear();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null && IsFree(spawnPoints[i].position))
                    freePoints.Add(spawnPoints[i]);
            }

            if (freePoints.Count == 0)
                return false;

            // Prefer a kind that is not lying on the map yet.
            candidates.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                if (!IsOnMap(pool[i]))
                    candidates.Add(pool[i]);
            }

            if (candidates.Count == 0)
                candidates.AddRange(pool);

            PowerUpConfig chosen = candidates[Random.Range(0, candidates.Count)];
            if (chosen == null)
                return false;

            Transform point = freePoints[Random.Range(0, freePoints.Count)];
            PowerUpPickup pickup = Instantiate(pickupPrefab, point.position, Quaternion.identity);
            pickup.Configure(chosen);
            pickup.ExpireAfter(pickupLifetime);
            onMap.Add(pickup);
            AudioManager.PlayAt(Sfx.PowerUpSpawn, point.position);
            return true;
        }

        /// <summary>Removes every power-up from the map and restarts the first-delay countdown (a new round starts).</summary>
        public void ResetSpawner()
        {
            for (int i = 0; i < onMap.Count; i++)
            {
                if (onMap[i] != null)
                    Destroy(onMap[i].gameObject);
            }

            onMap.Clear();
            timeLeft = Random.Range(firstDelay.x, firstDelay.y);
        }

        bool IsFree(Vector2 point)
        {
            for (int i = 0; i < onMap.Count; i++)
            {
                if (onMap[i] != null && ((Vector2)onMap[i].transform.position - point).sqrMagnitude < 0.01f)
                    return false;
            }

            return Physics2D.OverlapCircle(point, clearRadius, tankLayers) == null;
        }

        bool IsOnMap(PowerUpConfig config)
        {
            for (int i = 0; i < onMap.Count; i++)
            {
                if (onMap[i] != null && onMap[i].Config == config)
                    return true;
            }

            return false;
        }
    }
}
