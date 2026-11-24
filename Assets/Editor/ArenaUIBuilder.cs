using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using VehicularCombat.Audio;
using VehicularCombat.Combat;
using VehicularCombat.Core;
using VehicularCombat.Enemies;
using VehicularCombat.UI;
using VehicularCombat.Vehicle;
using VehicularCombat.VFX;

namespace VehicularCombat.EditorTools
{
    /// <summary>
    /// Builds the complete pixel-art uGUI Canvas (Scale With Screen Size 1920x1080),
    /// EventSystem (InputSystemUIInputModule), World-Space Enemy HealthBars, AudioManager & VfxManager.
    /// </summary>
    public static class ArenaUIBuilder
    {
        public static void EnsureUiSpritesImported()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/UI" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                VehicularAssetPostprocessor.ApplyPixelUiSpriteSettings(ti, path);
                ti.SaveAndReimport();
            }
        }

        public static void AttachAudioAndVfxManagers(Transform managersRoot)
        {
            var audioGo = new GameObject("AudioManager");
            audioGo.transform.SetParent(managersRoot, false);
            audioGo.AddComponent<AudioManager>();

            // Build crisp pixel square particle material using URP Particles/Unlit + Point-filtered UI_PixelWhite
            Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (pShader == null) pShader = Shader.Find("Sprites/Default");
            string matPath = "Assets/Materials/Combat/PixelParticle_Mat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(pShader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = pShader;
            var whiteTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/UI_PixelWhite.png");
            if (whiteTex != null && mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", whiteTex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);

            var vfxGo = new GameObject("VfxManager");
            vfxGo.transform.SetParent(managersRoot, false);
            var vfx = vfxGo.AddComponent<VfxManager>();
            vfx.pixelParticleMaterial = mat;
        }

        public static void AttachWorldSpaceHealthBar(GameObject enemyRoot, float heightOffset)
        {
            var frameSprite = LoadSprite("Assets/Art/UI/UI_PixelBarFrame.png");
            var whiteSprite = LoadSprite("Assets/Art/UI/UI_PixelWhite.png");

            var canvasGo = new GameObject("HealthBarCanvas");
            canvasGo.transform.SetParent(enemyRoot.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, heightOffset, 0f);
            canvasGo.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var canvas = canvasGo.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            canvas.enabled = false; // Hidden by default!

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(130f, 20f);

            var bgImg = canvasGo.AddComponent<UnityEngine.UI.Image>();
            bgImg.sprite = frameSprite;
            bgImg.type = UnityEngine.UI.Image.Type.Sliced;
            bgImg.color = Color.white;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);

            var fillImg = fillGo.AddComponent<UnityEngine.UI.Image>();
            fillImg.sprite = whiteSprite;
            fillImg.type = UnityEngine.UI.Image.Type.Filled;
            fillImg.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            fillImg.color = new Color(0.25f, 0.90f, 0.35f);

            var bar = canvasGo.AddComponent<EnemyWorldHealthBar>();
            bar.enemyHealth = enemyRoot.GetComponent<EnemyHealth>();
            bar.worldCanvas = canvas;
            bar.fillImage = fillImg;
            bar.showDistance = 12f;
        }

        public static GameObject BuildSceneUI(GameManager gm, WaveSpawner ws, GameObject playerInstance)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var dialSprite = LoadSprite("Assets/Art/UI/UI_SpeedometerDial.png");
            var needleSprite = LoadSprite("Assets/Art/UI/UI_SpeedometerNeedle.png");
            var heartSprite = LoadSprite("Assets/Art/UI/UI_PixelHeart.png");
            var shotgunIcon = LoadSprite("Assets/Art/UI/UI_Icon_Shotgun.png");
            var mgIcon = LoadSprite("Assets/Art/UI/UI_Icon_MachineGun.png");
            var driftIcon = LoadSprite("Assets/Art/UI/UI_Icon_DriftTires.png");
            var panelSprite = LoadSprite("Assets/Art/UI/UI_PixelPanel.png");
            var buttonSprite = LoadSprite("Assets/Art/UI/UI_PixelButton.png");
            var barFrameSprite = LoadSprite("Assets/Art/UI/UI_PixelBarFrame.png");
            var whiteSprite = LoadSprite("Assets/Art/UI/UI_PixelWhite.png");

            // Clean Chinese root (no ---)
            var uiRoot = new GameObject("界面UI");
            uiRoot.transform.position = Vector3.zero;

            // EventSystem with New Input System UI Module
            var esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(uiRoot.transform, false);
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            // Main Canvas (Scale With Screen Size 1920x1080)
            var canvasGo = new GameObject("游戏画布");
            canvasGo.transform.SetParent(uiRoot.transform, false);
            var canvas = canvasGo.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var hudCtrl = canvasGo.AddComponent<GameHUDController>();
            hudCtrl.gameManager = gm;
            hudCtrl.waveSpawner = ws;
            hudCtrl.playerCar = playerInstance.GetComponent<CarControl>();
            hudCtrl.playerHealth = playerInstance.GetComponent<PlayerHealth>();
            hudCtrl.playerWeapon = playerInstance.GetComponent<WeaponController>();

            // =====================================================================
            // 1. HUD Layer (shown during Playing & Paused)
            // =====================================================================
            var hudRoot = CreateFullRect("战斗HUD", canvasGo.transform);
            hudCtrl.hudRoot = hudRoot;

            // 1A. Bottom-Center: Semi-circular Pixel Speedometer + Needle + Pixel Heart & HP Number
            var speedoGo = new GameObject("车速与血量仪表盘");
            speedoGo.transform.SetParent(hudRoot.transform, false);
            var speedoRt = speedoGo.AddComponent<RectTransform>();
            speedoRt.anchorMin = new Vector2(0.5f, 0f);
            speedoRt.anchorMax = new Vector2(0.5f, 0f);
            speedoRt.pivot = new Vector2(0.5f, 0f);
            speedoRt.anchoredPosition = new Vector2(0f, 22f);
            speedoRt.sizeDelta = new Vector2(360f, 180f);
            var dialImg = speedoGo.AddComponent<UnityEngine.UI.Image>();
            dialImg.sprite = dialSprite;
            dialImg.preserveAspect = true;

            // Rotating speed needle anchored at bottom-center hub of dial
            var needleGo = new GameObject("速度指针");
            needleGo.transform.SetParent(speedoGo.transform, false);
            var needleRt = needleGo.AddComponent<RectTransform>();
            needleRt.anchorMin = new Vector2(0.5f, 0f);
            needleRt.anchorMax = new Vector2(0.5f, 0f);
            needleRt.pivot = new Vector2(0.5f, 0.125f);
            needleRt.anchoredPosition = new Vector2(0f, 14f);
            needleRt.sizeDelta = new Vector2(44f, 176f);
            var needleImg = needleGo.AddComponent<UnityEngine.UI.Image>();
            needleImg.sprite = needleSprite;
            needleImg.raycastTarget = false;
            hudCtrl.speedNeedleRect = needleRt;

            // Heart icon + HP number inside the speedometer gray interior
            var hpGroupGo = new GameObject("血量显示");
            hpGroupGo.transform.SetParent(speedoGo.transform, false);
            var hpGroupRt = hpGroupGo.AddComponent<RectTransform>();
            hpGroupRt.anchorMin = new Vector2(0.5f, 0f);
            hpGroupRt.anchorMax = new Vector2(0.5f, 0f);
            hpGroupRt.pivot = new Vector2(0.5f, 0.5f);
            hpGroupRt.anchoredPosition = new Vector2(0f, 52f);
            hpGroupRt.sizeDelta = new Vector2(150f, 44f);

            var heartGo = new GameObject("爱心图标");
            heartGo.transform.SetParent(hpGroupGo.transform, false);
            var heartRt = heartGo.AddComponent<RectTransform>();
            heartRt.anchorMin = new Vector2(0f, 0.5f);
            heartRt.anchorMax = new Vector2(0f, 0.5f);
            heartRt.pivot = new Vector2(0f, 0.5f);
            heartRt.anchoredPosition = new Vector2(12f, 0f);
            heartRt.sizeDelta = new Vector2(38f, 38f);
            var heartImg = heartGo.AddComponent<UnityEngine.UI.Image>();
            heartImg.sprite = heartSprite;
            heartImg.preserveAspect = true;
            heartImg.raycastTarget = false;

            var hpText = CreatePixelText("血量数值", hpGroupGo.transform, font, "100", 34, TextAnchor.MiddleLeft, Color.white);
            var hpTextRt = hpText.rectTransform;
            hpTextRt.anchorMin = new Vector2(0f, 0f);
            hpTextRt.anchorMax = new Vector2(1f, 1f);
            hpTextRt.offsetMin = new Vector2(58f, 0f);
            hpTextRt.offsetMax = new Vector2(-4f, 0f);
            hudCtrl.hpValueText = hpText;

            // 1B. Bottom-Left: Dynamic Buff Sliders (Shotgun, MachineGun, DriftTires)
            var buffContainer = new GameObject("道具Buff栏");
            buffContainer.transform.SetParent(hudRoot.transform, false);
            var buffRt = buffContainer.AddComponent<RectTransform>();
            buffRt.anchorMin = new Vector2(0f, 0f);
            buffRt.anchorMax = new Vector2(0f, 0f);
            buffRt.pivot = new Vector2(0f, 0f);
            buffRt.anchoredPosition = new Vector2(34f, 34f);
            buffRt.sizeDelta = new Vector2(360f, 220f);
            var vlg = buffContainer.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.spacing = 12f;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            CreateBuffRow("Buff_Shotgun", buffContainer.transform, shotgunIcon, barFrameSprite, whiteSprite, new Color(1.0f, 0.45f, 0.18f), out hudCtrl.shotgunBuffRow, out hudCtrl.shotgunSlider, out hudCtrl.shotgunIconRect);
            CreateBuffRow("Buff_MachineGun", buffContainer.transform, mgIcon, barFrameSprite, whiteSprite, new Color(1.0f, 0.82f, 0.22f), out hudCtrl.machineGunBuffRow, out hudCtrl.machineGunSlider, out hudCtrl.machineGunIconRect);
            CreateBuffRow("Buff_DriftTires", buffContainer.transform, driftIcon, barFrameSprite, whiteSprite, new Color(0.0f, 0.92f, 1.0f), out hudCtrl.driftBuffRow, out hudCtrl.driftSlider, out hudCtrl.driftIconRect);

            // 1C. Top-Center: Wave Title & Remaining Enemies
            var waveBox = new GameObject("波次信息");
            waveBox.transform.SetParent(hudRoot.transform, false);
            var waveRt = waveBox.AddComponent<RectTransform>();
            waveRt.anchorMin = new Vector2(0.5f, 1f);
            waveRt.anchorMax = new Vector2(0.5f, 1f);
            waveRt.pivot = new Vector2(0.5f, 1f);
            waveRt.anchoredPosition = new Vector2(0f, -24f);
            waveRt.sizeDelta = new Vector2(420f, 108f);

            var waveTitle = CreatePixelText("当前波次", waveBox.transform, font, "WAVE 1", 52, TextAnchor.UpperCenter, Color.white);
            waveTitle.rectTransform.anchorMin = new Vector2(0f, 0.4f);
            waveTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            waveTitle.rectTransform.offsetMin = Vector2.zero;
            waveTitle.rectTransform.offsetMax = Vector2.zero;
            hudCtrl.waveTitleText = waveTitle;

            var enemiesRem = CreatePixelText("剩余敌人", waveBox.transform, font, "ENEMIES: 4", 28, TextAnchor.LowerCenter, new Color(0.90f, 0.94f, 1.0f));
            enemiesRem.rectTransform.anchorMin = new Vector2(0f, 0f);
            enemiesRem.rectTransform.anchorMax = new Vector2(1f, 0.45f);
            enemiesRem.rectTransform.offsetMin = Vector2.zero;
            enemiesRem.rectTransform.offsetMax = Vector2.zero;
            hudCtrl.enemiesRemainingText = enemiesRem;

            // 1D. Top-Right: Score & Elapsed Time
            var scoreBox = new GameObject("分数与计时");
            scoreBox.transform.SetParent(hudRoot.transform, false);
            var scoreRt = scoreBox.AddComponent<RectTransform>();
            scoreRt.anchorMin = new Vector2(1f, 1f);
            scoreRt.anchorMax = new Vector2(1f, 1f);
            scoreRt.pivot = new Vector2(1f, 1f);
            scoreRt.anchoredPosition = new Vector2(-34f, -24f);
            scoreRt.sizeDelta = new Vector2(360f, 96f);

            var scoreTxt = CreatePixelText("得分文本", scoreBox.transform, font, "SCORE: 0", 34, TextAnchor.UpperRight, new Color(1.0f, 0.88f, 0.30f));
            scoreTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            scoreTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            scoreTxt.rectTransform.offsetMin = Vector2.zero;
            scoreTxt.rectTransform.offsetMax = Vector2.zero;
            hudCtrl.scoreText = scoreTxt;

            var timeTxt = CreatePixelText("计时文本", scoreBox.transform, font, "TIME: 00:00", 28, TextAnchor.LowerRight, Color.white);
            timeTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            timeTxt.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            timeTxt.rectTransform.offsetMin = Vector2.zero;
            timeTxt.rectTransform.offsetMax = Vector2.zero;
            hudCtrl.timerText = timeTxt;

            // =====================================================================
            // 2. Transparent Start Overlay (Click anywhere to start!)
            // =====================================================================
            var startGo = CreateFullRect("开始界面", canvasGo.transform);
            var startBg = startGo.AddComponent<UnityEngine.UI.Image>();
            startBg.color = new Color(0f, 0f, 0f, 0f); // Fully transparent background!
            var startBtn = startGo.AddComponent<UnityEngine.UI.Button>();
            startBtn.transition = UnityEngine.UI.Selectable.Transition.None;

            var startTxt = CreatePixelText("START标题", startGo.transform, font, "START", 110, TextAnchor.MiddleCenter, Color.white);
            startTxt.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            startTxt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            startTxt.rectTransform.sizeDelta = new Vector2(680f, 150f);
            startTxt.rectTransform.anchoredPosition = new Vector2(0f, 40f);

            var startSub = CreatePixelText("开始提示", startGo.transform, font, "CLICK ANYWHERE TO PLAY", 28, TextAnchor.MiddleCenter, new Color(0.88f, 0.92f, 0.98f));
            startSub.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            startSub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            startSub.rectTransform.sizeDelta = new Vector2(680f, 50f);
            startSub.rectTransform.anchoredPosition = new Vector2(0f, -55f);

            hudCtrl.startOverlayPanel = startGo;
            hudCtrl.startBlinkText = startTxt;
            hudCtrl.startAnywhereButton = startBtn;

            // =====================================================================
            // 3. Pause Menu (ESC)
            // =====================================================================
            var pauseBackdrop = CreateFullRect("暂停菜单", canvasGo.transform);
            var pauseDim = pauseBackdrop.AddComponent<UnityEngine.UI.Image>();
            pauseDim.color = new Color(0.04f, 0.05f, 0.08f, 0.68f);

            var pauseWindow = CreatePixelWindow("暂停面板框", pauseBackdrop.transform, panelSprite, new Vector2(480f, 420f));
            var pauseTitle = CreatePixelText("暂停标题", pauseWindow.transform, font, "PAUSED", 56, TextAnchor.MiddleCenter, Color.white);
            pauseTitle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            pauseTitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            pauseTitle.rectTransform.anchoredPosition = new Vector2(0f, -64f);
            pauseTitle.rectTransform.sizeDelta = new Vector2(400f, 70f);

            hudCtrl.pauseResumeButton = CreatePixelButton("继续按钮", pauseWindow.transform, buttonSprite, font, "RESUME", new Vector2(0f, 36f), new Vector2(300f, 64f));
            hudCtrl.pauseRestartButton = CreatePixelButton("重开按钮", pauseWindow.transform, buttonSprite, font, "RESTART", new Vector2(0f, -46f), new Vector2(300f, 64f));
            hudCtrl.pauseQuitButton = CreatePixelButton("退出按钮", pauseWindow.transform, buttonSprite, font, "QUIT", new Vector2(0f, -128f), new Vector2(300f, 64f));
            hudCtrl.pausePanel = pauseBackdrop;
            pauseBackdrop.SetActive(false);

            // =====================================================================
            // 4. Result Panel (Game Over / Victory)
            // =====================================================================
            var resultBackdrop = CreateFullRect("结算面板", canvasGo.transform);
            var resultDim = resultBackdrop.AddComponent<UnityEngine.UI.Image>();
            resultDim.color = new Color(0.04f, 0.05f, 0.08f, 0.74f);

            var resultWindow = CreatePixelWindow("结算面板框", resultBackdrop.transform, panelSprite, new Vector2(540f, 460f));
            var resTitle = CreatePixelText("结算标题", resultWindow.transform, font, "GAME OVER", 62, TextAnchor.MiddleCenter, new Color(1.0f, 0.28f, 0.28f));
            resTitle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            resTitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            resTitle.rectTransform.anchoredPosition = new Vector2(0f, -68f);
            resTitle.rectTransform.sizeDelta = new Vector2(480f, 80f);

            var resScore = CreatePixelText("最终分数", resultWindow.transform, font, "SCORE: 0", 36, TextAnchor.MiddleCenter, new Color(1.0f, 0.88f, 0.30f));
            resScore.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            resScore.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            resScore.rectTransform.anchoredPosition = new Vector2(0f, -150f);
            resScore.rectTransform.sizeDelta = new Vector2(440f, 50f);

            var resTime = CreatePixelText("本局耗时", resultWindow.transform, font, "TIME: 00:00", 32, TextAnchor.MiddleCenter, Color.white);
            resTime.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            resTime.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            resTime.rectTransform.anchoredPosition = new Vector2(0f, -205f);
            resTime.rectTransform.sizeDelta = new Vector2(440f, 46f);

            hudCtrl.resultTitleText = resTitle;
            hudCtrl.resultScoreText = resScore;
            hudCtrl.resultTimeText = resTime;
            hudCtrl.resultRestartButton = CreatePixelButton("结算重开按钮", resultWindow.transform, buttonSprite, font, "RESTART", new Vector2(0f, -68f), new Vector2(300f, 64f));
            hudCtrl.resultQuitButton = CreatePixelButton("结算退出按钮", resultWindow.transform, buttonSprite, font, "QUIT", new Vector2(0f, -150f), new Vector2(300f, 64f));
            hudCtrl.resultPanel = resultBackdrop;
            resultBackdrop.SetActive(false);

            return uiRoot;
        }

        private static void CreateBuffRow(string name, Transform parent, Sprite iconSprite, Sprite barFrameSprite, Sprite whiteSprite, Color fillColor,
            out GameObject rowGo, out UnityEngine.UI.Slider slider, out RectTransform iconRt)
        {
            rowGo = new GameObject(name);
            rowGo.transform.SetParent(parent, false);
            var rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(330f, 58f);

            var iconGo = new GameObject("图标");
            iconGo.transform.SetParent(rowGo.transform, false);
            iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(28f, 0f);
            iconRt.sizeDelta = new Vector2(54f, 54f);
            var iconImg = iconGo.AddComponent<UnityEngine.UI.Image>();
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var sliderGo = new GameObject("倒计时条");
            sliderGo.transform.SetParent(rowGo.transform, false);
            var sRt = sliderGo.AddComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0.5f);
            sRt.anchorMax = new Vector2(1f, 0.5f);
            sRt.pivot = new Vector2(0f, 0.5f);
            sRt.offsetMin = new Vector2(68f, -15f);
            sRt.offsetMax = new Vector2(0f, 15f);

            var bgImg = sliderGo.AddComponent<UnityEngine.UI.Image>();
            bgImg.sprite = barFrameSprite;
            bgImg.type = UnityEngine.UI.Image.Type.Sliced;
            bgImg.raycastTarget = false;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            var faRt = fillArea.AddComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.offsetMin = new Vector2(4f, 4f);
            faRt.offsetMax = new Vector2(-4f, -4f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillArea.transform, false);
            var fRt = fillGo.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            var fImg = fillGo.AddComponent<UnityEngine.UI.Image>();
            fImg.sprite = whiteSprite;
            fImg.color = fillColor;
            fImg.raycastTarget = false;

            slider = sliderGo.AddComponent<UnityEngine.UI.Slider>();
            slider.interactable = false;
            slider.transition = UnityEngine.UI.Selectable.Transition.None;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.fillRect = fRt;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            rowGo.SetActive(false); // Hidden until buff activates
        }

        private static GameObject CreateFullRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private static GameObject CreatePixelWindow(string name, Transform parent, Sprite panelSprite, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = panelSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.5f;
            return go;
        }

        private static UnityEngine.UI.Button CreatePixelButton(string name, Transform parent, Sprite buttonSprite, Font font, string label, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = buttonSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.5f;

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.25f, 1f);
            colors.pressedColor = new Color(0.75f, 0.80f, 0.90f, 1f);
            btn.colors = colors;

            var txt = CreatePixelText("按钮文本", go.transform, font, label, 30, TextAnchor.MiddleCenter, Color.white);
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;

            return btn;
        }

        private static UnityEngine.UI.Text CreatePixelText(string name, Transform parent, Font font, string content, int fontSize, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var txt = go.AddComponent<UnityEngine.UI.Text>();
            txt.font = font;
            txt.fontStyle = FontStyle.Bold;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = color;
            txt.text = content;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;

            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.05f, 0.06f, 0.09f, 0.95f);
            outline.effectDistance = new Vector2(3f, -3f);
            return txt;
        }

        private static Sprite LoadSprite(string path)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null)
                Debug.LogWarning($"[ArenaUIBuilder] Missing Sprite at {path}");
            return sp;
        }
    }
}
