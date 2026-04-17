using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RavenCheat
{
    public class Loader
    {
        private static GameObject loadObject;
        public static void Init()
        {
            loadObject = new GameObject("RavenCheat");
            loadObject.AddComponent<CheatMain>();
            UnityEngine.Object.DontDestroyOnLoad(loadObject);
        }
    }

    public class CheatMain : MonoBehaviour
    {
        // ============ UI ============
        private bool showMenu = true;
        private Rect windowRect = new Rect(20, 20, 380, 580);
        private Vector2 scroll;

        // ============ 功能开关 ============
        // 瞄准类
        public bool bESP = true;
        public bool bChams = false;
        public bool bRadar = true;
        public bool bAutoAim = false;
        public bool bRageAim = false;
        public bool bAutoKill = false;
        public bool bMagicBullet = false;
        public bool bKillAll = false;
        public bool bFriendlyFire = false;

        // 武器类
        public bool bUnlimitedAmmo = true;
        public bool bNoRecoil = true;
        public bool bRapidFire = false;
        public bool bBigBullets = false;

        // 防御类
        public bool bGodMode = false;
        public bool bVehicleGodMode = false;

        // 移动类
        public bool bSpeedHack = false;
        public bool bFly = false;
        public bool bNoClip = false;
        public bool bJumpBoost = false;
        public bool bGiantMode = false;

        // 载具类
        public bool bUFOMode = false;

        // 世界类
        public bool bTimeFreeze = false;
        public bool bLowGravity = false;

        // 新增
        public bool bExplosiveAmmo = false;     // 高爆子弹
        public bool bVehicleBoost = false;      // 载具飞跃
        public bool bFreezeEnemies = false;     // 冻结敌人位置
        public bool bBlindAI = false;           // AI 失明
        public bool bMagnet = false;            // 磁力吸附敌人
        public bool bLaserGun = false;          // 镭射枪（按住左键秒杀准星敌人）
        public bool bAimNearest = true;         // AutoAim 模式：true=按距离，false=按角度
        public bool bAimAtHead = true;          // 瞄头还是瞄胸
        public bool bAutoHeal = false;          // 自动回血
        public bool bEspLines = false;          // ESP 从屏幕中心画线
        public bool bVehicleRainbow = false;    // 载具彩虹色（彩蛋）
        public bool bSuperReach = false;        // 超远传送距离
        public bool bInfiniteGrenades = false;  // 无限手雷
        public bool bExplodeOnDeath = false;    // 敌人死亡时自爆（神风连锁）

        // ============ 运行时状态 ============
        private Actor localPlayer;
        private Camera mainCam;
        private string statusText = "";
        private float killTimer = 0f;
        private float autoKillFireTimer = 0f;

        // 反射缓存
        private FieldInfo fpcYawField;
        private FieldInfo fpcPitchField;
        private FieldInfo fpcControllerField;
        private bool reflectionCached = false;

        private Texture2D whiteTex;
        private Vector3 origScale = Vector3.one;
        private bool origScaleCaptured = false;

        // 追踪被改过属性的载具以便恢复
        private readonly HashSet<Rigidbody> ufoTouchedVehicles = new HashSet<Rigidbody>();
        private float laserTimer = 0f;
        private float airstrikeTimer = 0f;

        // 用于冻结敌人功能关闭后恢复 AI 控制器
        private readonly HashSet<Behaviour> frozenAiControllers = new HashSet<Behaviour>();
        // 已知死亡的敌人（用于“死亡时自爆”只触发一次）
        private readonly HashSet<int> alreadyDetonated = new HashSet<int>();
        // Chams 缓存：已染色的 Renderer 就不再 new 材质了（避免 Unity 材质泄漏）
        private readonly HashSet<int> chamsTouched = new HashSet<int>();
        private readonly Dictionary<int, float> rainbowLastUpdate = new Dictionary<int, float>();

        private void Start()
        {
            whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Insert)) showMenu = !showMenu;

            mainCam = Camera.main;
            localPlayer = GetLocalPlayer();

            // 世界类（即便玩家死亡也保持）
            try { ApplyTimeScale(); } catch { }
            try { ApplyGravity(); } catch { }

            if (localPlayer == null || mainCam == null)
            {
                statusText = "等待玩家生成...";
                return;
            }

            if (!reflectionCached) CacheReflection();
            if (!origScaleCaptured && localPlayer.transform != null)
            {
                origScale = localPlayer.transform.localScale;
                origScaleCaptured = true;
            }

            int enemyCount = 0;
            if (ActorManager.instance != null && ActorManager.instance.actors != null)
            {
                foreach (var a in ActorManager.instance.actors)
                    if (a != null && a != localPlayer && !a.dead && (bFriendlyFire || a.team != localPlayer.team)) enemyCount++;
            }
            statusText = "玩家已接管 | 敌人: " + enemyCount + " | 血量: " + Mathf.RoundToInt(localPlayer.health);

            if (localPlayer.dead) return;

            try { ApplyGiantMode(); } catch { }
            try { if (bGodMode) GodModeLogic(); } catch { }
            try { if (bUnlimitedAmmo) UnlimitedAmmoLogic(); } catch { }
            try { if (bNoRecoil) NoRecoilLogic(); } catch { }
            try { if (bRapidFire) RapidFireLogic(); } catch { }
            try { if (bBigBullets) BigBulletsLogic(); } catch { }
            try { if (bVehicleGodMode) VehicleGodModeLogic(); } catch { }
            try { if (bUFOMode) UFOModeLogic(); } catch { }
            try { RestoreUFOVehiclesIfNeeded(); } catch { }
            try { if (bSpeedHack || bFly || bNoClip || bJumpBoost) MovementHackLogic(); } catch { }
            try { if (bExplosiveAmmo) ExplosiveAmmoLogic(); } catch { }
            try { if (bVehicleBoost) VehicleBoostLogic(); } catch { }
            try { SummonVehicleHotkey(); } catch { }
            try { if (bMagicBullet && !bKillAll) MagicBulletLogic(); } catch { }
            try { if (bKillAll) KillAllLogic(); } catch { }
            try { if (bAutoKill) AutoKillLogic(); } catch { }
            try { if (bChams) ChamsLogic(); } catch { }
            try { if (bFreezeEnemies) FreezeEnemiesLogic(); } catch { }
            try { if (bBlindAI) BlindAILogic(); } catch { }
            try { if (bMagnet) MagnetLogic(); } catch { }
            try { if (bLaserGun) LaserGunLogic(); } catch { }
            try { if (bAutoHeal) AutoHealLogic(); } catch { }
            try { if (bVehicleRainbow) VehicleRainbowLogic(); } catch { }
            try { if (bInfiniteGrenades) InfiniteGrenadesLogic(); } catch { }
            try { if (bExplodeOnDeath) ExplodeOnDeathLogic(); } catch { }
            try { RestoreFrozenAiIfNeeded(); } catch { }
            try { TeleportLogic(); } catch { }
            try { ThorHammerLogic(); } catch { }
            try { AirstrikeLogic(); } catch { }
            try { ExplodeAllVehiclesHotkey(); } catch { }
            try { GrappleHookLogic(); } catch { }
            try { ForcePushLogic(); } catch { }
            try { TornadoHotkey(); } catch { }
            try { RecruitLogic(); } catch { }
            try { DeathRayLogic(); } catch { }
        }

        private void LateUpdate()
        {
            if ((bAutoAim || bRageAim) && localPlayer != null && !localPlayer.dead && mainCam != null)
            {
                try { AutoAimLogic(); } catch { }
            }
        }

        private Actor GetLocalPlayer()
        {
            if (ActorManager.instance != null && ActorManager.instance.player != null)
                return ActorManager.instance.player;
            if (FpsActorController.instance != null)
                return FpsActorController.instance.actor;
            return null;
        }

        private void CacheReflection()
        {
            if (FpsActorController.instance == null) return;
            var fpc = FpsActorController.instance;
            var fpcType = fpc.GetType();
            fpcControllerField = fpcType.GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            var controller = fpcControllerField != null ? fpcControllerField.GetValue(fpc) : null;
            if (controller != null)
            {
                var cType = controller.GetType();
                foreach (var f in cType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (f.FieldType != typeof(float)) continue;
                    string ln = f.Name.ToLower();
                    if (fpcYawField == null && (ln == "yaw" || ln.Contains("yaw"))) fpcYawField = f;
                    if (fpcPitchField == null && (ln == "pitch" || ln.Contains("pitch"))) fpcPitchField = f;
                }
            }
            reflectionCached = true;
        }

        // ============ GUI ============
        private void OnGUI()
        {
            if (showMenu) windowRect = GUI.Window(0, windowRect, CheatWindow, "Ravenfield 作弊菜单 [Insert 开关]");
            if (bESP && localPlayer != null && mainCam != null) DrawESP();
            if (bRadar && localPlayer != null && mainCam != null) DrawRadar();
        }

        private void CheatWindow(int id)
        {
            GUILayout.Space(4);
            GUILayout.Label(statusText);
            GUILayout.Space(4);
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("── 视觉辅助 ──");
            bESP = GUILayout.Toggle(bESP, "ESP 透视 (方框+距离)");
            bChams = GUILayout.Toggle(bChams, "敌人染色 (Chams 高亮)");
            bRadar = GUILayout.Toggle(bRadar, "雷达 (右上小地图)");

            GUILayout.Label("── 瞄准作弊 ──");
            bAutoAim = GUILayout.Toggle(bAutoAim, "自动瞄准 [按住右键]");
            bRageAim = GUILayout.Toggle(bRageAim, "狂暴锁头 (永远锁定最近敌人)");
            bAimNearest = GUILayout.Toggle(bAimNearest, "锁最近的敌人 (否则锁视线最近角度)");
            bAimAtHead = GUILayout.Toggle(bAimAtHead, "瞄头 (关掉则瞄胸)");
            bAutoKill = GUILayout.Toggle(bAutoKill, "自动开火 (自动对敌人射击)");
            bLaserGun = GUILayout.Toggle(bLaserGun, "镭射枪 [按住左键秒杀准星敌人]");
            bMagicBullet = GUILayout.Toggle(bMagicBullet, "魔法子弹 (子弹自动追踪)");
            bKillAll = GUILayout.Toggle(bKillAll, "一键全屠 (瞬移子弹爆头全图)");
            bFriendlyFire = GUILayout.Toggle(bFriendlyFire, "友军伤害 (含队友为目标)");

            GUILayout.Label("── 武器强化 ──");
            bUnlimitedAmmo = GUILayout.Toggle(bUnlimitedAmmo, "无限弹药 + 无需换弹");
            bNoRecoil = GUILayout.Toggle(bNoRecoil, "无后坐力 / 无散布");
            bRapidFire = GUILayout.Toggle(bRapidFire, "极速射击 (无冷却)");
            bBigBullets = GUILayout.Toggle(bBigBullets, "巨型子弹 (子弹变大)");
            bExplosiveAmmo = GUILayout.Toggle(bExplosiveAmmo, "高爆弹药 (左键指哪炸哪)");

            GUILayout.Label("── 防御 ──");
            bGodMode = GUILayout.Toggle(bGodMode, "无敌模式 (血量无限)");
            bAutoHeal = GUILayout.Toggle(bAutoHeal, "自动回血");
            bVehicleGodMode = GUILayout.Toggle(bVehicleGodMode, "载具无敌");

            GUILayout.Label("── 移动 ──");
            bSpeedHack = GUILayout.Toggle(bSpeedHack, "加速奔跑 [按住 Shift]");
            bFly = GUILayout.Toggle(bFly, "空中飞行 [按住空格]");
            bNoClip = GUILayout.Toggle(bNoClip, "穿墙模式 (关闭碰撞)");
            bJumpBoost = GUILayout.Toggle(bJumpBoost, "超级跳跃 [按空格]");
            bGiantMode = GUILayout.Toggle(bGiantMode, "巨人模式 (体积变大)");

            GUILayout.Label("── 载具 ──");
            bUFOMode = GUILayout.Toggle(bUFOMode, "UFO 载具 (鼠标控制方向)");
            bVehicleRainbow = GUILayout.Toggle(bVehicleRainbow, "载具彩虹色 (调教用)");
            bVehicleBoost = GUILayout.Toggle(bVehicleBoost, "载具飞跃 (Shift加速, 空格跳跃)");

            GUILayout.Label("── 控制敌人 ──");
            if (GUILayout.Button("🔥 触发全民暴乱 (全图大逃杀)")) TriggerRiotMode();
            bFreezeEnemies = GUILayout.Toggle(bFreezeEnemies, "冻结敌人 (敌人停止移动)");
            bBlindAI = GUILayout.Toggle(bBlindAI, "AI 失明 (敌人看不到你)");
            bMagnet = GUILayout.Toggle(bMagnet, "磁力吸附 (吸敌人过来)");
            bExplodeOnDeath = GUILayout.Toggle(bExplodeOnDeath, "神风连锁 (敌人死时自爆)");
            bInfiniteGrenades = GUILayout.Toggle(bInfiniteGrenades, "无限手雷");

            GUILayout.Label("── 世界 ──");
            bTimeFreeze = GUILayout.Toggle(bTimeFreeze, "时间减速 (敌人慢动作)");
            bLowGravity = GUILayout.Toggle(bLowGravity, "低重力模式");

            GUILayout.EndScrollView();

            GUILayout.Space(4);
            GUILayout.Label("── 快捷键 ──");
            GUILayout.Label("  T = 传送到准星位置");
            GUILayout.Label("  V = 隔空取车 (最近载具传送到面前)");
            GUILayout.Label("  G = 索尔之锤 (准星处爆炸)");
            GUILayout.Label("  B = 空袭轰炸 (准星处大范围杀敌)");
            GUILayout.Label("  H = 一键爆掉所有敌方载具");
            GUILayout.Label("  F = 🪝 钩爪飞爪 (看敌人拉过来、看空地飞过去、看车把车抽过来)");
            GUILayout.Label("  R = 💨 原力冲击波 (所有载具和敌人被炸飞)");
            GUILayout.Label("  N = 🌪 召唤龙卷风 (绝望射程外的绝对咲咴咵!)");
            GUILayout.Label("  J = 🔸 AI 招廓 (把敌人叛变成队友)");
            GUILayout.Label("  M + 左键 = ☠ 死亡射线 (路径上一切爆炸)");
            GUILayout.Label("  Insert = 开关此菜单");
            GUI.DragWindow();
        }

        // ============ ESP ============
        private void DrawESP()
        {
            if (ActorManager.instance == null || ActorManager.instance.actors == null) return;
            Color oldColor = GUI.color;
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                bool isEnemy = a.team != localPlayer.team;
                if (!isEnemy && !bFriendlyFire) continue;

                Vector3 center = GetActorCenter(a);
                Vector3 scr = mainCam.WorldToScreenPoint(center);
                if (scr.z <= 0f) continue;

                float dist = Vector3.Distance(mainCam.transform.position, center);
                GUI.color = isEnemy ? Color.red : Color.green;

                float boxSize = Mathf.Clamp(800f / Mathf.Max(dist, 1f), 4f, 60f);
                float x = scr.x - boxSize * 0.5f;
                float y = Screen.height - scr.y - boxSize * 0.5f;
                DrawRectBorder(new Rect(x, y, boxSize, boxSize), 1.5f, GUI.color);

                string label = "[" + Mathf.RoundToInt(dist) + "米] 血:" + Mathf.RoundToInt(a.health);
                GUI.Label(new Rect(scr.x - 40, Screen.height - scr.y + boxSize * 0.5f + 2, 140, 18), label);
            }
            GUI.color = oldColor;
        }

        // ============ 雷达 ============
        private void DrawRadar()
        {
            if (ActorManager.instance == null || ActorManager.instance.actors == null) return;
            float radarSize = 160f;
            float range = 150f;
            Rect radarRect = new Rect(Screen.width - radarSize - 20, 20, radarSize, radarSize);

            // 背景
            Color oldColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(radarRect, whiteTex);
            GUI.color = Color.white;
            DrawRectBorder(radarRect, 1.5f, Color.white);

            // 中心十字（玩家位置）
            float cx = radarRect.x + radarSize / 2;
            float cy = radarRect.y + radarSize / 2;
            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(cx - 1, cy - 4, 2, 8), whiteTex);
            GUI.DrawTexture(new Rect(cx - 4, cy - 1, 8, 2), whiteTex);

            Vector3 playerPos = localPlayer.transform.position;
            float camYaw = mainCam.transform.eulerAngles.y * Mathf.Deg2Rad;
            float cosY = Mathf.Cos(camYaw);
            float sinY = Mathf.Sin(camYaw);

            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                bool isEnemy = a.team != localPlayer.team;
                if (!isEnemy && !bFriendlyFire) continue;

                Vector3 delta = a.transform.position - playerPos;
                // 旋转到相机视角坐标系
                float rx = delta.x * cosY - delta.z * sinY;
                float rz = delta.x * sinY + delta.z * cosY;

                float dist = new Vector2(rx, rz).magnitude;
                if (dist > range) continue;

                float px = cx + (rx / range) * (radarSize / 2 - 4);
                float py = cy - (rz / range) * (radarSize / 2 - 4);

                GUI.color = isEnemy ? Color.red : Color.green;
                GUI.DrawTexture(new Rect(px - 2, py - 2, 4, 4), whiteTex);
            }
            GUI.color = oldColor;
        }

        private void DrawRectBorder(Rect r, float thickness, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), whiteTex);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), whiteTex);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), whiteTex);
            GUI.DrawTexture(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), whiteTex);
            GUI.color = old;
        }

        private Vector3 GetActorCenter(Actor a)
        {
            try { return a.CenterPosition(); } catch { }
            return a.transform.position + new Vector3(0, 1.0f, 0);
        }

        private Vector3 GetActorHead(Actor a)
        {
            if (a.animatedBones != null && a.animatedBones.Length > 1 && a.animatedBones[1] != null)
                return a.animatedBones[1].position;
            return a.transform.position + new Vector3(0, 1.7f, 0);
        }

        // ============ Chams 染色 ============
        private void ChamsLogic()
        {
            if (ActorManager.instance == null || ActorManager.instance.actors == null) return;
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                int id = a.GetInstanceID();
                // 修复：Unity 的 renderer.material 每帧 new 一个新材质会造成内存泄漏
                // 用 Actor InstanceID 标记已染色 → 只染一次，避免泏泊造材质
                if (chamsTouched.Contains(id)) continue;
                bool isEnemy = a.team != localPlayer.team;
                var renderers = a.GetComponentsInChildren<Renderer>();
                Color tint = isEnemy ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(0.2f, 1f, 0.2f, 1f);
                foreach (var r in renderers)
                {
                    if (r == null || r.material == null) continue;
                    try { r.material.color = tint; } catch { }
                }
                chamsTouched.Add(id);
            }
        }

        // ============ 自动瞄准 / 狂暴锁头 ============
        private void AutoAimLogic()
        {
            if (bAutoAim && !bRageAim && !Input.GetMouseButton(1)) return;
            if (ActorManager.instance == null || ActorManager.instance.actors == null) return;

            Actor best = null;
            float bestScore = float.MaxValue;
            Vector3 camPos = mainCam.transform.position;
            Vector3 camFwd = mainCam.transform.forward;
            float fovLimit = bRageAim ? 180f : 60f;

            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;

                Vector3 head = bAimAtHead ? GetActorHead(a) : GetActorCenter(a);
                Vector3 dir = head - camPos;
                float dist = dir.magnitude;
                if (dist < 0.1f) continue;
                float angle = Vector3.Angle(camFwd, dir);
                if (angle > fovLimit) continue;

                // 默认按距离，即使 bRageAim 也先按距离排序
                float score = bAimNearest ? dist : angle;
                if (score < bestScore) { bestScore = score; best = a; }
            }

            if (best == null) return;

            Vector3 targetPos = bAimAtHead ? GetActorHead(best) : GetActorCenter(best);
            Vector3 aimDir = (targetPos - camPos).normalized;
            Quaternion rot = Quaternion.LookRotation(aimDir);

            mainCam.transform.rotation = rot;
            if (FpsActorController.instance != null)
            {
                var fpc = FpsActorController.instance;
                var fpcT = fpc.GetType();
                fpc.transform.rotation = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);
                var fpCamField = fpcT.GetField("fpCamera", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fpCamField != null) { var fpCam = fpCamField.GetValue(fpc) as Camera; if (fpCam != null) fpCam.transform.rotation = rot; }
                var fpCamRootField = fpcT.GetField("fpCameraRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fpCamRootField != null) { var rootT = fpCamRootField.GetValue(fpc) as Transform; if (rootT != null) rootT.rotation = rot; }

                var controller = fpcControllerField != null ? fpcControllerField.GetValue(FpsActorController.instance) : null;
                if (controller != null)
                {
                    float yawAng = rot.eulerAngles.y;
                    float pitchAng = rot.eulerAngles.x;
                    if (pitchAng > 180f) pitchAng -= 360f;
                    if (fpcYawField != null) fpcYawField.SetValue(controller, yawAng);
                    if (fpcPitchField != null) fpcPitchField.SetValue(controller, -pitchAng);
                }
            }
        }

        // ============ 自动开火 ============
        private void AutoKillLogic()
        {
            var w = localPlayer.activeWeapon;
            if (w == null) return;
            autoKillFireTimer += Time.deltaTime;
            if (autoKillFireTimer < 0.05f) return;
            autoKillFireTimer = 0f;

            // 寻找任意存活敌人
            Actor target = null;
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;
                target = a; break;
            }
            if (target == null) return;

            // 直接调用武器的开火相关方法
            var t = w.GetType();
            SetField(w, t, "lastFiredTimestamp", 0f);
            try { var m1 = t.GetMethod("FireNow", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (m1 != null) m1.Invoke(w, null); } catch { }
            try { var m2 = t.GetMethod("PressTrigger", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (m2 != null) m2.Invoke(w, null); } catch { }
            try { var m3 = t.GetMethod("ContinuousFire", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (m3 != null) m3.Invoke(w, new object[] { true }); } catch { }
        }

        // ============ 无敌 ============
        private void GodModeLogic()
        {
            if (localPlayer.health < localPlayer.maxHealth) localPlayer.health = localPlayer.maxHealth;
        }

        // ============ 无限弹药 ============
        private void UnlimitedAmmoLogic()
        {
            var w = localPlayer.activeWeapon;
            if (w == null) return;
            var t = w.GetType();
            SetField(w, t, "ammo", 999);
            SetField(w, t, "spareAmmo", 999);
            SetField(w, t, "reloading", false);
            SetField(w, t, "heat", 0f);
            SetField(w, t, "isOverheating", false);
        }

        // ============ 无后坐力 ============
        private void NoRecoilLogic()
        {
            var w = localPlayer.activeWeapon;
            if (w == null) return;
            var t = w.GetType();
            var configField = t.GetField("configuration", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (configField == null) return;
            object cfg = configField.GetValue(w);
            if (cfg == null) return;
            var cfgType = cfg.GetType();
            SetField(cfg, cfgType, "spread", 0f);
            SetField(cfg, cfgType, "kickback", 0f);
            SetField(cfg, cfgType, "randomKick", 0f);
            SetField(cfg, cfgType, "followupSpreadGain", 0f);
            SetField(cfg, cfgType, "followupMaxSpreadHip", 0f);
            SetField(cfg, cfgType, "followupMaxSpreadAim", 0f);
            SetField(cfg, cfgType, "snapMagnitude", 0f);
            SetField(cfg, cfgType, "rattleMagnitude", 0f);
            configField.SetValue(w, cfg);
            SetField(w, t, "followupSpreadMagnitude", 0f);
        }

        // ============ 极速射击 ============
        private void RapidFireLogic()
        {
            var w = localPlayer.activeWeapon;
            if (w != null) StripCooldown(w);
            if (localPlayer.seat != null)
            {
                var seatType = localPlayer.seat.GetType();
                var weaponsField = seatType.GetField("weapons", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (weaponsField != null)
                {
                    var weapons = weaponsField.GetValue(localPlayer.seat) as IEnumerable;
                    if (weapons != null)
                        foreach (var wp in weapons) if (wp != null) StripCooldown(wp);
                }
                var activeField = seatType.GetField("activeWeapon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (activeField != null)
                {
                    var wp = activeField.GetValue(localPlayer.seat);
                    if (wp != null) StripCooldown(wp);
                }
            }
        }

        private void StripCooldown(object weapon)
        {
            var t = weapon.GetType();
            var configField = t.GetField("configuration", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (configField != null)
            {
                object cfg = configField.GetValue(weapon);
                if (cfg != null)
                {
                    var cfgType = cfg.GetType();
                    SetField(cfg, cfgType, "auto", true);
                    SetField(cfg, cfgType, "cooldown", 0.01f);
                    SetField(cfg, cfgType, "chargeTime", 0f);
                    SetField(cfg, cfgType, "useChargeTime", false);
                    SetField(cfg, cfgType, "reloadTime", 0.01f);
                    SetField(cfg, cfgType, "unholsterTime", 0f);
                    configField.SetValue(weapon, cfg);
                }
            }
            SetField(weapon, t, "lastFiredTimestamp", 0f);
        }

        // ============ 巨型子弹 ============
        private void BigBulletsLogic()
        {
            var projs = UnityEngine.Object.FindObjectsOfType<Projectile>();
            foreach (var p in projs)
            {
                if (p == null) continue;
                // 修复：只放大自己阵营的子弹，不然敌人子弹也变大会擞死自己
                int ownerTeam = -1;
                try
                {
                    var tf = p.GetType().GetField("ownerTeam", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (tf != null) ownerTeam = (int)tf.GetValue(p);
                } catch { }
                if (ownerTeam != -1 && ownerTeam != localPlayer.team) continue;

                if (p.transform.localScale.x < 5f)
                    p.transform.localScale = Vector3.one * 5f;
            }
        }

        // ============ 载具无敌 ============
        private void VehicleGodModeLogic()
        {
            if (localPlayer.seat == null || localPlayer.seat.vehicle == null) return;
            var v = localPlayer.seat.vehicle;
            v.health = v.maxHealth;
            var vt = v.GetType();
            SetField(v, vt, "dead", false);
            SetField(v, vt, "burning", false);
            SetField(v, vt, "burningDamageSource", null);
            SetField(v, vt, "isInvulnerable", true);
        }

        // ============ UFO 载具 ============
        private void UFOModeLogic()
        {
            if (localPlayer.seat == null || localPlayer.seat.vehicle == null) return;
            var v = localPlayer.seat.vehicle;
            Rigidbody rb = v.GetComponent<Rigidbody>();
            if (rb == null) return;
            rb.useGravity = false;
            rb.angularVelocity = Vector3.zero;
            ufoTouchedVehicles.Add(rb); // 记录以便下车恢复

            // 让载具完全跟随相机方向（含俯仰角）
            Vector3 camFwd = mainCam.transform.forward;
            Vector3 camRight = mainCam.transform.right;
            Vector3 camUp = mainCam.transform.up;

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += camFwd;
            if (Input.GetKey(KeyCode.S)) move -= camFwd;
            if (Input.GetKey(KeyCode.A)) move -= camRight;
            if (Input.GetKey(KeyCode.D)) move += camRight;
            if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) move -= Vector3.up;

            float speed = Input.GetKey(KeyCode.LeftShift) ? 120f : 50f;
            if (move.sqrMagnitude > 0.001f)
                rb.velocity = move.normalized * speed;
            else
                rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, 0.3f);

            // 车身朝向 = 相机朝向（含俯仰）
            Quaternion targetRot = Quaternion.LookRotation(camFwd, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, 0.2f));
        }

        // ============ 移动外挂（修复版） ============
        private void MovementHackLogic()
        {
            if (localPlayer.seat != null) return;

            // 获取 CharacterController（Ravenfield 玩家主要用这个控制移动）
            CharacterController cc = localPlayer.GetComponentInChildren<CharacterController>();
            if (cc == null && FpsActorController.instance != null)
                cc = FpsActorController.instance.GetComponentInChildren<CharacterController>();

            Rigidbody rb = null;
            try { rb = localPlayer.rigidbody; } catch { }
            if (rb == null) rb = localPlayer.GetComponent<Rigidbody>();

            // NoClip：关闭碰撞（穿墙）
            if (bNoClip)
            {
                if (cc != null && cc.enabled) cc.enabled = false;
                // 冻结 Rigidbody 的外力（避免被物理拖动/卡住）
                if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

                Vector3 fwd = mainCam.transform.forward;
                Vector3 right = mainCam.transform.right;
                Vector3 move = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) move += fwd;
                if (Input.GetKey(KeyCode.S)) move -= fwd;
                if (Input.GetKey(KeyCode.A)) move -= right;
                if (Input.GetKey(KeyCode.D)) move += right;
                if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
                if (Input.GetKey(KeyCode.LeftControl)) move -= Vector3.up;

                if (move.sqrMagnitude > 0.001f)
                {
                    float sp = Input.GetKey(KeyCode.LeftShift) ? 40f : 15f;
                    localPlayer.transform.position += move.normalized * sp * Time.deltaTime;
                }
            }
            else
            {
                if (cc != null && !cc.enabled)
                {
                    // 退出 NoClip：抬高一下玩家避免卡在地板/墙里
                    localPlayer.transform.position += Vector3.up * 1.5f;
                    cc.enabled = true;
                }
            }

            // SpeedHack：直接用 CharacterController.Move 实现（避开 FPS 控制器的速度限制）
            if (bSpeedHack && Input.GetKey(KeyCode.LeftShift) && !bNoClip)
            {
                Vector3 fwd = mainCam.transform.forward; fwd.y = 0; fwd.Normalize();
                Vector3 right = mainCam.transform.right; right.y = 0; right.Normalize();
                Vector3 move = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) move += fwd;
                if (Input.GetKey(KeyCode.S)) move -= fwd;
                if (Input.GetKey(KeyCode.A)) move -= right;
                if (Input.GetKey(KeyCode.D)) move += right;
                if (move.sqrMagnitude > 0.001f)
                {
                    float boost = 30f;
                    Vector3 delta = move.normalized * boost * Time.deltaTime;
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

            // Fly：按空格往上飞
            if (bFly && Input.GetKey(KeyCode.Space) && !bNoClip)
            {
                Vector3 up = Vector3.up * 20f * Time.deltaTime;
                if (cc != null && cc.enabled) cc.Move(up);
                else localPlayer.transform.position += up;
            }

            // JumpBoost：按空格超级跳
            if (bJumpBoost && Input.GetKeyDown(KeyCode.Space))
            {
                if (rb != null) rb.velocity = new Vector3(rb.velocity.x, 25f, rb.velocity.z);
                else localPlayer.transform.position += Vector3.up * 3f;
            }
        }

        // ============ 巨人模式 ============
        private void ApplyGiantMode()
        {
            if (localPlayer.transform == null) return;
            if (bGiantMode)
            {
                Vector3 target = origScale * 3f;
                if (Vector3.Distance(localPlayer.transform.localScale, target) > 0.01f)
                    localPlayer.transform.localScale = target;
            }
            else
            {
                if (origScaleCaptured && Vector3.Distance(localPlayer.transform.localScale, origScale) > 0.01f)
                    localPlayer.transform.localScale = origScale;
            }
        }

        // ============ 魔法子弹 ============
        private void MagicBulletLogic()
        {
            if (ActorManager.instance == null || ActorManager.instance.actors == null) return;
            var projs = UnityEngine.Object.FindObjectsOfType<Projectile>();
            foreach (var p in projs)
            {
                if (p == null) continue;
                int ownerTeam = -1;
                try
                {
                    var tf = p.GetType().GetField("ownerTeam", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (tf != null) ownerTeam = (int)tf.GetValue(p);
                } catch { }
                if (ownerTeam != localPlayer.team && ownerTeam != -1) continue;

                var velField = p.GetType().GetField("velocity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (velField == null) continue;
                Vector3 vel = (Vector3)velField.GetValue(p);
                if (vel.magnitude < 5f) continue;

                Actor target = FindClosestEnemy(p.transform.position);
                if (target == null) continue;

                Vector3 aim = GetActorCenter(target);
                Vector3 dir = (aim - p.transform.position).normalized;
                velField.SetValue(p, dir * Mathf.Max(vel.magnitude, 400f));
            }
        }

        // ============ 一键全屠 ============
        private void KillAllLogic()
        {
            if (ActorManager.instance == null || ActorManager.instance.actors == null) return;
            killTimer += Time.deltaTime;
            if (killTimer < 0.08f) return;
            killTimer = 0f;

            var projs = UnityEngine.Object.FindObjectsOfType<Projectile>();
            if (projs.Length == 0) return;

            int pi = 0;
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;
                if (pi >= projs.Length) break;

                var p = projs[pi++];
                if (p == null) continue;
                Vector3 head = GetActorHead(a);
                Vector3 dir = (head - p.transform.position).normalized;
                p.transform.position = head - dir * 0.3f;
                var velField = p.GetType().GetField("velocity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (velField != null) velField.SetValue(p, dir * 800f);
            }
        }

        // ============ 传送 ============
        private void TeleportLogic()
        {
            if (!Input.GetKeyDown(KeyCode.T)) return;
            if (mainCam == null) return;
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            int mask = ~((1 << 9) | (1 << 18));
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 5000f, mask))
            {
                Vector3 dest = hit.point + Vector3.up * 1.2f;

                CharacterController cc = localPlayer.GetComponentInChildren<CharacterController>();
                if (cc != null) cc.enabled = false;
                try { localPlayer.SetPositionAndRotation(dest, localPlayer.transform.rotation); }
                catch { localPlayer.transform.position = dest; }
                if (cc != null) cc.enabled = true;

                Rigidbody rb = null;
                try { rb = localPlayer.rigidbody; } catch { }
                if (rb != null) rb.velocity = Vector3.zero;
            }
        }

        // ============ 全民大逃杀 (真·修复版) ============
        // Bug 原因：游戏里 AI 的敌友判定并不直接看 actor.team，而是看 AiActorController.squad
        // 只改 team 字段 → AI 的 squad 没变，它们立刻回到原来的阵营继续打玩家
        // 真正生效的方法：
        //   1) 把全部 AI 的 squad 字段设为 null（断开队伍归属）
        //   2) 直接用反射把每个 AI 的 targetActor 强行设成另一个随机 AI
        //   3) 把 team 打乱（视觉上红蓝分明）
        //   4) 打开 bFriendlyFire 让所有人的子弹都能互相命中
        private void TriggerRiotMode()
        {
            if (ActorManager.instance == null || ActorManager.instance.actors == null) return;
            var asm = typeof(Actor).Assembly;
            var aiType = asm.GetType("AiActorController");

            // 收集所有存活 AI（不含玩家）
            var alive = new List<Actor>();
            foreach (var a in ActorManager.instance.actors)
                if (a != null && a != localPlayer && !a.dead) alive.Add(a);
            if (alive.Count < 2) return;

            // 打开友军伤害，不然互相打不到对方
            bFriendlyFire = true;

            foreach (var a in alive)
            {
                try { a.team = UnityEngine.Random.Range(0, 2); } catch { }

                if (aiType == null) continue;
                var aiComp = a.GetComponent(aiType);
                if (aiComp == null) continue;
                var t = aiComp.GetType();

                // 清空 squad → 这是让 AI 脱离原队伍的关键
                SetField(aiComp, t, "squad", null);
                SetField(aiComp, t, "cover", null);
                SetField(aiComp, t, "coverTarget", null);

                // 找一个随机的其他 AI 作为攻击目标
                Actor enemy;
                int tries = 0;
                do
                {
                    enemy = alive[UnityEngine.Random.Range(0, alive.Count)];
                    tries++;
                } while (enemy == a && tries < 10);
                if (enemy == a) continue;

                // 强行指定目标
                SetField(aiComp, t, "targetActor", enemy);
                SetField(aiComp, t, "hasTargetVisible", true);
                SetField(aiComp, t, "hasSeenTarget", true);
                SetField(aiComp, t, "lastKnownTargetPosition", enemy.transform.position);

                // 有的 AI 存在私有 attackTarget / engageTarget 字段，一起填
                SetField(aiComp, t, "attackTarget", enemy);
                SetField(aiComp, t, "engageTarget", enemy);
            }
        }

        // ============ 钩爪 / 飞爪 (F键) - Just Cause 风 ============
        // 看着空地：把自己拉过去并获得冲量飞行
        // 看着敌人/载具：把目标拉过来砸在你面前
        private float grappleCooldown = 0f;
        private void GrappleHookLogic()
        {
            grappleCooldown -= Time.deltaTime;
            if (!Input.GetKeyDown(KeyCode.F) || grappleCooldown > 0f) return;
            if (mainCam == null) return;

            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 500f)) return;
            grappleCooldown = 0.25f;

            // 1) 是否命中了载具 → 把车砸过来
            Vehicle hitVehicle = hit.collider.GetComponentInParent<Vehicle>();
            if (hitVehicle != null && (localPlayer.seat == null || hitVehicle != localPlayer.seat.vehicle))
            {
                Rigidbody vrb = hitVehicle.GetComponent<Rigidbody>();
                if (vrb != null)
                {
                    Vector3 playerFront = localPlayer.transform.position + mainCam.transform.forward * 4f + Vector3.up * 2f;
                    Vector3 toMe = (playerFront - hitVehicle.transform.position);
                    vrb.velocity = toMe.normalized * 60f + Vector3.up * 10f; // 飞过来
                    return;
                }
            }

            // 2) 是否命中了敌人 → 抓过来扔地上
            Actor hitActor = hit.collider.GetComponentInParent<Actor>();
            if (hitActor != null && hitActor != localPlayer && !hitActor.dead)
            {
                Rigidbody arb = null;
                try { arb = hitActor.rigidbody; } catch { }
                Vector3 front = localPlayer.transform.position + mainCam.transform.forward * 3f + Vector3.up * 1f;
                if (arb != null)
                {
                    arb.isKinematic = false;
                    arb.velocity = (front - hitActor.transform.position).normalized * 50f + Vector3.up * 5f;
                }
                else
                {
                    hitActor.transform.position = front;
                }
                // 让目标瘫倒
                try
                {
                    var m = hitActor.GetType().GetMethod("Ragdoll", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    if (m != null) m.Invoke(hitActor, null);
                } catch { }
                return;
            }

            // 3) 空地 → 自己飞过去（不是瞬移！带抛物线冲量飞行）
            Rigidbody rb = null;
            try { rb = localPlayer.rigidbody; } catch { }
            Vector3 dir = (hit.point + Vector3.up * 2f) - localPlayer.transform.position;
            float dist = dir.magnitude;
            if (dist < 0.1f) return;
            // 用抛物线公式算初速度，让你真的飞过去
            Vector3 flat = new Vector3(dir.x, 0, dir.z);
            float flatDist = flat.magnitude;
            float yDiff = dir.y;
            float t = Mathf.Clamp(dist / 30f, 0.4f, 1.2f); // 飞行时间
            float g = Mathf.Abs(Physics.gravity.y);
            Vector3 launch = flat.normalized * (flatDist / t) + Vector3.up * (yDiff / t + 0.5f * g * t);

            CharacterController cc = localPlayer.GetComponentInChildren<CharacterController>();
            if (cc != null) cc.enabled = false;
            if (rb != null) { rb.isKinematic = false; rb.velocity = launch; }
            else localPlayer.transform.position += launch.normalized * 2f;
            // 飞行中不让玩家落地卡住 → 0.3 秒后恢复 CharacterController
            StartCoroutine(ReenableCC(cc, 0.3f));
        }

        private IEnumerator ReenableCC(CharacterController cc, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (cc != null) cc.enabled = true;
        }

        // ============ 原力冲击波 (R键) ============
        private float forceCooldown = 0f;
        private void ForcePushLogic()
        {
            forceCooldown -= Time.deltaTime;
            if (!Input.GetKeyDown(KeyCode.R) || forceCooldown > 0f) return;
            if (ActorManager.instance == null || ActorManager.instance.actors == null) return;
            forceCooldown = 1f;

            Vector3 center = localPlayer.transform.position;
            // 炸飞所有人
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;
                Vector3 dir = a.transform.position - center;
                float d = dir.magnitude;
                if (d > 30f || d < 0.1f) continue;

                Vector3 push = dir.normalized * 40f + Vector3.up * 15f;
                Rigidbody arb = null;
                try { arb = a.rigidbody; } catch { }
                if (arb != null) { arb.isKinematic = false; arb.velocity = push; }
            }
            // 炸飞所有载具（这才是真爽）
            var vehicles = UnityEngine.Object.FindObjectsOfType<Vehicle>();
            foreach (var v in vehicles)
            {
                if (v == null) continue;
                if (localPlayer.seat != null && v == localPlayer.seat.vehicle) continue;
                Vector3 dir = v.transform.position - center;
                float d = dir.magnitude;
                if (d > 35f || d < 0.1f) continue;
                Rigidbody vrb = v.GetComponent<Rigidbody>();
                if (vrb != null) vrb.velocity = dir.normalized * 50f + Vector3.up * 20f;
            }
        }

        // ============ 龙卷风 (按 N 键召唤) ============
        // 在准星位置生成龙卷风，持续 6 秒吸引敌人和载具螺旋上升，结束后爆炸
        private readonly List<TornadoData> activeTornados = new List<TornadoData>();
        private class TornadoData { public Vector3 pos; public float timeLeft; }

        private void TornadoHotkey()
        {
            if (Input.GetKeyDown(KeyCode.N) && mainCam != null)
            {
                Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 5000f))
                {
                    activeTornados.Add(new TornadoData { pos = hit.point, timeLeft = 6f });
                }
            }

            if (activeTornados.Count == 0) return;
            for (int i = activeTornados.Count - 1; i >= 0; i--)
            {
                var t = activeTornados[i];
                t.timeLeft -= Time.deltaTime;

                float radius = 35f;
                // 吸敌人
                foreach (var a in ActorManager.instance.actors)
                {
                    if (a == null || a == localPlayer || a.dead) continue;
                    Vector3 rel = t.pos - a.transform.position;
                    rel.y = 0f;
                    float d = rel.magnitude;
                    if (d > radius || d < 0.5f) continue;

                    // 切向速度 (旋转) + 向中心 + 往上
                    Vector3 tangent = Vector3.Cross(Vector3.up, rel.normalized);
                    Vector3 vel = tangent * 25f + rel.normalized * 15f + Vector3.up * 12f;
                    Rigidbody arb = null;
                    try { arb = a.rigidbody; } catch { }
                    if (arb != null) { arb.isKinematic = false; arb.velocity = vel; }
                }
                // 吸载具
                var vehs = UnityEngine.Object.FindObjectsOfType<Vehicle>();
                foreach (var v in vehs)
                {
                    if (v == null) continue;
                    if (localPlayer.seat != null && v == localPlayer.seat.vehicle) continue;
                    Vector3 rel = t.pos - v.transform.position;
                    rel.y = 0f;
                    float d = rel.magnitude;
                    if (d > radius || d < 0.5f) continue;
                    Vector3 tangent = Vector3.Cross(Vector3.up, rel.normalized);
                    Vector3 vel = tangent * 30f + rel.normalized * 10f + Vector3.up * 14f;
                    Rigidbody vrb = v.GetComponent<Rigidbody>();
                    if (vrb != null) { vrb.useGravity = true; vrb.velocity = vel; }
                }

                // 时间到：原地大爆炸
                if (t.timeLeft <= 0f)
                {
                    try { ExplodeAt(t.pos, 30f); } catch { }
                    activeTornados.RemoveAt(i);
                }
            }
        }

        // ============ AI 招募 (J键) ============
        // 把准星指向的敌人叛变为你的队友
        private void RecruitLogic()
        {
            if (!Input.GetKeyDown(KeyCode.J)) return;
            if (mainCam == null) return;
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 500f)) return;
            Actor target = hit.collider.GetComponentInParent<Actor>();
            if (target == null || target == localPlayer || target.dead) return;

            try { target.team = localPlayer.team; } catch { }
            // 同时改 AI 里缓存的 squad/team
            var aiType = typeof(Actor).Assembly.GetType("AiActorController");
            if (aiType != null)
            {
                var aiComp = target.GetComponent(aiType);
                if (aiComp != null)
                {
                    var t = aiComp.GetType();
                    SetField(aiComp, t, "targetActor", null);
                    SetField(aiComp, t, "squad", null);
                    SetField(aiComp, t, "hasTargetVisible", false);
                    SetField(aiComp, t, "hasSeenTarget", false);
                }
            }
            target.health = target.maxHealth; // 回满血
        }

        // ============ 死亡射线 (按住 M + 左键) ============
        // 从视线发出一道光，经过路径上所有敌人和载具全部爆炸
        private float deathRayTimer = 0f;
        private void DeathRayLogic()
        {
            if (!Input.GetKey(KeyCode.M) || !Input.GetMouseButton(0)) return;
            deathRayTimer += Time.deltaTime;
            if (deathRayTimer < 0.05f) return;
            deathRayTimer = 0f;
            if (mainCam == null) return;

            Vector3 origin = mainCam.transform.position;
            Vector3 dir = mainCam.transform.forward;
            float maxDist = 1000f;

            // 炸敌人
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;
                Vector3 toA = GetActorCenter(a) - origin;
                float fw = Vector3.Dot(toA, dir);
                if (fw < 0f || fw > maxDist) continue;
                Vector3 nearest = origin + dir * fw;
                float perp = Vector3.Distance(GetActorCenter(a), nearest);
                if (perp > 3f) continue; // 3m 粗的射线
                try { ExplodeAt(a.transform.position + Vector3.up * 0.5f, 8f); } catch { }
            }
            // 炸载具
            var vehs = UnityEngine.Object.FindObjectsOfType<Vehicle>();
            foreach (var v in vehs)
            {
                if (v == null) continue;
                if (localPlayer.seat != null && v == localPlayer.seat.vehicle) continue;
                Vector3 toV = v.transform.position - origin;
                float fw = Vector3.Dot(toV, dir);
                if (fw < 0f || fw > maxDist) continue;
                Vector3 nearest = origin + dir * fw;
                if (Vector3.Distance(v.transform.position, nearest) > 4f) continue;
                try { v.health = -1000f; SetField(v, v.GetType(), "dead", true); } catch { }
            }
        }


        // ============ 无限手雷 ============
        private void InfiniteGrenadesLogic()
        {
            if (localPlayer.seat != null) return;
            var t = localPlayer.GetType();
            SetField(localPlayer, t, "grenades", 99);
            var f = t.GetField("maxGrenades", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) try { f.SetValue(localPlayer, 99); } catch { }
        }

        // ============ 敌人死亡自爆 (神风连锁) ============
        private void ExplodeOnDeathLogic()
        {
            if (ActorManager.instance == null || ActorManager.instance.actors == null) return;
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;
                int id = a.GetInstanceID();
                if (a.dead)
                {
                    if (!alreadyDetonated.Contains(id))
                    {
                        alreadyDetonated.Add(id);
                        try { ExplodeAt(a.transform.position + Vector3.up * 0.5f, 12f); } catch { }
                    }
                }
                else
                {
                    // 复活后允许再次触发
                    if (alreadyDetonated.Contains(id)) alreadyDetonated.Remove(id);
                }
            }
        }

        // ============ 公用爆炸方法 ============
        private void ExplodeAt(Vector3 point, float radius = 15f)
        {
            try
            {
                var amType = typeof(ActorManager);
                var explodeMethods = amType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var em in explodeMethods)
                {
                    if (em.Name != "Explode") continue;
                    var pars = em.GetParameters();
                    if (pars.Length == 1 && pars[0].ParameterType == typeof(Vector3))
                    { em.Invoke(null, new object[] { point }); return; }
                }
            } catch { }

            // Fallback
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;
                if (Vector3.Distance(a.transform.position, point) < radius)
                {
                    try { a.health = -1000f; a.dead = true; } catch { }
                }
            }
        }

        // ============ 高爆弹药 ============
        private void ExplosiveAmmoLogic()
        {
            if (!Input.GetMouseButton(0)) return;
            laserTimer += Time.deltaTime; // 借用 laserTimer 控速
            if (laserTimer < 0.1f) return;
            laserTimer = 0f;

            if (mainCam == null) return;
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 1000f))
            {
                ExplodeAt(hit.point + Vector3.up * 0.5f, 15f);
            }
        }

        // ============ 载具飞越强化 ============
        private void VehicleBoostLogic()
        {
            if (localPlayer.seat == null || localPlayer.seat.vehicle == null) return;
            Rigidbody rb = localPlayer.seat.vehicle.GetComponent<Rigidbody>();
            if (rb == null) return;

            if (Input.GetKey(KeyCode.LeftShift))
                rb.velocity += localPlayer.seat.vehicle.transform.forward * 40f * Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.Space))
                rb.velocity = new Vector3(rb.velocity.x, 15f, rb.velocity.z);
        }

        // ============ 隔空召唤载具 ============
        private void SummonVehicleHotkey()
        {
            if (!Input.GetKeyDown(KeyCode.V)) return;
            var vehicles = UnityEngine.Object.FindObjectsOfType<Vehicle>();
            Vehicle closest = null;
            float minDist = float.MaxValue;
            Vector3 playerPos = localPlayer.transform.position;

            foreach (var v in vehicles)
            {
                if (v == null || (localPlayer.seat != null && v == localPlayer.seat.vehicle)) continue;
                float d = Vector3.Distance(v.transform.position, playerPos);
                if (d < minDist) { minDist = d; closest = v; }
            }

            if (closest != null)
            {
                Vector3 fw = mainCam.transform.forward; fw.y = 0; fw.Normalize();
                closest.transform.position = playerPos + fw * 4f + Vector3.up * 1f;
                closest.transform.rotation = Quaternion.LookRotation(fw);
                Rigidbody rb = closest.GetComponent<Rigidbody>();
                if (rb != null) rb.velocity = Vector3.zero;
            }
        }

        // ============ 索尔之锤 ============
        private void ThorHammerLogic()
        {
            if (!Input.GetKeyDown(KeyCode.G)) return;
            if (mainCam == null) return;
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 5000f))
            {
                ExplodeAt(hit.point + Vector3.up * 0.5f, 30f);
            }
        }

        // ============ 时间减速 ============
        private void ApplyTimeScale()
        {
            Time.timeScale = bTimeFreeze ? 0.3f : 1f;
        }

        // ============ 低重力 ============
        private void ApplyGravity()
        {
            Physics.gravity = bLowGravity ? new Vector3(0, -3f, 0) : new Vector3(0, -9.81f, 0);
        }

        // ============ UFO 下车/关闭后恢复载具重力 ============
        private void RestoreUFOVehiclesIfNeeded()
        {
            if (ufoTouchedVehicles.Count == 0) return;
            // 当前正在驾驶的载具
            Rigidbody currentRb = null;
            if (bUFOMode && localPlayer != null && localPlayer.seat != null && localPlayer.seat.vehicle != null)
                currentRb = localPlayer.seat.vehicle.GetComponent<Rigidbody>();

            var toRemove = new List<Rigidbody>();
            foreach (var rb in ufoTouchedVehicles)
            {
                if (rb == null) { toRemove.Add(rb); continue; }
                if (rb != currentRb)
                {
                    rb.useGravity = true;
                    toRemove.Add(rb);
                }
            }
            foreach (var r in toRemove) ufoTouchedVehicles.Remove(r);
        }

        // ============ 冻结敌人 ============
        private void FreezeEnemiesLogic()
        {
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;

                Rigidbody rb = null;
                try { rb = a.rigidbody; } catch { }
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                // 禁用 AI 控制器
                var aiType = a.GetType().Assembly.GetType("AiActorController");
                if (aiType != null)
                {
                    var aiComp = a.GetComponent(aiType) as Behaviour;
                    if (aiComp != null && aiComp.enabled)
                    {
                        aiComp.enabled = false;
                        frozenAiControllers.Add(aiComp); // 记录以便关闭功能时恢复
                    }
                }
            }
        }

        // 关闭冻结后恢复所有 AI
        private void RestoreFrozenAiIfNeeded()
        {
            if (bFreezeEnemies || frozenAiControllers.Count == 0) return;
            foreach (var ai in frozenAiControllers)
            {
                if (ai != null) ai.enabled = true;
            }
            frozenAiControllers.Clear();
        }

        // ============ AI 失明 ============
        private void BlindAILogic()
        {
            var asm = typeof(Actor).Assembly;
            var aiType = asm.GetType("AiActorController");
            if (aiType == null) return;
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                var aiComp = a.GetComponent(aiType);
                if (aiComp == null) continue;
                var t = aiComp.GetType();
                SetField(aiComp, t, "hasTargetVisible", false);
                SetField(aiComp, t, "targetActor", null);
                SetField(aiComp, t, "hasHeardSomething", false);
                SetField(aiComp, t, "hasSeenTarget", false);
                SetField(aiComp, t, "coverTarget", null);
                SetField(aiComp, t, "lastKnownTargetPosition", Vector3.zero);
            }
        }

        // ============ 磁力吸附（吸敌人过来） ============
        private void MagnetLogic()
        {
            Vector3 center = localPlayer.transform.position;
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;
                Vector3 dir = center - a.transform.position;
                float d = dir.magnitude;
                if (d < 3f || d > 200f) continue;
                Vector3 step = dir.normalized * 25f * Time.deltaTime;
                Rigidbody arb = null;
                try { arb = a.rigidbody; } catch { }
                if (arb != null) arb.position += step;
                else a.transform.position += step;
            }
        }

        // ============ 镭射枪（持续射线秒杀） ============
        private void LaserGunLogic()
        {
            if (!Input.GetMouseButton(0)) return;
            laserTimer += Time.deltaTime;
            if (laserTimer < 0.05f) return;
            laserTimer = 0f;

            Actor target = null;
            float bestDist = float.MaxValue;
            Vector3 camPos = mainCam.transform.position;
            Vector3 camFwd = mainCam.transform.forward;
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;
                Vector3 toA = GetActorCenter(a) - camPos;
                float ang = Vector3.Angle(camFwd, toA);
                if (ang > 3f) continue; // 3° 视锥
                float d = toA.magnitude;
                if (d < bestDist) { bestDist = d; target = a; }
            }
            if (target != null)
            {
                try { target.health = -1000f; target.dead = true; } catch { }
                try
                {
                    var m = target.GetType().GetMethod("Kill", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
                    if (m != null) m.Invoke(target, null);
                } catch { }
            }
        }

        // ============ 自动回血 ============
        private void AutoHealLogic()
        {
            if (localPlayer.health < localPlayer.maxHealth - 5f)
                localPlayer.health = Mathf.Min(localPlayer.maxHealth, localPlayer.health + 80f * Time.deltaTime);
        }

        // ============ 载具彩虹色 ============
        private void VehicleRainbowLogic()
        {
            // 每 0.1s 更新一次，避免每帧泏泊造材质
            var vehicles = UnityEngine.Object.FindObjectsOfType<Vehicle>();
            foreach (var v in vehicles)
            {
                if (v == null) continue;
                int id = v.GetInstanceID();
                float last;
                if (rainbowLastUpdate.TryGetValue(id, out last) && Time.time - last < 0.1f) continue;
                rainbowLastUpdate[id] = Time.time;

                float hue = (Time.time * 0.5f + id * 0.1f) % 1f;
                if (hue < 0) hue += 1f;
                Color c = Color.HSVToRGB(hue, 1f, 1f);
                var renderers = v.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    try { r.material.color = c; } catch { }
                }
            }
        }

        // ============ 空袭（按 B 键对准星投下多发爆炸） ============
        private void AirstrikeLogic()
        {
            if (!Input.GetKeyDown(KeyCode.B)) return;
            if (mainCam == null) return;
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 5000f)) return;
            Vector3 target = hit.point;

            // 对范围内所有敌人造成伤害
            float radius = 40f;
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;
                if (Vector3.Distance(a.transform.position, target) < radius)
                {
                    try { a.health = -1000f; a.dead = true; } catch { }
                }
            }
            // 尝试触发视觉爆炸
            try
            {
                var amType = typeof(ActorManager);
                var em = amType.GetMethod("Explode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3) }, null);
                if (em != null)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 offset = new Vector3(UnityEngine.Random.Range(-15f, 15f), 0, UnityEngine.Random.Range(-15f, 15f));
                        em.Invoke(null, new object[] { target + offset });
                    }
                }
            } catch { }
        }

        // ============ 按 H 一键炸所有敌方载具 ============
        private void ExplodeAllVehiclesHotkey()
        {
            if (!Input.GetKeyDown(KeyCode.H)) return;
            var vehicles = UnityEngine.Object.FindObjectsOfType<Vehicle>();
            foreach (var v in vehicles)
            {
                if (v == null) continue;
                if (localPlayer.seat != null && localPlayer.seat.vehicle == v) continue; // 跳过自己坐的
                try { v.health = -1000f; SetField(v, v.GetType(), "dead", true); } catch { }
            }
        }

        // ============ Helpers ============
        private Actor FindClosestEnemy(Vector3 pos)
        {
            Actor best = null;
            float bestDist = float.MaxValue;
            foreach (var a in ActorManager.instance.actors)
            {
                if (a == null || a == localPlayer || a.dead) continue;
                if (!bFriendlyFire && a.team == localPlayer.team) continue;
                float d = Vector3.Distance(pos, a.transform.position);
                if (d < bestDist) { bestDist = d; best = a; }
            }
            return best;
        }

        private static void SetField(object obj, Type t, string name, object val)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null)
            {
                try { f.SetValue(obj, val); } catch { }
            }
        }
    }
}
