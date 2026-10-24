# RavenCheat (Ravenfield 单文件内置修改器)

基于对 `E:\SteamLibrary\steamapps\common\Ravenfield\ravenfield_Data\Managed\Assembly-CSharp.dll` 与 `Assembly-CSharp-firstpass.dll` 的完整逆向分析，重构并打包为单文件注入器 `RavenCheat.exe`（内嵌 `RavenCheat.dll`）。

## 下载与使用

- 从 GitHub Releases 下载 `RavenCheat.exe`，无需额外 DLL、`smi.exe` 或安装脚本。
- 启动 Ravenfield，进入主菜单或战局后，以管理员身份运行 `RavenCheat.exe`。
- 注入成功后回到游戏，按 `Insert` 显示或隐藏菜单。
- `RavenCheat.exe` 为 x64 Windows GUI 程序，运行时会把内嵌 DLL 释放到系统临时目录再注入；关闭游戏后可删除临时文件。

## 一、核心逆向级 Bug 根因修复

1. **透视框（ESP）尺寸异常、拉通屏幕、锁头/魔法弹指天 `(0, 10000, 0)` 修复**
   - **根因 1（布偶头部刚体休眠坐标）**：`Actor.headRigidbody` 位于 `a.ragdoll.ragdollObject` 下；当角色处于站立/行走动画状态（`!a.ragdoll.IsRagdoll()`）时，`ragdollObject` 被游戏设为 `SetActive(false)` 并停留在 `ActorManager.ACTOR_INSTANTIATE_POSITION = (0, 10000, 0)`。原代码直接读取 `a.headRigidbody.worldCenterOfMass`，导致头顶坐标指天、ESP 方框顶天立地、自瞄与魔法子弹打向高空。
   - **根因 2（掩体后骨骼裁剪冻结）**：`Actor.Update()` 对 AI 默认设置 `AnimatorCullingMode.CullUpdateTransforms`，导致墙后或视野外敌人的动画骨骼停止更新，与 `CenterPosition()` 脱节。
   - **修复方案**：
     - 仅在 `a.ragdoll.IsRagdoll()` 为真时使用 `headRigidbody`，正常动画状态统一通过 `a.ragdoll.HumanBoneTransformActive(HumanBodyBones.Head / Neck / LeftFoot / RightFoot)` 获取实时活动骨骼，并对 `(0, 10000, 0)` 做距离与世界坐标合法性校验。
     - 在 `[DefaultExecutionOrder(32000)]` 中将存活 AI 的 `animator.cullingMode` 覆写为 `AnimatorCullingMode.AlwaysAnimate`，保证墙后骨骼实时更新。
     - `ComputeActorScreenBox` 结合 11 个全身骨骼点屏幕投影与针孔相机透视公式 $H_{\text{expected}} = \frac{1.85 \cdot f_y}{z}$ 做雙重宽高比约束，彻底消除超大框与畸形框。

2. **人物穿墙高亮（Chams）不透墙、材质丢失修复**
   - **根因**：Unity 标准 Shader 开启深度测试（`_ZTest = LEqual`），被墙体深度缓冲遮挡；且直接替换 `Renderer.material` 会破坏多子网格材质数组与离屏蒙皮更新。
   - **修复方案**：
     - 采用 `Hidden/Internal-Colored` 构建专用透视材质，显式写入 `_ZTest = CompareFunction.Always (8)`、`_ZWrite = 0`、`_Cull = 0`、`renderQueue = 4000`。
     - 完整备份每个 `SkinnedMeshRenderer.sharedMaterials` 数组并开启 `smr.updateWhenOffscreen = true`；支持**彩虹流光高亮**与自定义不透明度（`0.20 ~ 1.00`），关闭时完美还原原始材质。

3. **穿墙魔法子弹（Magic Bullet）无法穿墙根因修复**
   - **根因 1（枪口初始射线撞墙）**：`Weapon.SpawnProjectile` 对玩家发射的子弹默认设置 `performInfantryInitialMuzzleTravel = true`，首帧从武器父节点到枪口按默认 `hitMask` 做射线检测，贴墙时直接引爆。
   - **根因 2（碰撞掩码包含地形与墙体）**：`Projectile.StartTravelling()` 将 `hitMask` 设为 `-29755909`（包含 Layer 0 墙体与 Layer 14 地形），并在 `Projectile.Update()` 中先撞墙销毁。
   - **根因 3（帧序滞后）**：玩家开火或自动扳机在帧内生成投射物后，若未在 `Projectile.Update()` 前介入，首帧会先按原方向飞出撞墙。
   - **修复方案**：
     - 直接挂载 `Weapon.onSpawnProjectiles` 事件钩子，并在 `[DefaultExecutionOrder(-32000)]`（`CheatEarlyUpdate`）+ `Update` + `LateUpdate` 三阶段同步接管玩家投射物。
     - 强制 `performInfantryInitialMuzzleTravel = false`，通过反射将 `Projectile.hitMask` 设为 `Hitbox.ALL_HITBOX_LAYER_MASK (66816)`（目标乘载具时附加 `1 << 12`），彻底剔除墙体与地形碰撞层，将弹道直接折跃至目标命门。

4. **第一人称自瞄视角回弹与移动参数精准控制**
   - **根因**：`FirstPersonController.RotateView()` 每帧从 `fpc.controller.cameraParent.localRotation` 读取旋转角并覆盖相机。
   - **修复方案**：自瞄同步写入 `fpc.controller.cameraParent.localRotation` 与 `activeCam.transform.rotation`，并直接读写 `FirstPersonController` 的 `m_WalkSpeed`、`m_RunSpeed`、`m_JumpSpeed` 与 `m_GravityMultiplier`。

---

## 二、全新界面与数值调节体系（无需快捷键）

- **菜单开关**：仅保留 `Insert` 键用于显示/隐藏菜单；`F1~F10` 快捷键默认**关闭**（可在“系统/设置”标签页按需开启），杜绝按键冲突。
- **六大分类标签页**：`全部功能`、`透视 / 自瞄`、`武器 / 弹道`、`玩家 / 载具`、`世界 / 娱乐`、`系统 / 设置`。
- **全参数可调（滑条 + 文本框直接输入 + `-/+` 微调按钮）**：
  - 透视最大距离、透视框外扩比例、穿墙高亮不透明度、雷达扫描半径、目标 HUD 范围
  - 自瞄/魔法弹 FOV 半径、自动秒杀间隔
  - 极速连发间隔、散弹弹丸数、子弹伤害倍率、子弹击飞倍率、巨型子弹倍率、高爆弹爆炸半径
  - 移动速度倍率、跳跃高度倍率、飞行速度、巨人/蚁人体型缩放（`0.3x ~ 6.0x`）、载具推进力、UFO 飞天载具速度
  - 全局时间流速、全局重力倍率（支持负数反重力）、黑洞吸人距离与引力、彩虹轨迹时长与宽度、护体震波间隔/半径/力度

### 目标阵营与友敌视觉筛选

- **统一目标筛选**：自瞄、魔法子弹、自动扳机、目标 HUD 与最近目标查找共用一个阵营选择器，可选 `仅敌军`、`仅友军`、`友敌均可`。
- **ESP 分开控制**：`ESP 显示敌军` 与 `ESP 显示友军` 可独立开关，互不影响。
- **模型高亮分开控制**：`高亮敌军模型` 与 `高亮友军模型` 可独立开关；关闭任一类别会立即恢复该类角色的原始材质。
- **独立颜色**：敌军与友军分别支持红、绿、蓝、黄、紫、青、白 7 种穿墙高亮颜色，ESP 方框同步使用对应颜色。

### 快捷键映射

- `Insert`：显示/隐藏菜单，始终可用。
- `F1~F10`：可选快捷键，默认关闭；仅在“系统 / 设置”中启用后才生效。
- `F1` 人物 ESP、`F2` 自瞄、`F3` 玩家无敌、`F4` 无限弹药、`F5` 飞行。
- `F6` 秒杀敌军、`F7` 准星瞬移、`F8` 慢动作、`F9` 模型高亮、`F10` 魔法子弹。
- 所有功能均可直接从菜单开关、按钮、滑条和数值输入框操作，不依赖快捷键。

---

## 三、新增好玩功能一览（菜单开关 & 一键按钮）

- **被动开关类**：
  - **载具 ESP 透视**：实时显示全场载具方框、名称、血量百分比与距离
  - **彩虹流光高亮/方框**：全身穿墙高亮与 ESP 方框动态 HSV 霓虹流光渐变
  - **全图敌军原生战术标记**：周期调用 `Actor.Highlight()` 触发游戏原生红框标记
  - **万物散弹枪模式**：任意步枪/手枪/火箭筒一枪发射多枚弹丸（弹丸数可调）
  - **超级伤害 / 超级击飞冲击波子弹**：一枪秒杀装甲载具或将敌人轰飞上天
  - **杀敌吸血进化**：每次击杀敌军自动回血并永久提升当前生命上限
  - **随身贴身护卫队**：自动招募友军加入玩家小队、提升血量上限至 500、自动补满弹药并跟随传送
  - **敌军内讧模式**：篡改敌方 `AiActorController` 索敌逻辑，令敌军互相瞄准开火残杀
  - **巨人 / 蚁人模式**：自由缩放玩家体型（`0.3x` 微缩潜行或 `6.0x` 进击巨人）
- **一键触发按钮（菜单直接点击）**：
  - `⚡ 全屏秒杀敌军`、`📍 准星瞬移`、`🚩 一键占领全据点`、`🎭 夺舍附身AI`（调用 `ActorManager.PlayerTakeOverBot`）
  - `✈ 呼叫准星地毯式空袭`、`🌪 在准星处生成超级龙卷风`、`🪂 全体敌军高空跳伞派对`
  - `🔫 缴械全体敌军`、`💥 全图敌军原地击飞暴动`、`🔥 引爆全场敌方载具`
  - `🚗 召唤空闲载具到面前`、`🛡 集结16名友军到身边`、`💊 全图友军满血+满弹药补给`

## 构建

Ravenfield 使用旧版 Unity/Mono，必须使用 C# 5 语法与 .NET Framework 编译器：

```powershell
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$managed = 'E:\SteamLibrary\steamapps\common\Ravenfield\ravenfield_Data\Managed'

& $csc /nologo /target:library /langversion:5 /codepage:65001 `
  /out:RavenCheat.dll /nostdlib+ /lib:$managed `
  /r:mscorlib.dll /r:netstandard.dll /r:System.Core.dll `
  /r:UnityEngine.dll /r:UnityEngine.CoreModule.dll `
  /r:UnityEngine.IMGUIModule.dll /r:UnityEngine.PhysicsModule.dll `
  /r:UnityEngine.InputLegacyModule.dll /r:UnityEngine.AnimationModule.dll `
  /r:UnityEngine.TextRenderingModule.dll /r:UnityEngine.ParticleSystemModule.dll `
  /r:Assembly-CSharp.dll /r:Assembly-CSharp-firstpass.dll RavenCheat.cs

& $csc /nologo /target:winexe /platform:x64 /codepage:65001 `
  /out:RavenCheat.exe /resource:RavenCheat.dll,RavenCheat.dll Injector.cs
```
