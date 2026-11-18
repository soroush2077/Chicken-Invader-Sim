using System;
using UnityEngine;

namespace ChickenInvader
{
    public enum ChickenState { Entering, Formation, Diving, Dead }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class ChickenController : MonoBehaviour
    {
        [SerializeField] private Transform eggPoint;
        [SerializeField] private ChickenState state;
        private Rigidbody2D body;
        private Health health;
        private WaveDataSO wave;
        private PrefabPool eggPool;
        private Transform player;
        private Camera playAreaCamera;
        private ChickenFormation formation;
        private SpriteRenderer visual;
        private Collider2D hitbox;
        private Vector2 slot;
        private Vector2 diveDirection;
        private float minimumEntryY;
        private float entryDuration;
        private float entryElapsed;
        private float nextEggAt;
        private float nextDiveAt;
        private float diveStartedAt;
        private bool initialized;
        private bool resolved;
        private bool appeared;

        public ChickenState State => state;
        internal bool HasEggPoint => eggPoint != null;
        internal Bounds OccupiedBounds
        {
            get
            {
                Bounds bounds = visual != null ? visual.bounds : new Bounds(transform.position, Vector3.zero);
                if (hitbox != null) bounds.Encapsulate(hitbox.bounds);
                return bounds;
            }
        }
        public event Action<ChickenController> Resolved;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            visual = GetComponentInChildren<SpriteRenderer>();
            hitbox = GetComponent<Collider2D>();
        }

        private void OnEnable() => health.Died += OnDeath;
        private void OnDisable() => health.Died -= OnDeath;
        private void OnDestroy() { if (initialized) Resolve(); }

        public void Initialize(WaveDataSO data, Vector2 formationSlot, PrefabPool projectiles,
            Transform target, Camera camera, ChickenFormation owner)
        {
            if (initialized) throw new InvalidOperationException("A chicken can only be initialized once.");
            if (eggPoint == null || body.bodyType != RigidbodyType2D.Kinematic)
                throw new InvalidOperationException("ChickenController needs EggPoint and a Kinematic Rigidbody2D.");
            wave = data;
            slot = formationSlot;
            eggPool = projectiles;
            player = target;
            playAreaCamera = camera;
            formation = owner;
            initialized = true;
            state = ChickenState.Entering;

            float depth = transform.position.z - camera.transform.position.z;
            minimumEntryY = camera.ViewportToWorldPoint(new Vector3(0f, 1f, depth)).y + 1f;
            Vector2 start = EvaluateEntry(wave.EntryPath, slot, minimumEntryY, 0f);
            transform.position = new Vector3(start.x, start.y, transform.position.z);
            body.position = start;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            float length = 0f;
            Vector2 previous = start;
            for (int i = 1; i <= 20; i++)
            {
                Vector2 point = EvaluateEntry(wave.EntryPath, slot, minimumEntryY, i / 20f);
                length += Vector2.Distance(previous, point);
                previous = point;
            }
            entryDuration = Mathf.Max(0.1f, length / wave.MoveSpeed);
        }

        private void FixedUpdate()
        {
            if (!initialized || resolved || health.IsDead ||
                (GameManager.Instance != null && GameManager.Instance.State.IsGameOver)) return;

            switch (state)
            {
                case ChickenState.Entering:
                    entryElapsed += Time.fixedDeltaTime;
                    float t = Mathf.Clamp01(entryElapsed / entryDuration);
                    body.MovePosition(EvaluateEntry(wave.EntryPath, slot, minimumEntryY, t));
                    if (t >= 1f)
                    {
                        state = ChickenState.Formation;
                        ScheduleDive();
                    }
                    break;

                case ChickenState.Formation:
                    body.MovePosition(slot + Vector2.right * formation.SwayOffset);
                    if (Time.time >= nextDiveAt)
                    {
                        ScheduleDive();
                        if (player != null && UnityEngine.Random.value < wave.DiveChance)
                        {
                            Vector2 direction = (Vector2)player.position - body.position;
                            diveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
                            state = ChickenState.Diving;
                            diveStartedAt = Time.time;
                            break;
                        }
                    }
                    break;

                case ChickenState.Diving:
                    body.MovePosition(body.position + diveDirection * (wave.DiveSpeed * Time.fixedDeltaTime));
                    Vector3 viewport = playAreaCamera.WorldToViewportPoint(transform.position);
                    if (viewport.z <= 0f || viewport.x < -0.2f || viewport.x > 1.2f ||
                        viewport.y < -0.2f || viewport.y > 1.2f || Time.time - diveStartedAt >= 10f)
                    {
                        Resolve();
                        Destroy(gameObject);
                    }
                    break;
            }
            if (state == ChickenState.Entering || state == ChickenState.Formation) TryLayEgg();
        }

        private void TryLayEgg()
        {
            Bounds bounds = OccupiedBounds;
            Vector3 minimum = playAreaCamera.WorldToViewportPoint(bounds.min);
            Vector3 maximum = playAreaCamera.WorldToViewportPoint(bounds.max);
            if (minimum.z <= 0f || maximum.x < 0f || minimum.x > 1f || maximum.y < 0f || minimum.y > 1f) return;
            if (!appeared)
            {
                appeared = true;
                nextEggAt = Time.time;
            }
            if (Time.time < nextEggAt) return;
            eggPool.Spawn(eggPoint.position, Quaternion.identity, Vector2.down * wave.EggSpeed);
            ScheduleEgg();
        }

        private void ScheduleEgg() => nextEggAt = Time.time + UnityEngine.Random.Range(wave.EggInterval.x, wave.EggInterval.y);

        private void ScheduleDive()
        {
            nextDiveAt = wave.DiveInterval <= 0f ? float.PositiveInfinity :
                Time.time + wave.DiveInterval * UnityEngine.Random.Range(0.8f, 1.2f);
        }

        private void OnDeath() => Resolve();

        private void Resolve()
        {
            if (resolved) return;
            resolved = true;
            state = ChickenState.Dead;
            if (body != null) body.linearVelocity = Vector2.zero;
            Resolved?.Invoke(this);
            Resolved = null;
        }

        public static Vector2 EvaluateEntry(Vector2[] path, Vector2 formationSlot, float minimumY, float progress)
        {
            Vector2 offset = formationSlot - path[3];
            Vector2 a = path[0] + offset;
            a.y = Mathf.Max(a.y, minimumY);
            Vector2 b = path[1] + offset;
            Vector2 c = path[2] + offset;
            Vector2 d = formationSlot;
            float t = Mathf.Clamp01(progress);
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
    }
}
