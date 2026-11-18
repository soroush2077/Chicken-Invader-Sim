using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChickenInvader
{
    public sealed class AsteroidSpawner : MonoBehaviour
    {
        [Tooltip("One pool per meteor prefab. Each spawn randomly selects a pool with equal probability.")]
        [SerializeField] private PrefabPool[] asteroidPools = Array.Empty<PrefabPool>();
        private readonly HashSet<PooledObject> living = new HashSet<PooledObject>();
        private Coroutine spawnRoutine;
        public bool IsSpawning { get; private set; }
        public int AliveCount => living.Count;
        public bool IsComplete => !IsSpawning && living.Count == 0;

        public void SpawnWave(WaveDataSO wave)
        {
            if (!isActiveAndEnabled || !IsComplete)
                throw new InvalidOperationException("The asteroid spawner must be active with its previous wave resolved.");
            Camera camera = Camera.main;
            if (wave == null || wave.Kind != WaveKind.Asteroids || asteroidPools == null || asteroidPools.Length == 0 ||
                camera == null || !camera.orthographic || wave.Count < 1 || wave.MoveSpeed <= 0f || wave.SpawnInterval < 0f ||
                !Enum.IsDefined(typeof(AsteroidTravel), wave.AsteroidDirection))
                throw new InvalidOperationException("Check asteroid wave data, Asteroid Pools list, and MainCamera.");
            foreach (var pool in asteroidPools)
            {
                if (pool == null || !pool.isActiveAndEnabled || pool.Prefab == null || pool.Prefab.GetComponent<Health>() == null)
                    throw new InvalidOperationException("Each asteroid pool needs an active PrefabPool and a meteor prefab with Health.");
                var body = pool.Prefab.GetComponent<Rigidbody2D>();
                if (body == null || body.bodyType != RigidbodyType2D.Kinematic || !body.simulated ||
                    body.constraints.HasFlag(RigidbodyConstraints2D.FreezePositionY) ||
                    (wave.AsteroidDirection != AsteroidTravel.TopToBottom && body.constraints.HasFlag(RigidbodyConstraints2D.FreezePositionX)))
                    throw new InvalidOperationException("Each meteor prefab needs a simulated Kinematic Rigidbody2D with its travel axes unfrozen.");
            }
            IsSpawning = true;
            spawnRoutine = StartCoroutine(Spawn(wave, camera));
        }

        private IEnumerator Spawn(WaveDataSO wave, Camera camera)
        {
            for (int i = 0; i < wave.Count; i++)
            {
                float depth = -camera.transform.position.z;
                Vector3 right = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
                Vector3 bottom = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
                Vector2 minimum = bottom;
                Vector2 maximum = right;
                Vector2 size = maximum - minimum;
                float padding = Mathf.Min(0.8f, Mathf.Min(size.x, size.y) * 0.1f);
                Vector2 position = SpawnPosition(wave.AsteroidDirection, minimum, maximum, padding,
                    UnityEngine.Random.value, UnityEngine.Random.value);
                Vector2 velocity = TravelDirection(wave.AsteroidDirection, size) * wave.MoveSpeed;
                var pool = asteroidPools[UnityEngine.Random.Range(0, asteroidPools.Length)];
                var asteroid = pool.Spawn(position, Quaternion.identity, velocity);
                living.Add(asteroid);
                asteroid.Despawned += OnDespawned;
                if (i < wave.Count - 1) yield return new WaitForSeconds(wave.SpawnInterval);
            }
            IsSpawning = false;
            spawnRoutine = null;
        }

        private void OnDespawned(PooledObject asteroid) => living.Remove(asteroid);
        private void OnDisable() => StopWave();

#if UNITY_EDITOR
        [ContextMenu("Check All Meteor Pools (Play Mode)")]
        private void CheckMeteorPools()
        {
            if (!Application.isPlaying || asteroidPools == null || asteroidPools.Length == 0)
            {
                Debug.Log("Enter Play Mode and assign Asteroid Pools first.", this);
                return;
            }
            foreach (var pool in asteroidPools)
            {
                if (pool == null || pool.Prefab == null || pool.Prefab.GetComponent<Health>() == null)
                    throw new InvalidOperationException("Assign each pool and its meteor prefab with Health.");
                var asteroid = pool.Spawn(Vector3.zero, Quaternion.identity, Vector2.down);
                Debug.Assert(asteroid.GetComponent<Health>().CurrentHealth == pool.Prefab.GetComponent<Health>().MaxHealth,
                    "Meteor health must come only from its prefab.");
                Debug.Assert(asteroid.GetComponent<Rigidbody2D>().linearVelocity == Vector2.down,
                    "Each meteor pool must apply spawn velocity.");
                asteroid.Despawn();
            }
            Debug.Log("Meteor pool checks completed; inspect the Console for assertion failures.", this);
        }
#endif

        public static Vector2 TravelDirection(AsteroidTravel travel, Vector2 viewSize)
        {
            if (viewSize.x <= 0f || viewSize.y <= 0f) throw new ArgumentOutOfRangeException(nameof(viewSize));
            switch (travel)
            {
                case AsteroidTravel.TopToBottom: return Vector2.down;
                case AsteroidTravel.TopLeftToBottomRight: return new Vector2(viewSize.x, -viewSize.y).normalized;
                case AsteroidTravel.TopRightToBottomLeft: return new Vector2(-viewSize.x, -viewSize.y).normalized;
                default: throw new ArgumentOutOfRangeException(nameof(travel));
            }
        }

        public static Vector2 SpawnPosition(AsteroidTravel travel, Vector2 minimum, Vector2 maximum,
            float padding, float edgeChoice, float alongEdge)
        {
            Vector2 center = (minimum + maximum) * 0.5f;
            switch (travel)
            {
                case AsteroidTravel.TopToBottom:
                    return new Vector2(Mathf.Lerp(minimum.x + padding, maximum.x - padding, alongEdge), maximum.y + padding);
                case AsteroidTravel.TopLeftToBottomRight:
                    return edgeChoice < 0.5f
                        ? new Vector2(Mathf.Lerp(minimum.x + padding, center.x, alongEdge), maximum.y + padding)
                        : new Vector2(minimum.x - padding, Mathf.Lerp(center.y, maximum.y - padding, alongEdge));
                case AsteroidTravel.TopRightToBottomLeft:
                    return edgeChoice < 0.5f
                        ? new Vector2(Mathf.Lerp(center.x, maximum.x - padding, alongEdge), maximum.y + padding)
                        : new Vector2(maximum.x + padding, Mathf.Lerp(center.y, maximum.y - padding, alongEdge));
                default: throw new ArgumentOutOfRangeException(nameof(travel));
            }
        }

        public void StopWave()
        {
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            spawnRoutine = null;
            IsSpawning = false;
            foreach (var asteroid in living)
            {
                if (asteroid == null) continue;
                asteroid.Despawned -= OnDespawned;
                asteroid.Despawn();
            }
            living.Clear();
        }
    }
}
