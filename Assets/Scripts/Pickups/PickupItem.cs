using System;
using UnityEngine;
using VehicularCombat.Audio;
using VehicularCombat.Combat;
using VehicularCombat.Vehicle;
using VehicularCombat.VFX;

namespace VehicularCombat.Pickups
{
    public enum PickupType
    {
        Shotgun,
        MachineGun,
        DriftTires,
        RepairKit
    }

    /// <summary>
    /// Collectible arena pickup with idle spin + vertical bob animation.
    /// Uses OnTriggerEnter with GetComponentInParent<CarControl>() so any child collider on PlayerCar triggers it.
    /// </summary>
    public class PickupItem : MonoBehaviour
    {
        [Header("Pickup Config")]
        public PickupType pickupType = PickupType.Shotgun;
        [Tooltip("Duration in seconds for timed weapon or drift tire buffs.")]
        public float duration = 12f;
        [Tooltip("Health restored when pickupType is RepairKit.")]
        public float healAmount = 35f;

        [Header("Idle Visual Animation")]
        public float rotationSpeed = 90f;
        public float bobAmplitude = 0.12f;
        public float bobFrequency = 2.4f;

        public event Action<PickupItem> OnCollected;

        private Vector3 basePosition;
        private float bobPhase;
        private bool collected;

        private void Start()
        {
            basePosition = transform.position;
            bobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

            float yOffset = Mathf.Sin(Time.time * bobFrequency + bobPhase) * bobAmplitude;
            Vector3 pos = basePosition;
            pos.y += yOffset;
            transform.position = pos;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected)
                return;

            var car = other.GetComponentInParent<CarControl>();
            if (car == null)
                return;

            collected = true;
            ApplyPickupEffect(car);
            VfxManager.Instance?.PlayPickupBurst(transform.position, GetPickupVfxColor());
            AudioManager.Instance?.PlayPickup();
            Debug.Log($"[PickupItem] Player collected {pickupType}!");

            OnCollected?.Invoke(this);
            Destroy(gameObject);
        }

        private Color GetPickupVfxColor()
        {
            return pickupType switch
            {
                PickupType.Shotgun => new Color(1.0f, 0.45f, 0.15f),
                PickupType.MachineGun => new Color(1.0f, 0.85f, 0.20f),
                PickupType.DriftTires => new Color(0.0f, 0.95f, 1.0f),
                PickupType.RepairKit => new Color(0.25f, 1.0f, 0.45f),
                _ => Color.white
            };
        }

        private void ApplyPickupEffect(CarControl car)
        {
            switch (pickupType)
            {
                case PickupType.Shotgun:
                    var sgWep = car.GetComponent<WeaponController>();
                    if (sgWep != null) sgWep.EquipWeapon(WeaponType.Shotgun, duration);
                    break;

                case PickupType.MachineGun:
                    var mgWep = car.GetComponent<WeaponController>();
                    if (mgWep != null) mgWep.EquipWeapon(WeaponType.MachineGun, duration);
                    break;

                case PickupType.DriftTires:
                    Debug.Log("Drift Tires Activated!");
                    car.ActivateDriftTires(duration);
                    break;

                case PickupType.RepairKit:
                    var health = car.GetComponent<PlayerHealth>();
                    if (health != null) health.Heal(healAmount);
                    break;
            }
        }
    }
}
