using System;
using System.Collections;
using UnityEngine;
using VehicularCombat.Audio;
using VehicularCombat.Core;
using VehicularCombat.VFX;

namespace VehicularCombat.Enemies
{
    /// <summary>
    /// Shared enemy health with crisp solid-white Unlit hit flash (0.09s), scale punch,
    /// voxel explosion VFX, 8-bit SFX, kill scoring, and WaveSpawner death notification.
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        [Header("Stats")]
        public float maxHealth = 50f;
        public float currentHealth = 50f;
        public int scoreValue = 100;

        [Header("Hit & Death Feedback")]
        public float flashDuration = 0.09f;
        public float hitScalePunch = 1.12f;
        public Color explosionAccentColor = new Color(1f, 0.35f, 0.15f);

        public float HealthNormalized => maxHealth > 0f ? currentHealth / maxHealth : 0f;
        public bool HasTakenDamage => currentHealth < maxHealth;
        public bool IsDead { get; private set; }

        public event Action<float, float> OnHealthChanged;
        public event Action<EnemyHealth> OnDied;

        private MeshRenderer[] renderers;
        private Material[] originalMaterials;
        private Vector3 baseScale;
        private Coroutine flashRoutine;
        private static Material sharedFlashWhiteMat;

        private void Awake()
        {
            currentHealth = maxHealth;
            baseScale = transform.localScale;
            renderers = GetComponentsInChildren<MeshRenderer>(true);
            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                originalMaterials[i] = renderers[i].sharedMaterial;

            EnsureFlashMaterial();
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            AudioManager.Instance?.PlayEnemyHit();
            TriggerHitFeedback();

            if (currentHealth <= 0f)
            {
                Die(awardScore: true);
            }
        }

        public void SelfDestruct()
        {
            if (IsDead)
                return;

            currentHealth = 0f;
            Die(awardScore: false);
        }

        private void Die(bool awardScore)
        {
            if (IsDead)
                return;

            IsDead = true;
            VfxManager.Instance?.PlayEnemyExplosion(transform.position, explosionAccentColor);
            AudioManager.Instance?.PlayEnemyExplosion();

            if (awardScore && GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(scoreValue);
            }

            OnDied?.Invoke(this);
            Destroy(gameObject);
        }

        private void TriggerHitFeedback()
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                RestoreMaterialsAndScale();
            }
            flashRoutine = StartCoroutine(HitFeedbackCoroutine());
        }

        private IEnumerator HitFeedbackCoroutine()
        {
            transform.localScale = baseScale * hitScalePunch;
            if (sharedFlashWhiteMat != null && renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].sharedMaterial = sharedFlashWhiteMat;
                }
            }

            yield return new WaitForSeconds(flashDuration);

            RestoreMaterialsAndScale();
            flashRoutine = null;
        }

        private void RestoreMaterialsAndScale()
        {
            transform.localScale = baseScale;
            if (renderers == null || originalMaterials == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && originalMaterials[i] != null)
                    renderers[i].sharedMaterial = originalMaterials[i];
            }
        }

        private static void EnsureFlashMaterial()
        {
            if (sharedFlashWhiteMat != null)
                return;

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            sharedFlashWhiteMat = new Material(sh) { name = "EnemyHitFlash_White" };
            if (sharedFlashWhiteMat.HasProperty("_BaseColor"))
                sharedFlashWhiteMat.SetColor("_BaseColor", Color.white);
            if (sharedFlashWhiteMat.HasProperty("_Color"))
                sharedFlashWhiteMat.SetColor("_Color", Color.white);
        }
    }
}
