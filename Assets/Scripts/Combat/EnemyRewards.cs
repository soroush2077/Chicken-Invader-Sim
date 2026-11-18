using UnityEngine;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyRewards : MonoBehaviour
    {
        [SerializeField, Min(0)] private int score = 100;
        [SerializeField, Range(0f, 1f)] private float giftChance = 0.15f;
        [SerializeField] private WeaponGift giftPrefab;
        private Health health;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (giftChance > 0f && giftPrefab == null)
                Debug.LogWarning("Assign a WeaponGift prefab to enable gift drops from this enemy.", this);
        }

        private void OnEnable() => health.Died += GrantReward;
        private void OnDisable() => health.Died -= GrantReward;

        private void GrantReward()
        {
            var manager = GameManager.Instance;
            if (manager == null || manager.State.IsGameOver) return;
            manager.AddScore(score);
            if (giftPrefab != null && Random.value < giftChance)
                Instantiate(giftPrefab, transform.position, Quaternion.identity);
        }
    }
}
