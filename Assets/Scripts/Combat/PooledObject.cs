using UnityEngine;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PooledObject : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 8f;
        private PrefabPool owner;
        private Rigidbody2D body;
        private Collider2D[] colliders;
        private bool[] colliderEnabled;
        private SpriteRenderer[] visuals;
        private Color[] colors;
        private bool[] visualEnabled;
        private Vector3 originalScale;
        private Camera playAreaCamera;
        private float expiresAt;
        public bool IsSpawned { get; private set; }
        public event System.Action<PooledObject> Despawned;

        private void Awake() => Initialize();

        internal void Initialize()
        {
            if (body != null) return;
            body = GetComponent<Rigidbody2D>();
            colliders = GetComponentsInChildren<Collider2D>(true);
            colliderEnabled = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) colliderEnabled[i] = colliders[i].enabled;
            visuals = GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[visuals.Length];
            visualEnabled = new bool[visuals.Length];
            for (int i = 0; i < visuals.Length; i++)
            {
                colors[i] = visuals[i].color;
                visualEnabled[i] = visuals[i].enabled;
            }
            originalScale = transform.localScale;
        }

        internal void SetOwner(PrefabPool pool) => owner = pool;

        internal void Spawn(Vector3 position, Quaternion rotation, Vector2 velocity)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = originalScale;
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = colliderEnabled[i];
            for (int i = 0; i < visuals.Length; i++)
            {
                visuals[i].color = colors[i];
                visuals[i].enabled = visualEnabled[i];
            }
            expiresAt = Time.time + lifetime;
            playAreaCamera = Camera.main;
            IsSpawned = true;
            gameObject.SetActive(true);
            // Reactivating a Rigidbody2D can reset its physics state; apply motion afterwards.
            body.simulated = true;
            body.position = position;
            body.rotation = rotation.eulerAngles.z;
            body.linearVelocity = velocity;
            body.angularVelocity = 0f;
            body.WakeUp();
        }

        internal void ResetForPool()
        {
            IsSpawned = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            gameObject.SetActive(false);
            Despawned?.Invoke(this);
            Despawned = null;
        }

        private void OnDestroy()
        {
            if (IsSpawned) Despawned?.Invoke(this);
            Despawned = null;
        }

        public void Despawn()
        {
            if (!IsSpawned)
            {
                // A manually placed test instance has no pool; still stop its collisions immediately.
                if (owner == null && gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }
            IsSpawned = false;
            owner.Return(this);
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (Time.time >= expiresAt) { Despawn(); return; }
            if (playAreaCamera == null) return;
            Vector3 point = playAreaCamera.WorldToViewportPoint(transform.position);
            if (point.z <= 0f || point.x < -0.2f || point.x > 1.2f || point.y < -0.2f || point.y > 1.2f)
                Despawn();
        }
    }
}
