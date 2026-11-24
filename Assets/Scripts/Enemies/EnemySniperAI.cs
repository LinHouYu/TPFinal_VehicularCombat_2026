using UnityEngine;
using UnityEngine.AI;
using VehicularCombat.Combat;
using VehicularCombat.Core;

namespace VehicularCombat.Enemies
{
    /// <summary>
    /// Medium-HP kiting sniper using NavMeshAgent: approaches if > 15m, retreats if < 10m,
    /// keeps independent Turret aimed at PlayerCar, and fires visible EnemyProjectile bolts.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
    public class EnemySniperAI : MonoBehaviour
    {
        [Header("References")]
        public Transform turretTransform;
        public Transform firePoint;
        public GameObject enemyProjectilePrefab;

        [Header("Positioning")]
        public float moveSpeed = 6.0f;
        public float approachDistance = 15f;
        public float retreatDistance = 10f;
        public float retreatStep = 5.5f;
        public float repathInterval = 0.25f;

        [Header("Turret & Firing")]
        public float turretTurnSpeed = 240f;
        public float fireInterval = 2.25f;
        public float maxFireRange = 25f;
        public float bulletSpeed = 13f;
        public float bulletDamage = 18f;

        private NavMeshAgent agent;
        private EnemyHealth enemyHealth;
        private Transform playerTransform;
        private float nextRepathTime;
        private float nextFireTime;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            enemyHealth = GetComponent<EnemyHealth>();
            agent.speed = moveSpeed;
        }

        private void Start()
        {
            FindPlayer();
            nextFireTime = Time.time + Random.Range(1.2f, fireInterval);
        }

        private void Update()
        {
            if (enemyHealth.IsDead)
                return;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                if (agent.isOnNavMesh) agent.ResetPath();
                return;
            }

            if (playerTransform == null)
            {
                FindPlayer();
                if (playerTransform == null) return;
            }

            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            // Keep turret smoothly locked onto player
            AimTurret(toPlayer);

            // Kiting movement via NavMeshAgent
            if (Time.time >= nextRepathTime && agent.isOnNavMesh)
            {
                nextRepathTime = Time.time + repathInterval;
                if (dist > approachDistance)
                {
                    agent.SetDestination(playerTransform.position);
                }
                else if (dist < retreatDistance)
                {
                    Vector3 awayDir = (-toPlayer).normalized;
                    Vector3 desiredRetreat = transform.position + awayDir * retreatStep;
                    if (NavMesh.SamplePosition(desiredRetreat, out NavMeshHit navHit, 4.5f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(navHit.position);
                    }
                    else
                    {
                        // Side-strafe fallback if backed against an arena wall
                        Vector3 strafe = Vector3.Cross(Vector3.up, awayDir) * retreatStep;
                        if (NavMesh.SamplePosition(transform.position + strafe, out navHit, 4.5f, NavMesh.AllAreas))
                            agent.SetDestination(navHit.position);
                    }
                }
                else
                {
                    agent.ResetPath();
                }
            }

            // Periodic sniper shot when within range
            if (dist <= maxFireRange && Time.time >= nextFireTime)
            {
                FireAtPlayer(toPlayer);
                nextFireTime = Time.time + fireInterval;
            }
        }

        private void AimTurret(Vector3 worldDir)
        {
            if (turretTransform == null || worldDir.sqrMagnitude < 0.001f)
                return;

            Transform parent = turretTransform.parent;
            if (parent != null)
            {
                Vector3 localDir = parent.InverseTransformDirection(worldDir);
                localDir.y = 0f;
                if (localDir.sqrMagnitude < 0.001f) return;
                Quaternion targetLocal = Quaternion.LookRotation(localDir.normalized, Vector3.up);
                turretTransform.localRotation = Quaternion.RotateTowards(turretTransform.localRotation, targetLocal, turretTurnSpeed * Time.deltaTime);
            }
            else
            {
                Quaternion targetRot = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
                turretTransform.rotation = Quaternion.RotateTowards(turretTransform.rotation, targetRot, turretTurnSpeed * Time.deltaTime);
            }
        }

        private void FireAtPlayer(Vector3 fallbackDir)
        {
            if (enemyProjectilePrefab == null)
                return;

            Transform origin = firePoint != null ? firePoint : (turretTransform != null ? turretTransform : transform);
            Vector3 shootDir = turretTransform != null ? turretTransform.forward : fallbackDir;
            shootDir.y = 0f;
            if (shootDir.sqrMagnitude < 0.001f) shootDir = transform.forward;
            shootDir.Normalize();

            var boltGo = Instantiate(enemyProjectilePrefab, origin.position, Quaternion.LookRotation(shootDir, Vector3.up));
            var proj = boltGo.GetComponent<EnemyProjectile>();
            if (proj != null)
            {
                proj.speed = bulletSpeed;
                proj.damage = bulletDamage;
                proj.Launch(shootDir);
            }
        }

        private void FindPlayer()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                playerTransform = playerGo.transform;
        }
    }
}
