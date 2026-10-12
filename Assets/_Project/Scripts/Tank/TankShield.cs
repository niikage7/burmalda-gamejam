using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// The shield power-up: absorbs one hit (any damage above zero), or expires by itself.
    /// For the last <c>warningTime</c> seconds the ring blinks. <see cref="TankHealth"/> asks it before taking damage.
    /// The ring is a child object that is inactive while there is no shield.
    /// </summary>
    public class TankShield : MonoBehaviour
    {
        [Tooltip("Ring around the tank. A child object, inactive in the prefab.")]
        [SerializeField] GameObject ring;

        [SerializeField, Min(0.02f)] float blinkInterval = 0.15f;

        TankConcealment concealment;
        float endTime;
        float warningTime;

        public bool IsActive { get; private set; }

        public float TimeLeft => IsActive ? Mathf.Max(endTime - Time.time, 0f) : 0f;

        /// <summary>Raises the shield for <paramref name="duration"/> s (a second pick-up just refills the timer).</summary>
        public void Activate(float duration, float blinkTime)
        {
            IsActive = true;
            endTime = Time.time + duration;
            warningTime = blinkTime;
            SetRing(true);
        }

        /// <summary>Uses up the shield if it is up. Returns true when it took the hit.</summary>
        public bool TryAbsorb()
        {
            if (!IsActive)
                return false;

            Deactivate();
            return true;
        }

        public void Deactivate()
        {
            IsActive = false;
            SetRing(false);
        }

        void Awake()
        {
            TryGetComponent(out concealment);
            if (ring == null)
                Debug.LogError($"{name}: the shield ring is not assigned.", this);
        }

        void OnDisable()
        {
            Deactivate();
        }

        void Update()
        {
            if (!IsActive)
                return;

            float left = endTime - Time.time;
            if (left <= 0f)
            {
                Deactivate();
                return;
            }

            // The ring is not among the tank sprites that the bushes fade, so it hides itself there.
            bool hiddenByBush = concealment != null && concealment.InBush;
            bool blinkOn = left > warningTime || (int)(left / blinkInterval) % 2 == 0;
            SetRing(blinkOn && !hiddenByBush);
        }

        void SetRing(bool shown)
        {
            if (ring != null && ring.activeSelf != shown)
                ring.SetActive(shown);
        }
    }
}
