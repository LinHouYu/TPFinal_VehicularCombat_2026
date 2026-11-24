using System;
using System.IO;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VehicularCombat.Combat;
using VehicularCombat.Core;
using VehicularCombat.Enemies;
using VehicularCombat.Pickups;
using VehicularCombat.Vehicle;

namespace VehicularCombat.EditorTools
{
    /// <summary>
    /// Builds MainArena (Assets/Scenes/MainArena.unity) with clean Chinese Hierarchy naming,
    /// Ground layer & unified BoxCollider, modular cover & outer occlusion, invisible walls,
    /// baked NavMeshSurface, spawn points, official Unity 6 WheelCollider PlayerCar prefab
    /// (CarControl, WheelControl, TurretAim, WeaponController, CinemachineImpulseSource),
    /// and Cinemachine 3 top-down camera with Confiner 3D + Impulse Listener.
    /// </summary>
    public static class ArenaSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MainArena.unity";
        private const string NavMeshAssetDir = "Assets/Scenes/MainArena";
        private const string PlayerPrefabPath = "Assets/Prefabs/Vehicles/PlayerCar.prefab";
        private const string BulletPrefabPath = "Assets/Prefabs/Combat/DefaultBullet.prefab";
        private const string EnemyBulletPrefabPath = "Assets/Prefabs/Combat/EnemyBullet.prefab";
        private const string KamikazePrefabPath = "Assets/Prefabs/Enemies/Enemy_Kamikaze.prefab";
        private const string SniperPrefabPath = "Assets/Prefabs/Enemies/Enemy_Sniper.prefab";
        private const string TankPrefabPath = "Assets/Prefabs/Enemies/Enemy_Tank.prefab";
        private const float ArenaHalfSize = 20f;   // 40m x 40m playable arena
        private const float TileSize = 2f;         // 32 BB units = 2.0m

        [MenuItem("Tools/Vehicular Combat/2. Build MainArena Scene")]
        public static string BuildMainArenaScene()
        {
            string texMatReport = VehicularAssetPostprocessor.ConfigureAllTexturesAndMaterials();
            ArenaUIBuilder.EnsureUiSpritesImported();
            int groundLayer = EnsureLayer("Ground");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Clean Chinese top-level Hierarchy categories (Transform all zero, no --- symbols)
            var envRoot = CreateRoot("地图环境");
            var boundariesRoot = CreateRoot("边界碰撞");
            var spawnsRoot = CreateRoot("刷新点");
            var playerRoot = CreateRoot("玩家");
            var camLightRoot = CreateRoot("相机与灯光");
            var managersRoot = CreateRoot("游戏管理");

            // Load Blockbench FBX assets
            var floorFbx = LoadModel("Assets/Models/Environment/Env_FloorTile/Env_FloorTile.fbx");
            var wallFbx = LoadModel("Assets/Models/Environment/Env_WallSegment/Env_WallSegment.fbx");
            var containerFbx = LoadModel("Assets/Models/Environment/Env_Container/Env_Container.fbx");
            var barricadeFbx = LoadModel("Assets/Models/Environment/Env_Barricade/Env_Barricade.fbx");
            var pillarFbx = LoadModel("Assets/Models/Environment/Env_PillarCover/Env_PillarCover.fbx");
            var bldgFbx = LoadModel("Assets/Models/Environment/Env_OuterBuilding/Env_OuterBuilding.fbx");
            var playerFbx = LoadModel("Assets/Models/Vehicles/PlayerCar/PlayerCar.fbx");

            // =====================================================================
            // 地图环境
            // =====================================================================
            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(envRoot.transform, false);
            groundGo.layer = groundLayer;
            groundGo.isStatic = true;

            int tilesPerSide = Mathf.RoundToInt((ArenaHalfSize * 2f) / TileSize);
            for (int ix = 0; ix < tilesPerSide; ix++)
            {
                for (int iz = 0; iz < tilesPerSide; iz++)
                {
                    float x = -ArenaHalfSize + TileSize * 0.5f + ix * TileSize;
                    float z = -ArenaHalfSize + TileSize * 0.5f + iz * TileSize;
                    var tile = InstantiateFbx(floorFbx, $"Floor_{ix}_{iz}", groundGo.transform, new Vector3(x, 0f, z), Quaternion.identity, Vector3.one);
                    SetLayerRecursively(tile, groundLayer);
                    SetStaticRecursively(tile, true);
                }
            }

            // Single unified BoxCollider on Ground for smooth WheelCollider driving
            var groundCol = groundGo.AddComponent<BoxCollider>();
            groundCol.center = new Vector3(0f, -0.5f, 0f);
            groundCol.size = new Vector3(ArenaHalfSize * 2f + 2f, 1f, ArenaHalfSize * 2f + 2f);

            // Perimeter Walls + Corner Pillars
            var wallsGo = new GameObject("PerimeterWalls");
            wallsGo.transform.SetParent(envRoot.transform, false);
            Vector3 wallScale = new Vector3(1f, 1.8f, 1.4f);
            for (int i = 0; i < tilesPerSide; i++)
            {
                float offset = -ArenaHalfSize + TileSize * 0.5f + i * TileSize;
                SetStaticRecursively(InstantiateFbx(wallFbx, $"Wall_N_{i}", wallsGo.transform, new Vector3(offset, 0f, ArenaHalfSize + 0.15f), Quaternion.Euler(0f, 180f, 0f), wallScale), true);
                SetStaticRecursively(InstantiateFbx(wallFbx, $"Wall_S_{i}", wallsGo.transform, new Vector3(offset, 0f, -ArenaHalfSize - 0.15f), Quaternion.identity, wallScale), true);
                SetStaticRecursively(InstantiateFbx(wallFbx, $"Wall_E_{i}", wallsGo.transform, new Vector3(ArenaHalfSize + 0.15f, 0f, offset), Quaternion.Euler(0f, -90f, 0f), wallScale), true);
                SetStaticRecursively(InstantiateFbx(wallFbx, $"Wall_W_{i}", wallsGo.transform, new Vector3(-ArenaHalfSize - 0.15f, 0f, offset), Quaternion.Euler(0f, 90f, 0f), wallScale), true);
            }
            Vector3[] corners =
            {
                new Vector3(ArenaHalfSize, 0f, ArenaHalfSize),
                new Vector3(-ArenaHalfSize, 0f, ArenaHalfSize),
                new Vector3(ArenaHalfSize, 0f, -ArenaHalfSize),
                new Vector3(-ArenaHalfSize, 0f, -ArenaHalfSize),
            };
            for (int i = 0; i < corners.Length; i++)
                SetStaticRecursively(InstantiateFbx(pillarFbx, $"CornerPillar_{i}", wallsGo.transform, corners[i], Quaternion.identity, new Vector3(1.5f, 1.6f, 1.5f)), true);

            // Inner Tactical Cover (6 clusters with wide drift lanes)
            var coverGo = new GameObject("InnerCover");
            coverGo.transform.SetParent(envRoot.transform, false);
            PlaceCoverCluster(coverGo.transform, "CoverCluster_NE", containerFbx, barricadeFbx, new Vector3(9.5f, 0f, 9.5f), 35f);
            PlaceCoverCluster(coverGo.transform, "CoverCluster_NW", containerFbx, barricadeFbx, new Vector3(-9.5f, 0f, 9.5f), -35f);
            PlaceCoverCluster(coverGo.transform, "CoverCluster_SE", containerFbx, barricadeFbx, new Vector3(9.5f, 0f, -9.5f), -35f);
            PlaceCoverCluster(coverGo.transform, "CoverCluster_SW", containerFbx, barricadeFbx, new Vector3(-9.5f, 0f, -9.5f), 35f);
            PlaceSolidCover(pillarFbx, "CoverCluster_EastPillar", coverGo.transform, new Vector3(12.5f, 0f, 0f), Quaternion.identity, new Vector3(1.4f, 1.3f, 1.4f), new Vector3(1.0f, 1.125f, 1.0f));
            PlaceSolidCover(pillarFbx, "CoverCluster_WestPillar", coverGo.transform, new Vector3(-12.5f, 0f, 0f), Quaternion.identity, new Vector3(1.4f, 1.3f, 1.4f), new Vector3(1.0f, 1.125f, 1.0f));

            // Outer Visual Occlusion Backdrop
            BuildOuterVisualOcclusion(envRoot.transform, containerFbx, bldgFbx);

            // =====================================================================
            // 边界碰撞
            // =====================================================================
            float wallThick = 4f;
            float wallHeight = 20f;
            float innerEdge = ArenaHalfSize - 0.25f;
            CreateInvisibleWall(boundariesRoot.transform, "InvisibleWall_North", new Vector3(0f, wallHeight * 0.5f - 2f, innerEdge + wallThick * 0.5f), new Vector3(ArenaHalfSize * 2f + wallThick * 2f, wallHeight, wallThick));
            CreateInvisibleWall(boundariesRoot.transform, "InvisibleWall_South", new Vector3(0f, wallHeight * 0.5f - 2f, -(innerEdge + wallThick * 0.5f)), new Vector3(ArenaHalfSize * 2f + wallThick * 2f, wallHeight, wallThick));
            CreateInvisibleWall(boundariesRoot.transform, "InvisibleWall_East", new Vector3(innerEdge + wallThick * 0.5f, wallHeight * 0.5f - 2f, 0f), new Vector3(wallThick, wallHeight, ArenaHalfSize * 2f + wallThick * 2f));
            CreateInvisibleWall(boundariesRoot.transform, "InvisibleWall_West", new Vector3(-(innerEdge + wallThick * 0.5f), wallHeight * 0.5f - 2f, 0f), new Vector3(wallThick, wallHeight, ArenaHalfSize * 2f + wallThick * 2f));
            CreateInvisibleWall(boundariesRoot.transform, "InvisibleCeiling_Cap", new Vector3(0f, wallHeight - 1.5f, 0f), new Vector3(ArenaHalfSize * 2f + wallThick * 2f, 1f, ArenaHalfSize * 2f + wallThick * 2f));

            var confinerGo = new GameObject("CameraConfinerBounds");
            confinerGo.transform.SetParent(boundariesRoot.transform, false);
            confinerGo.transform.position = new Vector3(0f, 18f, -10f);
            var confinerBox = confinerGo.AddComponent<BoxCollider>();
            confinerBox.isTrigger = true;
            confinerBox.size = new Vector3(32f, 16f, 34f);

            // =====================================================================
            // 刷新点
            // =====================================================================
            var enemySpawnsGo = new GameObject("EnemySpawnPoints");
            enemySpawnsGo.transform.SetParent(spawnsRoot.transform, false);
            Vector3[] enemyPositions =
            {
                new Vector3(0f, 0.25f, 16.5f),
                new Vector3(0f, 0.25f, -16.5f),
                new Vector3(16.5f, 0.25f, 12.5f),
                new Vector3(-16.5f, 0.25f, 12.5f),
                new Vector3(16.5f, 0.25f, -12.5f),
                new Vector3(-16.5f, 0.25f, -12.5f),
            };
            var enemySpawnTransforms = new Transform[enemyPositions.Length];
            for (int i = 0; i < enemyPositions.Length; i++)
            {
                var sp = new GameObject($"EnemySpawn_{i + 1:00}");
                sp.transform.SetParent(enemySpawnsGo.transform, false);
                sp.transform.position = enemyPositions[i];
                sp.transform.LookAt(new Vector3(0f, 0.25f, 0f));
                enemySpawnTransforms[i] = sp.transform;
            }

            var pickupSpawnsGo = new GameObject("PickupSpawnPoints");
            pickupSpawnsGo.transform.SetParent(spawnsRoot.transform, false);
            Vector3[] pickupPositions =
            {
                new Vector3(0f, 0.2f, 7.0f),
                new Vector3(0f, 0.2f, -7.0f),
                new Vector3(6.5f, 0.2f, 0f),
                new Vector3(-6.5f, 0.2f, 0f),
                new Vector3(14.0f, 0.2f, 4.5f),
                new Vector3(-14.0f, 0.2f, -4.5f),
            };
            var pickupSpawnTransforms = new Transform[pickupPositions.Length];
            for (int i = 0; i < pickupPositions.Length; i++)
            {
                var sp = new GameObject($"PickupSpawn_{i + 1:00}");
                sp.transform.SetParent(pickupSpawnsGo.transform, false);
                sp.transform.position = pickupPositions[i];
                pickupSpawnTransforms[i] = sp.transform;
            }

            // Build all 4 Pickup Prefabs (TestPickups removed - spawned dynamically by PickupSpawner)
            var pickupShotgunPrefab = BuildOrUpdatePickupPrefab("Assets/Models/Pickups/Pickup_Shotgun/Pickup_Shotgun.fbx", "Assets/Prefabs/Pickups/Pickup_Shotgun.prefab", PickupType.Shotgun, 12f, 0f);
            var pickupMgPrefab = BuildOrUpdatePickupPrefab("Assets/Models/Pickups/Pickup_MachineGun/Pickup_MachineGun.fbx", "Assets/Prefabs/Pickups/Pickup_MachineGun.prefab", PickupType.MachineGun, 12f, 0f);
            var pickupDriftPrefab = BuildOrUpdatePickupPrefab("Assets/Models/Pickups/Pickup_DriftTires/Pickup_DriftTires.fbx", "Assets/Prefabs/Pickups/Pickup_DriftTires.prefab", PickupType.DriftTires, 10f, 0f);
            var pickupRepairPrefab = BuildOrUpdatePickupPrefab("Assets/Models/Pickups/Pickup_RepairKit/Pickup_RepairKit.fbx", "Assets/Prefabs/Pickups/Pickup_RepairKit.prefab", PickupType.RepairKit, 0f, 35f);

            // Build Enemy Projectile + 3 NavMeshAgent Enemy Prefabs (NO WheelColliders!)
            var enemyBulletPrefab = BuildOrUpdateEnemyBulletPrefab();
            var kamikazePrefab = BuildOrUpdateKamikazePrefab();
            var sniperPrefab = BuildOrUpdateSniperPrefab(enemyBulletPrefab);
            var tankPrefab = BuildOrUpdateTankPrefab();

            // =====================================================================
            // 玩家 (DefaultBullet Prefab + Official Unity 6 WheelCollider PlayerCar Prefab)
            // =====================================================================
            var bulletPrefab = BuildOrUpdateDefaultBulletPrefab();
            var playerPrefab = BuildOrUpdatePlayerCarPrefab(playerFbx, bulletPrefab, groundLayer);
            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerInstance.name = "PlayerCar";
            playerInstance.transform.SetParent(playerRoot.transform, false);
            playerInstance.transform.position = new Vector3(0f, 0.15f, 0f);
            playerInstance.transform.rotation = Quaternion.identity;

            // =====================================================================
            // 相机与灯光
            // =====================================================================
            SetupLighting(camLightRoot.transform);
            SetupCinemachineRig(camLightRoot.transform, playerInstance.transform, confinerBox);

            // =====================================================================
            // 游戏管理
            // =====================================================================
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(managersRoot.transform, false);
            var gm = gmGo.AddComponent<GameManager>();
            gm.playerCar = playerInstance.GetComponent<CarControl>();
            gm.playerTurretAim = playerInstance.GetComponent<TurretAim>();
            gm.playerWeapon = playerInstance.GetComponent<WeaponController>();

            var waveSpawnerGo = new GameObject("WaveSpawner");
            waveSpawnerGo.transform.SetParent(managersRoot.transform, false);
            var waveSpawner = waveSpawnerGo.AddComponent<WaveSpawner>();
            waveSpawner.spawnPoints = enemySpawnTransforms;
            waveSpawner.autoStart = false;
            waveSpawner.initialDelay = 0.6f;
            waveSpawner.timeBetweenWaves = 3.0f;
            waveSpawner.minDistanceFromPlayer = 7.5f;
            waveSpawner.waves = new[]
            {
                new WaveDefinition
                {
                    waveName = "Wave 1 - Kamikaze Swarm",
                    spawnInterval = 1.5f,
                    groups = new[]
                    {
                        new WaveEnemyGroup { enemyPrefab = kamikazePrefab, count = 4 },
                    }
                },
                new WaveDefinition
                {
                    waveName = "Wave 2 - Chasers & Snipers",
                    spawnInterval = 1.4f,
                    groups = new[]
                    {
                        new WaveEnemyGroup { enemyPrefab = kamikazePrefab, count = 4 },
                        new WaveEnemyGroup { enemyPrefab = sniperPrefab, count = 2 },
                    }
                },
                new WaveDefinition
                {
                    waveName = "Wave 3 - Heavy Assault",
                    spawnInterval = 1.3f,
                    groups = new[]
                    {
                        new WaveEnemyGroup { enemyPrefab = kamikazePrefab, count = 4 },
                        new WaveEnemyGroup { enemyPrefab = sniperPrefab, count = 2 },
                        new WaveEnemyGroup { enemyPrefab = tankPrefab, count = 2 },
                    }
                },
            };
            gm.waveSpawner = waveSpawner;

            var pickupSpawnerGo = new GameObject("PickupSpawner");
            pickupSpawnerGo.transform.SetParent(managersRoot.transform, false);
            var spawner = pickupSpawnerGo.AddComponent<PickupSpawner>();
            spawner.spawnPoints = pickupSpawnTransforms;
            spawner.pickupPrefabs = new[]
            {
                pickupShotgunPrefab.GetComponent<PickupItem>(),
                pickupMgPrefab.GetComponent<PickupItem>(),
                pickupDriftPrefab.GetComponent<PickupItem>(),
                pickupRepairPrefab.GetComponent<PickupItem>(),
            };
            spawner.autoStart = false;
            spawner.minSpawnInterval = 8f;
            spawner.maxSpawnInterval = 12f;
            spawner.maxActivePickups = 3;
            spawner.spawnHeightOffset = 0.25f;
            gm.pickupSpawner = spawner;

            ArenaUIBuilder.AttachAudioAndVfxManagers(managersRoot.transform);
            ArenaUIBuilder.BuildSceneUI(gm, waveSpawner, playerInstance);

            // =====================================================================
            // NavMeshSurface Bake (Exclude PlayerCar while baking so center is continuous)
            // =====================================================================
            playerInstance.SetActive(false);
            var navSurface = groundGo.AddComponent<NavMeshSurface>();
            navSurface.agentTypeID = 0;
            navSurface.collectObjects = CollectObjects.Volume;
            navSurface.center = new Vector3(0f, 1.5f, 0f);
            navSurface.size = new Vector3(39.4f, 4.5f, 39.4f);
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navSurface.layerMask = ~0;
            navSurface.BuildNavMesh();

            EnsureFolder(NavMeshAssetDir);
            string navDataPath = $"{NavMeshAssetDir}/NavMesh-Ground.asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(navDataPath) != null)
                AssetDatabase.DeleteAsset(navDataPath);
            if (navSurface.navMeshData != null)
            {
                AssetDatabase.CreateAsset(navSurface.navMeshData, navDataPath);
                EditorUtility.SetDirty(navSurface);
            }
            playerInstance.SetActive(true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            string summary = $"MainArena built with clean Chinese Hierarchy at {ScenePath}. GroundLayer={groundLayer}, NavMeshBaked={(navSurface.navMeshData != null)}, PlayerPrefab={PlayerPrefabPath}, BulletPrefab={BulletPrefabPath}.\n{texMatReport}";
            Debug.Log("[ArenaSceneBuilder] " + summary);
            return summary;
        }

        private static GameObject BuildOrUpdateDefaultBulletPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Combat");
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Materials/Combat");

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            string bulletMatPath = "Assets/Materials/Combat/DefaultBullet_Mat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(bulletMatPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, bulletMatPath);
            }
            mat.shader = urpLit;
            mat.SetColor("_BaseColor", new Color(1.0f, 0.85f, 0.2f, 1f));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(2.4f, 1.5f, 0.25f, 1f));
            mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DefaultBullet";
            go.transform.localScale = new Vector3(0.14f, 0.14f, 0.44f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var proj = go.AddComponent<Projectile>();
            proj.speed = 34f;
            proj.lifeTime = 2.0f;

            var saved = PrefabUtility.SaveAsPrefabAsset(go, BulletPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject BuildOrUpdatePlayerCarPrefab(GameObject playerFbx, GameObject bulletPrefab, int groundLayer)
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Vehicles");

            var root = (GameObject)PrefabUtility.InstantiatePrefab(playerFbx);
            root.name = "PlayerCar";
            root.tag = "Player";
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // 1. Rigidbody (Mass = 1500)
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1500f;
            rb.linearDamping = 0.25f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 2. CarBody BoxCollider (strictly clears ground Y=0)
            var carBodyT = FindChildRecursive(root.transform, "CarBody");
            if (carBodyT == null)
                throw new InvalidOperationException("CarBody group not found on PlayerCar FBX!");
            var bodyBox = carBodyT.gameObject.AddComponent<BoxCollider>();
            bodyBox.center = new Vector3(0f, 0.42f, 0f);
            bodyBox.size = new Vector3(0.86f, 0.44f, 1.56f);

            // 3. Independent WheelCollider child objects (NOT children of wheel visuals!)
            // Lifted along Y by exact half of suspensionDistance (+0.15 for suspensionDistance = 0.30)
            var wcRoot = new GameObject("WheelColliders");
            wcRoot.transform.SetParent(root.transform, false);

            const float suspensionDistance = 0.30f; // Half = +0.15m per Unity 6 official WheelCollider spec
            string[,] wheelPairs =
            {
                { "Wheel_FL", "WC_FL", "steer" },
                { "Wheel_FR", "WC_FR", "steer" },
                { "Wheel_RL", "WC_RL", "rear" },
                { "Wheel_RR", "WC_RR", "rear" },
            };

            for (int i = 0; i < 4; i++)
            {
                string visualName = wheelPairs[i, 0];
                string wcName = wheelPairs[i, 1];
                bool isSteerable = wheelPairs[i, 2] == "steer";

                var visualT = FindChildRecursive(root.transform, visualName);
                if (visualT == null)
                    throw new InvalidOperationException($"Wheel visual group '{visualName}' not found on PlayerCar FBX!");

                var renderers = visualT.GetComponentsInChildren<Renderer>(true);
                Bounds wb = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++) wb.Encapsulate(renderers[r].bounds);

                Vector3 localCenter = root.transform.InverseTransformPoint(wb.center);
                float wheelRadius = wb.extents.y; // Exact 0.1875m aligned with wheel mesh

                var wcGo = new GameObject(wcName);
                wcGo.transform.SetParent(wcRoot.transform, false);
                wcGo.transform.localPosition = localCenter + Vector3.up * (suspensionDistance * 0.5f);
                wcGo.transform.localRotation = Quaternion.identity;

                var wc = wcGo.AddComponent<WheelCollider>();
                wc.mass = 25f;
                wc.radius = wheelRadius;
                wc.wheelDampingRate = 0.25f;
                wc.suspensionDistance = suspensionDistance;
                wc.forceAppPointDistance = 0.05f;
                wc.center = Vector3.zero;
                wc.suspensionSpring = new JointSpring
                {
                    spring = 45000f,  // Deflection = 3679N / 45000 = 0.082m -> 0.25*0.30 + 0.082 = 0.157m (exact 50% rest travel, pos.y~0, wheelY~0.188)
                    damper = 5500f,
                    targetPosition = 0.25f,
                };

                var fwd = wc.forwardFriction;
                fwd.extremumSlip = 0.4f;
                fwd.extremumValue = 1.2f;
                fwd.asymptoteSlip = 0.8f;
                fwd.asymptoteValue = 0.8f;
                fwd.stiffness = 1.5f;
                wc.forwardFriction = fwd;

                var side = wc.sidewaysFriction;
                side.extremumSlip = 0.35f;
                side.extremumValue = 1.0f;
                side.asymptoteSlip = 0.7f;
                side.asymptoteValue = 0.65f;
                side.stiffness = 1.25f;
                wc.sidewaysFriction = side;

                var wheelCtrl = wcGo.AddComponent<WheelControl>();
                wheelCtrl.wheelModel = visualT;
                wheelCtrl.steerable = isSteerable;
                wheelCtrl.motorized = true; // 4WD for snappy vehicular combat
            }

            // 4. CarControl (official Unity 6 structure + centreOfGravityOffset = -1f + ApplyRecoil + Rear Drift Buff)
            var carCtrl = root.AddComponent<CarControl>();
            carCtrl.motorTorque = 900f;
            carCtrl.brakeTorque = 1800f;
            carCtrl.maxSpeed = 18f;
            carCtrl.steeringRange = 32f;
            carCtrl.steeringRangeAtMaxSpeed = 14f;
            carCtrl.centreOfGravityOffset = -1f;
            carCtrl.idleBrakeTorque = 0f;
            carCtrl.driftRearSidewaysStiffness = 0.22f;
            carCtrl.driftMotorMultiplier = 1.5f;
            carCtrl.driftMaxSpeedMultiplier = 1.4f;
            carCtrl.driftTrails = BuildDriftTrails(root.transform);

            // 5. TurretAim & Turret/FirePoint positioned strictly at cannon muzzle tip
            var turretT = FindChildRecursive(root.transform, "Turret");
            if (turretT == null)
                throw new InvalidOperationException("Turret group not found on PlayerCar FBX!");

            var turretRenderers = turretT.GetComponentsInChildren<Renderer>(true);
            Bounds tb = turretRenderers[0].bounds;
            for (int r = 1; r < turretRenderers.Length; r++) tb.Encapsulate(turretRenderers[r].bounds);
            Vector3 muzzleTipWorld = new Vector3(turretT.position.x, tb.center.y, tb.max.z + 0.12f);

            var firePointGo = new GameObject("FirePoint");
            firePointGo.transform.SetParent(turretT, true);
            firePointGo.transform.position = muzzleTipWorld;
            firePointGo.transform.localRotation = Quaternion.identity;

            var turretAim = root.AddComponent<TurretAim>();
            turretAim.turretTransform = turretT;
            turretAim.groundLayerMask = 1 << groundLayer;
            turretAim.turnSpeed = 720f;

            // 6. CinemachineImpulseSource (gunshot recoil micro-shake)
            var impulseSource = root.AddComponent<CinemachineImpulseSource>();
            impulseSource.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulseSource.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Recoil;
            impulseSource.ImpulseDefinition.ImpulseDuration = 0.16f;
            impulseSource.DefaultVelocity = new Vector3(0f, -0.2f, -0.25f);

            // 7. WeaponController (Default + Shotgun + MachineGun + ApplyRecoil + Cinemachine Impulse)
            var weaponCtrl = root.AddComponent<WeaponController>();
            weaponCtrl.firePoint = firePointGo.transform;
            weaponCtrl.projectilePrefab = bulletPrefab;
            weaponCtrl.carControl = carCtrl;
            weaponCtrl.impulseSource = impulseSource;
            weaponCtrl.defaultFireInterval = 0.25f;
            weaponCtrl.defaultRecoilForce = 7500f;
            weaponCtrl.defaultShakeForce = 0.30f;
            weaponCtrl.shotgunPelletCount = 6;
            weaponCtrl.shotgunSpreadAngle = 30f;
            weaponCtrl.shotgunFireInterval = 0.72f;
            weaponCtrl.shotgunRecoilForce = 18500f;
            weaponCtrl.shotgunShakeForce = 0.75f;
            weaponCtrl.machineGunFireInterval = 0.075f;
            weaponCtrl.machineGunRecoilForce = 2400f;
            weaponCtrl.machineGunShakeForce = 0.14f;

            // 8. PlayerHealth (with 0.5s i-frames, Cinemachine Impulse hit shake & GameOver hook)
            var health = root.AddComponent<PlayerHealth>();
            health.maxHealth = 100f;
            health.currentHealth = 100f;
            health.invulnerabilityDuration = 0.5f;
            health.impulseSource = impulseSource;
            health.hitShakeForce = 0.65f;

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static GameObject BuildOrUpdateEnemyBulletPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Combat");
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Materials/Combat");

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            string matPath = "Assets/Materials/Combat/EnemyBullet_Mat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = urpLit;
            mat.SetColor("_BaseColor", new Color(1.0f, 0.12f, 0.35f, 1f));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(3.2f, 0.25f, 0.75f, 1f));
            mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "EnemyBullet";
            go.transform.localScale = new Vector3(0.24f, 0.24f, 0.56f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.GetComponent<BoxCollider>().isTrigger = true;

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            var proj = go.AddComponent<EnemyProjectile>();
            proj.speed = 13f;
            proj.damage = 18f;
            proj.knockbackForce = 2200f;
            proj.lifeTime = 4.5f;

            var saved = PrefabUtility.SaveAsPrefabAsset(go, EnemyBulletPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject SetupEnemyBase(string fbxPath, string name, float maxHealth, int score, float agentSpeed, float agentAccel, float agentAngular, float agentRadius, Vector3 colliderSize)
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Enemies");

            var fbx = LoadModel(fbxPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            root.name = name;
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // Kinematic Rigidbody + BoxCollider (per Unity NavMesh best practice: NO WheelColliders on enemies!)
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var col = root.AddComponent<BoxCollider>();
            col.size = colliderSize;
            col.center = new Vector3(0f, colliderSize.y * 0.5f + 0.08f, 0f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = agentSpeed;
            agent.acceleration = agentAccel;
            agent.angularSpeed = agentAngular;
            agent.radius = agentRadius;
            agent.height = 1.1f;
            agent.stoppingDistance = 0f;
            agent.autoBraking = true;

            var health = root.AddComponent<EnemyHealth>();
            health.maxHealth = maxHealth;
            health.currentHealth = maxHealth;
            health.scoreValue = score;

            // World-space overhead pixel health bar (hidden by default until damaged or within 12m)
            ArenaUIBuilder.AttachWorldSpaceHealthBar(root, 1.45f);

            // Visual wheel rotation only (no WheelCollider physics!)
            var spinner = root.AddComponent<EnemyWheelSpinner>();
            spinner.wheelRadius = 0.1875f;
            spinner.wheelVisuals = new[]
            {
                FindChildRecursive(root.transform, "Wheel_FL"),
                FindChildRecursive(root.transform, "Wheel_FR"),
                FindChildRecursive(root.transform, "Wheel_RL"),
                FindChildRecursive(root.transform, "Wheel_RR"),
            };

            return root;
        }

        private static GameObject BuildOrUpdateKamikazePrefab()
        {
            var root = SetupEnemyBase(
                "Assets/Models/Vehicles/Enemy_Kamikaze/Enemy_Kamikaze.fbx",
                "Enemy_Kamikaze",
                maxHealth: 40f,
                score: 100,
                agentSpeed: 9.5f,
                agentAccel: 18f,
                agentAngular: 360f,
                agentRadius: 0.62f,
                colliderSize: new Vector3(1.15f, 0.72f, 1.85f)
            );

            var triggerGo = new GameObject("RamTrigger");
            triggerGo.transform.SetParent(root.transform, false);
            var trig = triggerGo.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = new Vector3(0f, 0.45f, 0.15f);
            trig.size = new Vector3(1.35f, 0.85f, 2.05f);

            var ai = root.AddComponent<EnemyKamikazeAI>();
            ai.moveSpeed = 9.5f;
            ai.contactDamage = 20f;
            ai.knockbackForce = 4500f;
            ai.impactRadius = 1.55f;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, KamikazePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildOrUpdateSniperPrefab(GameObject enemyBulletPrefab)
        {
            var root = SetupEnemyBase(
                "Assets/Models/Vehicles/Enemy_Sniper/Enemy_Sniper.fbx",
                "Enemy_Sniper",
                maxHealth: 65f,
                score: 150,
                agentSpeed: 6.0f,
                agentAccel: 12f,
                agentAngular: 240f,
                agentRadius: 0.62f,
                colliderSize: new Vector3(1.15f, 0.85f, 1.75f)
            );

            var turretT = FindChildRecursive(root.transform, "Turret");
            var firePointGo = new GameObject("FirePoint");
            firePointGo.transform.SetParent(turretT != null ? turretT : root.transform, false);
            if (turretT != null)
            {
                var rends = turretT.GetComponentsInChildren<Renderer>(true);
                Bounds tb = rends[0].bounds;
                for (int r = 1; r < rends.Length; r++) tb.Encapsulate(rends[r].bounds);
                firePointGo.transform.position = new Vector3(turretT.position.x, tb.center.y, tb.max.z + 0.16f);
                firePointGo.transform.localRotation = Quaternion.identity;
            }

            var ai = root.AddComponent<EnemySniperAI>();
            ai.turretTransform = turretT;
            ai.firePoint = firePointGo.transform;
            ai.enemyProjectilePrefab = enemyBulletPrefab;
            ai.moveSpeed = 6.0f;
            ai.approachDistance = 15f;
            ai.retreatDistance = 10f;
            ai.fireInterval = 2.25f;
            ai.bulletSpeed = 13f;
            ai.bulletDamage = 18f;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, SniperPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildOrUpdateTankPrefab()
        {
            var root = SetupEnemyBase(
                "Assets/Models/Vehicles/Enemy_Tank/Enemy_Tank.fbx",
                "Enemy_Tank",
                maxHealth: 180f,
                score: 300,
                agentSpeed: 3.2f,
                agentAccel: 8f,
                agentAngular: 160f,
                agentRadius: 0.72f,
                colliderSize: new Vector3(1.25f, 0.95f, 1.85f)
            );

            var turretT = FindChildRecursive(root.transform, "Turret");
            var ai = root.AddComponent<EnemyTankAI>();
            ai.turretTransform = turretT;
            ai.patrolSpeed = 3.2f;
            ai.chargeCooldown = 4.0f;
            ai.windupDuration = 0.8f;
            ai.chargeSpeed = 14.5f;
            ai.maxChargeDuration = 1.05f;
            ai.chargeDamage = 30f;
            ai.chargeKnockback = 7500f;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, TankPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildOrUpdatePickupPrefab(string fbxPath, string prefabPath, PickupType type, float duration, float healAmount)
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Pickups");

            var fbx = LoadModel(fbxPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            root.name = Path.GetFileNameWithoutExtension(prefabPath);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = new Vector3(1.15f, 1.15f, 1.15f);

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.15f;
            trigger.center = new Vector3(0f, 0.35f, 0f);

            var item = root.AddComponent<PickupItem>();
            item.pickupType = type;
            item.duration = duration;
            item.healAmount = healAmount;
            item.rotationSpeed = 90f;
            item.bobAmplitude = 0.12f;
            item.bobFrequency = 2.4f;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static TrailRenderer[] BuildDriftTrails(Transform carRoot)
        {
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Materials/Combat");

            Shader trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (trailShader == null) trailShader = Shader.Find("Sprites/Default");

            string matPath = "Assets/Materials/Combat/DriftTrail_Mat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(trailShader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = trailShader;
            mat.SetColor("_BaseColor", Color.white);
            // Configure transparent surface for URP Particles/Unlit so colorGradient alpha fades cleanly
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);

            var trailsRoot = new GameObject("DriftTrails");
            trailsRoot.transform.SetParent(carRoot, false);

            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.0f, 0.96f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.15f, 0.65f, 1.0f), 0.55f),
                    new GradientColorKey(new Color(1.0f, 0.55f, 0.1f), 1.0f),
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0.0f),
                    new GradientAlphaKey(0.65f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f),
                }
            );

            string[] names = { "DriftTrail_RL", "DriftTrail_RR" };
            Vector3[] offsets = { new Vector3(-0.47f, 0.06f, -0.44f), new Vector3(0.47f, 0.06f, -0.44f) };
            var results = new TrailRenderer[2];

            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(names[i]);
                go.transform.SetParent(trailsRoot.transform, false);
                go.transform.localPosition = offsets[i];

                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = mat;
                tr.time = 0.65f;
                tr.startWidth = 0.22f;
                tr.endWidth = 0.04f;
                tr.minVertexDistance = 0.08f;
                tr.colorGradient = grad;
                tr.emitting = false;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tr.receiveShadows = false;
                tr.alignment = LineAlignment.TransformZ;
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                results[i] = tr;
            }

            return results;
        }

        private static void SetupLighting(Transform parent)
        {
            var sunGo = new GameObject("Directional Light");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
            var light = sunGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.color = new Color(1.0f, 0.94f, 0.85f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.78f;
            light.shadowBias = 0.04f;
            light.shadowNormalBias = 0.3f;

            // Rich cool-slate industrial ambient so warm amber & cool cyan glow lights stand out cleanly
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.38f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.24f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.12f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.09f, 0.11f, 0.16f);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 100f;

            // Subtle glowing arena accent lamps + Baked Point lights (eliminates flat cheap look)
            var accentRoot = new GameObject("氛围微光灯组");
            accentRoot.transform.SetParent(parent, false);

            var cyanMat = CreateEmissiveFixtureMaterial("Assets/Materials/Combat/LampCyan_Mat.mat", new Color(0.15f, 0.92f, 1.0f), new Color(0.3f, 2.2f, 2.6f));
            var amberMat = CreateEmissiveFixtureMaterial("Assets/Materials/Combat/LampAmber_Mat.mat", new Color(1.0f, 0.68f, 0.20f), new Color(2.4f, 1.35f, 0.28f));

            // 4 Corner Beacon Floodlights (cool cyan glow)
            Vector3[] cornerLamps =
            {
                new Vector3(19.6f, 2.6f, 19.6f),
                new Vector3(-19.6f, 2.6f, 19.6f),
                new Vector3(19.6f, 2.6f, -19.6f),
                new Vector3(-19.6f, 2.6f, -19.6f),
            };
            for (int i = 0; i < cornerLamps.Length; i++)
            {
                CreateGlowLamp(accentRoot.transform, $"CornerGlow_{i + 1}", cornerLamps[i], new Vector3(0.55f, 0.22f, 0.55f), cyanMat,
                    new Color(0.25f, 0.85f, 1.0f), intensity: 14f, range: 16f);
            }

            // 4 Quadrant Cover Work-Lamps (warm amber glow)
            Vector3[] coverLamps =
            {
                new Vector3(9.5f, 2.0f, 9.5f),
                new Vector3(-9.5f, 2.0f, 9.5f),
                new Vector3(9.5f, 2.0f, -9.5f),
                new Vector3(-9.5f, 2.0f, -9.5f),
            };
            for (int i = 0; i < coverLamps.Length; i++)
            {
                CreateGlowLamp(accentRoot.transform, $"CoverGlow_{i + 1}", coverLamps[i], new Vector3(0.36f, 0.16f, 0.36f), amberMat,
                    new Color(1.0f, 0.68f, 0.25f), intensity: 10f, range: 12f);
            }

            // 2 Mid-Pillar Tactical Beacons (subtle cyan glow on East/West pillars)
            CreateGlowLamp(accentRoot.transform, "PillarGlow_E", new Vector3(12.5f, 2.15f, 0f), new Vector3(0.45f, 0.16f, 0.45f), cyanMat,
                new Color(0.20f, 0.90f, 1.0f), intensity: 10f, range: 11f);
            CreateGlowLamp(accentRoot.transform, "PillarGlow_W", new Vector3(-12.5f, 2.15f, 0f), new Vector3(0.45f, 0.16f, 0.45f), cyanMat,
                new Color(0.20f, 0.90f, 1.0f), intensity: 10f, range: 11f);

            // Global URP Post-Processing Volume (Subtle Bloom + ACES Tonemapping + Crisp Color Grading + Soft Vignette)
            SetupPostProcessingVolume(parent);
        }

        private static Material CreateEmissiveFixtureMaterial(string path, Color baseCol, Color emissiveCol)
        {
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Materials/Combat");
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = urpLit;
            mat.SetColor("_BaseColor", baseCol);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissiveCol);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CreateGlowLamp(Transform parent, string name, Vector3 pos, Vector3 cubeScale, Material emissiveMat, Color lightColor, float intensity, float range)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = cubeScale;
            go.isStatic = true;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = emissiveMat;

            var lightGo = new GameObject($"{name}_Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var pt = lightGo.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.lightmapBakeType = LightmapBakeType.Baked;
            pt.color = lightColor;
            pt.intensity = intensity;
            pt.range = range;
            pt.shadows = LightShadows.Soft;
        }

        private static void SetupPostProcessingVolume(Transform parent)
        {
            EnsureFolder("Assets/Settings");
            string profilePath = "Assets/Settings/MainArenaVolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.components.Clear();

            // 1. Subtle Bloom so emissive lamps, bullets & cyan drift trails glow softly without blurring pixels
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.62f);

            // 2. ACES Tonemapping for rich contrast and non-washed highlights
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            // 3. Subtle Color Adjustments for crisp arcade punch
            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.18f);
            colorAdj.contrast.Override(12f);
            colorAdj.saturation.Override(14f);

            // 4. Gentle Vignette framing the arena edges
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.42f);

            EditorUtility.SetDirty(profile);

            var volGo = new GameObject("后处理体积");
            volGo.transform.SetParent(parent, false);
            volGo.layer = 0; // Default layer (included in Camera's volumeLayerMask)
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            vol.sharedProfile = profile;
        }

        [MenuItem("Tools/Vehicular Combat/3. Bake MainArena Lightmaps")]
        public static string BakeMainArenaLightmaps()
        {
            if (!Lightmapping.TryGetLightingSettings(out var ls) || ls == null)
            {
                string lsPath = "Assets/Settings/MainArenaLighting.lighting";
                ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
                if (ls == null)
                {
                    ls = new LightingSettings();
                    AssetDatabase.CreateAsset(ls, lsPath);
                }
                Lightmapping.lightingSettings = ls;
            }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.directSampleCount = 16;
            ls.indirectSampleCount = 64;
            ls.environmentSampleCount = 64;
            ls.lightmapResolution = 12f;
            ls.lightmapMaxSize = 1024;
            ls.ao = true;
            ls.aoMaxDistance = 1.5f;
            ls.aoExponentDirect = 0.6f;
            ls.aoExponentIndirect = 1.25f;
            EditorUtility.SetDirty(ls);

            bool ok = Lightmapping.Bake();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return $"LightmapBake={ok}, lightmaps={LightmapSettings.lightmaps.Length}";
        }

        [MenuItem("Tools/Vehicular Combat/4. Build Windows Exe")]
        public static string BuildWindowsExe()
        {
            PlayerSettings.productName = "Vehicular Combat";
            PlayerSettings.companyName = "LinHouYu";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;

            string outDir = "Builds/Windows";
            Directory.CreateDirectory(outDir);
            string exePath = $"{outDir}/VehicularCombat.exe";

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opts);
            return $"BuildResult={report.summary.result}, errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}, sizeBytes={report.summary.totalSize}, path={exePath}";
        }

        private static void SetupCinemachineRig(Transform parent, Transform playerTarget, BoxCollider confinerVolume)
        {
            var mainCamGo = new GameObject("Main Camera");
            mainCamGo.tag = "MainCamera";
            mainCamGo.transform.SetParent(parent, false);
            mainCamGo.transform.position = new Vector3(0f, 18f, -11f);
            mainCamGo.transform.rotation = Quaternion.Euler(58f, 0f, 0f);

            var cam = mainCamGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            mainCamGo.AddComponent<AudioListener>();
            var camData = mainCamGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            mainCamGo.AddComponent<CinemachineBrain>();

            var vcamGo = new GameObject("CM_TopDownCam");
            vcamGo.transform.SetParent(parent, false);
            vcamGo.transform.position = new Vector3(0f, 18f, -11f);
            vcamGo.transform.rotation = Quaternion.Euler(58f, 0f, 0f);

            var cmCam = vcamGo.AddComponent<CinemachineCamera>();
            cmCam.Target.TrackingTarget = playerTarget;
            cmCam.Lens.FieldOfView = 45f;
            cmCam.Lens.NearClipPlane = 0.3f;
            cmCam.Lens.FarClipPlane = 200f;

            var cmFollow = vcamGo.AddComponent<CinemachineFollow>();
            cmFollow.FollowOffset = new Vector3(0f, 18f, -11f);
            cmFollow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            cmFollow.TrackerSettings.PositionDamping = new Vector3(0.45f, 0.45f, 0.45f);

            var cmConfiner = vcamGo.AddComponent<CinemachineConfiner3D>();
            cmConfiner.BoundingVolume = confinerVolume;
            cmConfiner.SlowingDistance = 1.5f;

            var cmImpulse = vcamGo.AddComponent<CinemachineImpulseListener>();
            cmImpulse.ChannelMask = -1;
            cmImpulse.Gain = 1.0f;
        }

        private static void PlaceCoverCluster(Transform parent, string clusterName, GameObject containerFbx, GameObject barricadeFbx, Vector3 centerPos, float yawDeg)
        {
            var cluster = new GameObject(clusterName);
            cluster.transform.SetParent(parent, false);
            cluster.transform.position = centerPos;
            cluster.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            PlaceSolidCover(containerFbx, $"{clusterName}_Container", cluster.transform, Vector3.zero, Quaternion.identity, new Vector3(1.35f, 1.2f, 1.35f), new Vector3(1.25f, 0.94f, 2.25f), local: true);
            PlaceSolidCover(barricadeFbx, $"{clusterName}_Barricade", cluster.transform, new Vector3(2.1f, 0f, 0.8f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.15f, 1.15f, 1.15f), new Vector3(1.5f, 0.625f, 0.5f), local: true);
        }

        private static void PlaceSolidCover(GameObject fbx, string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Vector3 localColliderSize, bool local = false)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (local)
            {
                go.transform.localPosition = pos;
                go.transform.localRotation = rot;
            }
            else
            {
                go.transform.position = pos;
                go.transform.rotation = rot;
            }
            go.transform.localScale = scale;
            SetStaticRecursively(go, true);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = localColliderSize;
            bc.center = new Vector3(0f, localColliderSize.y * 0.5f, 0f);
        }

        private static void BuildOuterVisualOcclusion(Transform parent, GameObject containerFbx, GameObject bldgFbx)
        {
            var outerRoot = new GameObject("OuterBackdrop_VisualOcclusion");
            outerRoot.transform.SetParent(parent, false);

            var apron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apron.name = "OuterGroundApron_180x180";
            apron.transform.SetParent(outerRoot.transform, false);
            apron.transform.position = new Vector3(0f, -0.58f, 0f);
            apron.transform.localScale = new Vector3(180f, 1f, 180f);
            apron.isStatic = true;
            UnityEngine.Object.DestroyImmediate(apron.GetComponent<Collider>());

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            string outerMatPath = "Assets/Materials/Models/OuterGround_Mat.mat";
            var outerMat = AssetDatabase.LoadAssetAtPath<Material>(outerMatPath);
            if (outerMat == null)
            {
                outerMat = new Material(urpLit);
                AssetDatabase.CreateAsset(outerMat, outerMatPath);
            }
            outerMat.shader = urpLit;
            outerMat.SetColor("_BaseColor", new Color(0.16f, 0.18f, 0.22f, 1f));
            outerMat.SetFloat("_Smoothness", 0.05f);
            var floorTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Environment/Env_FloorTile/Env_FloorTile.png");
            if (floorTex != null)
            {
                outerMat.SetTexture("_BaseMap", floorTex);
                outerMat.SetTextureScale("_BaseMap", new Vector2(45f, 45f));
            }
            EditorUtility.SetDirty(outerMat);
            apron.GetComponent<MeshRenderer>().sharedMaterial = outerMat;

            var rng = new System.Random(2026);
            for (int side = 0; side < 4; side++)
            {
                for (float t = -22f; t <= 22f; t += 6.5f)
                {
                    float dist = 24.5f + (float)(rng.NextDouble() * 2.0);
                    Vector3 pos = side switch
                    {
                        0 => new Vector3(t, 0f, dist),
                        1 => new Vector3(t, 0f, -dist),
                        2 => new Vector3(dist, 0f, t),
                        _ => new Vector3(-dist, 0f, t),
                    };
                    float yaw = (side < 2 ? 90f : 0f) + (float)(rng.NextDouble() * 14.0 - 7.0);
                    SetStaticRecursively(InstantiateFbx(containerFbx, $"OuterContainer_{side}_{Mathf.RoundToInt(t)}", outerRoot.transform, pos, Quaternion.Euler(0f, yaw, 0f), new Vector3(1.8f, 1.8f, 1.8f)), true);
                    if (rng.Next(0, 3) == 0)
                        SetStaticRecursively(InstantiateFbx(containerFbx, $"OuterContainerTop_{side}_{Mathf.RoundToInt(t)}", outerRoot.transform, pos + new Vector3(0f, 1.68f, 0f), Quaternion.Euler(0f, yaw + 5f, 0f), new Vector3(1.8f, 1.8f, 1.8f)), true);
                }
            }

            float[] ringDists = { 33f, 45f };
            for (int rIdx = 0; rIdx < ringDists.Length; rIdx++)
            {
                float d = ringDists[rIdx];
                float step = rIdx == 0 ? 9.5f : 11.5f;
                for (float p = -d; p <= d; p += step)
                {
                    Vector3[] ringPts =
                    {
                        new Vector3(p, 0f, d),
                        new Vector3(p, 0f, -d),
                        new Vector3(d, 0f, p),
                        new Vector3(-d, 0f, p),
                    };
                    for (int k = 0; k < ringPts.Length; k++)
                    {
                        if (k >= 2 && Mathf.Abs(Mathf.Abs(p) - d) < 1f) continue;
                        float sx = 3.5f + (float)(rng.NextDouble() * 2.5);
                        float sy = (rIdx == 0 ? 3.2f : 5.0f) + (float)(rng.NextDouble() * 3.5);
                        float sz = 3.5f + (float)(rng.NextDouble() * 2.5);
                        float yaw = rng.Next(0, 4) * 90f;
                        SetStaticRecursively(InstantiateFbx(bldgFbx, $"OuterBldg_R{rIdx}_{k}_{Mathf.RoundToInt(p)}", outerRoot.transform, ringPts[k], Quaternion.Euler(0f, yaw, 0f), new Vector3(sx, sy, sz)), true);
                    }
                }
            }
        }

        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");
            for (int i = 6; i < layersProp.arraySize; i++)
            {
                var sp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    return i;
                }
            }
            throw new InvalidOperationException($"No free layer slot available for '{layerName}'.");
        }

        private static GameObject CreateRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static void CreateInvisibleWall(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.isStatic = true;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
        }

        private static GameObject LoadModel(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                throw new FileNotFoundException($"Required FBX model not found: {path}");
            return go;
        }

        private static GameObject InstantiateFbx(GameObject fbx, string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            return go;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }

        private static void SetStaticRecursively(GameObject go, bool isStatic)
        {
            go.isStatic = isStatic;
            for (int i = 0; i < go.transform.childCount; i++)
                SetStaticRecursively(go.transform.GetChild(i).gameObject, isStatic);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
            string name = Path.GetFileName(folderPath);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
