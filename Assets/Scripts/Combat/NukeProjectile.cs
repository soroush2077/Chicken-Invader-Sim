using System;
using UnityEngine;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    public sealed class NukeProjectile : MonoBehaviour
    {
        [Tooltip("Flight speed in world units per second. Orient the visual to point along the root's local up axis.")]
        [SerializeField, Min(0.01f)] private float travelSpeed = 5f;
        private Camera playAreaCamera;
        private GameManager manager;
        private Vector3 destination;
        private bool launched;
        private bool resolved;

        internal bool IsConfigured => enabled && gameObject.activeSelf && travelSpeed > 0f &&
            !float.IsNaN(travelSpeed) && !float.IsInfinity(travelSpeed);

        public void Launch(Camera camera, GameManager gameManager)
        {
            if (launched || resolved || !IsConfigured || camera == null || !camera.orthographic || gameManager == null)
                throw new InvalidOperationException("Nuke needs an active prefab, positive finite Travel Speed, orthographic camera, and GameManager.");
            playAreaCamera = camera;
            manager = gameManager;
            float depth = transform.position.z - camera.transform.position.z;
            destination = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
            Vector3 direction = destination - transform.position;
            if (direction.sqrMagnitude > 0.0001f) transform.up = direction.normalized;
            launched = true;
            manager.RunStarted += Cancel;
            manager.Defeated += Cancel;
        }

        private void Update()
        {
            if (!launched || resolved) return;
            if (manager == null || manager.State.IsGameOver || manager.IsLoading || playAreaCamera == null)
            {
                Cancel();
                return;
            }
            if (Time.timeScale == 0f) return;
            transform.position = Vector3.MoveTowards(transform.position, destination, travelSpeed * Time.deltaTime);
            if (transform.position == destination) Detonate();
        }

        private void Detonate()
        {
            if (resolved) return;
            resolved = true;
            Debug.Assert((transform.position - destination).sqrMagnitude <= 0.0001f,
                "The nuke must reach the screen center before applying damage.");
            // Nukes are rare, so a scene scan keeps their targets consistent with existing bomb rules.
            foreach (var target in FindObjectsByType<Health>())
            {
                if (!target.isActiveAndEnabled || target.IsDead || target.gameObject.scene != gameObject.scene) continue;
                var visual = target.GetComponentInChildren<SpriteRenderer>();
                Bounds bounds = visual != null ? visual.bounds : new Bounds(target.transform.position, Vector3.zero);
                Vector3 minimum = playAreaCamera.WorldToViewportPoint(bounds.min);
                Vector3 maximum = playAreaCamera.WorldToViewportPoint(bounds.max);
                if (minimum.z > 0f && maximum.x >= 0f && minimum.x <= 1f && maximum.y >= 0f && minimum.y <= 1f)
                    target.ApplyBomb();
            }
            Debug.Log("Nuke detonated.", this);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        public void Cancel()
        {
            if (resolved) return;
            resolved = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            manager.RunStarted -= Cancel;
            manager.Defeated -= Cancel;
        }
    }
}
