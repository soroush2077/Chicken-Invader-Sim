using System;
using System.Collections;
using UnityEngine;

namespace ChickenInvader
{
    public enum RoundPhase { Stopped, Spawning, Combat, Intermission, AwaitingBoss }

    public sealed class RoundManager : MonoBehaviour
    {
        [SerializeField] private LevelDataSO level;
        [SerializeField] private ChickenFormation chickenFormation;
        [SerializeField] private AsteroidSpawner asteroidSpawner;
        [SerializeField, Min(0f)] private float pickupSeconds = 2f;
        [SerializeField] private RoundPhase phase;
        private GameManager manager;
        private Coroutine runner;
        public event Action<LevelDataSO> ReadyForBoss;
        public RoundPhase Phase => phase;
        public LevelDataSO Level => level;

        private void OnEnable()
        {
            manager = GameManager.Instance;
            if (manager == null)
            {
                Debug.LogError("RoundManager needs an active GameManager in this scene.", this);
                enabled = false;
                return;
            }
            manager.Defeated += StopRun;
            manager.RunStarted += StartRun;
        }

        private void Start() => StartRun();

        private void OnDisable()
        {
            if (manager != null)
            {
                manager.Defeated -= StopRun;
                manager.RunStarted -= StartRun;
            }
            StopRun();
        }

        private void StartRun()
        {
            StopRun();
            try
            {
                if (level == null || level.Rounds == null || level.Rounds.Length == 0 ||
                    chickenFormation == null || asteroidSpawner == null ||
                    !chickenFormation.isActiveAndEnabled || !asteroidSpawner.isActiveAndEnabled)
                    throw new InvalidOperationException("Assign Level and active ChickenFormation and AsteroidSpawner components.");
                if (level.SceneName != gameObject.scene.name)
                    throw new InvalidOperationException($"RoundManager uses {level.name}, whose Scene Name is '{level.SceneName}', but this scene is '{gameObject.scene.name}'. Assign the matching level asset and scene name.");
                int levelIndex = -1;
                for (int i = 0; i < manager.LevelCount; i++)
                    if (manager.GetLevel(i) == level) { levelIndex = i; break; }
                if (levelIndex < 0) throw new InvalidOperationException("Add this LevelData asset to GameManager's Levels list.");
                foreach (var round in level.Rounds)
                {
                    if (round == null || round.Wave == null)
                        throw new InvalidOperationException("Every round needs a Wave asset.");
                    if (round.Wave.Kind == WaveKind.Chickens && level.ChickenPrefab == null)
                        throw new InvalidOperationException("Assign this level's Chicken Prefab.");
                }
                int firstRound = manager.State.LevelIndex == levelIndex ? manager.State.RoundIndex : 0;
                if (firstRound < 0 || firstRound >= level.Rounds.Length)
                    throw new InvalidOperationException("The current round index is outside this level's round list.");
                runner = StartCoroutine(Run(levelIndex, firstRound));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private IEnumerator Run(int levelIndex, int roundIndex)
        {
            while (!manager.State.IsGameOver)
            {
                manager.SetProgress(levelIndex, roundIndex);
                RoundEntry round = level.Rounds[roundIndex];
                Debug.Log($"Level {level.LevelNumber} - Round {roundIndex + 1}/{level.Rounds.Length}: {round.Wave.name}", this);
                phase = RoundPhase.Spawning;
                if (!TrySpawn(round)) yield break;

                int next;
                do
                {
                    bool spawning = round.Wave.Kind == WaveKind.Chickens ? chickenFormation.IsSpawning : asteroidSpawner.IsSpawning;
                    int alive = round.Wave.Kind == WaveKind.Chickens ? chickenFormation.AliveCount : asteroidSpawner.AliveCount;
                    phase = spawning ? RoundPhase.Spawning : RoundPhase.Combat;
                    next = NextRoundIndex(roundIndex, level.Rounds.Length, spawning, alive);
                    if (next == roundIndex) yield return null;
                } while (next == roundIndex);

                phase = RoundPhase.Intermission;
                yield return new WaitForSeconds(pickupSeconds);
                if (manager.State.IsGameOver) yield break;
                ClearTransientObjects();
                if (next < 0)
                {
                    phase = RoundPhase.AwaitingBoss;
                    runner = null;
                    Debug.Log("All numbered rounds cleared. Awaiting the boss/Stage 1 completion gate.", this);
                    ReadyForBoss?.Invoke(level);
                    yield break;
                }
                roundIndex = next;
            }
        }

        private bool TrySpawn(RoundEntry round)
        {
            try
            {
                if (round.Wave.Kind == WaveKind.Chickens)
                    chickenFormation.SpawnFormation(round.Wave, level.ChickenPrefab);
                else if (round.Wave.Kind == WaveKind.Asteroids) asteroidSpawner.SpawnWave(round.Wave);
                else throw new InvalidOperationException("Unknown wave kind.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
                return false;
            }
        }

        private void StopRun()
        {
            if (runner != null) StopCoroutine(runner);
            runner = null;
            phase = RoundPhase.Stopped;
            if (chickenFormation != null) chickenFormation.StopFormation();
            if (asteroidSpawner != null) asteroidSpawner.StopWave();
            ClearTransientObjects();
        }

        private void ClearTransientObjects()
        {
            foreach (var nuke in FindObjectsByType<NukeProjectile>())
                if (nuke.gameObject.scene == gameObject.scene) nuke.Cancel();
            foreach (var pool in FindObjectsByType<PrefabPool>())
                if (pool.gameObject.scene == gameObject.scene) pool.DespawnAll();
            foreach (var gift in FindObjectsByType<WeaponGift>())
            {
                if (gift.gameObject.scene != gameObject.scene) continue;
                gift.gameObject.SetActive(false);
                Destroy(gift.gameObject);
            }
        }

        public static int NextRoundIndex(int current, int count, bool isSpawning, int aliveCount)
        {
            if (count < 1 || current < 0 || current >= count || aliveCount < 0)
                throw new ArgumentOutOfRangeException(nameof(current));
            if (isSpawning || aliveCount > 0) return current;
            return current + 1 < count ? current + 1 : -1;
        }

#if UNITY_EDITOR
        [ContextMenu("Check Round Progression Rules")]
        private void CheckRoundRules()
        {
            Debug.Assert(NextRoundIndex(0, 5, true, 0) == 0, "An empty screen during spawning must not advance.");
            Debug.Assert(NextRoundIndex(0, 5, false, 1) == 0, "A remaining enemy must block advancement.");
            Debug.Assert(NextRoundIndex(0, 5, false, 0) == 1);
            Debug.Assert(NextRoundIndex(4, 5, true, 0) == 4);
            Debug.Assert(NextRoundIndex(4, 5, false, 0) == -1, "The final round must end at the boss gate.");
            Debug.Log("Round rule checks completed; inspect the Console for assertion failures.", this);
        }
#endif
    }
}
