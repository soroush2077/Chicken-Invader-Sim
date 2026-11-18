using UnityEngine;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class HitFlash : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float duration = 0.12f;
        [SerializeField] private Color flashTint = new Color(1f, 0.25f, 0.25f, 0.4f);
        private Health health;
        private SpriteRenderer[] visuals;
        private Color[] normalColors;
        private float flashUntil;

        private void Awake()
        {
            health = GetComponent<Health>();
            visuals = GetComponentsInChildren<SpriteRenderer>(true);
            normalColors = new Color[visuals.Length];
            for (int i = 0; i < visuals.Length; i++) normalColors[i] = visuals[i].color;
        }

        private void OnEnable()
        {
            RestoreColors();
            health.Damaged += Flash;
        }

        private void OnDisable()
        {
            health.Damaged -= Flash;
            RestoreColors();
        }

        private void Flash()
        {
            flashUntil = Time.time + duration;
            for (int i = 0; i < visuals.Length; i++)
                visuals[i].color = new Color(flashTint.r, flashTint.g, flashTint.b, normalColors[i].a * flashTint.a);
        }

        private void Update()
        {
            if (flashUntil > 0f && Time.time >= flashUntil) RestoreColors();
        }

        private void RestoreColors()
        {
            flashUntil = 0f;
            for (int i = 0; i < visuals.Length; i++)
                if (visuals[i] != null) visuals[i].color = normalColors[i];
        }

#if UNITY_EDITOR
        [ContextMenu("Check Flash Reset (Play Mode)")]
        private void CheckFlashReset()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                Debug.Log("Run this check on an active instance during Play Mode.", this);
                return;
            }
            Flash();
            for (int i = 0; i < visuals.Length; i++)
                Debug.Assert(Mathf.Approximately(visuals[i].color.a, normalColors[i].a * flashTint.a));
            RestoreColors();
            Debug.Assert(flashUntil == 0f);
            for (int i = 0; i < visuals.Length; i++) Debug.Assert(visuals[i].color == normalColors[i]);
            Debug.Log("Hit-flash reset checks completed; inspect the Console for assertion failures.", this);
        }
#endif
    }
}
