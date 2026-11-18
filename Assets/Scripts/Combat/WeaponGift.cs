using UnityEngine;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class WeaponGift : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float fallSpeed = 2f;
        [SerializeField, Min(0.1f)] private float lifetime = 12f;
        private bool collected;
        private float expiresAt;

        private void OnEnable()
        {
            collected = false;
            expiresAt = Time.time + lifetime;
            GetComponent<Rigidbody2D>().linearVelocity = Vector2.down * fallSpeed;
        }

        private void Update()
        {
            if (Time.time >= expiresAt) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other) => Collect(other);
        private void OnTriggerStay2D(Collider2D other) => Collect(other);

        private void Collect(Collider2D other)
        {
            if (collected || !isActiveAndEnabled || other.GetComponentInParent<PlayerHealth>() == null) return;
            var manager = GameManager.Instance;
            if (manager == null || manager.State.IsGameOver || Time.timeScale == 0f) return;
            collected = true;
            manager.UpgradeWeapon();
            Destroy(gameObject);
        }
    }
}
