using UnityEngine;
using VehicularCombat.Enemies;
using VehicularCombat.VFX;

namespace VehicularCombat.Combat
{
    /// <summary>
    /// Player combat projectile: flies straight, damages EnemyHealth on hit, and destroys on impact/timeout.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public float speed = 34f;
        public float damage = 25f;
        public float lifeTime = 2.0f;

        private Rigidbody rb;
        private float spawnTime;
        private bool consumed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void Launch(Vector3 direction, Collider[] ownerColliders)
        {
            spawnTime = Time.time;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = transform.forward;
            direction.Normalize();

            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            var myCol = GetComponent<Collider>();
            if (myCol != null && ownerColliders != null)
            {
                foreach (var oc in ownerColliders)
                {
                    if (oc != null)
                        Physics.IgnoreCollision(myCol, oc, true);
                }
            }

            rb.linearVelocity = direction * speed;
        }

        private void Update()
        {
            if (Time.time - spawnTime >= lifeTime)
            {
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (consumed)
                return;

            if (collision.collider.GetComponent<Projectile>() != null)
                return;

            consumed = true;
            Vector3 hitPos = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Vector3 hitNormal = collision.contactCount > 0 ? collision.GetContact(0).normal : -transform.forward;
            VfxManager.Instance?.PlayBulletHit(hitPos, hitNormal);

            var enemy = collision.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}
