using System;
using Unity.Cinemachine;
using UnityEngine;
using VehicularCombat.Audio;
using VehicularCombat.Core;
using VehicularCombat.Vehicle;
using VehicularCombat.VFX;

namespace VehicularCombat.Combat
{
    /// <summary>
    /// Player health with 0.5s invulnerability frames, Cinemachine Impulse hit shake,
    /// optional knockback, and GameManager GameOver notification on death.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        public float maxHealth = 100f;
        public float currentHealth = 100f;
        public float invulnerabilityDuration = 0.5f;

        [Header("Hit Feedback")]
        public CinemachineImpulseSource impulseSource;
        public float hitShakeForce = 0.65f;

        // Public HUD-ready properties & events
        public float HealthNormalized => maxHealth > 0f ? currentHealth / maxHealth : 0f;
        public bool IsDead => currentHealth <= 0f;
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        public event Action<float, float> OnHealthChanged;
        public event Action OnPlayerDied;

        private float invulnerableUntil = -10f;
        private CarControl carControl;

        private void Awake()
        {
            if (currentHealth <= 0f || currentHealth > maxHealth)
                currentHealth = maxHealth;

            carControl = GetComponent<CarControl>();
            if (impulseSource == null)
                impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || IsDead)
                return;

            float before = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            float actualHealed = currentHealth - before;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            Debug.Log($"[PlayerHealth] RepairKit healed +{actualHealed:F1} (requested {amount:F1}) -> HP: {currentHealth:F1}/{maxHealth:F1}");
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, Vector3.zero, 0f);
        }

        public void TakeDamage(float amount, Vector3 knockbackDirection, float knockbackForce)
        {
            if (amount <= 0f || IsDead || IsInvulnerable)
                return;

            invulnerableUntil = Time.time + invulnerabilityDuration;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            Debug.Log($"[PlayerHealth] Took {amount:F1} damage -> HP: {currentHealth:F1}/{maxHealth:F1}");

            VfxManager.Instance?.PlayPlayerHit(transform.position);
            AudioManager.Instance?.PlayPlayerHit();

            if (impulseSource != null)
            {
                Vector3 shakeDir = knockbackDirection.sqrMagnitude > 0.001f
                    ? knockbackDirection.normalized
                    : Vector3.back;
                impulseSource.GenerateImpulseWithVelocity(shakeDir * hitShakeForce + Vector3.down * (hitShakeForce * 0.5f));
            }

            if (carControl != null && knockbackForce > 0f && knockbackDirection.sqrMagnitude > 0.001f)
            {
                // ApplyRecoil pushes opposite to shootDir, so pass -knockbackDirection to push along knockbackDirection
                carControl.ApplyRecoil(-knockbackDirection, knockbackForce);
            }

            if (currentHealth <= 0f)
            {
                OnPlayerDied?.Invoke();
                if (GameManager.Instance != null)
                    GameManager.Instance.TriggerGameOver();
            }
        }
    }
}
