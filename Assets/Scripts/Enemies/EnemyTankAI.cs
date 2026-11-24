using UnityEngine;
using UnityEngine.AI;
using VehicularCombat.Combat;
using VehicularCombat.Core;

namespace VehicularCombat.Enemies
{
    /// <summary>
    /// High-HP slow armored Tank using NavMeshAgent: patrols toward PlayerCar,
    /// pauses 0.8s every 4s to telegraph & lock target, then high-speed straight-line charges!
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
    public class EnemyTankAI : MonoBehaviour
    {
        private enum TankState
        {
            Patrolling,
            Windup,
            Charging
        }

        [Header("References")]
        public Transform turretTransform;

        [Header("Movement & Charge")]
        public float patrolSpeed = 3.2f;
        public float chargeCooldown = 4.0f;
        public float chargeRange = 20f;
        public float windupDuration = 0.8f;
        public float chargeSpeed = 14.5f;
        public float maxChargeDuration = 1.05f;
        public float repathInterval = 0.25f;

        [Header("Charge Impact")]
        public float chargeDamage = 30f;
        public float patrolContactDamage = 12f;
        public float chargeKnockback = 7500f;
        public float hitRadius = 1.65f;
        public float obstacleStopDistance = 1.05f;

        private NavMeshAgent agent;
        private EnemyHealth enemyHealth;
        private Transform playerTransform;
        private TankState state = TankState.Patrolling;

        private float stateTimer;
        private float nextChargeReadyTime;
        private float nextRepathTime;
        private Vector3 chargeDir;
        private bool hitPlayerThisCharge;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            enemyHealth = GetComponent<EnemyHealth>();
            agent.speed = patrolSpeed;
        }

        private void Start()
        {
            FindPlayer();
            nextChargeReadyTime = Time.time + Random.Range(2.5f, chargeCooldown);
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

            switch (state)
            {
                case TankState.Patrolling:
                    UpdatePatrol(toPlayer, dist);
                    break;

                case TankState.Windup:
                    UpdateWindup(toPlayer);
                    break;

                case TankState.Charging:
                    UpdateCharge(toPlayer, dist);
                    break;
            }
        }

        private void UpdatePatrol(Vector3 toPlayer, float dist)
        {
            AimTurret(toPlayer, 180f);

            if (Time.time >= nextRepathTime && agent.isOnNavMesh)
            {
                nextRepathTime = Time.time + repathInterval;
                agent.SetDestination(playerTransform.position);
            }

            if (dist <= hitRadius)
            {
                var ph = playerTransform.GetComponent<PlayerHealth>();
                if (ph != null)
                    ph.TakeDamage(patrolContactDamage, toPlayer.normalized, 3500f);
            }

            if (dist <= chargeRange && Time.time >= nextChargeReadyTime)
            {
                state = TankState.Windup;
                stateTimer = windupDuration;
                chargeDir = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : transform.forward;
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
            }
        }

        private void UpdateWindup(Vector3 toPlayer)
        {
            // Continuously lock onto player's latest position during windup telegraph
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                chargeDir = toPlayer.normalized;
                Quaternion hullTarget = Quaternion.LookRotation(chargeDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, hullTarget, 260f * Time.deltaTime);
                AimTurret(chargeDir, 360f);
            }

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                state = TankState.Charging;
                stateTimer = maxChargeDuration;
                hitPlayerThisCharge = false;
                transform.rotation = Quaternion.LookRotation(chargeDir, Vector3.up);
            }
        }

        private void UpdateCharge(Vector3 toPlayer, float dist)
        {
            float step = chargeSpeed * Time.deltaTime;

            // Check obstacle / wall ahead before moving so Tank never clips through walls or cover
            Vector3 rayOrigin = transform.position + Vector3.up * 0.55f;
            if (Physics.SphereCast(rayOrigin, 0.55f, chargeDir, out RaycastHit hit, obstacleStopDistance + step, ~0, QueryTriggerInteraction.Ignore))
            {
                bool isSelf = hit.collider.transform.IsChildOf(transform);
                bool isGround = hit.collider.gameObject.layer == LayerMask.NameToLayer("Ground");
                var hitPlayer = hit.collider.GetComponentInParent<PlayerHealth>();

                if (hitPlayer != null)
                {
                    ApplyChargeHit(hitPlayer);
                    EndCharge();
                    return;
                }

                if (!isSelf && !isGround && hit.collider.GetComponentInParent<EnemyHealth>() == null)
                {
                    EndCharge();
                    return;
                }
            }

            if (agent.isOnNavMesh)
            {
                agent.Move(chargeDir * step);
            }
            else
            {
                transform.position += chargeDir * step;
            }

            if (!hitPlayerThisCharge && dist <= hitRadius)
            {
                var ph = playerTransform.GetComponent<PlayerHealth>();
                if (ph != null)
                {
                    ApplyChargeHit(ph);
                    EndCharge();
                    return;
                }
            }

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                EndCharge();
            }
        }

        private void ApplyChargeHit(PlayerHealth playerHealth)
        {
            if (hitPlayerThisCharge || playerHealth == null)
                return;

            hitPlayerThisCharge = true;
            playerHealth.TakeDamage(chargeDamage, chargeDir, chargeKnockback);
            Debug.Log($"[EnemyTank] Heavy charge hit player for {chargeDamage} damage!");
        }

        private void EndCharge()
        {
            state = TankState.Patrolling;
            nextChargeReadyTime = Time.time + chargeCooldown;
            if (agent.isOnNavMesh)
            {
                agent.Warp(transform.position);
                agent.isStopped = false;
            }
        }

        private void AimTurret(Vector3 worldDir, float turnSpeed)
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
                turretTransform.localRotation = Quaternion.RotateTowards(turretTransform.localRotation, targetLocal, turnSpeed * Time.deltaTime);
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
