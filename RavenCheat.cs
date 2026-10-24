using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityStandardAssets.Characters.FirstPerson;

namespace RavenCheat
{
    public static class Loader
    {
        private static GameObject hostObject;
        private static int initState;
        private static int wrongThreadLogged;

        public static void Init()
        {
            if (Interlocked.CompareExchange(ref initState, 1, 0) != 0) return;

            try
            {
                Application.onBeforeRender += OnBeforeRender;
                WriteLog("Initialization queued in domain " + AppDomain.CurrentDomain.FriendlyName + ".");
            }
            catch (Exception exception)
            {
                Interlocked.Exchange(ref initState, 0);
                WriteLog("Failed to queue initialization: " + exception);
                throw;
            }
        }

        private static void OnBeforeRender()
        {
            SynchronizationContext context = SynchronizationContext.Current;
            if (context == null || context.GetType().FullName != "UnityEngine.UnitySynchronizationContext")
            {
                if (Interlocked.Exchange(ref wrongThreadLogged, 1) == 0)
                {
                    WriteLog("Render callback did not run on the Unity main thread.");
                }
                return;
            }

            if (Interlocked.CompareExchange(ref initState, 2, 1) != 1) return;

            try
            {
                hostObject = new GameObject("RavenCheat_Host");
                hostObject.AddComponent<CheatEarlyUpdate>();
                hostObject.AddComponent<CheatMain>();
                UnityEngine.Object.DontDestroyOnLoad(hostObject);
                WriteLog("Unity main-thread initialization completed.");
            }
            catch (Exception exception)
            {
                WriteLog("Unity main-thread initialization failed: " + exception);
                if (hostObject != null) UnityEngine.Object.Destroy(hostObject);
                hostObject = null;
                Interlocked.Exchange(ref initState, 0);
            }
        }

        private static void WriteLog(string message)
        {
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), "RavenCheat-loader.log");
                File.AppendAllText(logPath, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }
    }

    [DefaultExecutionOrder(-32000)]
    public class CheatEarlyUpdate : MonoBehaviour
    {
        private void Update()
        {
            if (CheatMain.Instance != null)
            {
                CheatMain.Instance.EarlyTick();
            }
        }
    }

    [DefaultExecutionOrder(32000)]
    public class CheatMain : MonoBehaviour
    {
        public static CheatMain Instance;

        private const float MathF_Deg2Rad = 0.0174532924f;
        private static readonly string[] highlightColorNames = { "红", "绿", "蓝", "黄", "紫", "青", "白" };
        private static readonly Color[] highlightColors =
        {
            new Color(1f, 0.18f, 0.22f),
            new Color(0.15f, 0.92f, 0.45f),
            new Color(0.2f, 0.55f, 1f),
            new Color(1f, 0.85f, 0.15f),
            new Color(0.75f, 0.3f, 1f),
            new Color(0.15f, 0.9f, 1f),
            Color.white
        };
        private static readonly FieldInfo fiProjectileHitMask = typeof(Projectile).GetField("hitMask", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo fiGravityMultiplier = typeof(FirstPersonController).GetField("m_GravityMultiplier", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        // UI State
        private bool showMenu = true;
        private int menuTab = 0;
        private Rect winRect = new Rect(30f, 30f, 710f, 820f);
        private Vector2 scrollPos = Vector2.zero;
        private string bannerText = "就绪 - 点击右侧按钮或开关即可生效";
        private float bannerUntil = 0f;

        // Features - Visuals & Aim
        private bool bESP = true;
        private bool bEspEnemies = true;
        private bool bEspAllies = true;
        private bool bVehicleESP = true;
        private bool bChams = true;
        private bool bChamsEnemies = true;
        private bool bChamsAllies = true;
        private bool bRainbowChams = false;
        private int enemyChamsColorIndex = 0;
        private int allyChamsColorIndex = 1;
        private bool bAutoSpotEnemies = false;
        private bool bRadar = true;
        private bool bTargetHud = true;
        private bool bCrosshair = true;
        private bool bAimbot = true;
        private int targetTeamMode = 0;
        private bool bAimOnFire = true;
        private bool bAimAtHead = true;
        private bool bVisCheck = false;
        private bool bDrawFov = true;
        private bool bTriggerBot = false;
        private bool bMagicBullet = false;
        private bool bKillAllLoop = false;

        // Features - Weapon & Combat
        private bool bGod = true;
        private bool bAutoHeal = true;
        private bool bNoFall = true;
        private bool bUnlimitedAmmo = true;
        private bool bInfiniteGrenades = true;
        private bool bRapidFire = false;
        private bool bNoRecoil = true;
        private bool bNoSpread = true;
        private bool bInstantHit = true;
        private bool bShotgunMode = false;
        private bool bSuperDamage = false;
        private bool bSuperKnockback = false;
        private bool bVampire = false;
        private bool bBigBullets = false;
        private bool bExplosiveAmmo = false;

        // Features - Player & Vehicle
        private bool bSpeed = true;
        private bool bSuperJump = true;
        private bool bFly = false;
        private bool bNoclip = false;
        private bool bGiant = false;
        private bool bVehicleGod = true;
        private bool bVehicleBoost = true;
        private bool bUfoVehicle = false;
        private bool bBodyguardSquad = false;

        // Features - World & Fun
        private bool bSlowMo = false;
        private bool bLowGravity = false;
        private bool bMagnet = false;
        private bool bFreezeEnemies = false;
        private bool bEnemyInfighting = false;
        private bool bTrail = false;
        private bool bBounce = false;

        // Optional Hotkey Master Switch (Disabled by default per user request)
        private bool bHotkeys = false;

        // Tunable Numeric Settings
        private float espMaxDistance = 800f;
        private float espPadding = 0.06f;
        private float chamsOpacity = 0.72f;
        private float radarRange = 220f;
        private float targetHudFov = 35f;
        private float aimFov = 180f;
        private float killAllInterval = 0.45f;

        private float rapidFireCooldown = 0.03f;
        private float shotgunPellets = 12f;
        private float damageMultiplier = 5f;
        private float knockbackMultiplier = 6f;
        private float bulletScale = 5f;
        private float explosiveAmmoRadius = 6f;

        private float speedMultiplier = 1.9f;
        private float jumpMultiplier = 1.8f;
        private float flySpeed = 28f;
        private float giantScale = 3.0f;
        private float ufoSpeed = 48f;
        private float vehicleBoostPower = 28f;

        private float timeScale = 0.35f;
        private float gravityScale = 0.25f;
        private float magnetRadius = 15f;
        private float magnetForce = 18f;
        private float trailLifetime = 0.55f;
        private float trailWidth = 0.14f;
        private float bounceInterval = 2.0f;
        private float bounceForce = 18f;
        private float bounceRadius = 10f;

        private readonly Dictionary<string, string> numericInputs = new Dictionary<string, string>();

        // Cached Game Context
        private ActorManager actorManager;
        private FpsActorController fpc;
        private Actor localPlayer;
        private Camera activeCam;
        private Actor lastPlayer;
        private FpsActorController lastFpc;

        // Timers
        private float nextCacheTime = 0f;
        private float nextKillLoopTime = 0f;
        private float nextChamsTime = 0f;
        private float nextSpotTime = 0f;
        private float nextBodyguardTime = 0f;
        private float nextInfightTime = 0f;
        private float nextAirstrikeTime = 0f;
        private int pendingAirstrikes = 0;
        private Vector3 airstrikeCenter = Vector3.zero;
        private bool tornadoActive = false;
        private float tornadoUntil = 0f;
        private Vector3 tornadoCenter = Vector3.zero;
        private float nextTrailTime = 0f;
        private float nextBounceTime = 0f;
        private int lastAliveEnemyCount = -1;

        // Saved State for Clean Restoration
        private bool hasBaseMoveValues = false;
        private float baseWalkSpeed = 4.5f;
        private float baseRunSpeed = 7.5f;
        private float baseJumpSpeed = 8.0f;
        private float baseGravityMult = 2.0f;

        private Vector3 baseGravity = new Vector3(0f, -9.81f, 0f);
        private bool hasBaseGravity = false;
        private float baseFixedDelta = 0.02f;
        private bool hasBaseFixedDelta = false;
        private bool noclipApplied = false;
        private bool ufoApplied = false;
        private Rigidbody ufoLastRb;
        private bool giantApplied = false;

        private class WeaponBackup
        {
            public float cooldown;
            public float kickback;
            public float randomKick;
            public float snapMagnitude;
            public float rattleMagnitude;
            public float spread;
            public float followupMaxSpreadHip;
            public float followupMaxSpreadAim;
            public float followupSpreadGain;
            public int projectilesPerShot;
            public bool auto;
        }

        private class ProjectileBackup
        {
            public float speed;
            public float gravityMultiplier;
            public float damage;
            public float balanceDamage;
            public float impactForce;
            public int hitMask;
            public bool performInitialMuzzleTravel;
            public bool hasExploding;
            public bool explodeAtWaterSurface;
            public Vector3 localScale;
        }

        private class RendererBackup
        {
            public Renderer renderer;
            public Material[] sharedMaterials;
        }

        private readonly Dictionary<int, WeaponBackup> weaponBackups = new Dictionary<int, WeaponBackup>();
        private readonly HashSet<int> hookedWeaponIds = new HashSet<int>();
        private readonly List<Projectile> activePlayerProjectiles = new List<Projectile>();
        private readonly Dictionary<int, ProjectileBackup> projectileBackups = new Dictionary<int, ProjectileBackup>();
        private readonly Dictionary<int, int> projectileTargetIds = new Dictionary<int, int>();
        private readonly Dictionary<int, List<RendererBackup>> chamsBackups = new Dictionary<int, List<RendererBackup>>();
        private readonly HashSet<int> frozenEnemyIds = new HashSet<int>();
        private readonly HashSet<int> godVehicleIds = new HashSet<int>();

        private Material chamsEnemyMat;
        private Material chamsAllyMat;
        private Texture2D whiteTex;

        private GUIStyle windowStyle;
        private GUIStyle sectionStyle;
        private GUIStyle labelStyle;
        private GUIStyle espTextStyle;
        private GUIStyle watermarkStyle;
        private GUIStyle tabButtonStyle;

        private void Awake()
        {
            Instance = this;
            hasBaseGravity = true;
            baseGravity = Physics.gravity;
            hasBaseFixedDelta = true;
            baseFixedDelta = Time.fixedDeltaTime > 0.0001f ? Time.fixedDeltaTime : 0.02f;
            Banner("RavenCheat 已注入 | 菜单默认打开 (INS 隐藏/显示)");
        }

        private void OnDisable()
        {
            RestoreAllModifiedState();
        }

        private void OnDestroy()
        {
            RestoreAllModifiedState();
            if (Instance == this) Instance = null;
        }

        private void Banner(string msg)
        {
            bannerText = msg;
            bannerUntil = Time.unscaledTime + 4.5f;
        }

        private static int GetProjectileHitMask(Projectile p)
        {
            if (p == null || fiProjectileHitMask == null) return Hitbox.ALL_HITBOX_LAYER_MASK;
            try
            {
                return (int)fiProjectileHitMask.GetValue(p);
            }
            catch (Exception)
            {
                return Hitbox.ALL_HITBOX_LAYER_MASK;
            }
        }

        private static void SetProjectileHitMask(Projectile p, int mask)
        {
            if (p == null || fiProjectileHitMask == null) return;
            try
            {
                fiProjectileHitMask.SetValue(p, mask);
            }
            catch (Exception)
            {
            }
        }

        private static void TriggerGameExplosion(Actor source, Weapon weapon, Vector3 pos, float dmg, float radius)
        {
            try
            {
                ExplodingProjectile.ExplosionConfiguration cfg = new ExplodingProjectile.ExplosionConfiguration
                {
                    damage = dmg,
                    balanceDamage = Mathf.Max(350f, dmg),
                    force = 600f,
                    damageRange = Mathf.Max(2f, radius),
                    balanceRange = Mathf.Max(3f, radius * 1.3f),
                    damageFalloff = (ActorManager.instance != null && ActorManager.instance.linearFalloffCurve != null) ? ActorManager.instance.linearFalloffCurve : AnimationCurve.Linear(0f, 1f, 1f, 0.2f), balanceFalloff = (ActorManager.instance != null && ActorManager.instance.linearFalloffCurve != null) ? ActorManager.instance.linearFalloffCurve : AnimationCurve.Linear(0f, 1f, 1f, 0.2f)
                };
                ActorManager.Explode(source, weapon, pos, cfg, Vehicle.ArmorRating.AntiTank, false);
                ActorManager.CreateExplosionEffect(pos, Vector3.up, Mathf.Clamp(radius * 0.3f, 1.0f, 3.5f));
            }
            catch (Exception)
            {
            }
        }

        public void EarlyTick()
        {
            try
            {
                if (actorManager == null || localPlayer == null || localPlayer.dead) return;
                if (bInstantHit || bBigBullets || bExplosiveAmmo || bMagicBullet || bSuperDamage || bSuperKnockback)
                {
                    ApplyProjectiles();
                }
            }
            catch (Exception)
            {
            }
        }

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Insert))
                {
                    showMenu = !showMenu;
                }

                ResolveContext();
                if (bHotkeys)
                {
                    HandleHotkeys();
                }

                UpdateAnimatorCullingForAllActors();
                ApplyWorldStates();
                ApplyPlayerStates();
                ApplyWeapons();

                if (bTriggerBot && localPlayer != null && !localPlayer.dead)
                {
                    TriggerBot();
                }

                ApplyProjectiles();
                ApplyVehicleStates();
                ApplyEnemyStates();
                ApplyVampireCheck();
                ApplyBodyguardSquad();
                ApplyAutoSpotEnemies();

                if (bAimbot && localPlayer != null && !localPlayer.dead)
                {
                    bool shouldAim = !bAimOnFire || Input.GetMouseButton(0) || Input.GetMouseButton(1);
                    if (shouldAim)
                    {
                        AimAtBest();
                    }
                }

                if (bKillAllLoop && Time.unscaledTime >= nextKillLoopTime)
                {
                    nextKillLoopTime = Time.unscaledTime + Mathf.Max(0.05f, killAllInterval);
                    KillAllEnemies(true);
                }

                UpdateChams(false);
                UpdateAirstrike();
                UpdateTornado();
                UpdateTrail();
                UpdateBounce();
            }
            catch (Exception)
            {
            }
        }

        private void LateUpdate()
        {
            try
            {
                if (actorManager == null || localPlayer == null || localPlayer.dead) return;
                if (bAimbot)
                {
                    bool shouldAim = !bAimOnFire || Input.GetMouseButton(0) || Input.GetMouseButton(1);
                    if (shouldAim)
                    {
                        AimAtBest();
                    }
                }
                if (bInstantHit || bBigBullets || bExplosiveAmmo || bMagicBullet || bSuperDamage || bSuperKnockback)
                {
                    ApplyProjectiles();
                }
            }
            catch (Exception)
            {
            }
        }

        private void ResolveContext()
        {
            if (Time.unscaledTime >= nextCacheTime || actorManager == null || localPlayer == null)
            {
                nextCacheTime = Time.unscaledTime + 0.35f;
                actorManager = ActorManager.instance;
                fpc = FpsActorController.instance;
                localPlayer = actorManager != null ? actorManager.player : null;
                if (localPlayer == null && fpc != null)
                {
                    localPlayer = fpc.actor;
                }
            }

            if (fpc != null)
            {
                try
                {
                    activeCam = fpc.GetActiveCamera();
                }
                catch (Exception)
                {
                    activeCam = null;
                }
                if (activeCam == null)
                {
                    activeCam = fpc.fpCamera;
                }
            }
            if (activeCam == null)
            {
                activeCam = Camera.main;
            }

            if (localPlayer != lastPlayer || fpc != lastFpc)
            {
                lastPlayer = localPlayer;
                lastFpc = fpc;
                hasBaseMoveValues = false;
                noclipApplied = false;
                giantApplied = false;
                ufoApplied = false;
                ufoLastRb = null;
                lastAliveEnemyCount = -1;
            }
        }

        private void UpdateAnimatorCullingForAllActors()
        {
            if (actorManager == null || actorManager.actors == null) return;
            bool needAlwaysAnimate = bESP || bChams || bAimbot || bTriggerBot || bMagicBullet || bTargetHud;
            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a.dead || a.isDeactivated) continue;
                if (a.animator != null)
                {
                    a.animator.cullingMode = needAlwaysAnimate
                        ? AnimatorCullingMode.AlwaysAnimate
                        : AnimatorCullingMode.CullUpdateTransforms;
                }
            }
        }

        private void RestoreAllModifiedState()
        {
            try
            {
                Time.timeScale = 1f;
                if (hasBaseFixedDelta) Time.fixedDeltaTime = baseFixedDelta;
                if (hasBaseGravity) Physics.gravity = baseGravity;

                if (localPlayer != null)
                {
                    localPlayer.isInvulnerable = false;
                    if (localPlayer.transform != null)
                    {
                        localPlayer.transform.localScale = Vector3.one;
                    }
                    SetActorCollidersEnabled(localPlayer, true);
                }

                RestoreBaseMoveSpeed();

                if (ufoLastRb != null)
                {
                    ufoLastRb.useGravity = true;
                    ufoLastRb = null;
                }

                RestoreAllWeapons();
                RestoreAllProjectiles();
                RestoreAllVehicles();
                RestoreAllEnemies();
                ClearChamsCache();
            }
            catch (Exception)
            {
            }
        }

        private void ResetAllSettings()
        {
            bESP = true;
            bEspEnemies = true;
            bEspAllies = true;
            bVehicleESP = true;
            bChams = true;
            bChamsEnemies = true;
            bChamsAllies = true;
            bRainbowChams = false;
            enemyChamsColorIndex = 0;
            allyChamsColorIndex = 1;
            bAutoSpotEnemies = false;
            bRadar = true;
            bTargetHud = true;
            bCrosshair = true;
            bAimbot = true;
            targetTeamMode = 0;
            bAimOnFire = true;
            bAimAtHead = true;
            bVisCheck = false;
            bDrawFov = true;
            bTriggerBot = false;
            bMagicBullet = false;
            bKillAllLoop = false;

            bGod = true;
            bAutoHeal = true;
            bNoFall = true;
            bUnlimitedAmmo = true;
            bInfiniteGrenades = true;
            bRapidFire = false;
            bNoRecoil = true;
            bNoSpread = true;
            bInstantHit = true;
            bShotgunMode = false;
            bSuperDamage = false;
            bSuperKnockback = false;
            bVampire = false;
            bBigBullets = false;
            bExplosiveAmmo = false;

            bSpeed = true;
            bSuperJump = true;
            bFly = false;
            bNoclip = false;
            bGiant = false;
            bVehicleGod = true;
            bVehicleBoost = true;
            bUfoVehicle = false;
            bBodyguardSquad = false;

            bSlowMo = false;
            bLowGravity = false;
            bMagnet = false;
            bFreezeEnemies = false;
            bEnemyInfighting = false;
            bTrail = false;
            bBounce = false;
            bHotkeys = false;

            espMaxDistance = 800f;
            espPadding = 0.06f;
            chamsOpacity = 0.72f;
            radarRange = 220f;
            targetHudFov = 35f;
            aimFov = 180f;
            killAllInterval = 0.45f;
            rapidFireCooldown = 0.03f;
            shotgunPellets = 12f;
            damageMultiplier = 5f;
            knockbackMultiplier = 6f;
            bulletScale = 5f;
            explosiveAmmoRadius = 6f;
            speedMultiplier = 1.9f;
            jumpMultiplier = 1.8f;
            flySpeed = 28f;
            giantScale = 3.0f;
            ufoSpeed = 48f;
            vehicleBoostPower = 28f;
            timeScale = 0.35f;
            gravityScale = 0.25f;
            magnetRadius = 15f;
            magnetForce = 18f;
            trailLifetime = 0.55f;
            trailWidth = 0.14f;
            bounceInterval = 2.0f;
            bounceForce = 18f;
            bounceRadius = 10f;

            numericInputs.Clear();
            RestoreAllModifiedState();
            UpdateChams(true);
            Banner("已恢复默认设置并还原游戏原始状态");
        }

        public static bool IsValidWorldPos(Vector3 v)
        {
            if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) return false;
            if (float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z)) return false;
            if (v.y > 5000f || v.y < -5000f) return false;
            if (Mathf.Abs(v.x) > 50000f || Mathf.Abs(v.z) > 50000f) return false;
            return true;
        }

        public Vector3 Eye()
        {
            if (activeCam != null)
            {
                return activeCam.transform.position;
            }
            if (localPlayer != null)
            {
                return GetActorWorldCenter(localPlayer) + Vector3.up * 0.6f;
            }
            return Vector3.zero;
        }

        public Vector3 GetActorWorldCenter(Actor a)
        {
            if (a == null) return Vector3.zero;

            if (a.IsSeated() && a.seat != null && a.seat.transform != null)
            {
                Vector3 seatPos = a.seat.transform.position + Vector3.up * 0.45f;
                if (IsValidWorldPos(seatPos)) return seatPos;
            }

            Vector3 cPos = a.CenterPosition();
            Vector3 tPos = a.transform != null ? a.transform.position : a.Position();

            if (IsValidWorldPos(cPos))
            {
                if (!IsValidWorldPos(tPos) || (cPos - tPos).sqrMagnitude < 25f)
                {
                    return cPos;
                }
            }

            if (a.ragdoll != null)
            {
                try
                {
                    Transform chest = a.ragdoll.HumanBoneTransformActive(HumanBodyBones.Chest);
                    if (chest != null && IsValidWorldPos(chest.position))
                    {
                        return chest.position;
                    }
                }
                catch (Exception)
                {
                }
            }

            if (IsValidWorldPos(tPos))
            {
                return tPos + Vector3.up * 0.9f;
            }
            return cPos;
        }

        public Vector3 FootOf(Actor a)
        {
            if (a == null) return Vector3.zero;
            Vector3 center = GetActorWorldCenter(a);

            if (a.ragdoll != null)
            {
                try
                {
                    Transform lf = a.ragdoll.HumanBoneTransformActive(HumanBodyBones.LeftFoot);
                    Transform rf = a.ragdoll.HumanBoneTransformActive(HumanBodyBones.RightFoot);
                    if (lf != null && rf != null)
                    {
                        Vector3 midFoot = 0.5f * (lf.position + rf.position) - Vector3.up * 0.08f;
                        if (IsValidWorldPos(midFoot) && (midFoot - center).sqrMagnitude < 16f)
                        {
                            return midFoot;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            if (a.transform != null && IsValidWorldPos(a.transform.position) && (a.transform.position - center).sqrMagnitude < 16f)
            {
                return a.transform.position;
            }
            return center - Vector3.up * 0.9f;
        }

        public Vector3 HeadOf(Actor a)
        {
            if (a == null) return Vector3.zero;
            Vector3 center = GetActorWorldCenter(a);

            if (a.ragdoll != null)
            {
                try
                {
                    if (a.ragdoll.IsRagdoll() && a.headRigidbody != null)
                    {
                        Vector3 rbHead = a.headRigidbody.worldCenterOfMass;
                        if (IsValidWorldPos(rbHead) && (rbHead - center).sqrMagnitude < 16f)
                        {
                            return rbHead;
                        }
                    }

                    Transform headBone = a.ragdoll.HumanBoneTransformActive(HumanBodyBones.Head);
                    if (headBone != null)
                    {
                        Vector3 bonePos = headBone.position + headBone.up * 0.08f;
                        if (IsValidWorldPos(bonePos) && (bonePos - center).sqrMagnitude < 16f)
                        {
                            return bonePos;
                        }
                    }

                    Transform neckBone = a.ragdoll.HumanBoneTransformActive(HumanBodyBones.Neck);
                    if (neckBone != null)
                    {
                        Vector3 neckPos = neckBone.position + Vector3.up * 0.16f;
                        if (IsValidWorldPos(neckPos) && (neckPos - center).sqrMagnitude < 16f)
                        {
                            return neckPos;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            return center + Vector3.up * 0.65f;
        }

        public Vector3 AimPoint(Actor a)
        {
            if (a == null) return Vector3.zero;
            return bAimAtHead ? HeadOf(a) : GetActorWorldCenter(a);
        }

        private void ApplyWorldStates()
        {
            float wantedTime = bSlowMo ? Mathf.Clamp(timeScale, 0.05f, 3.0f) : 1.0f;
            if (Mathf.Abs(Time.timeScale - wantedTime) > 0.005f)
            {
                Time.timeScale = wantedTime;
                if (hasBaseFixedDelta)
                {
                    Time.fixedDeltaTime = baseFixedDelta * Mathf.Clamp(wantedTime, 0.05f, 1.0f);
                }
            }

            if (hasBaseGravity)
            {
                Vector3 wantedGravity = bLowGravity ? (baseGravity * Mathf.Clamp(gravityScale, -1.0f, 3.0f)) : baseGravity;
                if ((Physics.gravity - wantedGravity).sqrMagnitude > 0.001f)
                {
                    Physics.gravity = wantedGravity;
                }
            }
        }

        private void ApplyPlayerStates()
        {
            if (localPlayer == null || localPlayer.dead) return;

            localPlayer.isInvulnerable = bGod;

            if (bAutoHeal || bGod)
            {
                if (localPlayer.maxHealth < 100f) localPlayer.maxHealth = 100f;
                if (localPlayer.health < localPlayer.maxHealth)
                {
                    localPlayer.health = localPlayer.maxHealth;
                }
                if (localPlayer.balance < localPlayer.maxBalance)
                {
                    localPlayer.balance = localPlayer.maxBalance;
                }
            }

            if ((bNoFall || bGod) && localPlayer.fallenOver)
            {
                try
                {
                    localPlayer.InstantGetUpAtPosition(localPlayer.transform.position);
                    localPlayer.balance = localPlayer.maxBalance;
                }
                catch (Exception)
                {
                }
            }

            ApplyMoveAndJump();
            ApplyFlyAndNoclip();
            ApplyGiantMode();
        }

        private void EnsureFpsBaseMoveValues()
        {
            if (fpc == null || fpc.controller == null) return;
            if (hasBaseMoveValues) return;

            try
            {
                FirstPersonController ctrl = fpc.controller;
                baseWalkSpeed = ctrl.m_WalkSpeed > 0.1f ? ctrl.m_WalkSpeed : 4.5f;
                baseRunSpeed = ctrl.m_RunSpeed > 0.1f ? ctrl.m_RunSpeed : 7.5f;
                baseJumpSpeed = ctrl.m_JumpSpeed > 0.1f ? ctrl.m_JumpSpeed : 8.0f;
                if (fiGravityMultiplier != null)
                {
                    baseGravityMult = Convert.ToSingle(fiGravityMultiplier.GetValue(ctrl));
                }
                hasBaseMoveValues = true;
            }
            catch (Exception)
            {
            }
        }

        private void RestoreBaseMoveSpeed()
        {
            if (!hasBaseMoveValues || fpc == null || fpc.controller == null) return;
            try
            {
                FirstPersonController ctrl = fpc.controller;
                ctrl.m_WalkSpeed = baseWalkSpeed;
                ctrl.m_RunSpeed = baseRunSpeed;
                ctrl.m_JumpSpeed = baseJumpSpeed;
                if (fiGravityMultiplier != null)
                {
                    fiGravityMultiplier.SetValue(ctrl, baseGravityMult);
                }
            }
            catch (Exception)
            {
            }
        }

        private void ApplyMoveAndJump()
        {
            EnsureFpsBaseMoveValues();
            if (!hasBaseMoveValues || fpc == null || fpc.controller == null) return;

            try
            {
                FirstPersonController ctrl = fpc.controller;
                float sMult = bSpeed ? Mathf.Clamp(speedMultiplier, 0.2f, 10f) : 1f;
                float jMult = bSuperJump ? Mathf.Clamp(jumpMultiplier, 0.2f, 10f) : 1f;
                ctrl.m_WalkSpeed = baseWalkSpeed * sMult;
                ctrl.m_RunSpeed = baseRunSpeed * sMult;
                ctrl.m_JumpSpeed = baseJumpSpeed * jMult;
            }
            catch (Exception)
            {
            }
        }

        private void SetActorCollidersEnabled(Actor a, bool enabled)
        {
            if (a == null) return;
            try
            {
                CharacterController cc = a.GetComponent<CharacterController>();
                if (cc != null) cc.detectCollisions = enabled;
                Collider[] cols = a.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] == null) continue;
                    if (cols[i].GetComponent<Hitbox>() != null) continue;
                    if (cols[i] is CharacterController) continue;
                    cols[i].enabled = enabled;
                }
            }
            catch (Exception)
            {
            }
        }

        private void ApplyFlyAndNoclip()
        {
            if (localPlayer == null || fpc == null || fpc.controller == null) return;

            bool wantNoClipCol = bNoclip && !localPlayer.IsSeated();
            if (wantNoClipCol != noclipApplied)
            {
                SetActorCollidersEnabled(localPlayer, !wantNoClipCol);
                noclipApplied = wantNoClipCol;
            }

            if ((bFly || bNoclip) && !localPlayer.IsSeated())
            {
                if (fiGravityMultiplier != null)
                {
                    try { fiGravityMultiplier.SetValue(fpc.controller, 0f); } catch (Exception) { }
                }
                Transform camT = activeCam != null ? activeCam.transform : localPlayer.transform;
                Vector3 move = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) move += camT.forward;
                if (Input.GetKey(KeyCode.S)) move -= camT.forward;
                if (Input.GetKey(KeyCode.D)) move += camT.right;
                if (Input.GetKey(KeyCode.A)) move -= camT.right;
                if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) move -= Vector3.up;

                float spd = Mathf.Clamp(flySpeed, 2f, 200f) * (Input.GetKey(KeyCode.LeftShift) ? 2.2f : 1f);
                if (move.sqrMagnitude > 0.001f)
                {
                    Vector3 delta = move.normalized * spd * Time.unscaledDeltaTime;
                    if (bNoclip)
                    {
                        localPlayer.transform.position += delta;
                    }
                    else
                    {
                        CharacterController cc = localPlayer.GetComponent<CharacterController>();
                        if (cc != null && cc.enabled)
                        {
                            cc.Move(delta);
                        }
                        else
                        {
                            localPlayer.transform.position += delta;
                        }
                    }
                }
            }
            else if (hasBaseMoveValues && fiGravityMultiplier != null)
            {
                try { fiGravityMultiplier.SetValue(fpc.controller, baseGravityMult); } catch (Exception) { }
            }
        }

        private void ApplyGiantMode()
        {
            if (localPlayer == null || localPlayer.transform == null) return;
            if (bGiant && !localPlayer.IsSeated())
            {
                float s = Mathf.Clamp(giantScale, 0.25f, 8.0f);
                localPlayer.transform.localScale = new Vector3(s, s, s);
                giantApplied = true;
            }
            else if (giantApplied)
            {
                localPlayer.transform.localScale = Vector3.one;
                giantApplied = false;
            }
        }

        private void ApplyWeapons()
        {
            if (localPlayer == null || localPlayer.dead) return;

            if (localPlayer.weapons != null)
            {
                for (int i = 0; i < localPlayer.weapons.Length; i++)
                {
                    Weapon w = localPlayer.weapons[i];
                    if (w != null)
                    {
                        TuneWeaponHierarchy(w, i);
                    }
                }
            }

            if (localPlayer.activeWeapon != null)
            {
                TuneWeaponHierarchy(localPlayer.activeWeapon, 0);
            }

            if (localPlayer.IsSeated() && localPlayer.seat != null && localPlayer.seat.weapons != null)
            {
                for (int i = 0; i < localPlayer.seat.weapons.Length; i++)
                {
                    MountedWeapon mw = localPlayer.seat.weapons[i];
                    if (mw != null)
                    {
                        TuneWeaponHierarchy(mw, 0);
                    }
                }
            }
        }

        private void TuneWeaponHierarchy(Weapon root, int slotIndex)
        {
            if (root == null) return;
            TuneSingleWeapon(root, slotIndex);

            Weapon activeSub = root.GetActiveSubWeapon();
            if (activeSub != null && activeSub != root)
            {
                TuneSingleWeapon(activeSub, slotIndex);
            }

            if (root.alternativeWeapons != null)
            {
                for (int i = 0; i < root.alternativeWeapons.Count; i++)
                {
                    Weapon sub = root.alternativeWeapons[i];
                    if (sub != null && sub != root)
                    {
                        TuneSingleWeapon(sub, slotIndex);
                    }
                }
            }
        }

        private void TuneSingleWeapon(Weapon w, int slotIndex)
        {
            if (w == null || w.configuration == null) return;
            int id = w.GetInstanceID();
            Weapon.Configuration c = w.configuration;

            if (!hookedWeaponIds.Contains(id) && w.onSpawnProjectiles != null)
            {
                try
                {
                    w.onSpawnProjectiles.AddListener(OnPlayerWeaponSpawnedProjectiles);
                    hookedWeaponIds.Add(id);
                }
                catch (Exception)
                {
                }
            }

            WeaponBackup bak;
            if (!weaponBackups.TryGetValue(id, out bak))
            {
                bak = new WeaponBackup
                {
                    cooldown = c.cooldown,
                    kickback = c.kickback,
                    randomKick = c.randomKick,
                    snapMagnitude = c.snapMagnitude,
                    rattleMagnitude = c.rattleMagnitude,
                    spread = c.spread,
                    followupMaxSpreadHip = c.followupMaxSpreadHip,
                    followupMaxSpreadAim = c.followupMaxSpreadAim,
                    followupSpreadGain = c.followupSpreadGain,
                    projectilesPerShot = c.projectilesPerShot,
                    auto = c.auto
                };
                weaponBackups[id] = bak;
            }

            bool isGearSlot = slotIndex >= 2;
            bool infAmmoThisSlot = isGearSlot ? (bInfiniteGrenades || bUnlimitedAmmo) : bUnlimitedAmmo;

            if (infAmmoThisSlot)
            {
                int targetClip = c.ammo > 0 ? Mathf.Max(c.ammo, 30) : 1;
                if (w.ammo < targetClip)
                {
                    w.ammo = targetClip;
                    try
                    {
                        w.OnAmmoChanged();
                    }
                    catch (Exception)
                    {
                    }
                }
                if (w.spareAmmo < 999)
                {
                    w.spareAmmo = 999;
                }
                if (w.reloading)
                {
                    try
                    {
                        w.InstantlyReload();
                    }
                    catch (Exception)
                    {
                        w.reloading = false;
                    }
                }
            }

            if (bRapidFire)
            {
                c.cooldown = Mathf.Clamp(rapidFireCooldown, 0.01f, 0.5f);
                c.auto = true;
            }
            else
            {
                c.cooldown = bak.cooldown;
                c.auto = bak.auto;
            }

            if (bNoRecoil)
            {
                c.kickback = 0f;
                c.randomKick = 0f;
                c.snapMagnitude = 0f;
                c.rattleMagnitude = 0f;
            }
            else
            {
                c.kickback = bak.kickback;
                c.randomKick = bak.randomKick;
                c.snapMagnitude = bak.snapMagnitude;
                c.rattleMagnitude = bak.rattleMagnitude;
            }

            if (bNoSpread)
            {
                c.spread = 0f;
                c.followupMaxSpreadHip = 0f;
                c.followupMaxSpreadAim = 0f;
                c.followupSpreadGain = 0f;
            }
            else
            {
                c.spread = bak.spread;
                c.followupMaxSpreadHip = bak.followupMaxSpreadHip;
                c.followupMaxSpreadAim = bak.followupMaxSpreadAim;
                c.followupSpreadGain = bak.followupSpreadGain;
            }

            if (bShotgunMode)
            {
                c.projectilesPerShot = Mathf.Clamp(Mathf.RoundToInt(shotgunPellets), 2, 40);
                if (!bNoSpread && c.spread < 0.015f)
                {
                    c.spread = 0.025f;
                }
            }
            else
            {
                c.projectilesPerShot = bak.projectilesPerShot;
            }
        }

        private void OnPlayerWeaponSpawnedProjectiles(Projectile[] projectiles)
        {
            if (projectiles == null) return;
            for (int i = 0; i < projectiles.Length; i++)
            {
                Projectile p = projectiles[i];
                if (p == null) continue;
                activePlayerProjectiles.Add(p);
                ProcessSingleProjectile(p);
            }
        }

        private void RestoreAllWeapons()
        {
            if (localPlayer == null || localPlayer.weapons == null) return;
            for (int i = 0; i < localPlayer.weapons.Length; i++)
            {
                Weapon w = localPlayer.weapons[i];
                if (w == null) continue;
                RestoreSingleWeapon(w);
                if (w.alternativeWeapons != null)
                {
                    for (int j = 0; j < w.alternativeWeapons.Count; j++)
                    {
                        RestoreSingleWeapon(w.alternativeWeapons[j]);
                    }
                }
            }
        }

        private void RestoreSingleWeapon(Weapon w)
        {
            if (w == null || w.configuration == null) return;
            WeaponBackup bak;
            if (weaponBackups.TryGetValue(w.GetInstanceID(), out bak))
            {
                Weapon.Configuration c = w.configuration;
                c.cooldown = bak.cooldown;
                c.kickback = bak.kickback;
                c.randomKick = bak.randomKick;
                c.snapMagnitude = bak.snapMagnitude;
                c.rattleMagnitude = bak.rattleMagnitude;
                c.spread = bak.spread;
                c.followupMaxSpreadHip = bak.followupMaxSpreadHip;
                c.followupMaxSpreadAim = bak.followupMaxSpreadAim;
                c.followupSpreadGain = bak.followupSpreadGain;
                c.projectilesPerShot = bak.projectilesPerShot;
                c.auto = bak.auto;
            }
        }

        public void ApplyProjectiles()
        {
            if (actorManager == null || localPlayer == null || localPlayer.dead) return;
            if (activePlayerProjectiles.Count == 0) return;

            bool anyMod = bInstantHit || bBigBullets || bExplosiveAmmo || bMagicBullet || bSuperDamage || bSuperKnockback;

            for (int i = activePlayerProjectiles.Count - 1; i >= 0; i--)
            {
                Projectile p = activePlayerProjectiles[i];
                if (p == null || !p.enabled || !p.gameObject.activeInHierarchy || p.killCredit != localPlayer)
                {
                    activePlayerProjectiles.RemoveAt(i);
                    continue;
                }

                if (anyMod)
                {
                    ProcessSingleProjectile(p);
                }
            }
        }

        private void ProcessSingleProjectile(Projectile p)
        {
            if (p == null || !p.enabled || p.configuration == null || localPlayer == null) return;

            int pid = p.GetInstanceID();
            Projectile.Configuration pc = p.configuration;
            ProjectileBackup bak;
            if (!projectileBackups.TryGetValue(pid, out bak))
            {
                ExplodingProjectile epInit = p as ExplodingProjectile;
                bak = new ProjectileBackup
                {
                    speed = pc.speed,
                    gravityMultiplier = pc.gravityMultiplier,
                    damage = pc.damage,
                    balanceDamage = pc.balanceDamage,
                    impactForce = pc.impactForce,
                    hitMask = GetProjectileHitMask(p),
                    performInitialMuzzleTravel = p.performInfantryInitialMuzzleTravel,
                    hasExploding = epInit != null,
                    explodeAtWaterSurface = epInit != null && epInit.explodeAtWaterSurface,
                    localScale = p.transform != null ? p.transform.localScale : Vector3.one
                };
                projectileBackups[pid] = bak;
            }

            pc.damage = bSuperDamage ? (bak.damage * Mathf.Clamp(damageMultiplier, 1f, 50f)) : bak.damage;
            pc.balanceDamage = bSuperKnockback ? (bak.balanceDamage * Mathf.Clamp(knockbackMultiplier, 1f, 50f)) : bak.balanceDamage;
            pc.impactForce = bSuperKnockback ? (Mathf.Max(bak.impactForce, 150f) * Mathf.Clamp(knockbackMultiplier, 1f, 50f)) : bak.impactForce;

            if (p.transform != null)
            {
                if (bBigBullets)
                {
                    float bs = Mathf.Clamp(bulletScale, 1f, 20f);
                    p.transform.localScale = new Vector3(bs, bs, bs);
                }
                else
                {
                    p.transform.localScale = bak.localScale;
                }
            }

            if (bInstantHit && !bMagicBullet)
            {
                pc.speed = Mathf.Max(bak.speed, 1600f);
                pc.gravityMultiplier = 0f;
                p.velocity = p.transform.forward * pc.speed;
            }
            else if (!bMagicBullet)
            {
                pc.speed = bak.speed;
                pc.gravityMultiplier = bak.gravityMultiplier;
            }

            if (bMagicBullet)
            {
                p.performInfantryInitialMuzzleTravel = false;
                pc.gravityMultiplier = 0f;

                ExplodingProjectile ep = p as ExplodingProjectile;
                if (ep != null)
                {
                    ep.explodeAtWaterSurface = false;
                }

                Actor target = ResolveMagicBulletTarget(pid);
                if (target != null && !target.dead && !target.isDeactivated)
                {
                    int wallThroughMask = Hitbox.ALL_HITBOX_LAYER_MASK;
                    if (target.IsSeated())
                    {
                        wallThroughMask |= (1 << 12);
                    }
                    SetProjectileHitMask(p, wallThroughMask);

                    Vector3 aimPos = AimPoint(target) + target.Velocity() * Time.deltaTime;
                    Vector3 toTarget = aimPos - p.transform.position;
                    float dist = toTarget.magnitude;
                    Vector3 dir = dist > 0.001f ? (toTarget / dist) : p.transform.forward;
                    float spd = Mathf.Max(bak.speed, 950f);

                    p.transform.forward = dir;
                    p.velocity = dir * spd;

                    float step = spd * Time.deltaTime;
                    if (dist > step * 1.35f && dist > 1.8f)
                    {
                        p.transform.position = aimPos - dir * Mathf.Min(dist * 0.45f, 0.9f);
                        dist = (aimPos - p.transform.position).magnitude;
                    }

                    if (bExplosiveAmmo && dist <= step + 1.1f)
                    {
                        float expDmg = 320f * (bSuperDamage ? Mathf.Clamp(damageMultiplier, 1f, 20f) : 1f);
                        TriggerGameExplosion(localPlayer, p.sourceWeapon, aimPos, expDmg, explosiveAmmoRadius);
                        DamageInfo di = new DamageInfo(DamageInfo.DamageSourceType.Projectile, localPlayer, p.sourceWeapon)
                        {
                            healthDamage = pc.damage,
                            balanceDamage = pc.balanceDamage,
                            isPiercing = true,
                            isCriticalHit = bAimAtHead,
                            point = aimPos,
                            direction = dir,
                            impactForce = dir * pc.impactForce
                        };
                        target.Damage(di);
                        p.enabled = false;
                    }
                }
                else
                {
                    SetProjectileHitMask(p, bak.hitMask);
                }
            }
            else
            {
                SetProjectileHitMask(p, bak.hitMask);
                p.performInfantryInitialMuzzleTravel = bak.performInitialMuzzleTravel;
                ExplodingProjectile ep = p as ExplodingProjectile;
                if (ep != null && bak.hasExploding)
                {
                    ep.explodeAtWaterSurface = bak.explodeAtWaterSurface;
                }

                if (bExplosiveAmmo && p.velocity.sqrMagnitude > 0.01f)
                {
                    float step = p.velocity.magnitude * Time.deltaTime * 1.25f;
                    RaycastHit hit;
                    if (Physics.Raycast(p.transform.position, p.velocity.normalized, out hit, step, bak.hitMask))
                    {
                        float expDmg = 320f * (bSuperDamage ? Mathf.Clamp(damageMultiplier, 1f, 20f) : 1f);
                        TriggerGameExplosion(localPlayer, p.sourceWeapon, hit.point, expDmg, explosiveAmmoRadius);
                        p.enabled = false;
                    }
                }
            }
        }

        private bool IsSelectableTarget(Actor actor)
        {
            if (actor == null || localPlayer == null || actor == localPlayer || actor.dead || actor.isDeactivated) return false;
            bool isAlly = actor.team == localPlayer.team;
            return targetTeamMode == 2 || (targetTeamMode == 1 ? isAlly : !isAlly);
        }

        private Actor ResolveMagicBulletTarget(int projectileId)
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return null;

            int savedActorIdx;
            if (projectileTargetIds.TryGetValue(projectileId, out savedActorIdx))
            {
                if (savedActorIdx >= 0 && savedActorIdx < actorManager.actors.Count)
                {
                    Actor cand = actorManager.actors[savedActorIdx];
                    if (IsSelectableTarget(cand))
                    {
                        return cand;
                    }
                }
            }

            Actor best = FindBest(false, 360f);
            if (best == null)
            {
                best = FindNearestTarget();
            }
            if (best != null)
            {
                projectileTargetIds[projectileId] = best.actorIndex;
            }
            return best;
        }

        private Actor FindNearestTarget()
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return null;
            Vector3 eye = Eye();
            Actor best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (!IsSelectableTarget(a)) continue;
                Vector3 pos = GetActorWorldCenter(a);
                if (!IsValidWorldPos(pos)) continue;
                float d2 = (pos - eye).sqrMagnitude;
                if (d2 < bestSqr)
                {
                    bestSqr = d2;
                    best = a;
                }
            }
            return best;
        }

        private void RestoreAllProjectiles()
        {
            for (int i = 0; i < activePlayerProjectiles.Count; i++)
            {
                Projectile p = activePlayerProjectiles[i];
                if (p == null || p.configuration == null) continue;
                ProjectileBackup bak;
                if (projectileBackups.TryGetValue(p.GetInstanceID(), out bak))
                {
                    p.configuration.speed = bak.speed;
                    p.configuration.gravityMultiplier = bak.gravityMultiplier;
                    p.configuration.damage = bak.damage;
                    p.configuration.balanceDamage = bak.balanceDamage;
                    p.configuration.impactForce = bak.impactForce;
                    SetProjectileHitMask(p, bak.hitMask);
                    p.performInfantryInitialMuzzleTravel = bak.performInitialMuzzleTravel;
                    if (p.transform != null) p.transform.localScale = bak.localScale;
                    ExplodingProjectile ep = p as ExplodingProjectile;
                    if (ep != null && bak.hasExploding)
                    {
                        ep.explodeAtWaterSurface = bak.explodeAtWaterSurface;
                    }
                }
            }
            activePlayerProjectiles.Clear();
        }

        private void ApplyVehicleStates()
        {
            if (localPlayer == null || localPlayer.dead) return;

            if (localPlayer.IsSeated() && localPlayer.seat != null && localPlayer.seat.vehicle != null)
            {
                Vehicle v = localPlayer.seat.vehicle;
                int vid = v.GetInstanceID();

                if (bVehicleGod)
                {
                    v.isInvulnerable = true;
                    v.burning = false;
                    if (v.health < v.maxHealth)
                    {
                        v.health = v.maxHealth;
                    }
                    godVehicleIds.Add(vid);
                }
                else if (godVehicleIds.Contains(vid))
                {
                    v.isInvulnerable = false;
                    godVehicleIds.Remove(vid);
                }

                Rigidbody rb = v.rigidbody != null ? v.rigidbody : v.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    if (bVehicleBoost && Input.GetKey(KeyCode.LeftShift) && !bUfoVehicle)
                    {
                        rb.AddForce(v.transform.forward * Mathf.Clamp(vehicleBoostPower, 5f, 150f), ForceMode.Acceleration);
                    }

                    if (bUfoVehicle)
                    {
                        if (ufoLastRb != null && ufoLastRb != rb)
                        {
                            ufoLastRb.useGravity = true;
                        }
                        rb.useGravity = false;
                        ufoLastRb = rb;
                        ufoApplied = true;

                        Transform camT = activeCam != null ? activeCam.transform : v.transform;
                        Vector3 dir = Vector3.zero;
                        if (Input.GetKey(KeyCode.W)) dir += camT.forward;
                        if (Input.GetKey(KeyCode.S)) dir -= camT.forward;
                        if (Input.GetKey(KeyCode.D)) dir += camT.right;
                        if (Input.GetKey(KeyCode.A)) dir -= camT.right;
                        if (Input.GetKey(KeyCode.Space)) dir += Vector3.up;
                        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) dir -= Vector3.up;

                        float spd = Mathf.Clamp(ufoSpeed, 10f, 250f) * (Input.GetKey(KeyCode.LeftShift) ? 2.2f : 1f);
                        rb.velocity = dir.sqrMagnitude > 0.001f ? (dir.normalized * spd) : Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        if (activeCam != null)
                        {
                            Vector3 fwd = activeCam.transform.forward;
                            fwd.y = 0f;
                            if (fwd.sqrMagnitude > 0.01f)
                            {
                                v.transform.rotation = Quaternion.Slerp(v.transform.rotation, Quaternion.LookRotation(fwd.normalized, Vector3.up), Time.unscaledDeltaTime * 8f);
                            }
                        }
                    }
                    else if (ufoApplied && ufoLastRb != null)
                    {
                        ufoLastRb.useGravity = true;
                        ufoLastRb = null;
                        ufoApplied = false;
                    }
                }
            }
            else if (ufoApplied && ufoLastRb != null)
            {
                ufoLastRb.useGravity = true;
                ufoLastRb = null;
                ufoApplied = false;
            }
        }

        private void RestoreAllVehicles()
        {
            if (actorManager == null || actorManager.vehicles == null) return;
            for (int i = 0; i < actorManager.vehicles.Count; i++)
            {
                Vehicle v = actorManager.vehicles[i];
                if (v == null) continue;
                if (godVehicleIds.Contains(v.GetInstanceID()))
                {
                    v.isInvulnerable = false;
                }
                Rigidbody rb = v.rigidbody != null ? v.rigidbody : v.GetComponent<Rigidbody>();
                if (rb != null) rb.useGravity = true;
            }
            godVehicleIds.Clear();
        }

        private void ApplyEnemyStates()
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return;

            Vector3 magnetPoint = Eye();
            if (activeCam != null)
            {
                magnetPoint = activeCam.transform.position + activeCam.transform.forward * Mathf.Clamp(magnetRadius, 3f, 80f);
            }

            bool doInfightTick = bEnemyInfighting && Time.unscaledTime >= nextInfightTime;
            if (doInfightTick)
            {
                nextInfightTime = Time.unscaledTime + 0.08f;
            }

            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a == localPlayer) continue;
                int aid = a.GetInstanceID();

                if (a.dead || a.isDeactivated || a.team == localPlayer.team)
                {
                    if (frozenEnemyIds.Contains(aid))
                    {
                        if (a.controller != null) a.controller.enabled = true;
                        if (a.animator != null) a.animator.speed = 1f;
                        frozenEnemyIds.Remove(aid);
                    }
                    continue;
                }

                if (bFreezeEnemies)
                {
                    if (a.controller != null) a.controller.enabled = false;
                    if (a.animator != null) a.animator.speed = 0f;
                    frozenEnemyIds.Add(aid);
                }
                else if (frozenEnemyIds.Contains(aid))
                {
                    if (a.controller != null) a.controller.enabled = true;
                    if (a.animator != null) a.animator.speed = 1f;
                    frozenEnemyIds.Remove(aid);
                }

                if (bMagnet && !localPlayer.dead)
                {
                    if (a.IsSeated())
                    {
                        try { a.LeaveSeat(false); } catch (Exception) { }
                    }
                    Vector3 cur = a.transform.position;
                    Vector3 next = Vector3.Lerp(cur, magnetPoint, Mathf.Clamp01(Time.unscaledDeltaTime * Mathf.Clamp(magnetForce, 1f, 60f)));
                    a.transform.position = next;
                }

                if (doInfightTick && !bFreezeEnemies)
                {
                    AiActorController ai = a.controller as AiActorController;
                    if (ai != null)
                    {
                        Actor rival = FindNearestOtherEnemy(a);
                        if (rival != null)
                        {
                            ai.target = rival;
                            Vector3 rPos = GetActorWorldCenter(rival);
                            ai.LookAt(rPos);
                            if (a.activeWeapon != null)
                            {
                                if (a.activeWeapon.ammo <= 0)
                                {
                                    try { a.activeWeapon.InstantlyReload(); } catch (Exception) { a.activeWeapon.ammo = 30; }
                                }
                                Vector3 muzzle = a.WeaponMuzzlePosition();
                                Vector3 dir = (rPos - muzzle).normalized;
                                if ((rPos - muzzle).sqrMagnitude < 160f * 160f)
                                {
                                    try { a.activeWeapon.Fire(dir, false); } catch (Exception) { }
                                }
                            }
                        }
                    }
                }
            }
        }

        private Actor FindNearestOtherEnemy(Actor self)
        {
            if (actorManager == null || actorManager.actors == null || self == null) return null;
            Vector3 myPos = GetActorWorldCenter(self);
            Actor best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor other = actorManager.actors[i];
                if (other == null || other == self || other == localPlayer || other.dead || other.isDeactivated) continue;
                if (other.team != self.team) continue;
                Vector3 oPos = GetActorWorldCenter(other);
                if (!IsValidWorldPos(oPos)) continue;
                float d2 = (oPos - myPos).sqrMagnitude;
                if (d2 < bestSqr)
                {
                    bestSqr = d2;
                    best = other;
                }
            }
            return best;
        }

        private void RestoreAllEnemies()
        {
            if (actorManager == null || actorManager.actors == null) return;
            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null) continue;
                if (frozenEnemyIds.Contains(a.GetInstanceID()))
                {
                    if (a.controller != null) a.controller.enabled = true;
                    if (a.animator != null) a.animator.speed = 1f;
                }
            }
            frozenEnemyIds.Clear();
        }

        private void ApplyVampireCheck()
        {
            if (!bVampire || actorManager == null || actorManager.actors == null || localPlayer == null || localPlayer.dead)
            {
                lastAliveEnemyCount = -1;
                return;
            }

            int aliveEnemies = 0;
            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a != null && !a.dead && !a.isDeactivated && a.team != localPlayer.team)
                {
                    aliveEnemies++;
                }
            }

            if (lastAliveEnemyCount >= 0 && aliveEnemies < lastAliveEnemyCount)
            {
                int diff = Mathf.Clamp(lastAliveEnemyCount - aliveEnemies, 1, 10);
                localPlayer.maxHealth = Mathf.Min(2500f, localPlayer.maxHealth + 25f * diff);
                localPlayer.health = Mathf.Min(localPlayer.maxHealth, localPlayer.health + 45f * diff);
                localPlayer.balance = localPlayer.maxBalance;
            }
            lastAliveEnemyCount = aliveEnemies;
        }

        private void ApplyBodyguardSquad()
        {
            if (!bBodyguardSquad || actorManager == null || actorManager.actors == null || localPlayer == null || localPlayer.dead) return;
            if (Time.unscaledTime < nextBodyguardTime) return;
            nextBodyguardTime = Time.unscaledTime + 1.2f;

            Squad playerSquad = fpc != null ? fpc.playerSquad : null;
            Vector3 pPos = GetActorWorldCenter(localPlayer);
            int guardCount = 0;

            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a == localPlayer || a.dead || a.isDeactivated || a.team != localPlayer.team) continue;

                AiActorController ai = a.controller as AiActorController;
                if (ai == null) continue;

                bool inMySquad = playerSquad != null && ai.squad == playerSquad;
                if (!inMySquad && guardCount < 6 && playerSquad != null)
                {
                    try
                    {
                        ai.ChangeToSquad(playerSquad);
                        inMySquad = true;
                    }
                    catch (Exception)
                    {
                    }
                }

                if (inMySquad || guardCount < 6)
                {
                    guardCount++;
                    a.maxHealth = Mathf.Max(a.maxHealth, 500f);
                    a.health = a.maxHealth;
                    a.balance = a.maxBalance;
                    if (a.activeWeapon != null && a.activeWeapon.ammo <= 2)
                    {
                        try { a.activeWeapon.InstantlyReload(); } catch (Exception) { a.activeWeapon.ammo = 30; }
                    }
                    Vector3 aPos = GetActorWorldCenter(a);
                    if (!a.IsSeated() && !localPlayer.IsSeated() && (aPos - pPos).sqrMagnitude > 45f * 45f)
                    {
                        Vector2 rnd = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(3f, 6f);
                        a.transform.position = pPos + new Vector3(rnd.x, 0.3f, rnd.y);
                    }
                }
            }
        }

        private void ApplyAutoSpotEnemies()
        {
            if (!bAutoSpotEnemies || actorManager == null || actorManager.actors == null || localPlayer == null) return;
            if (Time.unscaledTime < nextSpotTime) return;
            nextSpotTime = Time.unscaledTime + 1.5f;

            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a.dead || a.isDeactivated || a.team == localPlayer.team) continue;
                try
                {
                    a.Highlight(3.5f);
                }
                catch (Exception)
                {
                }
            }
        }

        public bool Vis(Vector3 targetPos, Actor targetActor)
        {
            Vector3 origin = Eye();
            Vector3 delta = targetPos - origin;
            float dist = delta.magnitude;
            if (dist < 0.5f) return true;

            RaycastHit hit;
            int obstacleMask = (1 << 0) | (1 << 14);
            if (Physics.Raycast(origin, delta / dist, out hit, dist - 0.35f, obstacleMask))
            {
                if (targetActor != null && hit.transform != null)
                {
                    if (hit.transform.IsChildOf(targetActor.transform)) return true;
                    if (targetActor.IsSeated() && targetActor.seat != null && targetActor.seat.vehicle != null &&
                        hit.transform.IsChildOf(targetActor.seat.vehicle.transform))
                    {
                        return true;
                    }
                }
                return false;
            }
            return true;
        }

        public Actor FindBest(bool requireScreenFov, float maxRadiusPx)
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null || activeCam == null) return null;

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector3 eye = Eye();
            Actor best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (!IsSelectableTarget(a)) continue;

                Vector3 aimPos = AimPoint(a);
                if (!IsValidWorldPos(aimPos)) continue;

                float worldDist = Vector3.Distance(eye, aimPos);
                if (worldDist > espMaxDistance) continue;

                if (bVisCheck && !bMagicBullet && !Vis(aimPos, a)) continue;

                Vector3 sp = activeCam.WorldToScreenPoint(aimPos);
                if (sp.z <= 0.05f) continue;

                Vector2 guiPt = new Vector2(sp.x, Screen.height - sp.y);
                float pxDist = Vector2.Distance(screenCenter, guiPt);
                if (requireScreenFov && pxDist > maxRadiusPx) continue;

                float score = pxDist + worldDist * 0.15f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = a;
                }
            }
            return best;
        }

        private void AimAtBest()
        {
            if (activeCam == null || localPlayer == null || localPlayer.dead) return;
            Actor target = FindBest(true, Mathf.Clamp(aimFov, 15f, 1200f));
            if (target == null) return;

            Vector3 aimPos = AimPoint(target);
            if (!bInstantHit && !bMagicBullet && localPlayer.activeWeapon != null && localPlayer.activeWeapon.configuration != null)
            {
                float projSpeed = 450f;
                GameObject pfGo = localPlayer.activeWeapon.configuration.projectilePrefab;
                if (pfGo != null)
                {
                    Projectile pf = pfGo.GetComponent<Projectile>();
                    if (pf != null && pf.configuration != null && pf.configuration.speed > 10f)
                    {
                        projSpeed = pf.configuration.speed;
                    }
                }
                float t = Vector3.Distance(Eye(), aimPos) / projSpeed;
                aimPos += target.Velocity() * Mathf.Clamp(t, 0f, 1.2f);
            }

            Vector3 dir = aimPos - Eye();
            if (dir.sqrMagnitude < 0.0001f) return;

            Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 euler = lookRot.eulerAngles;
            float pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;
            pitch = Mathf.Clamp(pitch, -87f, 87f);

            Quaternion targetRot = Quaternion.Euler(pitch, euler.y, 0f);
            if (!localPlayer.IsSeated() && fpc != null && fpc.controller != null && fpc.controller.cameraParent != null)
            {
                fpc.controller.cameraParent.localRotation = targetRot;
            }
            activeCam.transform.rotation = targetRot;
        }

        private void TriggerBot()
        {
            if (localPlayer == null || localPlayer.dead || activeCam == null) return;
            Weapon w = localPlayer.activeWeapon;
            if (w == null || w.reloading || w.CoolingDown()) return;

            Actor target = null;
            Vector3 fireDir = activeCam.transform.forward;

            if (bMagicBullet)
            {
                target = FindBest(true, Mathf.Clamp(aimFov, 25f, 1200f));
                if (target != null)
                {
                    Vector3 aimPos = AimPoint(target);
                    fireDir = (aimPos - Eye()).normalized;
                }
            }
            else
            {
                RaycastHit hit;
                int mask = Hitbox.ALL_HITBOX_LAYER_MASK | (1 << 0) | (1 << 14);
                if (Physics.Raycast(activeCam.transform.position, activeCam.transform.forward, out hit, espMaxDistance, mask))
                {
                    Hitbox hb = hit.collider != null ? hit.collider.GetComponent<Hitbox>() : null;
                    Actor hitActor = (hb != null ? (hb.parent as Actor) : null) ?? hit.collider.GetComponentInParent<Actor>();
                    if (IsSelectableTarget(hitActor))
                    {
                        target = hitActor;
                    }
                }

                if (target == null)
                {
                    Actor cand = FindBest(true, 22f);
                    if (cand != null && Vis(AimPoint(cand), cand))
                    {
                        target = cand;
                        fireDir = (AimPoint(cand) - Eye()).normalized;
                    }
                }
            }

            if (target != null)
            {
                try
                {
                    w.Fire(fireDir, false);
                }
                catch (Exception)
                {
                }
            }
        }

        private void KillAllEnemies(bool silent)
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return;
            int count = 0;
            Weapon srcWeapon = localPlayer.activeWeapon;
            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a.dead || a.isDeactivated || a == localPlayer || a.team == localPlayer.team) continue;

                try
                {
                    a.isInvulnerable = false;
                    if (a.IsSeated())
                    {
                        a.LeaveSeat(false);
                    }
                    Vector3 hPos = HeadOf(a);
                    DamageInfo di = new DamageInfo(DamageInfo.DamageSourceType.Projectile, localPlayer, srcWeapon)
                    {
                        healthDamage = 99999f,
                        balanceDamage = 99999f,
                        isPiercing = true,
                        isCriticalHit = true,
                        point = hPos,
                        direction = Vector3.down,
                        impactForce = Vector3.up * 180f
                    };
                    a.Damage(di);
                    count++;
                }
                catch (Exception)
                {
                }
            }
            if (!silent)
            {
                Banner("全屏秒杀已执行：击杀 " + count + " 名敌人");
            }
        }

        private void Teleport()
        {
            if (localPlayer == null || activeCam == null) return;
            RaycastHit hit;
            Vector3 origin = activeCam.transform.position + activeCam.transform.forward * 0.6f;
            Vector3 dest;
            if (Physics.Raycast(origin, activeCam.transform.forward, out hit, 2500f))
            {
                dest = hit.point + Vector3.up * 1.6f;
            }
            else
            {
                dest = activeCam.transform.position + activeCam.transform.forward * 35f;
            }

            if (localPlayer.IsSeated() && localPlayer.seat != null && localPlayer.seat.vehicle != null)
            {
                localPlayer.seat.vehicle.transform.position = dest + Vector3.up * 1.5f;
                Banner("已携载具瞬移至准星位置");
            }
            else
            {
                localPlayer.transform.position = dest;
                Banner("已瞬移至准星位置");
            }
        }

        private void SummonVehicle()
        {
            if (actorManager == null || localPlayer == null || activeCam == null) return;
            Vector3 spawnPos = GetActorWorldCenter(localPlayer) + activeCam.transform.forward * 9f + Vector3.up * 2.5f;
            Quaternion spawnRot = Quaternion.Euler(0f, activeCam.transform.eulerAngles.y, 0f);

            if (actorManager.vehicles != null)
            {
                for (int i = 0; i < actorManager.vehicles.Count; i++)
                {
                    Vehicle v = actorManager.vehicles[i];
                    if (v == null || v.dead || v.isTurret) continue;
                    if (!v.AnySeatsTaken())
                    {
                        v.transform.position = spawnPos;
                        v.transform.rotation = spawnRot;
                        v.health = v.maxHealth;
                        v.burning = false;
                        Rigidbody rb = v.rigidbody != null ? v.rigidbody : v.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                        Banner("已召唤空闲载具：" + v.name);
                        return;
                    }
                }
            }

            VehicleSpawner[] spawners = UnityEngine.Object.FindObjectsOfType<VehicleSpawner>();
            if (spawners != null && spawners.Length > 0)
            {
                for (int i = 0; i < spawners.Length; i++)
                {
                    VehicleSpawner sp = spawners[i];
                    if (sp == null) continue;
                    GameObject prefab = sp.GetPrefab(localPlayer.team);
                    if (prefab != null)
                    {
                        GameObject go = UnityEngine.Object.Instantiate(prefab, spawnPos, spawnRot);
                        Vehicle v = go.GetComponent<Vehicle>();
                        if (v != null)
                        {
                            Banner("已生成新载具：" + v.name);
                            return;
                        }
                    }
                }
            }
            Banner("当前地图未找到可用载具");
        }

        private void Recruit()
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return;
            Squad playerSquad = fpc != null ? fpc.playerSquad : null;
            Vector3 pPos = GetActorWorldCenter(localPlayer);
            int count = 0;

            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a == localPlayer || a.dead || a.isDeactivated || a.team != localPlayer.team) continue;

                if (a.IsSeated())
                {
                    try { a.LeaveSeat(false); } catch (Exception) { }
                }

                Vector2 rnd = UnityEngine.Random.insideUnitCircle * 5.5f;
                a.transform.position = pPos + new Vector3(rnd.x, 0.5f, rnd.y);
                a.maxHealth = Mathf.Max(a.maxHealth, 350f);
                a.health = a.maxHealth;
                a.balance = a.maxBalance;

                AiActorController ai = a.controller as AiActorController;
                if (ai != null && playerSquad != null && count < 12)
                {
                    try { ai.ChangeToSquad(playerSquad); } catch (Exception) { }
                }
                count++;
                if (count >= 16) break;
            }
            Banner("已集结并强化 " + count + " 名友军到身边");
        }

        private void Riot()
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return;
            int count = 0;
            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a.dead || a.isDeactivated || a == localPlayer || a.team == localPlayer.team) continue;

                try
                {
                    if (a.IsSeated()) a.LeaveSeat(false);
                    Vector3 force = Vector3.up * 26f + UnityEngine.Random.insideUnitSphere * 12f;
                    a.KnockOver(force * 25f);
                    if (a.ragdoll != null && a.ragdoll.MainRigidbody() != null)
                    {
                        a.ragdoll.MainRigidbody().AddForce(force, ForceMode.VelocityChange);
                    }
                    count++;
                }
                catch (Exception)
                {
                }
            }
            Banner("全图击飞已触发：掀翻 " + count + " 名敌人");
        }

        private void LaunchEnemiesParachute()
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return;
            int count = 0;
            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a.dead || a.isDeactivated || a == localPlayer || a.team == localPlayer.team) continue;

                try
                {
                    if (a.IsSeated()) a.LeaveSeat(false);
                    Vector3 dest = GetActorWorldCenter(a) + Vector3.up * 95f;
                    if (a.fallenOver)
                    {
                        a.InstantGetUpAtPosition(dest);
                    }
                    else
                    {
                        a.transform.position = dest;
                    }
                    a.canDeployParachute = true;
                    a.DeployParachute();
                    count++;
                }
                catch (Exception)
                {
                }
            }
            Banner("全敌高空跳伞：已将 " + count + " 名敌人送入高空开伞");
        }

        private void DisarmAllEnemies()
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return;
            int count = 0;
            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a.dead || a.isDeactivated || a == localPlayer || a.team == localPlayer.team) continue;

                if (a.weapons != null)
                {
                    for (int j = 0; j < a.weapons.Length; j++)
                    {
                        Weapon w = a.weapons[j];
                        if (w != null)
                        {
                            w.ammo = 0;
                            w.spareAmmo = 0;
                        }
                    }
                }
                if (a.activeWeapon != null)
                {
                    a.activeWeapon.ammo = 0;
                    a.activeWeapon.spareAmmo = 0;
                }
                count++;
            }
            Banner("全敌缴械完成：已清空 " + count + " 名敌人的弹药");
        }

        private void HealAndResupplyAllies()
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return;
            int count = 0;
            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a.dead || a.isDeactivated || a.team != localPlayer.team) continue;

                a.maxHealth = Mathf.Max(a.maxHealth, 300f);
                a.health = a.maxHealth;
                a.balance = a.maxBalance;
                if (a.weapons != null)
                {
                    for (int j = 0; j < a.weapons.Length; j++)
                    {
                        Weapon w = a.weapons[j];
                        if (w != null && w.configuration != null)
                        {
                            w.ammo = Mathf.Max(w.configuration.ammo, 30);
                            w.spareAmmo = 999;
                        }
                    }
                }
                count++;
            }
            Banner("全友军满血补给完成：强化 " + count + " 名友军");
        }

        private void ExplodeEnemyVehicles()
        {
            if (actorManager == null || actorManager.vehicles == null || localPlayer == null) return;
            int count = 0;
            for (int i = 0; i < actorManager.vehicles.Count; i++)
            {
                Vehicle v = actorManager.vehicles[i];
                if (v == null || v.dead) continue;
                if (localPlayer.IsSeated() && localPlayer.seat != null && localPlayer.seat.vehicle == v) continue;
                if (v.ownerTeam == localPlayer.team || v.ownerTeam < 0) continue;

                try
                {
                    v.isInvulnerable = false;
                    DamageInfo di = new DamageInfo(DamageInfo.DamageSourceType.Explosion, localPlayer, localPlayer.activeWeapon)
                    {
                        healthDamage = v.maxHealth * 10f + 50000f,
                        balanceDamage = 5000f,
                        isPiercing = true,
                        point = v.transform.position,
                        direction = Vector3.down,
                        impactForce = Vector3.up * 500f
                    };
                    v.Damage(di);
                    count++;
                }
                catch (Exception)
                {
                }
            }
            Banner("已引爆全部敌方载具：共摧毁 " + count + " 辆");
        }

        private void CaptureAllPoints()
        {
            if (actorManager == null || actorManager.spawnPoints == null || localPlayer == null) return;
            int count = 0;
            for (int i = 0; i < actorManager.spawnPoints.Length; i++)
            {
                SpawnPoint sp = actorManager.spawnPoints[i];
                if (sp == null) continue;
                try
                {
                    sp.SetOwner(localPlayer.team, false);
                    count++;
                }
                catch (Exception)
                {
                }
            }
            Banner("一键夺取全图据点：已占领 " + count + " 个据点");
        }

        private void PossessBot()
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return;

            Actor cand = FindBest(false, 9999f);
            if (cand == null)
            {
                Vector3 eye = Eye();
                float bestD2 = float.MaxValue;
                for (int i = 0; i < actorManager.actors.Count; i++)
                {
                    Actor a = actorManager.actors[i];
                    if (a == null || a == localPlayer || a.dead || a.isDeactivated || !a.aiControlled) continue;
                    if (a.team != localPlayer.team) continue;
                    float d2 = (GetActorWorldCenter(a) - eye).sqrMagnitude;
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        cand = a;
                    }
                }
            }

            if (cand == null)
            {
                Banner("未找到可夺舍的目标单位");
                return;
            }

            try
            {
                cand.team = localPlayer.team;
                ActorManager.PlayerTakeOverBot(cand);
                ResolveContext();
                Banner("夺舍成功：已附身控制 " + cand.name);
            }
            catch (Exception)
            {
                Banner("当前状态无法执行夺舍（请先离开载具）");
            }
        }

        private void QueueAirstrike()
        {
            if (activeCam == null) return;
            RaycastHit hit;
            if (Physics.Raycast(activeCam.transform.position + activeCam.transform.forward * 0.6f, activeCam.transform.forward, out hit, 2500f))
            {
                airstrikeCenter = hit.point;
            }
            else
            {
                airstrikeCenter = activeCam.transform.position + activeCam.transform.forward * 45f;
            }
            pendingAirstrikes = 14;
            nextAirstrikeTime = Time.unscaledTime;
            Banner("地毯式空袭已锁定目标区域 (14 轮轰炸)");
        }

        private void UpdateAirstrike()
        {
            if (pendingAirstrikes <= 0 || Time.unscaledTime < nextAirstrikeTime) return;
            nextAirstrikeTime = Time.unscaledTime + 0.11f;
            pendingAirstrikes--;

            Vector2 offset = UnityEngine.Random.insideUnitCircle * 18f;
            Vector3 pos = airstrikeCenter + new Vector3(offset.x, 1.0f, offset.y);
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 60f, Vector3.down, out hit, 140f))
            {
                pos = hit.point;
            }

            TriggerGameExplosion(localPlayer, localPlayer != null ? localPlayer.activeWeapon : null, pos, 480f, 14f);
        }

        private void SpawnTornado()
        {
            if (activeCam == null) return;
            RaycastHit hit;
            if (Physics.Raycast(activeCam.transform.position + activeCam.transform.forward * 0.6f, activeCam.transform.forward, out hit, 2500f))
            {
                tornadoCenter = hit.point;
            }
            else
            {
                tornadoCenter = activeCam.transform.position + activeCam.transform.forward * 35f;
            }
            tornadoActive = true;
            tornadoUntil = Time.unscaledTime + 10f;
            Banner("超级龙卷风已在准星位置生成 (持续 10 秒)");
        }

        private void UpdateTornado()
        {
            if (!tornadoActive) return;
            if (Time.unscaledTime >= tornadoUntil)
            {
                tornadoActive = false;
                return;
            }
            if (actorManager == null || actorManager.actors == null || localPlayer == null) return;

            tornadoCenter += new Vector3(Mathf.Sin(Time.unscaledTime * 1.7f), 0f, Mathf.Cos(Time.unscaledTime * 1.4f)) * Time.unscaledDeltaTime * 3.5f;

            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a.dead || a.isDeactivated || a == localPlayer || a.team == localPlayer.team) continue;

                Vector3 aPos = GetActorWorldCenter(a);
                Vector3 toCenter = tornadoCenter - aPos;
                Vector3 flat = new Vector3(toCenter.x, 0f, toCenter.z);
                float dist = flat.magnitude;
                if (dist > 45f) continue;

                try
                {
                    if (a.IsSeated()) a.LeaveSeat(false);
                    Vector3 tangent = Vector3.Cross(Vector3.up, flat.normalized);
                    Vector3 vel = (flat.normalized * 12f + tangent * 24f + Vector3.up * 16f) * Time.unscaledDeltaTime;
                    a.transform.position += vel;
                    if (!a.fallenOver)
                    {
                        a.KnockOver(Vector3.up * 180f);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void UpdateTrail()
        {
            if (!bTrail || localPlayer == null || localPlayer.dead) return;
            if (Time.unscaledTime < nextTrailTime) return;
            nextTrailTime = Time.unscaledTime + 0.045f;

            Vector3 pos = FootOf(localPlayer) + Vector3.up * 0.12f;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "RC_TrailNode";
            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);

            float w = Mathf.Clamp(trailWidth, 0.04f, 1.2f);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(w, w, w);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                EnsureMaterials();
                Material m = new Material(chamsEnemyMat);
                Color c = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.65f, 1f), 0.9f, 1f);
                c.a = 0.85f;
                m.color = c;
                r.material = m;
            }
            UnityEngine.Object.Destroy(go, Mathf.Clamp(trailLifetime, 0.1f, 5.0f));
        }

        private void UpdateBounce()
        {
            if (!bBounce || localPlayer == null || localPlayer.dead || actorManager == null || actorManager.actors == null) return;
            if (Time.unscaledTime < nextBounceTime) return;
            nextBounceTime = Time.unscaledTime + Mathf.Clamp(bounceInterval, 0.25f, 10f);

            Vector3 pPos = GetActorWorldCenter(localPlayer);
            float rad2 = Mathf.Clamp(bounceRadius, 2f, 80f);
            rad2 *= rad2;
            float frc = Mathf.Clamp(bounceForce, 5f, 80f);

            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a.dead || a.isDeactivated || a == localPlayer || a.team == localPlayer.team) continue;

                Vector3 aPos = GetActorWorldCenter(a);
                Vector3 diff = aPos - pPos;
                if (diff.sqrMagnitude <= rad2)
                {
                    try
                    {
                        if (a.IsSeated()) a.LeaveSeat(false);
                        Vector3 push = (diff.normalized + Vector3.up * 1.15f).normalized * frc;
                        a.KnockOver(push * 20f);
                        if (a.ragdoll != null && a.ragdoll.MainRigidbody() != null)
                        {
                            a.ragdoll.MainRigidbody().AddForce(push, ForceMode.VelocityChange);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.F1)) bESP = !bESP;
            if (Input.GetKeyDown(KeyCode.F2)) bAimbot = !bAimbot;
            if (Input.GetKeyDown(KeyCode.F3)) bGod = !bGod;
            if (Input.GetKeyDown(KeyCode.F4)) bUnlimitedAmmo = !bUnlimitedAmmo;
            if (Input.GetKeyDown(KeyCode.F5)) bFly = !bFly;
            if (Input.GetKeyDown(KeyCode.F6)) KillAllEnemies(false);
            if (Input.GetKeyDown(KeyCode.F7)) Teleport();
            if (Input.GetKeyDown(KeyCode.F8)) bSlowMo = !bSlowMo;
            if (Input.GetKeyDown(KeyCode.F9))
            {
                bChams = !bChams;
                UpdateChams(true);
            }
            if (Input.GetKeyDown(KeyCode.F10)) bMagicBullet = !bMagicBullet;
        }

        private Color SelectedHighlightColor(bool isAlly)
        {
            int index = isAlly ? allyChamsColorIndex : enemyChamsColorIndex;
            return highlightColors[Mathf.Clamp(index, 0, highlightColors.Length - 1)];
        }

        private void EnsureMaterials()
        {
            if (whiteTex == null)
            {
                whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                whiteTex.SetPixel(0, 0, Color.white);
                whiteTex.Apply();
            }

            if (chamsEnemyMat == null)
            {
                Color color = SelectedHighlightColor(false);
                color.a = chamsOpacity;
                chamsEnemyMat = CreateWallhackMaterial(color);
            }
            if (chamsAllyMat == null)
            {
                Color color = SelectedHighlightColor(true);
                color.a = chamsOpacity * 0.85f;
                chamsAllyMat = CreateWallhackMaterial(color);
            }
        }

        private Material CreateWallhackMaterial(Color col)
        {
            Shader s = Shader.Find("Hidden/Internal-Colored");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Color");

            Material m = new Material(s);
            m.hideFlags = HideFlags.HideAndDontSave;
            m.color = col;
            m.SetColor("_Color", col);
            m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            m.SetInt("_ZWrite", 0);
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.renderQueue = 4000;
            return m;
        }

        private void UpdateChamsColors()
        {
            EnsureMaterials();
            float alpha = Mathf.Clamp(chamsOpacity, 0.15f, 1.0f);
            Color eCol;
            Color aCol;

            if (bRainbowChams)
            {
                float h1 = Mathf.Repeat(Time.unscaledTime * 0.45f, 1f);
                float h2 = Mathf.Repeat(h1 + 0.45f, 1f);
                eCol = Color.HSVToRGB(h1, 0.92f, 1f);
                aCol = Color.HSVToRGB(h2, 0.85f, 1f);
            }
            else
            {
                eCol = SelectedHighlightColor(false);
                aCol = SelectedHighlightColor(true);
            }
            eCol.a = alpha;
            aCol.a = alpha * 0.85f;

            chamsEnemyMat.color = eCol;
            chamsEnemyMat.SetColor("_Color", eCol);
            chamsEnemyMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            chamsEnemyMat.SetInt("_ZWrite", 0);

            chamsAllyMat.color = aCol;
            chamsAllyMat.SetColor("_Color", aCol);
            chamsAllyMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            chamsAllyMat.SetInt("_ZWrite", 0);
        }

        private void UpdateChams(bool forceImmediate)
        {
            if (!bChams)
            {
                if (chamsBackups.Count > 0)
                {
                    ClearChamsCache();
                }
                return;
            }

            UpdateChamsColors();

            if (!forceImmediate && Time.unscaledTime < nextChamsTime) return;
            nextChamsTime = Time.unscaledTime + 0.35f;

            if (actorManager == null || actorManager.actors == null || localPlayer == null) return;

            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a == localPlayer) continue;

                int id = a.GetInstanceID();
                if (a.dead || a.isDeactivated)
                {
                    RestoreActorChams(id);
                    continue;
                }

                bool isAlly = a.team == localPlayer.team;
                if ((isAlly && !bChamsAllies) || (!isAlly && !bChamsEnemies))
                {
                    RestoreActorChams(id);
                    continue;
                }

                Material targetMat = isAlly ? chamsAllyMat : chamsEnemyMat;
                ApplyActorChams(a, id, targetMat);
            }
        }

        private void ApplyActorChams(Actor a, int id, Material targetMat)
        {
            List<RendererBackup> backups;
            if (!chamsBackups.TryGetValue(id, out backups))
            {
                backups = new List<RendererBackup>();
                SkinnedMeshRenderer[] skinned = a.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < skinned.Length; i++)
                {
                    SkinnedMeshRenderer smr = skinned[i];
                    if (smr == null) continue;
                    backups.Add(new RendererBackup
                    {
                        renderer = smr,
                        sharedMaterials = smr.sharedMaterials
                    });
                }
                chamsBackups[id] = backups;
            }

            for (int i = 0; i < backups.Count; i++)
            {
                Renderer r = backups[i].renderer;
                if (r == null) continue;

                SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
                if (smr != null)
                {
                    smr.updateWhenOffscreen = true;
                }

                Material[] cur = r.sharedMaterials;
                int len = (cur != null && cur.Length > 0) ? cur.Length : 1;
                bool needsAssign = false;
                for (int m = 0; m < len; m++)
                {
                    if (cur[m] != targetMat)
                    {
                        needsAssign = true;
                        break;
                    }
                }
                if (needsAssign)
                {
                    Material[] rep = new Material[len];
                    for (int m = 0; m < len; m++) rep[m] = targetMat;
                    r.sharedMaterials = rep;
                }
            }
        }

        private void RestoreActorChams(int id)
        {
            List<RendererBackup> backups;
            if (!chamsBackups.TryGetValue(id, out backups)) return;
            for (int i = 0; i < backups.Count; i++)
            {
                Renderer r = backups[i].renderer;
                if (r != null && backups[i].sharedMaterials != null)
                {
                    r.sharedMaterials = backups[i].sharedMaterials;
                }
            }
            chamsBackups.Remove(id);
        }

        private void ClearChamsCache()
        {
            foreach (KeyValuePair<int, List<RendererBackup>> kv in chamsBackups)
            {
                List<RendererBackup> list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].renderer != null && list[i].sharedMaterials != null)
                    {
                        list[i].renderer.sharedMaterials = list[i].sharedMaterials;
                    }
                }
            }
            chamsBackups.Clear();
        }

        private bool ComputeActorScreenBox(Actor a, Camera cam, out Rect boxRect, out Vector2 headGui)
        {
            boxRect = default(Rect);
            headGui = Vector2.zero;
            if (a == null || cam == null) return false;

            Vector3 center = GetActorWorldCenter(a);
            if (!IsValidWorldPos(center)) return false;

            Vector3 spCenter = cam.WorldToScreenPoint(center);
            if (spCenter.z <= 0.25f) return false;

            Vector3 headW = HeadOf(a) + Vector3.up * 0.14f;
            Vector3 footW = FootOf(a) - Vector3.up * 0.06f;

            Vector3 spHead = cam.WorldToScreenPoint(headW);
            Vector3 spFoot = cam.WorldToScreenPoint(footW);
            if (spHead.z <= 0.15f || spFoot.z <= 0.15f) return false;

            headGui = new Vector2(spHead.x, Screen.height - spHead.y);
            Vector2 footGui = new Vector2(spFoot.x, Screen.height - spFoot.y);

            float minX = Mathf.Min(headGui.x, footGui.x);
            float maxX = Mathf.Max(headGui.x, footGui.x);
            float minY = Mathf.Min(headGui.y, footGui.y);
            float maxY = Mathf.Max(headGui.y, footGui.y);

            bool usedBones = false;
            if (a.ragdoll != null && !a.IsSeated())
            {
                try
                {
                    HumanBodyBones[] sampleBones = new HumanBodyBones[]
                    {
                        HumanBodyBones.Head,
                        HumanBodyBones.Chest,
                        HumanBodyBones.Hips,
                        HumanBodyBones.LeftShoulder,
                        HumanBodyBones.RightShoulder,
                        HumanBodyBones.LeftHand,
                        HumanBodyBones.RightHand,
                        HumanBodyBones.LeftLowerLeg,
                        HumanBodyBones.RightLowerLeg,
                        HumanBodyBones.LeftFoot,
                        HumanBodyBones.RightFoot
                    };

                    int validBones = 0;
                    for (int i = 0; i < sampleBones.Length; i++)
                    {
                        Transform bt = a.ragdoll.HumanBoneTransformActive(sampleBones[i]);
                        if (bt == null) continue;
                        Vector3 bp = bt.position;
                        if (!IsValidWorldPos(bp) || (bp - center).sqrMagnitude > 9f) continue;

                        Vector3 bsp = cam.WorldToScreenPoint(bp);
                        if (bsp.z <= 0.15f) continue;
                        float gx = bsp.x;
                        float gy = Screen.height - bsp.y;
                        if (gx < minX) minX = gx;
                        if (gx > maxX) maxX = gx;
                        if (gy < minY) minY = gy;
                        if (gy > maxY) maxY = gy;
                        validBones++;
                    }
                    usedBones = validBones >= 4;
                }
                catch (Exception)
                {
                }
            }

            float focalPx = (Screen.height * 0.5f) / Mathf.Tan(cam.fieldOfView * 0.5f * MathF_Deg2Rad);
            float expectedStandH = (1.85f * focalPx) / Mathf.Max(spCenter.z, 1.0f);

            float h = maxY - minY;
            if (h < expectedStandH * 0.32f || h > expectedStandH * 1.65f)
            {
                float cx = spCenter.x;
                float cy = Screen.height - spCenter.y;
                h = Mathf.Clamp(expectedStandH, 14f, Screen.height * 0.85f);
                minY = cy - h * 0.52f;
                maxY = cy + h * 0.48f;
                float wFallback = h * 0.48f;
                minX = cx - wFallback * 0.5f;
                maxX = cx + wFallback * 0.5f;
            }
            else
            {
                float w = maxX - minX;
                float minW = h * 0.36f;
                float maxW = h * (usedBones ? 0.72f : 0.52f);
                if (w < minW || w > maxW)
                {
                    float cx = 0.5f * (minX + maxX);
                    float clampedW = Mathf.Clamp(w, minW, maxW);
                    minX = cx - clampedW * 0.5f;
                    maxX = cx + clampedW * 0.5f;
                }
            }

            float pad = Mathf.Clamp(espPadding, 0f, 0.35f);
            float padX = (maxX - minX) * pad;
            float padY = (maxY - minY) * pad;
            minX -= padX;
            maxX += padX;
            minY -= padY;
            maxY += padY;

            float finalW = maxX - minX;
            float finalH = maxY - minY;
            if (finalW < 6f || finalH < 8f || finalW > Screen.width * 0.9f || finalH > Screen.height * 0.95f)
            {
                return false;
            }

            boxRect = new Rect(minX, minY, finalW, finalH);
            return true;
        }

        private void EnsureStyles()
        {
            EnsureMaterials();
            if (windowStyle == null)
            {
                windowStyle = new GUIStyle(GUI.skin.window);
                windowStyle.fontSize = 14;
                windowStyle.fontStyle = FontStyle.Bold;
            }
            if (sectionStyle == null)
            {
                sectionStyle = new GUIStyle(GUI.skin.label);
                sectionStyle.fontSize = 13;
                sectionStyle.fontStyle = FontStyle.Bold;
                sectionStyle.normal.textColor = new Color(0.35f, 0.85f, 1f, 1f);
            }
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 12;
                labelStyle.normal.textColor = new Color(0.92f, 0.95f, 1f, 1f);
            }
            if (espTextStyle == null)
            {
                espTextStyle = new GUIStyle(GUI.skin.label);
                espTextStyle.fontSize = 11;
                espTextStyle.fontStyle = FontStyle.Bold;
                espTextStyle.alignment = TextAnchor.UpperCenter;
                espTextStyle.normal.textColor = Color.white;
            }
            if (watermarkStyle == null)
            {
                watermarkStyle = new GUIStyle(GUI.skin.label);
                watermarkStyle.fontSize = 13;
                watermarkStyle.fontStyle = FontStyle.Bold;
                watermarkStyle.normal.textColor = new Color(0.4f, 0.95f, 1f, 1f);
            }
            if (tabButtonStyle == null)
            {
                tabButtonStyle = new GUIStyle(GUI.skin.button);
                tabButtonStyle.fontSize = 12;
                tabButtonStyle.fontStyle = FontStyle.Bold;
                tabButtonStyle.fixedHeight = 28f;
            }
        }

        private void OnGUI()
        {
            try
            {
                EnsureStyles();

                if (Event.current.type == EventType.Repaint)
                {
                    DrawWatermarkAndBanner();
                    if (localPlayer != null && !localPlayer.dead && activeCam != null)
                    {
                        if (bESP) DrawESP();
                        if (bVehicleESP) DrawVehicleESP();
                        if (bRadar) DrawRadar();
                        if (bTargetHud) DrawTargetHud();
                        if (bCrosshair || (bAimbot && bDrawFov)) DrawCrosshairAndFov();
                    }
                }

                if (showMenu)
                {
                    winRect = GUI.Window(9527, winRect, DrawWindow, "RavenCheat 终极内置控制台 (INS 显示/隐藏)");
                }
            }
            catch (Exception)
            {
            }
        }

        private void DrawWatermarkAndBanner()
        {
            string status = string.Format(
                "RavenCheat PRO | 透视:{0} 穿墙高亮:{1} 自瞄:{2} 穿墙魔法弹:{3} 无敌:{4}",
                bESP ? "开" : "关",
                bChams ? "开" : "关",
                bAimbot ? "开" : "关",
                bMagicBullet ? "开" : "关",
                bGod ? "开" : "关"
            );
            DrawFilledRect(new Rect(12f, 8f, 520f, 24f), new Color(0.06f, 0.08f, 0.12f, 0.78f));
            GUI.Label(new Rect(18f, 10f, 510f, 22f), status, watermarkStyle);

            if (Time.unscaledTime < bannerUntil && !string.IsNullOrEmpty(bannerText))
            {
                DrawFilledRect(new Rect(12f, 36f, 520f, 24f), new Color(0.12f, 0.28f, 0.42f, 0.85f));
                GUI.Label(new Rect(18f, 38f, 510f, 22f), "提示: " + bannerText, labelStyle);
            }
        }

        private void DrawESP()
        {
            if (actorManager == null || actorManager.actors == null || localPlayer == null || activeCam == null) return;
            Vector3 eye = Eye();

            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a == localPlayer || a.dead || a.isDeactivated) continue;

                bool isAlly = a.team == localPlayer.team;
                if ((isAlly && !bEspAllies) || (!isAlly && !bEspEnemies)) continue;

                Vector3 center = GetActorWorldCenter(a);
                if (!IsValidWorldPos(center)) continue;

                float dist = Vector3.Distance(eye, center);
                if (dist > espMaxDistance) continue;

                Rect box;
                Vector2 headGui;
                if (!ComputeActorScreenBox(a, activeCam, out box, out headGui)) continue;

                Color col = bRainbowChams
                    ? Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.45f + i * 0.03f + (isAlly ? 0.45f : 0f), 1f), 0.9f, 1f)
                    : SelectedHighlightColor(isAlly);
                col.a = isAlly ? 0.92f : 0.95f;

                DrawCornerBox(box, col, 1.5f);

                float hpFrac = Mathf.Clamp01(a.health / Mathf.Max(1f, a.maxHealth));
                Rect hpBg = new Rect(box.x - 5f, box.y, 3f, box.height);
                DrawFilledRect(hpBg, new Color(0f, 0f, 0f, 0.65f));
                float fillH = box.height * hpFrac;
                Rect hpFg = new Rect(box.x - 5f, box.y + (box.height - fillH), 3f, fillH);
                DrawFilledRect(hpFg, Color.Lerp(Color.red, Color.green, hpFrac));

                if (!isAlly && headGui.x >= 0f && headGui.x <= Screen.width && headGui.y >= 0f && headGui.y <= Screen.height)
                {
                    DrawFilledRect(new Rect(headGui.x - 2f, headGui.y - 2f, 4f, 4f), new Color(1f, 0.92f, 0.2f, 0.95f));
                }

                if (dist < 420f)
                {
                    string wName = (a.activeWeapon != null && a.activeWeapon.weaponEntry != null)
                        ? a.activeWeapon.weaponEntry.name
                        : "";
                    string tag = string.IsNullOrEmpty(wName)
                        ? string.Format("{0:0}m", dist)
                        : string.Format("{0:0}m [{1}]", dist, wName);
                    espTextStyle.normal.textColor = col;
                    GUI.Label(new Rect(box.center.x - 90f, box.yMax + 1f, 180f, 18f), tag, espTextStyle);
                }
            }
        }

        private void DrawVehicleESP()
        {
            if (actorManager == null || actorManager.vehicles == null || localPlayer == null || activeCam == null) return;
            Vector3 eye = Eye();

            for (int i = 0; i < actorManager.vehicles.Count; i++)
            {
                Vehicle v = actorManager.vehicles[i];
                if (v == null || v.dead || v.isTurret) continue;
                if (localPlayer.IsSeated() && localPlayer.seat != null && localPlayer.seat.vehicle == v) continue;

                Vector3 vPos = v.transform.position + Vector3.up * 1.2f;
                if (!IsValidWorldPos(vPos)) continue;
                float dist = Vector3.Distance(eye, vPos);
                if (dist > espMaxDistance) continue;

                Vector3 sp = activeCam.WorldToScreenPoint(vPos);
                if (sp.z <= 0.5f) continue;

                float gx = sp.x;
                float gy = Screen.height - sp.y;
                float sz = Mathf.Clamp(2200f / Mathf.Max(sp.z, 5f), 18f, 90f);
                Rect vBox = new Rect(gx - sz * 0.65f, gy - sz * 0.45f, sz * 1.3f, sz * 0.9f);

                Color vCol;
                if (v.ownerTeam < 0) vCol = new Color(0.85f, 0.85f, 0.85f, 0.75f);
                else if (v.ownerTeam == localPlayer.team) vCol = new Color(0.25f, 0.85f, 1f, 0.88f);
                else vCol = new Color(1f, 0.55f, 0.15f, 0.92f);

                DrawOutlineRect(vBox, vCol, 1.5f);
                float vHp = Mathf.Clamp01(v.health / Mathf.Max(1f, v.maxHealth));
                espTextStyle.normal.textColor = vCol;
                GUI.Label(new Rect(gx - 100f, vBox.y - 16f, 200f, 18f),
                    string.Format("[载具] {0} ({1:0}%) {2:0}m", v.name, vHp * 100f, dist), espTextStyle);
            }
        }

        private void DrawRadar()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (actorManager == null || actorManager.actors == null || localPlayer == null || activeCam == null) return;

            float size = 166f;
            Rect rRect = new Rect(Screen.width - size - 18f, 18f, size, size);
            DrawFilledRect(rRect, new Color(0.05f, 0.08f, 0.12f, 0.82f));
            DrawOutlineRect(rRect, new Color(0.3f, 0.8f, 1f, 0.7f), 1.5f);

            Vector2 center = rRect.center;
            DrawFilledRect(new Rect(center.x - 1f, rRect.y + 4f, 2f, size - 8f), new Color(1f, 1f, 1f, 0.12f));
            DrawFilledRect(new Rect(rRect.x + 4f, center.y - 1f, size - 8f, 2f), new Color(1f, 1f, 1f, 0.12f));

            Vector3 myPos = Eye();
            float yawRad = activeCam.transform.eulerAngles.y * MathF_Deg2Rad;
            float cos = Mathf.Cos(yawRad);
            float sin = Mathf.Sin(yawRad);
            float maxR = Mathf.Max(30f, radarRange);
            float halfSz = (size * 0.5f) - 8f;

            for (int i = 0; i < actorManager.actors.Count; i++)
            {
                Actor a = actorManager.actors[i];
                if (a == null || a == localPlayer || a.dead || a.isDeactivated) continue;

                Vector3 aPos = GetActorWorldCenter(a);
                if (!IsValidWorldPos(aPos)) continue;

                float dx = aPos.x - myPos.x;
                float dz = aPos.z - myPos.z;

                float rx = dx * cos - dz * sin;
                float ry = dx * sin + dz * cos;

                float nx = Mathf.Clamp(rx / maxR, -1f, 1f);
                float ny = Mathf.Clamp(ry / maxR, -1f, 1f);

                float px = center.x + nx * halfSz;
                float py = center.y - ny * halfSz;

                Color dotCol = (a.team == localPlayer.team)
                    ? new Color(0.2f, 0.95f, 0.45f, 0.95f)
                    : new Color(1f, 0.22f, 0.25f, 0.95f);
                DrawFilledRect(new Rect(px - 2.5f, py - 2.5f, 5f, 5f), dotCol);
            }

            DrawFilledRect(new Rect(center.x - 3f, center.y - 3f, 6f, 6f), Color.cyan);
        }

        private void DrawTargetHud()
        {
            Actor t = FindBest(true, Mathf.Clamp(targetHudFov * 6f, 40f, 600f));
            if (t == null) return;

            float w = 260f;
            float h = 62f;
            Rect hud = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 42f, w, h);
            DrawFilledRect(hud, new Color(0.06f, 0.09f, 0.14f, 0.85f));
            DrawOutlineRect(hud, new Color(1f, 0.3f, 0.35f, 0.85f), 1.5f);

            float dist = Vector3.Distance(Eye(), GetActorWorldCenter(t));
            float hpFrac = Mathf.Clamp01(t.health / Mathf.Max(1f, t.maxHealth));
            string wName = (t.activeWeapon != null && t.activeWeapon.weaponEntry != null)
                ? t.activeWeapon.weaponEntry.name
                : "未知武器";

            GUI.Label(new Rect(hud.x + 10f, hud.y + 6f, w - 20f, 20f),
                string.Format("锁定目标: {0} ({1:0}m) [{2}]", t.name, dist, wName), labelStyle);

            Rect barBg = new Rect(hud.x + 10f, hud.y + 32f, w - 20f, 16f);
            DrawFilledRect(barBg, new Color(0.2f, 0.2f, 0.2f, 0.9f));
            DrawFilledRect(new Rect(barBg.x, barBg.y, barBg.width * hpFrac, barBg.height),
                Color.Lerp(Color.red, Color.green, hpFrac));
            GUI.Label(new Rect(barBg.x + 6f, barBg.y - 1f, barBg.width, 18f),
                string.Format("HP: {0:0} / {1:0}", t.health, t.maxHealth), labelStyle);
        }

        private void DrawCrosshairAndFov()
        {
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (bCrosshair)
            {
                Color cc = new Color(0.2f, 1f, 0.6f, 0.9f);
                DrawFilledRect(new Rect(c.x - 8f, c.y - 1f, 16f, 2f), cc);
                DrawFilledRect(new Rect(c.x - 1f, c.y - 8f, 2f, 16f), cc);
            }

            if (bAimbot && bDrawFov)
            {
                DrawCircle(c, Mathf.Clamp(aimFov, 15f, 1000f), new Color(1f, 1f, 1f, 0.28f), 48);
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            string[] tabs = new string[] { "全部功能", "透视 / 自瞄", "武器 / 弹道", "玩家 / 载具", "世界 / 娱乐", "系统 / 设置" };
            menuTab = GUILayout.Toolbar(menuTab, tabs, tabButtonStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ 全屏秒杀敌军", GUILayout.Height(26f))) KillAllEnemies(false);
            if (GUILayout.Button("📍 准星瞬移", GUILayout.Height(26f))) Teleport();
            if (GUILayout.Button("🚩 一键占领全据点", GUILayout.Height(26f))) CaptureAllPoints();
            if (GUILayout.Button("🎭 夺舍附身AI", GUILayout.Height(26f))) PossessBot();
            if (GUILayout.Button("🔄 恢复默认", GUILayout.Height(26f))) ResetAllSettings();
            GUILayout.EndHorizontal();

            scrollPos = GUILayout.BeginScrollView(scrollPos, false, true);

            bool showVisual = (menuTab == 0 || menuTab == 1);
            bool showWeapon = (menuTab == 0 || menuTab == 2);
            bool showPlayer = (menuTab == 0 || menuTab == 3);
            bool showWorld  = (menuTab == 0 || menuTab == 4);
            bool showSystem = (menuTab == 0 || menuTab == 5);

            if (showVisual)
            {
                GUILayout.Label("【透视与穿墙高亮 (ESP / Chams)】", sectionStyle);
                GUILayout.BeginHorizontal();
                bESP = GUILayout.Toggle(bESP, "人物方框/血条/距离透视");
                bVehicleESP = GUILayout.Toggle(bVehicleESP, "载具方框与血量透视");
                bool newChams = GUILayout.Toggle(bChams, "人物穿墙高亮 (ZTest Always)");
                if (newChams != bChams)
                {
                    bChams = newChams;
                    UpdateChams(true);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                bEspEnemies = GUILayout.Toggle(bEspEnemies, "ESP 显示敌军", GUILayout.Width(145f));
                bEspAllies = GUILayout.Toggle(bEspAllies, "ESP 显示友军", GUILayout.Width(145f));
                bool newEnemyChams = GUILayout.Toggle(bChamsEnemies, "高亮敌军模型", GUILayout.Width(145f));
                bool newAllyChams = GUILayout.Toggle(bChamsAllies, "高亮友军模型", GUILayout.Width(145f));
                GUILayout.EndHorizontal();
                if (newEnemyChams != bChamsEnemies || newAllyChams != bChamsAllies)
                {
                    bChamsEnemies = newEnemyChams;
                    bChamsAllies = newAllyChams;
                    UpdateChams(true);
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label("敌军高亮颜色", labelStyle, GUILayout.Width(95f));
                enemyChamsColorIndex = GUILayout.Toolbar(enemyChamsColorIndex, highlightColorNames, GUILayout.Width(420f));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("友军高亮颜色", labelStyle, GUILayout.Width(95f));
                allyChamsColorIndex = GUILayout.Toolbar(allyChamsColorIndex, highlightColorNames, GUILayout.Width(420f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                bRainbowChams = GUILayout.Toggle(bRainbowChams, "彩虹流光高亮/方框");
                bAutoSpotEnemies = GUILayout.Toggle(bAutoSpotEnemies, "全图敌军原生战术标记");
                bRadar = GUILayout.Toggle(bRadar, "右上角雷达小地图");
                bTargetHud = GUILayout.Toggle(bTargetHud, "底部锁定目标HUD");
                bCrosshair = GUILayout.Toggle(bCrosshair, "中心十字准星");
                GUILayout.EndHorizontal();

                DrawFloatSetting("espMaxDistance", "透视最大距离 (m)", ref espMaxDistance, 100f, 2500f, "0");
                DrawFloatSetting("espPadding", "透视框外扩比例", ref espPadding, 0.0f, 0.25f, "0.00");
                DrawFloatSetting("chamsOpacity", "穿墙高亮不透明度", ref chamsOpacity, 0.20f, 1.00f, "0.00");
                DrawFloatSetting("radarRange", "雷达扫描半径 (m)", ref radarRange, 50f, 800f, "0");
                DrawFloatSetting("targetHudFov", "目标HUD锁定范围", ref targetHudFov, 10f, 90f, "0");

                GUILayout.Space(6f);
                GUILayout.Label("【智能自瞄与穿墙魔法子弹】", sectionStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("自瞄/魔法弹/扳机/HUD 目标", labelStyle, GUILayout.Width(205f));
                targetTeamMode = GUILayout.Toolbar(targetTeamMode, new string[] { "仅敌军", "仅友军", "友敌均可" }, GUILayout.Width(340f));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                bAimbot = GUILayout.Toggle(bAimbot, "启用自动瞄准");
                bAimOnFire = GUILayout.Toggle(bAimOnFire, "仅开火/右键时自瞄");
                bAimAtHead = GUILayout.Toggle(bAimAtHead, "优先锁头 (关闭则锁胸)");
                bVisCheck = GUILayout.Toggle(bVisCheck, "仅瞄准可见目标");
                bDrawFov = GUILayout.Toggle(bDrawFov, "显示自瞄FOV圆圈");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                bTriggerBot = GUILayout.Toggle(bTriggerBot, "自动扳机 (对准即开火)");
                bMagicBullet = GUILayout.Toggle(bMagicBullet, "穿墙魔法子弹 (无视掩体直击目标)");
                bKillAllLoop = GUILayout.Toggle(bKillAllLoop, "全图自动连续秒杀敌军");
                GUILayout.EndHorizontal();

                DrawFloatSetting("aimFov", "自瞄/魔法弹搜索半径 (px)", ref aimFov, 20f, 800f, "0");
                DrawFloatSetting("killAllInterval", "自动秒杀间隔 (秒)", ref killAllInterval, 0.08f, 3.0f, "0.00");
                GUILayout.Space(8f);
            }

            if (showWeapon)
            {
                GUILayout.Label("【武器强化与魔改弹道】", sectionStyle);
                GUILayout.BeginHorizontal();
                bUnlimitedAmmo = GUILayout.Toggle(bUnlimitedAmmo, "主副武器无限子弹 (免换弹)");
                bInfiniteGrenades = GUILayout.Toggle(bInfiniteGrenades, "无限手雷/火箭筒/装备");
                bRapidFire = GUILayout.Toggle(bRapidFire, "极速连发 (全自动)");
                bNoRecoil = GUILayout.Toggle(bNoRecoil, "完全无后坐力");
                bNoSpread = GUILayout.Toggle(bNoSpread, "完全无散布/无晃动");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                bInstantHit = GUILayout.Toggle(bInstantHit, "激光瞬中弹道 (无下坠)");
                bShotgunMode = GUILayout.Toggle(bShotgunMode, "万物散弹枪模式");
                bSuperDamage = GUILayout.Toggle(bSuperDamage, "超级伤害倍率 (一枪秒载具)");
                bSuperKnockback = GUILayout.Toggle(bSuperKnockback, "超级击飞冲击波子弹");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                bBigBullets = GUILayout.Toggle(bBigBullets, "巨型炮弹模型");
                bExplosiveAmmo = GUILayout.Toggle(bExplosiveAmmo, "高爆弹头 (命中引发爆炸)");
                bVampire = GUILayout.Toggle(bVampire, "杀敌吸血并永久提升血上限");
                GUILayout.EndHorizontal();

                DrawFloatSetting("rapidFireCooldown", "极速连发射击间隔 (秒)", ref rapidFireCooldown, 0.01f, 0.25f, "0.00");
                DrawFloatSetting("shotgunPellets", "散弹模式每发弹丸数", ref shotgunPellets, 2f, 36f, "0");
                DrawFloatSetting("damageMultiplier", "子弹伤害倍率", ref damageMultiplier, 1f, 30f, "0.0");
                DrawFloatSetting("knockbackMultiplier", "子弹物理击飞倍率", ref knockbackMultiplier, 1f, 30f, "0.0");
                DrawFloatSetting("bulletScale", "巨型子弹尺寸倍率", ref bulletScale, 1f, 15f, "0.0");
                DrawFloatSetting("explosiveAmmoRadius", "高爆弹爆炸半径 (m)", ref explosiveAmmoRadius, 2f, 25f, "0.0");
                GUILayout.Space(8f);
            }

            if (showPlayer)
            {
                GUILayout.Label("【玩家强化与载具控制】", sectionStyle);
                GUILayout.BeginHorizontal();
                bGod = GUILayout.Toggle(bGod, "玩家无敌模式");
                bAutoHeal = GUILayout.Toggle(bAutoHeal, "自动满血与满平衡值");
                bNoFall = GUILayout.Toggle(bNoFall, "免疫跌倒/布偶化");
                bSpeed = GUILayout.Toggle(bSpeed, "移动加速");
                bSuperJump = GUILayout.Toggle(bSuperJump, "超级跳跃");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                bFly = GUILayout.Toggle(bFly, "自由飞行 (WASD+空格/Ctrl)");
                bNoclip = GUILayout.Toggle(bNoclip, "穿墙幽灵模式 (Noclip)");
                bGiant = GUILayout.Toggle(bGiant, "巨人/蚁人体型缩放");
                bBodyguardSquad = GUILayout.Toggle(bBodyguardSquad, "随身贴身护卫队 (自动招募+回血)");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                bVehicleGod = GUILayout.Toggle(bVehicleGod, "所乘载具无敌自动修复");
                bVehicleBoost = GUILayout.Toggle(bVehicleBoost, "载具Shift氮气推进");
                bUfoVehicle = GUILayout.Toggle(bUfoVehicle, "UFO飞天载具模式 (全向悬浮飞行)");
                GUILayout.EndHorizontal();

                DrawFloatSetting("speedMultiplier", "步行/奔跑速度倍率", ref speedMultiplier, 0.5f, 6.0f, "0.0");
                DrawFloatSetting("jumpMultiplier", "跳跃高度倍率", ref jumpMultiplier, 0.5f, 6.0f, "0.0");
                DrawFloatSetting("flySpeed", "自由飞行速度", ref flySpeed, 5f, 120f, "0");
                DrawFloatSetting("giantScale", "玩家体型缩放 (0.3=蚁人, 3+=巨人)", ref giantScale, 0.3f, 6.0f, "0.0");
                DrawFloatSetting("vehicleBoostPower", "载具推进加速度", ref vehicleBoostPower, 5f, 100f, "0");
                DrawFloatSetting("ufoSpeed", "UFO飞天载具速度", ref ufoSpeed, 15f, 160f, "0");
                GUILayout.Space(8f);
            }

            if (showWorld)
            {
                GUILayout.Label("【世界物理与趣味娱乐功能】", sectionStyle);
                GUILayout.BeginHorizontal();
                bSlowMo = GUILayout.Toggle(bSlowMo, "子弹时间 / 全局变速");
                bLowGravity = GUILayout.Toggle(bLowGravity, "自定义重力缩放");
                bFreezeEnemies = GUILayout.Toggle(bFreezeEnemies, "冻结全体敌军AI与动作");
                bEnemyInfighting = GUILayout.Toggle(bEnemyInfighting, "敌军内讧 (敌方AI互相残杀)");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                bMagnet = GUILayout.Toggle(bMagnet, "黑洞吸人 (将敌人吸到准星前)");
                bTrail = GUILayout.Toggle(bTrail, "脚底彩虹流光轨迹");
                bBounce = GUILayout.Toggle(bBounce, "护体反重力震波 (周期击飞周围敌人)");
                GUILayout.EndHorizontal();

                DrawFloatSetting("timeScale", "全局时间流速 (0.1=慢动作, 2=加速)", ref timeScale, 0.1f, 2.5f, "0.00");
                DrawFloatSetting("gravityScale", "全局重力倍率 (0.2=月球, -0.3=反重力)", ref gravityScale, -0.5f, 2.5f, "0.00");
                DrawFloatSetting("magnetRadius", "吸人准星前方距离 (m)", ref magnetRadius, 4f, 50f, "0");
                DrawFloatSetting("magnetForce", "黑洞吸附牵引强度", ref magnetForce, 2f, 40f, "0");
                DrawFloatSetting("trailLifetime", "彩虹轨迹持续时间 (秒)", ref trailLifetime, 0.1f, 3.0f, "0.00");
                DrawFloatSetting("trailWidth", "彩虹轨迹光球大小", ref trailWidth, 0.05f, 0.8f, "0.00");
                DrawFloatSetting("bounceInterval", "护体震波间隔 (秒)", ref bounceInterval, 0.3f, 5.0f, "0.0");
                DrawFloatSetting("bounceRadius", "护体震波半径 (m)", ref bounceRadius, 3f, 40f, "0");
                DrawFloatSetting("bounceForce", "护体震波击飞力度", ref bounceForce, 5f, 60f, "0");

                GUILayout.Space(6f);
                GUILayout.Label("【一键娱乐大招按钮 (无需任何快捷键，点击即触发)】", sectionStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("✈ 呼叫准星地毯式空袭", GUILayout.Height(28f))) QueueAirstrike();
                if (GUILayout.Button("🌪 在准星处生成超级龙卷风", GUILayout.Height(28f))) SpawnTornado();
                if (GUILayout.Button("🪂 全体敌军高空跳伞派对", GUILayout.Height(28f))) LaunchEnemiesParachute();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🔫 缴械全体敌军 (清空弹药)", GUILayout.Height(28f))) DisarmAllEnemies();
                if (GUILayout.Button("💥 全图敌军原地击飞暴动", GUILayout.Height(28f))) Riot();
                if (GUILayout.Button("🔥 引爆全场敌方载具", GUILayout.Height(28f))) ExplodeEnemyVehicles();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🚗 召唤空闲载具到面前", GUILayout.Height(28f))) SummonVehicle();
                if (GUILayout.Button("🛡 集结16名友军到身边", GUILayout.Height(28f))) Recruit();
                if (GUILayout.Button("💊 全图友军满血+满弹药补给", GUILayout.Height(28f))) HealAndResupplyAllies();
                GUILayout.EndHorizontal();
                GUILayout.Space(8f);
            }

            if (showSystem)
            {
                GUILayout.Label("【系统与可选按键设置】", sectionStyle);
                bHotkeys = GUILayout.Toggle(bHotkeys, "启用 F1-F10 功能快捷键 (默认关闭，防止与游戏按键冲突；INS 始终可开关菜单)");
                GUILayout.Label("Insert 菜单  |  F1 人物 ESP  |  F2 自瞄  |  F3 玩家无敌  |  F4 无限弹药  |  F5 飞行", labelStyle);
                GUILayout.Label("F6 秒杀敌军  |  F7 准星瞬移  |  F8 慢动作  |  F9 模型高亮  |  F10 魔法子弹", labelStyle);
                GUILayout.Label("说明：所有功能与大招均可直接通过上方菜单开关、滑条、数值输入框和按钮控制。", labelStyle);
                if (GUILayout.Button("🔄 立即重置所有数值与开关并恢复游戏原始状态", GUILayout.Height(30f)))
                {
                    ResetAllSettings();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
        }

        private void DrawFloatSetting(string key, string title, ref float val, float min, float max, string fmt)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title + ": " + val.ToString(fmt, CultureInfo.InvariantCulture), labelStyle, GUILayout.Width(240f));

            float sliderVal = GUILayout.HorizontalSlider(val, min, max, GUILayout.Width(220f));
            if (Mathf.Abs(sliderVal - val) > 0.0001f)
            {
                val = sliderVal;
                numericInputs[key] = val.ToString(fmt, CultureInfo.InvariantCulture);
            }

            string curText;
            if (!numericInputs.TryGetValue(key, out curText))
            {
                curText = val.ToString(fmt, CultureInfo.InvariantCulture);
                numericInputs[key] = curText;
            }

            string edited = GUILayout.TextField(curText, 10, GUILayout.Width(72f));
            if (edited != curText)
            {
                numericInputs[key] = edited;
                float parsed;
                if (float.TryParse(edited, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
                    !float.IsNaN(parsed) && !float.IsInfinity(parsed) && parsed >= min && parsed <= max)
                {
                    val = parsed;
                }
            }

            if (GUILayout.Button("-", GUILayout.Width(26f)))
            {
                float step = (max - min) * 0.05f;
                val = Mathf.Clamp(val - step, min, max);
                numericInputs[key] = val.ToString(fmt, CultureInfo.InvariantCulture);
            }
            if (GUILayout.Button("+", GUILayout.Width(26f)))
            {
                float step = (max - min) * 0.05f;
                val = Mathf.Clamp(val + step, min, max);
                numericInputs[key] = val.ToString(fmt, CultureInfo.InvariantCulture);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawFilledRect(Rect r, Color col)
        {
            EnsureMaterials();
            Color prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(r, whiteTex);
            GUI.color = prev;
        }

        private void DrawOutlineRect(Rect r, Color col, float t)
        {
            DrawFilledRect(new Rect(r.x, r.y, r.width, t), col);
            DrawFilledRect(new Rect(r.x, r.yMax - t, r.width, t), col);
            DrawFilledRect(new Rect(r.x, r.y, t, r.height), col);
            DrawFilledRect(new Rect(r.xMax - t, r.y, t, r.height), col);
        }

        private void DrawCornerBox(Rect r, Color col, float t)
        {
            float cw = Mathf.Max(4f, r.width * 0.25f);
            float ch = Mathf.Max(4f, r.height * 0.22f);

            DrawFilledRect(new Rect(r.x, r.y, cw, t), col);
            DrawFilledRect(new Rect(r.x, r.y, t, ch), col);

            DrawFilledRect(new Rect(r.xMax - cw, r.y, cw, t), col);
            DrawFilledRect(new Rect(r.xMax - t, r.y, t, ch), col);

            DrawFilledRect(new Rect(r.x, r.yMax - t, cw, t), col);
            DrawFilledRect(new Rect(r.x, r.yMax - ch, t, ch), col);

            DrawFilledRect(new Rect(r.xMax - cw, r.yMax - t, cw, t), col);
            DrawFilledRect(new Rect(r.xMax - t, r.yMax - ch, t, ch), col);
        }

        private void DrawCircle(Vector2 center, float radius, Color col, int segments)
        {
            float step = (Mathf.PI * 2f) / Mathf.Max(8, segments);
            for (int i = 0; i < segments; i++)
            {
                float a = i * step;
                float px = center.x + Mathf.Cos(a) * radius;
                float py = center.y + Mathf.Sin(a) * radius;
                DrawFilledRect(new Rect(px - 1f, py - 1f, 2f, 2f), col);
            }
        }
    }
}
