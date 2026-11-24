using UnityEngine;
using UnityEngine.AI;
using VehicularCombat.Combat;
using VehicularCombat.Core;

namespace VehicularCombat.Enemies
{
    /// <summary>
    /// Fast, low-HP chaser using NavMeshAgent. Rams directly into PlayerCar,
    /// deals damage + slight knockback, then immediately self-destructs.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
    public class EnemyKamikazeAI : MonoBehaviour
    {
        [Header("Combat & Movement")]
        public float moveSpeed = 9.5f;
        public float contactDamage = 20f;
        public float knockbackForce = 4500f;
        public float impactRadius = 1.55f;
        public float repathInterval = 0.12f;

        private NavMeshAgent agent;
        private EnemyHealth enemyHealth;
        private Transform playerTransform;
        private float nextRepathTime;
        private bool detonated;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            enemyHealth = GetComponent<EnemyHealth>();
            agent.speed = moveSpeed;
            agent.stoppingDistance = 0f;
        }

        private void Start()
        {
            FindPlayer();
        }

        private void Update()
        {
            if (detonated || enemyHealth.IsDead)
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
            if (toPlayer.magnitude <= impactRadius)
            {
                DetonateOnPlayer(playerTransform.GetComponent<PlayerHealth>(), toPlayer);
                return;
            }

            if (Time.time >= nextRepathTime && agent.isOnNavMesh)
            {
                nextRepathTime = Time.time + repathInterval;
                agent.SetDestination(playerTransform.position);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (detonated || enemyHealth.IsDead)
                return;

            var playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                Vector3 dir = playerHealth.transform.position - transform.position;
                DetonateOnPlayer(playerHealth, dir);
            }
        }

        private void DetonateOnPlayer(PlayerHealth playerHealth, Vector3 impactDir)
        {
            if (detonated)
                return;
            detonated = true;

            if (playerHealth != null)
            {
                impactDir.y = 0f;
                if (impactDir.sqrMagnitude < 0.001f) impactDir = transform.forward;
                playerHealth.TakeDamage(contactDamage, impactDir.normalized, knockbackForce);
            }

            Debug.Log($"[EnemyKamikaze] Rammed player for {contactDamage} damage and self-destructed!");
            enemyHealth.SelfDestruct();
        }

        private void FindPlayer()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                playerTransform = playerGo.transform;
        }
    }
}
