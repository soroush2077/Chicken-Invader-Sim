using System;
using UnityEngine;

namespace ChickenInvader
{
    [Serializable]
    public sealed class RoundEntry
    {
        public WaveDataSO Wave;
    }

    [CreateAssetMenu(menuName = "Chicken Invader/Level Data")]
    public sealed class LevelDataSO : ScriptableObject
    {
        [Min(1)] public int LevelNumber = 1;
        public string SceneName = "Level1";
        public string Theme = "Earth Orbit";
        public AudioClip Music;
        [Tooltip("Required chicken prefab for this level's chicken rounds. Health is configured on the prefab.")]
        public GameObject ChickenPrefab;
        public RoundEntry[] Rounds = Array.Empty<RoundEntry>();
    }
}
