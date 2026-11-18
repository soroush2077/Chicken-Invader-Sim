using UnityEngine;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    public sealed class ContactDamage : MonoBehaviour
    {
        [SerializeField] private LayerMask targetLayers;
        [Tooltip("Damage per hit, configured only on this prefab.")]
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField] private bool consumeOnContact;
        private bool consumed;
        private Health health;

        private void Awake()
        {
            health = GetComponent<Health>();
            var hitbox = GetComponent<Collider2D>();
            if (hitbox == null || !hitbox.isTrigger)
            {
                Debug.LogError("ContactDamage needs a trigger Collider2D on the same object.", this);
                enabled = false;
            }
        }
        private void OnEnable() => consumed = false;
        private void OnTriggerEnter2D(Collider2D other) => Hit(other);
        private void OnTriggerStay2D(Collider2D other) => Hit(other);

        private void Hit(Collider2D other)
        {
            if (!isActiveAndEnabled || consumed || (health != null && health.IsDead) ||
                (targetLayers.value & (1 << other.gameObject.layer)) == 0) return;
            if (GameManager.Instance != null && GameManager.Instance.State.IsGameOver) return;
            var target = other.GetComponentInParent<IDamageable>();
            if (target == null) return;

            // Eggs/shots are consumed even when the target's invulnerability rejects the hit.
            if (consumeOnContact) consumed = true;
            target.TakeDamage(damage);
            if (!consumeOnContact) return;
            var pooled = GetComponent<PooledObject>();
            if (pooled != null) pooled.Despawn();
            else Destroy(gameObject);
        }
    }
}
