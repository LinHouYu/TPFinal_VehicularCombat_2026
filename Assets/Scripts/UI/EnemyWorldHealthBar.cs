using UnityEngine;
using VehicularCombat.Enemies;

namespace VehicularCombat.UI
{
    /// <summary>
    /// World-space overhead pixel health bar for enemies.
    /// Hidden by default; reveals only after taking damage or when within showDistance (12m) of PlayerCar.
    /// </summary>
    public class EnemyWorldHealthBar : MonoBehaviour
    {
        [Header("References")]
        public EnemyHealth enemyHealth;
        public UnityEngine.Canvas worldCanvas;
        public UnityEngine.UI.Image fillImage;

        [Header("Visibility")]
        public float showDistance = 12f;

        private Transform playerTransform;
        private Camera mainCam;

        private void Awake()
        {
            if (enemyHealth == null)
                enemyHealth = GetComponentInParent<EnemyHealth>();
            if (worldCanvas == null)
                worldCanvas = GetComponent<UnityEngine.Canvas>();

            // Hidden by default at spawn
            if (worldCanvas != null)
                worldCanvas.enabled = false;
        }

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            mainCam = Camera.main;
            UpdateFill();
        }

        private void LateUpdate()
        {
            if (enemyHealth == null || worldCanvas == null)
                return;

            if (playerTransform == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            bool nearPlayer = false;
            if (playerTransform != null)
            {
                Vector3 delta = playerTransform.position - enemyHealth.transform.position;
                delta.y = 0f;
                nearPlayer = delta.sqrMagnitude <= showDistance * showDistance;
            }

            bool shouldShow = !enemyHealth.IsDead && (enemyHealth.HasTakenDamage || nearPlayer);
            if (worldCanvas.enabled != shouldShow)
                worldCanvas.enabled = shouldShow;

            if (!shouldShow)
                return;

            UpdateFill();

            if (mainCam == null) mainCam = Camera.main;
            if (mainCam != null)
            {
                // Keep overhead bar facing the top-down camera cleanly
                transform.rotation = mainCam.transform.rotation;
            }
        }

        private void UpdateFill()
        {
            if (fillImage == null || enemyHealth == null)
                return;

            float ratio = Mathf.Clamp01(enemyHealth.HealthNormalized);
            fillImage.fillAmount = ratio;
            fillImage.color = ratio > 0.5f
                ? new Color(0.25f, 0.90f, 0.35f)
                : (ratio > 0.25f ? new Color(1.0f, 0.80f, 0.18f) : new Color(1.0f, 0.24f, 0.24f));
        }
    }
}
