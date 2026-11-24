using UnityEngine;
using VehicularCombat.Enemies;
using VehicularCombat.Pickups;

namespace VehicularCombat.Combat
{
    /// <summary>
    /// Enemy Sniper projectile: vivid slow-moving bolt that damages PlayerHealth and destroys on cover/walls.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyProjectile : MonoBehaviour
    {
        public float speed = 13f;
        public float damage = 18f;
        public float knockbackForce = 2200f;
        public float lifeTime = 4.5f;

        private Rigidbody rb;
        private float spawnTime;
        private Vector3 travelDir;
        private bool consumed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        public void Launch(Vector3 direction)
        {
            spawnTime = Time.time;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = transform.forward;
            travelDir = direction.normalized;
            transform.rotation = Quaternion.LookRotation(travelDir, Vector3.up);
        }

        private void Update()
        {
            transform.position += travelDir * (speed * Time.deltaTime);

            if (Time.time - spawnTime >= lifeTime)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (consumed)
                return;

            // Ignore other enemies, enemy bullets, and pickup triggers
            if (other.GetComponentInParent<EnemyHealth>() != null)
                return;
            if (other.GetComponent<EnemyProjectile>() != null)
                return;
            if (other.GetComponentInParent<PickupItem>() != null)
                return;

            var player = other.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                consumed = true;
                player.TakeDamage(damage, travelDir, knockbackForce);
                Destroy(gameObject);
                return;
            }

            // Destroy when hitting solid walls / cover (ignore non-solid trigger volumes like CameraConfinerBounds)
            if (!other.isTrigger)
            {
                consumed = true;
                Destroy(gameObject);
            }
        }
    }
}
