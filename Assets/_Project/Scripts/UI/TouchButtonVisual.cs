using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// Visual feedback for an on-screen button: shrinks and brightens while pressed.
    /// Lives next to OnScreenButton; both receive the same pointer events.
    /// </summary>
    public class TouchButtonVisual : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] Graphic target;
        [SerializeField, Range(0.5f, 1f)] float pressedScale = 0.92f;
        [SerializeField, Range(0f, 1f)] float pressedAlpha = 0.9f;

        Color idleColor;
        Vector3 idleScale;

        void Awake()
        {
            if (target == null)
                target = GetComponent<Graphic>();
            idleColor = target.color;
            idleScale = transform.localScale;
        }

        void OnDisable()
        {
            Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Color color = idleColor;
            color.a = pressedAlpha;
            target.color = color;
            transform.localScale = idleScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        void Release()
        {
            if (target == null)
                return;
            target.color = idleColor;
            transform.localScale = idleScale;
        }
    }
}
