using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChickenInvader
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelDataSO[] levels = Array.Empty<LevelDataSO>();
        [SerializeField] private RunState state = new RunState();

        public static GameManager Instance { get; private set; }
        public RunState State => state;
        public int LevelCount => levels.Length;
        public LevelDataSO CurrentLevel => GetLevel(state.LevelIndex);
        public event Action Defeated;
        public event Action RunStarted;
        public bool IsLoading { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload is disabled in this project's Play Mode settings.
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            state = new RunState();
            Time.timeScale = 1f;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsLoading) return;
            IsLoading = false;
            Time.timeScale = 1f;
        }

        public void LoadLevel(int index)
        {
            if (IsLoading) throw new InvalidOperationException("A scene load is already in progress.");
            ValidateProgress(index, 0);
            string sceneName = GetLevel(index).SceneName;
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
                throw new InvalidOperationException($"Scene '{sceneName}' is not available. Check LevelData's Scene Name and the enabled Build Profiles Scene List.");

            int previousLevel = state.LevelIndex;
            int previousRound = state.RoundIndex;
            float previousTimeScale = Time.timeScale;
            IsLoading = true;
            try
            {
                state.SetProgress(index, 0);
                // Keep the old scene frozen until the replacement scene has loaded.
                Time.timeScale = 0f;
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            catch
            {
                IsLoading = false;
                state.SetProgress(previousLevel, previousRound);
                Time.timeScale = previousTimeScale;
                throw;
            }
        }

        public static int NextLevelIndex(int current, int count)
        {
            if (count < 1 || current < 0 || current >= count)
                throw new ArgumentOutOfRangeException(nameof(current));
            return current + 1 < count ? current + 1 : -1;
        }

        public LevelDataSO GetLevel(int index)
        {
            if (index < 0 || index >= levels.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (levels[index] == null)
                throw new InvalidOperationException($"Level catalog entry {index} is unassigned.");
            return levels[index];
        }

        [ContextMenu("Start New Run")]
        public void StartNewRun()
        {
            if (IsLoading) throw new InvalidOperationException("Wait for the scene load before starting a new run.");
            ValidateProgress(0, 0);
            state.Reset();
            Time.timeScale = 1f;
            RunStarted?.Invoke();
        }

        public void SetProgress(int levelIndex, int roundIndex)
        {
            ValidateProgress(levelIndex, roundIndex);
            state.SetProgress(levelIndex, roundIndex);
        }

        public void AddScore(int amount) => state.AddScore(amount);
        public bool UpgradeWeapon() => state.UpgradeWeapon();
        public bool SpendBomb() => state.SpendBomb();

        public bool TakePlayerHit()
        {
            if (!state.TakePlayerHit()) return false;
            if (state.IsGameOver)
            {
                Time.timeScale = 0f;
                Defeated?.Invoke();
                Debug.Log("Run defeated. Stop Play Mode, or use Start New Run on GameManager to test again.", this);
            }
            return true;
        }

#if UNITY_EDITOR
        [ContextMenu("Check Damage Rules")]
        private void CheckDamageRules() => RunState.CheckDamageRules();

        [ContextMenu("Check Weapon Rules")]
        private void CheckWeaponRules() => RunState.CheckWeaponRules();

        [ContextMenu("Check Scene Progress Rules")]
        private void CheckSceneProgressRules()
        {
            Debug.Assert(NextLevelIndex(0, 3) == 1 && NextLevelIndex(1, 3) == 2 && NextLevelIndex(2, 3) == -1);
            RunState.CheckScenePersistence();
        }
#endif

        private void ValidateProgress(int levelIndex, int roundIndex)
        {
            LevelDataSO level = GetLevel(levelIndex);
            if (level.Rounds == null || roundIndex < 0 || roundIndex >= level.Rounds.Length)
                throw new ArgumentOutOfRangeException(nameof(roundIndex));
            if (level.Rounds[roundIndex] == null || level.Rounds[roundIndex].Wave == null)
                throw new InvalidOperationException($"Round {roundIndex} in {level.name} has no wave.");
        }
    }
}
