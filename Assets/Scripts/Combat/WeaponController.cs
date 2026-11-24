using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using VehicularCombat.Audio;
using VehicularCombat.Vehicle;
using VehicularCombat.VFX;

namespace VehicularCombat.Combat
{
    public enum WeaponType
    {
        Default,
        Shotgun,
        MachineGun
    }

    /// <summary>
    /// Handles weapon switching (Default, Shotgun, MachineGun) with auto-revert timers,
    /// directional recoil impulses to CarControl, and Cinemachine Impulse micro-shake.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Header("References")]
        public Transform firePoint;
        public GameObject projectilePrefab;
        public CarControl carControl;
        public CinemachineImpulseSource impulseSource;

        [Header("Default Weapon")]
        public float defaultFireInterval = 0.25f;
        public float defaultRecoilForce = 7500f;
        public float defaultShakeForce = 0.30f;
        public float defaultBulletSpeed = 34f;
        public float defaultBulletDamage = 25f;

        [Header("Shotgun (Spread + Massive Recoil)")]
        [Range(5, 9)] public int shotgunPelletCount = 6;
        [Range(20f, 45f)] public float shotgunSpreadAngle = 30f;
        public float shotgunFireInterval = 0.72f;
        public float shotgunRecoilForce = 18500f;
        public float shotgunShakeForce = 0.75f;
        public float shotgunBulletSpeed = 30f;
        public float shotgunPelletDamage = 18f;

        [Header("MachineGun (Rapid Continuous Recoil)")]
        public float machineGunFireInterval = 0.075f;
        public float machineGunSpreadJitter = 2.5f;
        public float machineGunRecoilForce = 2400f;
        public float machineGunShakeForce = 0.14f;
        public float machineGunBulletSpeed = 40f;
        public float machineGunBulletDamage = 12f;

        // Public HUD-ready state properties
        public WeaponType CurrentWeapon { get; private set; } = WeaponType.Default;
        public float WeaponTimeRemaining { get; private set; }
        public float WeaponDurationTotal { get; private set; }
        public bool IsSpecialWeaponActive => CurrentWeapon != WeaponType.Default && WeaponTimeRemaining > 0f;

        private float nextFireTime;
        private Collider[] ownerColliders;

        private void Awake()
        {
            if (carControl == null)
                carControl = GetComponent<CarControl>();
            if (impulseSource == null)
                impulseSource = GetComponent<CinemachineImpulseSource>();

            ownerColliders = GetComponentsInChildren<Collider>(true);
        }

        private void Update()
        {
            // Timed special weapon countdown -> auto-revert to Default
            if (CurrentWeapon != WeaponType.Default)
            {
                WeaponTimeRemaining -= Time.deltaTime;
                if (WeaponTimeRemaining <= 0f)
                {
                    WeaponTimeRemaining = 0f;
                    WeaponDurationTotal = 0f;
                    CurrentWeapon = WeaponType.Default;
                    Debug.Log("[WeaponController] Special weapon expired -> reverted to Default");
                }
            }

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.leftButton.isPressed && Time.time >= nextFireTime)
            {
                Fire();
            }
        }

        /// <summary>
        /// Equips a timed weapon (or resets to Default). Re-picking refreshes remaining duration.
        /// </summary>
        public void EquipWeapon(WeaponType type, float duration)
        {
            if (type == WeaponType.Default || duration <= 0f)
            {
                CurrentWeapon = WeaponType.Default;
                WeaponTimeRemaining = 0f;
                WeaponDurationTotal = 0f;
                Debug.Log("[WeaponController] Weapon reset to Default");
                return;
            }

            bool isRefresh = CurrentWeapon == type;
            CurrentWeapon = type;
            WeaponDurationTotal = duration;
            WeaponTimeRemaining = duration;
            nextFireTime = Mathf.Min(nextFireTime, Time.time); // Ready to fire immediately on pickup
            Debug.Log($"[WeaponController] {(isRefresh ? "Refreshed" : "Equipped")} {type} for {duration:F1}s");
        }

        public void SuppressFireFor(float seconds)
        {
            nextFireTime = Mathf.Max(nextFireTime, Time.time + seconds);
        }

        public void Fire()
        {
            Transform origin = firePoint != null ? firePoint : transform;
            Vector3 shootDir = origin.forward;
            shootDir.y = 0f;
            if (shootDir.sqrMagnitude < 0.0001f)
                shootDir = transform.forward;
            shootDir.Normalize();

            float interval;
            float recoil;
            float shake;

            switch (CurrentWeapon)
            {
                case WeaponType.Shotgun:
                    interval = shotgunFireInterval;
                    recoil = shotgunRecoilForce;
                    shake = shotgunShakeForce;
                    FireShotgunSpread(origin.position, shootDir);
                    break;

                case WeaponType.MachineGun:
                    interval = machineGunFireInterval;
                    recoil = machineGunRecoilForce;
                    shake = machineGunShakeForce;
                    float jitter = Random.Range(-machineGunSpreadJitter, machineGunSpreadJitter);
                    Vector3 mgDir = Quaternion.Euler(0f, jitter, 0f) * shootDir;
                    SpawnBullet(origin.position, mgDir, machineGunBulletSpeed, machineGunBulletDamage);
                    break;

                default:
                    interval = defaultFireInterval;
                    recoil = defaultRecoilForce;
                    shake = defaultShakeForce;
                    SpawnBullet(origin.position, shootDir, defaultBulletSpeed, defaultBulletDamage);
                    break;
            }

            nextFireTime = Time.time + interval;

            VfxManager.Instance?.PlayMuzzleFlash(origin.position, shootDir, CurrentWeapon);
            AudioManager.Instance?.PlayShoot(CurrentWeapon);

            if (carControl != null)
            {
                carControl.ApplyRecoil(shootDir, recoil);
            }

            if (impulseSource != null)
            {
                Vector3 impulseVel = (-shootDir * shake) + (Vector3.down * (shake * 0.35f));
                impulseSource.GenerateImpulseWithVelocity(impulseVel);
            }
        }

        private void FireShotgunSpread(Vector3 spawnPos, Vector3 baseDir)
        {
            int count = Mathf.Max(1, shotgunPelletCount);
            float halfSpread = shotgunSpreadAngle * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float angle = Mathf.Lerp(-halfSpread, halfSpread, t);
                Vector3 pelletDir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                Vector3 lateralOffset = Vector3.Cross(Vector3.up, baseDir) * ((t - 0.5f) * 0.25f);
                SpawnBullet(spawnPos + lateralOffset, pelletDir, shotgunBulletSpeed, shotgunPelletDamage);
            }
        }

        private void SpawnBullet(Vector3 pos, Vector3 dir, float speed, float damage)
        {
            if (projectilePrefab == null)
                return;

            var bulletGo = Instantiate(projectilePrefab, pos, Quaternion.LookRotation(dir, Vector3.up));
            var proj = bulletGo.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.speed = speed;
                proj.damage = damage;
                proj.Launch(dir, ownerColliders);
            }
        }
    }
}
