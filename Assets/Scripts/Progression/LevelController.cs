using System;
using System.Collections;
using UnityEngine;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RoundManager))]
    public sealed class LevelController : MonoBehaviour
    {
        [Tooltip("Stage 1 only: finish after the last wave. Disable when implementing bosses in B1.")]
        [SerializeField] private bool stage1BossBypass = true;
        [SerializeField, Min(0f)] private float transitionDelay = 1.5f;
        private RoundManager rounds;
        private GameManager manager;
        private Coroutine transition;
        private bool completing;
        private float previousTimeScale;

        private void Awake() => rounds = GetComponent<RoundManager>();

        private void OnEnable()
        {
            manager = GameManager.Instance;
            if (manager == null)
            {
                Debug.LogError("LevelController needs an active GameManager in this scene.", this);
                enabled = false;
                return;
            }
            rounds.ReadyForBoss += OnReadyForBoss;
            manager.RunStarted += ResetCompletion;
        }

        private void OnDisable()
        {
            rounds.ReadyForBoss -= OnReadyForBoss;
            if (manager != null) manager.RunStarted -= ResetCompletion;
            if (transition != null)
            {
                StopCoroutine(transition);
                if (manager == null || !manager.IsLoading) Time.timeScale = previousTimeScale;
                transition = null;
            }
        }

        private void OnReadyForBoss(LevelDataSO level)
        {
            // shortcut: skip the boss for Stage 1; B1 calls CompleteLevel after actual boss defeat.
            if (stage1BossBypass) CompleteLevel();
        }

#if UNITY_EDITOR
        [ContextMenu("Complete Level (At Boss Gate)")]
#endif
        public void CompleteLevel()
        {
            if (!Application.isPlaying || manager == null || completing || manager.IsLoading || manager.State.IsGameOver) return;
            if (rounds.Phase != RoundPhase.AwaitingBoss || manager.CurrentLevel != rounds.Level)
            {
                Debug.LogWarning("Finish this level's configured rounds before completing it.", this);
                return;
            }
            completing = true;
            transition = StartCoroutine(FinishLevel());
        }

        private IEnumerator FinishLevel()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Debug.Log($"Level {rounds.Level.LevelNumber} cleared. Stage 1 boss bypass: {stage1BossBypass}.", this);
            yield return new WaitForSecondsRealtime(transitionDelay);
            try
            {
                int next = GameManager.NextLevelIndex(manager.State.LevelIndex, manager.LevelCount);
                if (next >= 0) manager.LoadLevel(next);
                else
                {
                    Time.timeScale = 0f;
                    Debug.Log($"Stage 1 campaign complete. Score: {manager.State.Score}. Stop Play Mode to finish this prototype run.", this);
                }
            }
            catch (Exception exception)
            {
                Time.timeScale = previousTimeScale;
                completing = false;
                Debug.LogException(exception, this);
            }
            transition = null;
        }

        private void ResetCompletion()
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            completing = false;
        }
    }
}
