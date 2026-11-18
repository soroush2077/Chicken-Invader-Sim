using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChickenInvader
{
    public sealed class ChickenFormation : MonoBehaviour
    {
        [SerializeField] private PrefabPool eggPool;
#if UNITY_EDITOR
        [Tooltip("Editor preview only. RoundManager starts the level's configured waves.")]
        [SerializeField] private WaveDataSO previewWave;
#endif
        private readonly HashSet<ChickenController> living = new HashSet<ChickenController>();
        private Coroutine spawnRoutine;
        private WaveDataSO motionWave;
        private bool motionStarted;
        private float motionStartedAt;
        private float swayDistance;
        public bool IsSpawning { get; private set; }
        public int AliveCount => living.Count;
        public bool IsComplete => !IsSpawning && living.Count == 0;
        public float SwayOffset => motionStarted ? CalculateSway(Time.time - motionStartedAt, swayDistance, motionWave.FormationSwayPeriod) : 0f;

        public void SpawnFormation(WaveDataSO wave, GameObject prefab)
        {
            if (!isActiveAndEnabled || !IsComplete)
                throw new InvalidOperationException("The formation must be active and its previous wave resolved.");
            var player = FindAnyObjectByType<PlayerHealth>();
            Camera camera = Camera.main;
            if (wave == null || wave.Kind != WaveKind.Chickens || prefab == null ||
                prefab.GetComponent<ChickenController>() == null ||
                eggPool == null || eggPool.Prefab == null || player == null || camera == null || !camera.orthographic ||
                wave.EntryPath == null || wave.EntryPath.Length != 4 || wave.Count < 1 ||
                wave.MoveSpeed <= 0f || wave.SpawnInterval < 0f || wave.EggSpeed <= 0f ||
                wave.EggInterval.x <= 0f || wave.EggInterval.y < wave.EggInterval.x ||
                wave.FormationSpacing.x <= 0f || wave.FormationSpacing.y <= 0f ||
                wave.FormationColumns < 0 || wave.FormationSwayDistance < 0f || wave.FormationSwayPeriod <= 0f ||
                !Enum.IsDefined(typeof(FormationKind), wave.Formation) ||
                (wave.Formation == FormationKind.Custom && (wave.CustomSlots == null || wave.CustomSlots.Length != wave.Count)) ||
                wave.DiveSpeed <= 0f)
                throw new InvalidOperationException("Check chicken wave data, four entry points, prefab, EggPool, PlayerHealth, and MainCamera.");

            var controller = prefab.GetComponent<ChickenController>();
            var rigidbody = prefab.GetComponent<Rigidbody2D>();
            if (!controller.enabled || !controller.HasEggPoint || controller.GetComponent<Health>() == null ||
                rigidbody == null || rigidbody.bodyType != RigidbodyType2D.Kinematic ||
                !rigidbody.simulated || rigidbody.constraints.HasFlag(RigidbodyConstraints2D.FreezePositionX) ||
                rigidbody.constraints.HasFlag(RigidbodyConstraints2D.FreezePositionY))
                throw new InvalidOperationException("Chicken prefab needs an enabled controller, EggPoint, Health, and a simulated Kinematic Rigidbody2D with position unfrozen.");
            IsSpawning = true;
            motionWave = wave;
            motionStarted = false;
            spawnRoutine = StartCoroutine(Spawn(wave, prefab, player.transform, camera));
        }

        private IEnumerator Spawn(WaveDataSO wave, GameObject prefab, Transform player, Camera camera)
        {
            for (int i = 0; i < wave.Count; i++)
            {
                var instance = Instantiate(prefab, transform);
                instance.SetActive(true);
                var chicken = instance.GetComponent<ChickenController>();
                Vector2 slot = (Vector2)transform.position + SlotOffset(wave.Formation, i, wave.Count,
                    wave.FormationSpacing, wave.FormationColumns, wave.CustomSlots);
                chicken.Initialize(wave, slot, eggPool, player, camera, this);
                living.Add(chicken);
                chicken.Resolved += OnResolved;
                if (i < wave.Count - 1) yield return new WaitForSeconds(wave.SpawnInterval);
            }
            IsSpawning = false;
            spawnRoutine = null;
        }

        private void OnResolved(ChickenController chicken) => living.Remove(chicken);

        private void LateUpdate()
        {
            if (motionStarted || IsSpawning || motionWave == null || living.Count == 0) return;
            bool hasBounds = false;
            Bounds occupied = default;
            foreach (var chicken in living)
            {
                if (chicken == null) continue;
                if (chicken.State == ChickenState.Entering) return;
                if (chicken.State != ChickenState.Formation) continue;
                if (!hasBounds) { occupied = chicken.OccupiedBounds; hasBounds = true; }
                else occupied.Encapsulate(chicken.OccupiedBounds);
            }
            if (!hasBounds) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            float depth = transform.position.z - camera.transform.position.z;
            float left = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth)).x + 0.1f;
            float right = camera.ViewportToWorldPoint(new Vector3(1f, 0f, depth)).x - 0.1f;
            swayDistance = ClampSwayDistance(motionWave.FormationSwayDistance, occupied.min.x, occupied.max.x, left, right);
            if (occupied.min.x < left || occupied.max.x > right)
                Debug.LogWarning("Formation is wider than the camera area. Reduce Formation Spacing or change Formation Columns.", this);
            motionStartedAt = Time.time;
            motionStarted = true;
        }

        private void OnDisable() => StopFormation();

        public void StopFormation()
        {
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            spawnRoutine = null;
            IsSpawning = false;
            motionStarted = false;
            motionWave = null;
            foreach (var chicken in living)
            {
                if (chicken == null) continue;
                chicken.Resolved -= OnResolved;
                chicken.gameObject.SetActive(false);
                Destroy(chicken.gameObject);
            }
            living.Clear();
        }

        public static Vector2 SlotOffset(FormationKind kind, int index, int count, Vector2 spacing,
            int columnsOverride = 0, Vector2[] customSlots = null)
        {
            if (count < 1 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            if (kind == FormationKind.Custom)
            {
                if (customSlots == null || customSlots.Length != count)
                    throw new ArgumentException("Custom Slots must contain exactly Count offsets.", nameof(customSlots));
                return customSlots[index];
            }
            if (count == 1) return Vector2.zero;
            if (kind == FormationKind.Ring)
            {
                float angle = index * Mathf.PI * 2f / count;
                float radius = Mathf.Max(1f, count / (2f * Mathf.PI));
                return new Vector2(Mathf.Cos(angle) * radius * spacing.x, Mathf.Sin(angle) * radius * spacing.y);
            }
            if (kind == FormationKind.Diamond)
            {
                float along = index * 4f / count;
                int edge = Mathf.FloorToInt(along);
                float t = along - edge;
                Vector2 point = edge switch
                {
                    0 => new Vector2(t, 1f - t),
                    1 => new Vector2(1f - t, -t),
                    2 => new Vector2(-t, t - 1f),
                    _ => new Vector2(t - 1f, t)
                };
                return Vector2.Scale(point * Mathf.Max(1f, count / 4f), spacing);
            }
            if (kind == FormationKind.V)
            {
                float x = index - (count - 1) * 0.5f;
                return new Vector2(x * spacing.x, Mathf.Abs(x) * spacing.y);
            }
            int columns = columnsOverride > 0 ? Mathf.Min(columnsOverride, count) :
                Mathf.CeilToInt(Mathf.Sqrt(kind == FormationKind.Dense || kind == FormationKind.Staggered ? count * 2f : count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            int row = index / columns;
            int rowLength = Mathf.Min(columns, count - row * columns);
            float stagger = kind == FormationKind.Staggered && row % 2 == 1 ? 0.5f : 0f;
            return new Vector2((index % columns - (rowLength - 1) * 0.5f + stagger) * spacing.x,
                ((rows - 1) * 0.5f - row) * spacing.y);
        }

        public static float CalculateSway(float elapsed, float distance, float period)
        {
            if (period <= 0f || distance < 0f) throw new ArgumentOutOfRangeException(nameof(period));
            return Mathf.Sin(Mathf.Max(0f, elapsed) * Mathf.PI * 2f / period) * distance;
        }

        public static float ClampSwayDistance(float requested, float formationLeft, float formationRight, float cameraLeft, float cameraRight)
        {
            return Mathf.Max(0f, Mathf.Min(requested, Mathf.Min(formationLeft - cameraLeft, cameraRight - formationRight)));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            WaveDataSO wave = previewWave;
            if (Application.isPlaying || wave == null || wave.Count < 1 ||
                wave.EntryPath == null || wave.EntryPath.Length != 4 ||
                (wave.Formation == FormationKind.Custom && (wave.CustomSlots == null || wave.CustomSlots.Length != wave.Count))) return;
            Camera camera = Camera.main;
            float top = camera != null ? camera.transform.position.y + camera.orthographicSize + 1f : 6f;
            for (int i = 0; i < wave.Count; i++)
            {
                Vector2 slot = (Vector2)transform.position + SlotOffset(wave.Formation, i, wave.Count,
                    wave.FormationSpacing, wave.FormationColumns, wave.CustomSlots);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(slot, 0.15f);
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(slot - Vector2.right * wave.FormationSwayDistance, slot + Vector2.right * wave.FormationSwayDistance);
                Gizmos.color = Color.cyan;
                Vector2 previous = ChickenController.EvaluateEntry(wave.EntryPath, slot, top, 0f);
                for (int step = 1; step <= 20; step++)
                {
                    Vector2 point = ChickenController.EvaluateEntry(wave.EntryPath, slot, top, step / 20f);
                    Gizmos.DrawLine(previous, point);
                    previous = point;
                }
            }
        }
#endif
    }
}
