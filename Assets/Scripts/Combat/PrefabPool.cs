using UnityEngine;
using UnityEngine.Pool;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    public sealed class PrefabPool : MonoBehaviour
    {
        [SerializeField] private PooledObject prefab;
        [SerializeField, Min(1)] private int maxStored = 100;
        private ObjectPool<PooledObject> pool;
        public GameObject Prefab => prefab != null ? prefab.gameObject : null;

        private void Awake()
        {
            if (prefab == null)
            {
                Debug.LogError("Assign a prefab with PooledObject to this pool.", this);
                enabled = false;
                return;
            }
            pool = new ObjectPool<PooledObject>(CreateInstance, null,
                item => item.ResetForPool(), item => { if (item != null) Destroy(item.gameObject); },
                collectionCheck: true, defaultCapacity: 10, maxSize: Mathf.Max(1, maxStored));
        }

        private PooledObject CreateInstance()
        {
            var item = Instantiate(prefab, transform);
            item.Initialize();
            item.SetOwner(this);
            item.gameObject.SetActive(false);
            return item;
        }

        public PooledObject Spawn(Vector3 position, Quaternion rotation, Vector2 velocity)
        {
            if (pool == null) throw new System.InvalidOperationException("The prefab pool is not configured.");
            var item = pool.Get();
            item.Spawn(position, rotation, velocity);
            return item;
        }

        internal void Return(PooledObject item) => pool.Release(item);
        private void OnDestroy() => pool?.Clear();

        public void DespawnAll()
        {
            foreach (var item in GetComponentsInChildren<PooledObject>())
                if (item.IsSpawned) item.Despawn();
        }

#if UNITY_EDITOR
        [ContextMenu("Spawn Stationary Test Object (Play Mode)")]
        private void SpawnTestObject()
        {
            if (Application.isPlaying) Spawn(transform.position, Quaternion.identity, Vector2.zero);
            else Debug.Log("Enter Play Mode first.", this);
        }

        [ContextMenu("Check Pool Reuse (Play Mode)")]
        private void CheckPoolReuse()
        {
            if (!Application.isPlaying || pool == null ||
                (GameManager.Instance != null && GameManager.Instance.State.IsGameOver))
            {
                Debug.Log("Run this check during a live Play Mode session with a configured pool.", this);
                return;
            }
            var first = Spawn(Vector3.zero, Quaternion.identity, Vector2.one);
            int returns = 0;
            first.Despawned += item => returns++;
            Debug.Assert(first.GetComponent<Rigidbody2D>().linearVelocity == Vector2.one,
                "Spawn velocity must survive activation of a pooled Rigidbody2D.");
            var health = first.GetComponent<Health>();
            int deaths = 0;
            if (health != null)
            {
                health.Died += () => deaths++;
                health.TakeDamage(int.MaxValue);
                Debug.Assert(!health.TakeDamage(1) && deaths == 1, "Death must resolve once.");
            }
            first.Despawn();
            first.Despawn();
            Debug.Assert(returns == 1, "A pooled instance must report its return once.");
            var second = Spawn(Vector3.zero, Quaternion.identity, Vector2.zero);
            Debug.Assert(first == second && second.IsSpawned, "Pool must reuse the returned instance.");
            Debug.Assert(second.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero,
                "Old velocity must not survive reuse.");
            if (health != null)
            {
                Debug.Assert(!health.IsDead && health.CurrentHealth > 0, "Health must reset on reuse.");
                health.TakeDamage(int.MaxValue);
                Debug.Assert(deaths == 1, "Old death listeners must not survive reuse.");
            }
            second.Despawn();
            Debug.Assert(returns == 1, "Old return listeners must not survive reuse.");
            Debug.Log("Pool checks completed; inspect the Console for assertion failures.", this);
        }
#endif
    }
}
