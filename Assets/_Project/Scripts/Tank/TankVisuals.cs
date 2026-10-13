using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// Puts a <see cref="TankSkin"/> on the tank's sprites, animates the tracks and keeps the shadow in place.
    /// The track animation follows the distance each track actually travels, measured from how the tank moved
    /// this frame: driving and nitro speed it up, a hull turning on the spot runs the tracks in opposite directions,
    /// a stopped tank holds its frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankVisuals : MonoBehaviour
    {
        [SerializeField] TankSkin skin;

        [Header("Parts")]
        [SerializeField] SpriteRenderer shadowRenderer;
        [SerializeField] SpriteRenderer trackLeft;
        [SerializeField] SpriteRenderer trackRight;
        [SerializeField] SpriteRenderer hull;
        [SerializeField] SpriteRenderer turret;
        [SerializeField] SpriteRenderer barrel;

        [Header("Tracks")]
        [Tooltip("Distance a track travels before it shows the next frame, units. Lower = faster animation.")]
        [SerializeField, Min(0.02f)] float trackStride = 0.25f;

        [Tooltip("A step faster than this is a teleport (respawn), not driving: the tracks ignore it.")]
        [SerializeField, Min(1f)] float maxPlausibleSpeed = 30f;

        float leftPhase;
        float rightPhase;
        Vector2 lastPosition;
        float lastAngle;
        bool hasLast;

        public TankSkin Skin => skin;

        public int LeftFrame => FrameOf(leftPhase, skin != null && skin.trackLeft != null ? skin.trackLeft.Length : 0);

        public int RightFrame => FrameOf(rightPhase, skin != null && skin.trackRight != null ? skin.trackRight.Length : 0);

        void Awake()
        {
            ApplySkin();
        }

        void OnEnable()
        {
            hasLast = false;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector2 position = transform.position;
            float angle = transform.eulerAngles.z;

            if (hasLast && dt > 0f)
            {
                float forwardSpeed = Vector2.Dot(position - lastPosition, transform.up) / dt;
                float turnRate = Mathf.DeltaAngle(lastAngle, angle) * Mathf.Deg2Rad / dt;
                Advance(forwardSpeed, turnRate, dt);
            }

            lastPosition = position;
            lastAngle = angle;
            hasLast = true;
            PlaceShadow();
        }

        /// <summary>
        /// Moves both tracks. <paramref name="forwardSpeed"/> is the hull speed along its heading (units/s),
        /// <paramref name="turnRate"/> the turn rate (rad/s, counter-clockwise positive).
        /// </summary>
        public void Advance(float forwardSpeed, float turnRate, float dt)
        {
            if (Mathf.Abs(forwardSpeed) > maxPlausibleSpeed)
                return;

            float half = skin != null ? skin.trackHalfSpacing : 0f;
            leftPhase += Mathf.Abs(forwardSpeed - turnRate * half) * dt / trackStride;
            rightPhase += Mathf.Abs(forwardSpeed + turnRate * half) * dt / trackStride;

            // 2520 = lcm(1..9): wrapping keeps the float precise on long sessions and never changes the frame.
            leftPhase %= 2520f;
            rightPhase %= 2520f;
            ShowTrackFrames();
        }

        /// <summary>Switches to another skin (the deathmatch gives every tank its own colour).</summary>
        public void SetSkin(TankSkin newSkin)
        {
            if (newSkin == null || newSkin == skin)
                return;

            skin = newSkin;
            ApplySkin();
        }

        /// <summary>Puts the skin's sprites on the part renderers. Also available from the component's context menu.</summary>
        [ContextMenu("Apply Skin")]
        public void ApplySkin()
        {
            if (skin == null)
                return;

            SetSprite(shadowRenderer, skin.shadow);
            SetSprite(hull, skin.body);
            SetSprite(turret, skin.turret);
            SetSprite(barrel, skin.barrel);
            ShowTrackFrames();
        }

        void ShowTrackFrames()
        {
            if (skin == null)
                return;

            SetSprite(trackLeft, Pick(skin.trackLeft, LeftFrame));
            SetSprite(trackRight, Pick(skin.trackRight, RightFrame));
        }

        void PlaceShadow()
        {
            if (shadowRenderer == null || skin == null)
                return;

            Transform shadow = shadowRenderer.transform;
            Vector3 position = transform.position + (Vector3)skin.shadowOffset;
            position.z = shadow.position.z;
            shadow.SetPositionAndRotation(position, transform.rotation);
        }

        static int FrameOf(float phase, int count)
        {
            return count > 0 ? (int)phase % count : 0;
        }

        static Sprite Pick(Sprite[] frames, int index)
        {
            return frames != null && frames.Length > 0 ? frames[index] : null;
        }

        static void SetSprite(SpriteRenderer target, Sprite sprite)
        {
            if (target != null && sprite != null && target.sprite != sprite)
                target.sprite = sprite;
        }
    }
}
