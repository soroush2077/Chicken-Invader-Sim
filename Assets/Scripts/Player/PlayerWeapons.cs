using UnityEngine;
using UnityEngine.InputSystem;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    public sealed class PlayerWeapons : MonoBehaviour
    {
        [SerializeField] private InputActionReference fireAction;
        [SerializeField] private InputActionReference bombAction;
        [SerializeField] private Transform firePoint;
        [Tooltip("Visible nuke launched from Fire Point. Flight speed is configured on this prefab.")]
        [SerializeField] private NukeProjectile nukePrefab;
        [Tooltip("Power levels 1, 2, 3, in that order.")]
        [SerializeField] private WeaponDataSO[] weapons = new WeaponDataSO[3];
        [Tooltip("Scene-local pools corresponding to the three weapon assets.")]
        [SerializeField] private PrefabPool[] projectilePools = new PrefabPool[3];
        private GameManager manager;
        private Camera playAreaCamera;
        private float nextFireAt;

        private void OnEnable()
        {
            manager = GameManager.Instance;
            playAreaCamera = Camera.main;
            if (!HasValidSetup())
            {
                Debug.LogError("PlayerWeapons needs GameManager, MainCamera, Button Fire/Bomb actions, FirePoint, and three matching weapon/pool pairs.", this);
                enabled = false;
                return;
            }
            fireAction.action.Enable();
            bombAction.action.Enable();
            manager.RunStarted += ResetCooldown;
            ResetCooldown();
        }

        private bool HasValidSetup()
        {
            if (manager == null || playAreaCamera == null || firePoint == null ||
                fireAction == null || fireAction.action == null || fireAction.action.type != InputActionType.Button ||
                bombAction == null || bombAction.action == null || bombAction.action.type != InputActionType.Button ||
                weapons == null || projectilePools == null || weapons.Length != 3 || projectilePools.Length != 3)
                return false;
            for (int i = 0; i < 3; i++)
            {
                var weapon = weapons[i];
                var pool = projectilePools[i];
                if (weapon == null || pool == null || weapon.ProjectilePrefab == null ||
                    pool.Prefab != weapon.ProjectilePrefab ||
                    weapon.ProjectilePrefab.GetComponent<ContactDamage>() == null ||
                    weapon.ProjectileSpeed <= 0f || weapon.FireInterval <= 0f ||
                    weapon.Shots == null || weapon.Shots.Length == 0) return false;
            }
            return true;
        }

        private void OnDisable()
        {
            if (fireAction != null && fireAction.action != null) fireAction.action.Disable();
            if (bombAction != null && bombAction.action != null) bombAction.action.Disable();
            if (manager != null) manager.RunStarted -= ResetCooldown;
        }

        private void Update()
        {
            if (manager.State.IsGameOver || Time.timeScale == 0f) return;
            if (bombAction.action.WasPressedThisFrame()) UseBomb();
            if ((fireAction.action.IsPressed() || fireAction.action.WasPressedThisFrame()) && Time.time >= nextFireAt)
                Fire();
        }

        private void Fire()
        {
            int index = manager.State.WeaponPower - 1;
            WeaponDataSO weapon = weapons[index];
            nextFireAt = Time.time + weapon.FireInterval;
            foreach (WeaponShot shot in weapon.Shots)
            {
                Quaternion rotation = firePoint.rotation * Quaternion.Euler(0f, 0f, shot.AngleDegrees);
                Vector3 position = firePoint.position + firePoint.rotation * (Vector3)shot.Offset;
                Vector2 velocity = rotation * Vector3.up * weapon.ProjectileSpeed;
                projectilePools[index].Spawn(position, rotation, velocity);
            }
        }

        private void UseBomb()
        {
            if (nukePrefab == null || !nukePrefab.IsConfigured)
            {
                Debug.LogError("Assign an active Nuke prefab with NukeProjectile and positive Travel Speed. No bomb was spent.", this);
                return;
            }
            if (!manager.SpendBomb()) return;
            var nuke = Instantiate(nukePrefab, firePoint.position, Quaternion.identity);
            nuke.Launch(playAreaCamera, manager);
            Debug.Log($"Nuke launched. Remaining bombs: {manager.State.Bombs}", this);
        }

        private void ResetCooldown() => nextFireAt = 0f;
    }
}
