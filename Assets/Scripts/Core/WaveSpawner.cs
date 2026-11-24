using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using VehicularCombat.Enemies;

namespace VehicularCombat.Core
{
    [Serializable]
    public class WaveEnemyGroup
    {
        public GameObject enemyPrefab;
        public int count = 3;
    }

    [Serializable]
    public class WaveDefinition
    {
        public string waveName = "Wave 1";
        public float spawnInterval = 1.4f;
        public WaveEnemyGroup[] groups;
    }

    /// <summary>
    /// Spawns escalating waves of enemies across EnemySpawnPoints, waits 3s between cleared waves,
    /// and notifies GameManager.TriggerVictory() once the final wave is eliminated.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        public Transform[] spawnPoints;
        public WaveDefinition[] waves;
        public bool autoStart = false;
        public float initialDelay = 0.6f;
        public float timeBetweenWaves = 3.0f;
        public float minDistanceFromPlayer = 7.5f;

        // Public HUD-ready state & events
        public int CurrentWaveNumber { get; private set; }
        public int TotalWaves => waves != null ? waves.Length : 0;
        public int AliveEnemyCount { get; private set; }
        public int RemainingToSpawn { get; private set; }
        public int TotalRemainingInWave => AliveEnemyCount + RemainingToSpawn;
        public bool IsBetweenWaves { get; private set; }
        public float NextWaveCountdown { get; private set; }

        public event Action<int, int> OnWaveStarted;
        public event Action<int> OnAliveCountChanged;

        private Coroutine waveRoutine;
        private Transform playerTransform;
        private bool stopped;
        private bool started;

        private void Start()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                playerTransform = playerGo.transform;

            if (autoStart)
                BeginWaves();
        }

        public void BeginWaves()
        {
            if (started) return;
            if (waves == null || waves.Length == 0 || spawnPoints == null || spawnPoints.Length == 0)
                return;

            started = true;
            stopped = false;
            waveRoutine = StartCoroutine(RunWavesCoroutine());
        }

        public void StopSpawning()
        {
            stopped = true;
            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }
        }

        private IEnumerator RunWavesCoroutine()
        {
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);

            for (int w = 0; w < waves.Length; w++)
            {
                if (stopped) yield break;

                CurrentWaveNumber = w + 1;
                IsBetweenWaves = false;
                NextWaveCountdown = 0f;

                var wave = waves[w];
                var spawnQueue = BuildShuffledQueue(wave);
                RemainingToSpawn = spawnQueue.Count;
                Debug.Log($"[WaveSpawner] === Starting {wave.waveName} ({CurrentWaveNumber}/{TotalWaves}) - {spawnQueue.Count} enemies ===");
                OnWaveStarted?.Invoke(CurrentWaveNumber, TotalWaves);

                float interval = Mathf.Max(0.25f, wave.spawnInterval);
                for (int i = 0; i < spawnQueue.Count; i++)
                {
                    if (stopped) yield break;
                    if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.GameOver)
                        yield break;

                    RemainingToSpawn = Mathf.Max(0, RemainingToSpawn - 1);
                    SpawnEnemy(spawnQueue[i]);
                    if (i < spawnQueue.Count - 1)
                        yield return new WaitForSeconds(interval);
                }

                // Wait until every enemy from this wave is defeated
                while (AliveEnemyCount > 0)
                {
                    if (stopped) yield break;
                    yield return null;
                }

                Debug.Log($"[WaveSpawner] {wave.waveName} cleared!");

                // Pause between waves unless this was the final wave
                if (w < waves.Length - 1)
                {
                    IsBetweenWaves = true;
                    NextWaveCountdown = timeBetweenWaves;
                    Debug.Log($"[WaveSpawner] Next wave starting in {timeBetweenWaves:F1}s...");
                    while (NextWaveCountdown > 0f)
                    {
                        if (stopped) yield break;
                        yield return null;
                        NextWaveCountdown = Mathf.Max(0f, NextWaveCountdown - Time.deltaTime);
                    }
                    IsBetweenWaves = false;
                }
            }

            if (!stopped && GameManager.Instance != null)
            {
                GameManager.Instance.TriggerVictory();
            }
        }

        public EnemyHealth SpawnEnemy(GameObject prefab)
        {
            if (prefab == null || spawnPoints == null || spawnPoints.Length == 0)
                return null;

            Transform sp = PickSafeSpawnPoint();
            Vector3 spawnPos = sp.position;
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                spawnPos = hit.position;

            var go = Instantiate(prefab, spawnPos, sp.rotation);
            var health = go.GetComponent<EnemyHealth>();
            if (health != null)
            {
                AliveEnemyCount++;
                OnAliveCountChanged?.Invoke(AliveEnemyCount);
                health.OnDied += HandleEnemyDied;
            }

            return health;
        }

        private void HandleEnemyDied(EnemyHealth deadEnemy)
        {
            if (deadEnemy != null)
                deadEnemy.OnDied -= HandleEnemyDied;

            AliveEnemyCount = Mathf.Max(0, AliveEnemyCount - 1);
            OnAliveCountChanged?.Invoke(AliveEnemyCount);
            Debug.Log($"[WaveSpawner] Enemy removed. Remaining alive in Wave {CurrentWaveNumber}: {AliveEnemyCount}");
        }

        private Transform PickSafeSpawnPoint()
        {
            if (playerTransform == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            var candidates = new List<Transform>();
            if (playerTransform != null)
            {
                foreach (var sp in spawnPoints)
                {
                    if (sp == null) continue;
                    float dist = Vector3.Distance(sp.position, playerTransform.position);
                    if (dist >= minDistanceFromPlayer)
                        candidates.Add(sp);
                }
            }

            if (candidates.Count > 0)
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];

            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        }

        private static List<GameObject> BuildShuffledQueue(WaveDefinition wave)
        {
            var list = new List<GameObject>();
            if (wave.groups != null)
            {
                foreach (var g in wave.groups)
                {
                    if (g == null || g.enemyPrefab == null || g.count <= 0) continue;
                    for (int i = 0; i < g.count; i++)
                        list.Add(g.enemyPrefab);
                }
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }
    }
}
