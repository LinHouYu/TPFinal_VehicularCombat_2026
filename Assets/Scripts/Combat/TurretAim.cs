using UnityEngine;
using UnityEngine.InputSystem;

namespace VehicularCombat.Combat
{
    /// <summary>
    /// Raycasts mouse cursor onto Ground layer and smoothly rotates the Turret around local Y axis.
    /// </summary>
    public class TurretAim : MonoBehaviour
    {
        [Header("References")]
        public Transform turretTransform;
        public Camera aimCamera;
        public LayerMask groundLayerMask = ~0;

        [Header("Aiming")]
        [Tooltip("Degrees per second for smooth Y-axis turret rotation.")]
        public float turnSpeed = 720f;

        public Vector3 AimWorldPoint { get; private set; }
        public Vector3 AimDirection => turretTransform != null ? turretTransform.forward : transform.forward;

        private void Awake()
        {
            if (aimCamera == null)
                aimCamera = Camera.main;
        }

        private void Update()
        {
            if (turretTransform == null)
                return;

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
                if (aimCamera == null) return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 mouseScreenPos = mouse.position.ReadValue();
            Ray ray = aimCamera.ScreenPointToRay(mouseScreenPos);

            Vector3 targetPoint;
            if (Physics.Raycast(ray, out RaycastHit hit, 250f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;
            }
            else
            {
                // Fallback horizontal plane at turret height if pointer hovers outer decorative backdrop
                Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0f, turretTransform.position.y, 0f));
                if (!fallbackPlane.Raycast(ray, out float enter))
                    return;
                targetPoint = ray.GetPoint(enter);
            }

            AimWorldPoint = targetPoint;
            RotateTurretToward(targetPoint, Time.deltaTime);
        }

        public void RotateTurretToward(Vector3 worldPoint, float deltaTime)
        {
            if (turretTransform == null)
                return;

            Vector3 worldDir = worldPoint - turretTransform.position;
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.001f)
                return;

            Transform parent = turretTransform.parent;
            if (parent != null)
            {
                Vector3 localDir = parent.InverseTransformDirection(worldDir);
                localDir.y = 0f;
                if (localDir.sqrMagnitude < 0.001f)
                    return;

                Quaternion targetLocalRot = Quaternion.LookRotation(localDir.normalized, Vector3.up);
                turretTransform.localRotation = Quaternion.RotateTowards(
                    turretTransform.localRotation,
                    targetLocalRot,
                    turnSpeed * deltaTime
                );
            }
            else
            {
                Quaternion targetRot = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
                turretTransform.rotation = Quaternion.RotateTowards(
                    turretTransform.rotation,
                    targetRot,
                    turnSpeed * deltaTime
                );
            }
        }
    }
}
