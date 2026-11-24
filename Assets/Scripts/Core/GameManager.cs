using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VehicularCombat.Audio;
using VehicularCombat.Combat;
using VehicularCombat.Pickups;
using VehicularCombat.Vehicle;

namespace VehicularCombat.Core
{
    public enum GameState
    {
        WaitingToStart,
        Playing,
        Paused,
        Victory,
        GameOver
    }

    /// <summary>
    /// Full match lifecycle manager: WaitingToStart (transparent overlay) -> Playing -> Paused (ESC) -> Victory/GameOver -> Restart/Quit.
    /// Tracks Score, TotalKills, and ElapsedTime (stops on pause or match end).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        public CarControl playerCar;
        public TurretAim playerTurretAim;
        public WeaponController playerWeapon;
        public WaveSpawner waveSpawner;
        public PickupSpawner pickupSpawner;

        // Public HUD-ready properties & events
        public GameState CurrentState { get; private set; } = GameState.WaitingToStart;
        public int Score { get; private set; }
        public int TotalKills { get; private set; }
        public float ElapsedTime { get; private set; }

        public event Action<int> OnScoreChanged;
        public event Action<GameState> OnGameStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            ResolveReferences();
            CurrentState = GameState.WaitingToStart;
            ElapsedTime = 0f;
            Score = 0;
            TotalKills = 0;

            // Lock controls & spawners while transparent Start Overlay is displayed
            SetPlayerControlEnabled(false);
            OnGameStateChanged?.Invoke(CurrentState);
        }

        private void Update()
        {
            if (CurrentState == GameState.Playing)
            {
                ElapsedTime += Time.deltaTime;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (CurrentState == GameState.Playing)
                    PauseGame();
                else if (CurrentState == GameState.Paused)
                    ResumeGame();
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (Instance == this)
                Instance = null;
        }

        public void StartGame()
        {
            if (CurrentState != GameState.WaitingToStart)
                return;

            ResolveReferences();
            Time.timeScale = 1f;
            ElapsedTime = 0f;
            CurrentState = GameState.Playing;

            SetPlayerControlEnabled(true);
            if (playerWeapon != null)
                playerWeapon.SuppressFireFor(0.22f);

            if (waveSpawner != null)
                waveSpawner.BeginWaves();
            if (pickupSpawner != null)
                pickupSpawner.BeginSpawning();

            AudioManager.Instance?.PlayUIClick();
            OnGameStateChanged?.Invoke(CurrentState);
            Debug.Log("[GameManager] Game Started!");
        }

        public void PauseGame()
        {
            if (CurrentState != GameState.Playing)
                return;

            CurrentState = GameState.Paused;
            Time.timeScale = 0f;
            if (playerTurretAim != null) playerTurretAim.enabled = false;
            if (playerWeapon != null) playerWeapon.enabled = false;

            AudioManager.Instance?.PlayUIClick();
            OnGameStateChanged?.Invoke(CurrentState);
        }

        public void ResumeGame()
        {
            if (CurrentState != GameState.Paused)
                return;

            CurrentState = GameState.Playing;
            Time.timeScale = 1f;
            if (playerTurretAim != null) playerTurretAim.enabled = true;
            if (playerWeapon != null)
            {
                playerWeapon.enabled = true;
                playerWeapon.SuppressFireFor(0.18f);
            }

            AudioManager.Instance?.PlayUIClick();
            OnGameStateChanged?.Invoke(CurrentState);
        }

        public void RestartGame()
        {
            AudioManager.Instance?.PlayUIClick();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void QuitGame()
        {
            AudioManager.Instance?.PlayUIClick();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void AddScore(int amount)
        {
            if (CurrentState != GameState.Playing || amount <= 0)
                return;

            Score += amount;
            TotalKills++;
            OnScoreChanged?.Invoke(Score);
            Debug.Log($"[GameManager] Enemy defeated! +{amount} Score -> Total Score: {Score} (Kills: {TotalKills})");
        }

        public void TriggerGameOver()
        {
            if (CurrentState != GameState.Playing)
                return;

            CurrentState = GameState.GameOver;
            SetPlayerControlEnabled(false);

            if (waveSpawner != null)
                waveSpawner.StopSpawning();
            if (pickupSpawner != null)
                pickupSpawner.StopSpawning();

            OnGameStateChanged?.Invoke(CurrentState);
            Debug.Log($"[GameManager] === GAME OVER === Final Score: {Score}, Time: {FormatTime(ElapsedTime)}");
        }

        public void TriggerVictory()
        {
            if (CurrentState != GameState.Playing)
                return;

            CurrentState = GameState.Victory;
            SetPlayerControlEnabled(false);

            if (pickupSpawner != null)
                pickupSpawner.StopSpawning();

            OnGameStateChanged?.Invoke(CurrentState);
            Debug.Log($"[GameManager] === VICTORY === Final Score: {Score}, Time: {FormatTime(ElapsedTime)}");
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int mins = total / 60;
            int secs = total % 60;
            return $"{mins:00}:{secs:00}";
        }

        private void ResolveReferences()
        {
            if (playerCar == null)
                playerCar = FindFirstObjectByType<CarControl>();
            if (playerCar != null)
            {
                if (playerTurretAim == null) playerTurretAim = playerCar.GetComponent<TurretAim>();
                if (playerWeapon == null) playerWeapon = playerCar.GetComponent<WeaponController>();
            }
            if (waveSpawner == null)
                waveSpawner = FindFirstObjectByType<WaveSpawner>();
            if (pickupSpawner == null)
                pickupSpawner = FindFirstObjectByType<PickupSpawner>();
        }

        private void SetPlayerControlEnabled(bool enabled)
        {
            ResolveReferences();
            if (playerCar != null) playerCar.enabled = enabled;
            if (playerTurretAim != null) playerTurretAim.enabled = enabled;
            if (playerWeapon != null) playerWeapon.enabled = enabled;
        }
    }
}
