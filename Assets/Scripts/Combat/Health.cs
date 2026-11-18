using System;
using UnityEngine;

namespace ChickenInvader
{
    public enum BombResponse { Ignore, InstantKill, PercentageDamage }

    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Tooltip("Maximum HP for this prefab. Restored whenever this object is enabled.")]
        [SerializeField, Min(1)] private int maxHealth = 1;
        [SerializeField] private BombResponse bombResponse = BombResponse.Ignore;
        [SerializeField, Range(0.15f, 0.2f)] private float bombDamageFraction = 0.2f;
        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth == 0;
        public event Action Died;
        public event Action Damaged;

        public void ApplyBomb()
        {
            if (bombResponse == BombResponse.InstantKill) TakeDamage(int.MaxValue);
            else if (bombResponse == BombResponse.PercentageDamage)
                TakeDamage(Mathf.Max(1, Mathf.CeilToInt(maxHealth * bombDamageFraction)));
        }

        private void OnEnable() => CurrentHealth = Mathf.Max(1, maxHealth);

        private void OnDisable()
        {
            // Spawn-specific listeners must not survive a pooled object's next life.
            Died = null;
            Damaged = null;
        }

        public bool TakeDamage(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!isActiveAndEnabled || IsDead) return false;
            if (GameManager.Instance != null && GameManager.Instance.State.IsGameOver) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            Damaged?.Invoke();
            if (!IsDead) return true;

            Died?.Invoke();
            var pooled = GetComponent<PooledObject>();
            if (pooled != null) pooled.Despawn();
            else Destroy(gameObject);
            return true;
        }
    }
}
