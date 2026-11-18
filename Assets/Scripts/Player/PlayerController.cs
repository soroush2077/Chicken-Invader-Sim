using UnityEngine;
using UnityEngine.InputSystem;

namespace ChickenInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private SpriteRenderer shipVisual;
        [SerializeField] private Camera playAreaCamera;
        [SerializeField, Min(0.01f)] private float moveSpeed = 6f;

        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private Vector2 moveInput;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<BoxCollider2D>();
            if (shipVisual == null) shipVisual = GetComponentInChildren<SpriteRenderer>();
            if (playAreaCamera == null) playAreaCamera = Camera.main;

            if (moveAction == null || moveAction.action == null ||
                moveAction.action.type != InputActionType.Value ||
                moveAction.action.expectedControlType != "Vector2" ||
                shipVisual == null || playAreaCamera == null)
            {
                Debug.LogError("Player needs a Value/Vector2 Move action, a ship SpriteRenderer, and a MainCamera.", this);
                enabled = false;
                return;
            }

            // shortcut: bounds assume an unrotated orthographic camera; revise for rotating/perspective levels.
            if (!playAreaCamera.orthographic ||
                Quaternion.Angle(playAreaCamera.transform.rotation, Quaternion.identity) > 0.01f ||
                transform.position.z <= playAreaCamera.transform.position.z ||
                body.bodyType != RigidbodyType2D.Kinematic)
            {
                Debug.LogError("Use a Kinematic Rigidbody2D and an unrotated Orthographic camera behind the player.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (moveAction != null && moveAction.action != null) moveAction.action.Enable();
        }

        private void OnDisable()
        {
            if (moveAction != null && moveAction.action != null) moveAction.action.Disable();
            moveInput = Vector2.zero;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            moveInput = Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
        }

        private void FixedUpdate()
        {
            Vector2 target = body.position + moveInput * (moveSpeed * Time.fixedDeltaTime);
            Bounds bounds = shipVisual.bounds;
            bounds.Encapsulate(hitbox.bounds);
            Vector2 rootPosition = transform.position;
            float depth = transform.position.z - playAreaCamera.transform.position.z;
            Vector2 screenMin = playAreaCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector2 screenMax = playAreaCamera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            Vector2 minimum = screenMin - ((Vector2)bounds.min - rootPosition);
            Vector2 maximum = screenMax - ((Vector2)bounds.max - rootPosition);

            body.MovePosition(ClampPosition(target, minimum, maximum));
        }

        private static Vector2 ClampPosition(Vector2 position, Vector2 minimum, Vector2 maximum)
        {
            return new Vector2(
                minimum.x <= maximum.x ? Mathf.Clamp(position.x, minimum.x, maximum.x) : (minimum.x + maximum.x) * 0.5f,
                minimum.y <= maximum.y ? Mathf.Clamp(position.y, minimum.y, maximum.y) : (minimum.y + maximum.y) * 0.5f);
        }

        [ContextMenu("Check Movement Math")]
        private void CheckMovementMath()
        {
            Debug.Assert(Mathf.Approximately(Vector2.ClampMagnitude(Vector2.one, 1f).magnitude, 1f),
                "Diagonal input must not exceed straight-line speed.");
            Vector2 minimum = new Vector2(-4.5f, -3.5f);
            Vector2 maximum = new Vector2(4.5f, 3.5f);
            Debug.Assert(ClampPosition(new Vector2(99f, -99f), minimum, maximum) == new Vector2(4.5f, -3.5f),
                "Movement must stop at both screen edges.");
            Debug.Assert(ClampPosition(Vector2.one, minimum, maximum) == Vector2.one,
                "Movement inside the screen must be unchanged.");
            Debug.Assert(ClampPosition(Vector2.one, new Vector2(1f, 2f), new Vector2(-1f, -2f)) == Vector2.zero,
                "A viewport smaller than the ship must center it, not produce an invalid clamp.");
            Debug.Log("Movement math checks completed; any failed assertion appears above.", this);
        }
    }
}
