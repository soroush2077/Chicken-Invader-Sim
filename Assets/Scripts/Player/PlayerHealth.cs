using System;
using UnityEngine;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private SpriteRenderer shipVisual;
        [SerializeField, Min(0.1f)] private float invulnerabilitySeconds = 1.5f;
        [SerializeField, Min(0.01f)] private float blinkInterval = 0.1f;
        private GameManager manager;
        private Color normalColor;
        private float invulnerableUntil;

        private void Awake()
        {
            if (shipVisual == null) shipVisual = GetComponentInChildren<SpriteRenderer>();
            if (shipVisual == null)
            {
                Debug.LogError("PlayerHealth needs the ship SpriteRenderer.", this);
                enabled = false;
                return;
            }
            normalColor = shipVisual.color;
        }

        private void OnEnable()
        {
            manager = GameManager.Instance;
            if (manager == null)
            {
                Debug.LogError("Place an active GameManager in this scene before testing PlayerHealth.", this);
                enabled = false;
                return;
            }
            manager.RunStarted += ResetDamageState;
            ResetDamageState();
        }

        private void OnDisable()
        {
            if (manager != null) manager.RunStarted -= ResetDamageState;
            if (shipVisual != null) shipVisual.color = normalColor;
        }

        private void Update()
        {
            Color color = normalColor;
            if (Time.time < invulnerableUntil &&
                Mathf.FloorToInt((invulnerableUntil - Time.time) / blinkInterval) % 2 == 0)
                color.a *= 0.2f;
            shipVisual.color = color;
        }

        public bool TakeDamage(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!isActiveAndEnabled || manager == null || manager.State.IsGameOver ||
                Time.timeScale == 0f || Time.time < invulnerableUntil) return false;

            // Set the guard before state events can trigger another collision or damage call.
            invulnerableUntil = Time.time + invulnerabilitySeconds;
            return manager.TakePlayerHit();
        }

        private void ResetDamageState()
        {
            invulnerableUntil = 0f;
            if (shipVisual != null) shipVisual.color = normalColor;
        }

#if UNITY_EDITOR
        [ContextMenu("Test One Hit (Play Mode)")]
        private void TestOneHit()
        {
            if (Application.isPlaying) TakeDamage(1);
            else Debug.Log("Enter Play Mode first.", this);
        }
#endif
    }
}
