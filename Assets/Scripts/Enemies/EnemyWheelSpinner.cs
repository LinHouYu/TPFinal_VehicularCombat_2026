using UnityEngine;
using UnityEngine.AI;

namespace VehicularCombat.Enemies
{
    /// <summary>
    /// Spins visual enemy wheel models based on actual movement speed (no WheelColliders on enemies).
    /// </summary>
    public class EnemyWheelSpinner : MonoBehaviour
    {
        public Transform[] wheelVisuals;
        public float wheelRadius = 0.1875f;

        private NavMeshAgent agent;
        private Vector3 lastPosition;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (wheelVisuals == null || wheelVisuals.Length == 0 || wheelRadius <= 0.001f)
                return;

            float speed;
            if (agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.001f)
            {
                speed = agent.velocity.magnitude;
            }
            else
            {
                Vector3 delta = transform.position - lastPosition;
                delta.y = 0f;
                speed = Time.deltaTime > 0.0001f ? delta.magnitude / Time.deltaTime : 0f;
            }
            lastPosition = transform.position;

            float degrees = (speed / wheelRadius) * Mathf.Rad2Deg * Time.deltaTime;
            for (int i = 0; i < wheelVisuals.Length; i++)
            {
                if (wheelVisuals[i] != null)
                    wheelVisuals[i].Rotate(degrees, 0f, 0f, Space.Self);
            }
        }
    }
}
