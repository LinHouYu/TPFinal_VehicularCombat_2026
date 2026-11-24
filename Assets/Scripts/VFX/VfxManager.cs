using UnityEngine;
using UnityEngine.Rendering;
using VehicularCombat.Combat;

namespace VehicularCombat.VFX
{
    /// <summary>
    /// Lightweight pixel-square ParticleSystem manager (URP Particles/Unlit with Point-filtered square texture).
    /// Handles muzzle sparks, bullet hit sparks, pickup light rings, player hit smoke/sparks, and voxel enemy explosions.
    /// </summary>
    public class VfxManager : MonoBehaviour
    {
        public static VfxManager Instance { get; private set; }

        [Header("Particle Material (URP Particles/Unlit + Pixel Square)")]
        public Material pixelParticleMaterial;

        private ParticleSystem muzzlePs;
        private ParticleSystem hitSparkPs;
        private ParticleSystem pickupPs;
        private ParticleSystem playerHitPs;
        private ParticleSystem explosionPs;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureMaterial();
            muzzlePs = CreateEmitter("PS_MuzzleSparks", gravity: 0.2f, startSizeMin: 0.10f, startSizeMax: 0.20f, lifeMin: 0.08f, lifeMax: 0.18f);
            hitSparkPs = CreateEmitter("PS_BulletHit", gravity: 1.0f, startSizeMin: 0.09f, startSizeMax: 0.18f, lifeMin: 0.14f, lifeMax: 0.28f);
            pickupPs = CreateEmitter("PS_PickupBurst", gravity: -0.35f, startSizeMin: 0.12f, startSizeMax: 0.22f, lifeMin: 0.35f, lifeMax: 0.60f);
            playerHitPs = CreateEmitter("PS_PlayerHit", gravity: 0.4f, startSizeMin: 0.16f, startSizeMax: 0.32f, lifeMin: 0.25f, lifeMax: 0.50f);
            explosionPs = CreateEmitter("PS_EnemyExplosion", gravity: 1.6f, startSizeMin: 0.18f, startSizeMax: 0.42f, lifeMin: 0.35f, lifeMax: 0.72f);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PlayMuzzleFlash(Vector3 pos, Vector3 dir, WeaponType weapon)
        {
            if (muzzlePs == null) return;
            int count = weapon == WeaponType.Shotgun ? 18 : (weapon == WeaponType.MachineGun ? 5 : 9);
            float speed = weapon == WeaponType.Shotgun ? 11f : 7.5f;
            Vector3 fwd = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;

            for (int i = 0; i < count; i++)
            {
                Vector3 spread = (fwd + Random.insideUnitSphere * (weapon == WeaponType.Shotgun ? 0.55f : 0.28f)).normalized;
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos,
                    velocity = spread * Random.Range(speed * 0.45f, speed),
                    startColor = i % 3 == 0 ? new Color(1f, 0.96f, 0.65f) : new Color(1f, 0.62f, 0.12f),
                    startSize = Random.Range(0.10f, weapon == WeaponType.Shotgun ? 0.24f : 0.17f),
                    startLifetime = Random.Range(0.08f, 0.16f)
                };
                muzzlePs.Emit(ep, 1);
            }
        }

        public void PlayBulletHit(Vector3 pos, Vector3 normal)
        {
            if (hitSparkPs == null) return;
            Vector3 baseDir = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
            for (int i = 0; i < 10; i++)
            {
                Vector3 vel = (baseDir + Random.insideUnitSphere * 0.9f).normalized * Random.Range(3.5f, 8.5f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos,
                    velocity = vel,
                    startColor = i % 2 == 0 ? new Color(1f, 0.92f, 0.45f) : new Color(1f, 0.48f, 0.15f),
                    startSize = Random.Range(0.09f, 0.18f),
                    startLifetime = Random.Range(0.14f, 0.26f)
                };
                hitSparkPs.Emit(ep, 1);
            }
        }

        public void PlayPickupBurst(Vector3 pos, Color color)
        {
            if (pickupPs == null) return;
            for (int i = 0; i < 18; i++)
            {
                float angle = (i / 18f) * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), Random.Range(0.4f, 1.1f), Mathf.Sin(angle));
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos + Vector3.up * 0.2f,
                    velocity = radial * Random.Range(2.5f, 5.2f),
                    startColor = i % 3 == 0 ? Color.white : color,
                    startSize = Random.Range(0.12f, 0.22f),
                    startLifetime = Random.Range(0.35f, 0.60f)
                };
                pickupPs.Emit(ep, 1);
            }
        }

        public void PlayPlayerHit(Vector3 pos)
        {
            if (playerHitPs == null) return;
            for (int i = 0; i < 16; i++)
            {
                bool isSmoke = i % 2 == 0;
                Vector3 vel = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1.6f), Random.Range(-1f, 1f)).normalized * Random.Range(2.2f, 6.0f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos + Vector3.up * 0.5f,
                    velocity = vel,
                    startColor = isSmoke ? new Color(0.28f, 0.30f, 0.34f, 0.9f) : new Color(1f, 0.32f, 0.18f, 1f),
                    startSize = isSmoke ? Random.Range(0.24f, 0.38f) : Random.Range(0.12f, 0.20f),
                    startLifetime = Random.Range(0.28f, 0.52f)
                };
                playerHitPs.Emit(ep, 1);
            }
        }

        public void PlayEnemyExplosion(Vector3 pos, Color accentColor)
        {
            if (explosionPs == null) return;
            for (int i = 0; i < 34; i++)
            {
                Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.35f, 1.4f), Random.Range(-1f, 1f)).normalized;
                Color col;
                int bucket = i % 4;
                if (bucket == 0) col = new Color(1f, 0.85f, 0.22f);
                else if (bucket == 1) col = new Color(1f, 0.38f, 0.12f);
                else if (bucket == 2) col = accentColor;
                else col = new Color(0.22f, 0.24f, 0.28f);

                var ep = new ParticleSystem.EmitParams
                {
                    position = pos + Vector3.up * 0.45f,
                    velocity = dir * Random.Range(4.0f, 11.5f),
                    startColor = col,
                    startSize = Random.Range(0.18f, 0.42f),
                    startLifetime = Random.Range(0.35f, 0.72f)
                };
                explosionPs.Emit(ep, 1);
            }
        }

        private ParticleSystem CreateEmitter(string name, float gravity, float startSizeMin, float startSizeMax, float lifeMin, float lifeMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSize = new ParticleSystem.MinMaxCurve(startSizeMin, startSizeMax);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.maxParticles = 400;

            var emission = ps.emission;
            emission.enabled = false;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = pixelParticleMaterial;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;

            return ps;
        }

        private void EnsureMaterial()
        {
            if (pixelParticleMaterial != null)
                return;

            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            pixelParticleMaterial = new Material(sh);
            pixelParticleMaterial.SetColor("_BaseColor", Color.white);
            if (pixelParticleMaterial.HasProperty("_Surface")) pixelParticleMaterial.SetFloat("_Surface", 1f);
            if (pixelParticleMaterial.HasProperty("_SrcBlend")) pixelParticleMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (pixelParticleMaterial.HasProperty("_DstBlend")) pixelParticleMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (pixelParticleMaterial.HasProperty("_ZWrite")) pixelParticleMaterial.SetFloat("_ZWrite", 0f);
            pixelParticleMaterial.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
