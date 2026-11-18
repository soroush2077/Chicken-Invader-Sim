using System;
using UnityEngine;

namespace ChickenInvader
{
    [Serializable]
    public struct WeaponShot
    {
        public Vector2 Offset;
        public float AngleDegrees;
    }

    [CreateAssetMenu(menuName = "Chicken Invader/Weapon Data")]
    public sealed class WeaponDataSO : ScriptableObject
    {
        public string DisplayName = "Single Laser";
        public Sprite Icon;
        public GameObject ProjectilePrefab;
        [Tooltip("Projectile travel speed in world units per second.")]
        [Min(0.01f)] public float ProjectileSpeed = 12f;
        [Tooltip("Seconds between volleys. Increase this value to fire less often.")]
        [Min(0.01f)] public float FireInterval = 0.2f;
        [Tooltip("Angles are relative to up; positive angles turn counterclockwise.")]
        public WeaponShot[] Shots = { new WeaponShot() };
    }
}
