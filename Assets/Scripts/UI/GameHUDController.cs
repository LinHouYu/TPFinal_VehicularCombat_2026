using UnityEngine;
using VehicularCombat.Combat;
using VehicularCombat.Core;
using VehicularCombat.Vehicle;

namespace VehicularCombat.UI
{
    /// <summary>
    /// Drives the pixel-art HUD (speedometer needle + HP, bottom-left buff sliders with animated icons,
    /// top-center wave/enemies, top-right score/timer) and flow overlays (Start, Pause, GameOver/Victory).
    /// </summary>
    public class GameHUDController : MonoBehaviour
    {
        [Header("Core References")]
        public GameManager gameManager;
        public WaveSpawner waveSpawner;
        public CarControl playerCar;
        public PlayerHealth playerHealth;
        public WeaponController playerWeapon;

        [Header("HUD Root & Speedometer / HP")]
        public GameObject hudRoot;
        public RectTransform speedNeedleRect;
        public UnityEngine.UI.Text hpValueText;
        public float needleMinAngle = 82f;   // Left side = 0 speed
        public float needleMaxAngle = -82f;  // Right side = maxSpeed
        public float needleSmoothSpeed = 12f;

        [Header("Bottom-Left Buff Sliders")]
        public GameObject shotgunBuffRow;
        public UnityEngine.UI.Slider shotgunSlider;
        public RectTransform shotgunIconRect;

        public GameObject machineGunBuffRow;
        public UnityEngine.UI.Slider machineGunSlider;
        public RectTransform machineGunIconRect;

        public GameObject driftBuffRow;
        public UnityEngine.UI.Slider driftSlider;
        public RectTransform driftIconRect;

        [Header("Top-Center Wave & Top-Right Score/Time")]
        public UnityEngine.UI.Text waveTitleText;
        public UnityEngine.UI.Text enemiesRemainingText;
        public UnityEngine.UI.Text scoreText;
        public UnityEngine.UI.Text timerText;

        [Header("Flow Panels")]
        public GameObject startOverlayPanel;
        public UnityEngine.UI.Text startBlinkText;
        public UnityEngine.UI.Button startAnywhereButton;

        public GameObject pausePanel;
        public UnityEngine.UI.Button pauseResumeButton;
        public UnityEngine.UI.Button pauseRestartButton;
        public UnityEngine.UI.Button pauseQuitButton;

        public GameObject resultPanel;
        public UnityEngine.UI.Text resultTitleText;
        public UnityEngine.UI.Text resultScoreText;
        public UnityEngine.UI.Text resultTimeText;
        public UnityEngine.UI.Button resultRestartButton;
        public UnityEngine.UI.Button resultQuitButton;

        private float currentNeedleAngle;

        private void Awake()
        {
            currentNeedleAngle = needleMinAngle;
        }

        private void Start()
        {
            ResolveReferences();
            BindButtons();

            if (gameManager != null)
            {
                gameManager.OnGameStateChanged += HandleGameStateChanged;
                HandleGameStateChanged(gameManager.CurrentState);
            }
        }

        private void OnDestroy()
        {
            if (gameManager != null)
                gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void Update()
        {
            if (gameManager == null)
            {
                ResolveReferences();
                if (gameManager == null) return;
            }

            // Gentle retro arcade blink on START text while waiting
            if (gameManager.CurrentState == GameState.WaitingToStart && startBlinkText != null)
            {
                float alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5.0f);
                Color c = startBlinkText.color;
                c.a = alpha;
                startBlinkText.color = c;
            }

            UpdateSpeedometerAndHp();
            UpdateBuffSliders();
            UpdateWaveAndScoreHeader();
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (startOverlayPanel != null) startOverlayPanel.SetActive(state == GameState.WaitingToStart);
            if (hudRoot != null) hudRoot.SetActive(state == GameState.Playing || state == GameState.Paused);
            if (pausePanel != null) pausePanel.SetActive(state == GameState.Paused);

            bool isEnded = state == GameState.GameOver || state == GameState.Victory;
            if (resultPanel != null)
            {
                resultPanel.SetActive(isEnded);
                if (isEnded)
                {
                    if (resultTitleText != null)
                    {
                        resultTitleText.text = state == GameState.Victory ? "VICTORY" : "GAME OVER";
                        resultTitleText.color = state == GameState.Victory
                            ? new Color(1.0f, 0.85f, 0.22f)
                            : new Color(1.0f, 0.28f, 0.28f);
                    }
                    if (resultScoreText != null)
                        resultScoreText.text = $"SCORE: {gameManager.Score}";
                    if (resultTimeText != null)
                        resultTimeText.text = $"TIME: {GameManager.FormatTime(gameManager.ElapsedTime)}";
                }
            }
        }

        private void UpdateSpeedometerAndHp()
        {
            if (speedNeedleRect != null && playerCar != null && playerCar.RigidBody != null)
            {
                Vector3 vel = playerCar.RigidBody.linearVelocity;
                vel.y = 0f;
                float speed = vel.magnitude;
                float maxSpd = Mathf.Max(1f, playerCar.maxSpeed);
                float t = Mathf.Clamp01(speed / maxSpd);
                float targetAngle = Mathf.Lerp(needleMinAngle, needleMaxAngle, t);
                currentNeedleAngle = Mathf.Lerp(currentNeedleAngle, targetAngle, Time.unscaledDeltaTime * needleSmoothSpeed);
                speedNeedleRect.localEulerAngles = new Vector3(0f, 0f, currentNeedleAngle);
            }

            if (hpValueText != null && playerHealth != null)
            {
                int hpInt = Mathf.CeilToInt(Mathf.Max(0f, playerHealth.currentHealth));
                hpValueText.text = hpInt.ToString();
            }
        }

        private void UpdateBuffSliders()
        {
            float pulseScale = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6.5f);
            float pulseTilt = 6f * Mathf.Sin(Time.unscaledTime * 4.5f);

            bool shotgunActive = playerWeapon != null && playerWeapon.CurrentWeapon == WeaponType.Shotgun && playerWeapon.WeaponTimeRemaining > 0f;
            UpdateSingleBuff(shotgunBuffRow, shotgunSlider, shotgunIconRect, shotgunActive,
                playerWeapon != null ? playerWeapon.WeaponTimeRemaining : 0f,
                playerWeapon != null ? playerWeapon.WeaponDurationTotal : 1f,
                pulseScale, pulseTilt);

            bool mgActive = playerWeapon != null && playerWeapon.CurrentWeapon == WeaponType.MachineGun && playerWeapon.WeaponTimeRemaining > 0f;
            UpdateSingleBuff(machineGunBuffRow, machineGunSlider, machineGunIconRect, mgActive,
                playerWeapon != null ? playerWeapon.WeaponTimeRemaining : 0f,
                playerWeapon != null ? playerWeapon.WeaponDurationTotal : 1f,
                pulseScale, -pulseTilt);

            bool driftActive = playerCar != null && playerCar.IsDriftTiresActive && playerCar.DriftTimeRemaining > 0f;
            UpdateSingleBuff(driftBuffRow, driftSlider, driftIconRect, driftActive,
                playerCar != null ? playerCar.DriftTimeRemaining : 0f,
                playerCar != null ? playerCar.DriftDurationTotal : 1f,
                pulseScale, pulseTilt * 1.4f);
        }

        private static void UpdateSingleBuff(GameObject row, UnityEngine.UI.Slider slider, RectTransform iconRect, bool active, float rem, float total, float pulseScale, float pulseTilt)
        {
            if (row != null && row.activeSelf != active)
                row.SetActive(active);

            if (!active)
                return;

            if (slider != null)
                slider.value = total > 0.001f ? Mathf.Clamp01(rem / total) : 0f;

            if (iconRect != null)
            {
                iconRect.localScale = new Vector3(pulseScale, pulseScale, 1f);
                iconRect.localEulerAngles = new Vector3(0f, 0f, pulseTilt);
            }
        }

        private void UpdateWaveAndScoreHeader()
        {
            if (waveSpawner != null)
            {
                int displayWave = Mathf.Max(1, waveSpawner.CurrentWaveNumber);
                if (waveTitleText != null)
                    waveTitleText.text = $"WAVE {displayWave}";

                if (enemiesRemainingText != null)
                {
                    if (waveSpawner.IsBetweenWaves)
                        enemiesRemainingText.text = $"NEXT WAVE: {waveSpawner.NextWaveCountdown:F1}s";
                    else
                        enemiesRemainingText.text = $"ENEMIES: {waveSpawner.TotalRemainingInWave}";
                }
            }

            if (gameManager != null)
            {
                if (scoreText != null)
                    scoreText.text = $"SCORE: {gameManager.Score}";
                if (timerText != null)
                    timerText.text = $"TIME: {GameManager.FormatTime(gameManager.ElapsedTime)}";
            }
        }

        private void BindButtons()
        {
            if (startAnywhereButton != null)
                startAnywhereButton.onClick.AddListener(() => gameManager?.StartGame());
            if (pauseResumeButton != null)
                pauseResumeButton.onClick.AddListener(() => gameManager?.ResumeGame());
            if (pauseRestartButton != null)
                pauseRestartButton.onClick.AddListener(() => gameManager?.RestartGame());
            if (pauseQuitButton != null)
                pauseQuitButton.onClick.AddListener(() => gameManager?.QuitGame());
            if (resultRestartButton != null)
                resultRestartButton.onClick.AddListener(() => gameManager?.RestartGame());
            if (resultQuitButton != null)
                resultQuitButton.onClick.AddListener(() => gameManager?.QuitGame());
        }

        private void ResolveReferences()
        {
            if (gameManager == null) gameManager = GameManager.Instance ?? FindFirstObjectByType<GameManager>();
            if (waveSpawner == null) waveSpawner = FindFirstObjectByType<WaveSpawner>();
            if (playerCar == null) playerCar = FindFirstObjectByType<CarControl>();
            if (playerCar != null)
            {
                if (playerHealth == null) playerHealth = playerCar.GetComponent<PlayerHealth>();
                if (playerWeapon == null) playerWeapon = playerCar.GetComponent<WeaponController>();
            }
        }
    }
}
