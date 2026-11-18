using UnityEngine;

namespace ChickenInvader
{
    public enum WaveKind { Chickens, Asteroids }
    public enum FormationKind { Grid = 0, V = 1, Dense = 2, Ring = 3, Diamond = 4, Staggered = 5, Custom = 6 }
    public enum AsteroidTravel { TopToBottom, TopLeftToBottomRight, TopRightToBottomLeft }

    [CreateAssetMenu(menuName = "Chicken Invader/Wave Data")]
    public sealed class WaveDataSO : ScriptableObject
    {
        public WaveKind Kind;
        [Min(1)] public int Count = 6;
        [Tooltip("Chicken entry speed or asteroid travel speed in world units per second.")]
        [Min(0.01f)] public float MoveSpeed = 3f;
        [Min(0.01f)] public float SpawnInterval = 0.25f;
        public FormationKind Formation;
        [Tooltip("Grid, Dense and Staggered columns. Zero chooses the column count automatically.")]
        [Min(0)] public int FormationColumns;
        public Vector2 FormationSpacing = new Vector2(1.6f, 1.2f);
        [Tooltip("Custom formation only: one world-unit offset from the formation anchor per chicken. Length must equal Count.")]
        public Vector2[] CustomSlots = new Vector2[0];
        [Tooltip("Maximum left/right travel in world units after all chickens have entered. Zero keeps the formation still.")]
        [Min(0f)] public float FormationSwayDistance = 1.5f;
        [Tooltip("Seconds for one complete left/right cycle. Larger values give slower movement.")]
        [Min(0.1f)] public float FormationSwayPeriod = 10f;
        [Tooltip("Seconds between egg drops, sampled between X and Y.")]
        public Vector2 EggInterval = new Vector2(2f, 4f);
        [Min(0.01f)] public float EggSpeed = 4f;
        [Tooltip("Exactly four cubic Bezier control points. The last point maps to each chicken's formation slot.")]
        public Vector2[] EntryPath =
        {
            new Vector2(-6f, 6f), new Vector2(-4f, 1f),
            new Vector2(4f, 5f), new Vector2(0f, 3f)
        };
        [Min(0f)] public float DiveInterval = 6f;
        [Range(0f, 1f)] public float DiveChance = 0.25f;
        [Min(0.01f)] public float DiveSpeed = 6f;
        public AsteroidTravel AsteroidDirection;
    }
}
