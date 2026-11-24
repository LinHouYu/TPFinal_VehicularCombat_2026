using System.Collections.Generic;
using UnityEngine;

namespace VehicularCombat.Pickups
{
    /// <summary>
    /// Periodically spawns random pickups at unoccupied PickupSpawnPoints up to maxActivePickups.
    /// </summary>
    public class PickupSpawner : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        public Transform[] spawnPoints;
        public PickupItem[] pickupPrefabs;
        public bool autoStart = false;
        public float minSpawnInterval = 8f;
        public float maxSpawnInterval = 12f;
        public int maxActivePickups = 3;
        public float spawnHeightOffset = 0.25f;

        // Public HUD / debug properties
        public int ActivePickupCount { get; private set; }
        public float NextSpawnCountdown => Mathf.Max(0f, nextSpawnTime - Time.time);

        private PickupItem[] slotOccupants;
        private readonly List<int> freeSlotIndices = new List<int>();
        private float nextSpawnTime;
        private bool isSpawning;

        private void Awake()
        {
            int count = spawnPoints != null ? spawnPoints.Length : 0;
            slotOccupants = new PickupItem[count];
        }

        private void Start()
        {
            if (autoStart)
                BeginSpawning();
        }

        public void BeginSpawning()
        {
            isSpawning = true;
            // First pickup appears relatively soon (3.5s) so player gets early action, then normal 8-12s
            nextSpawnTime = Time.time + Mathf.Min(3.5f, minSpawnInterval);
        }

        public void StopSpawning()
        {
            isSpawning = false;
        }

        private void Update()
        {
            if (!isSpawning)
                return;

            CleanupDestroyedSlots();

            if (ActivePickupCount >= maxActivePickups)
                return;

            if (Time.time >= nextSpawnTime)
            {
                TrySpawnOne();
                ScheduleNextSpawn();
            }
        }

        public PickupItem TrySpawnOne()
        {
            CleanupDestroyedSlots();

            if (spawnPoints == null || spawnPoints.Length == 0 || pickupPrefabs == null || pickupPrefabs.Length == 0)
                return null;

            if (ActivePickupCount >= maxActivePickups)
                return null;

            freeSlotIndices.Clear();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null && slotOccupants[i] == null)
                    freeSlotIndices.Add(i);
            }

            if (freeSlotIndices.Count == 0)
                return null;

            int slotIndex = freeSlotIndices[Random.Range(0, freeSlotIndices.Count)];
            PickupItem prefab = pickupPrefabs[Random.Range(0, pickupPrefabs.Length)];
            if (prefab == null)
                return null;

            Vector3 spawnPos = spawnPoints[slotIndex].position;
            spawnPos.y = spawnHeightOffset;

            PickupItem instance = Instantiate(prefab, spawnPos, Quaternion.identity);
            slotOccupants[slotIndex] = instance;
            ActivePickupCount++;

            int capturedSlot = slotIndex;
            instance.OnCollected += _ =>
            {
                if (capturedSlot >= 0 && capturedSlot < slotOccupants.Length && slotOccupants[capturedSlot] == instance)
                {
                    slotOccupants[capturedSlot] = null;
                    ActivePickupCount = Mathf.Max(0, ActivePickupCount - 1);
                }
            };

            return instance;
        }

        private void CleanupDestroyedSlots()
        {
            if (slotOccupants == null) return;
            int alive = 0;
            for (int i = 0; i < slotOccupants.Length; i++)
            {
                if (slotOccupants[i] != null)
                    alive++;
            }
            ActivePickupCount = alive;
        }

        private void ScheduleNextSpawn()
        {
            float min = Mathf.Max(0.5f, minSpawnInterval);
            float max = Mathf.Max(min, maxSpawnInterval);
            nextSpawnTime = Time.time + Random.Range(min, max);
        }
    }
}
