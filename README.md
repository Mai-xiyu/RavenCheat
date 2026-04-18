# Ravenfield Internal Cheat (RavenCheat)

一个功能极其丰富的 Ravenfield (战地模拟器) 内部作弊/Mod菜单。
不仅包含传统的自瞄、透视等功能，还加入了一系列 **GTA V 风格** 的疯狂沙盒玩法（龙卷风、原力冲击、真实钩爪、全图大逃杀等）。

## 🚀 核心特色功能 (Sandbox & Chaos)
- **🌪️ 龙卷风 (Tornado)** - 在准星处生成龙卷风，将范围内的敌人和载具卷入高空，6秒后剧烈爆炸。
- **🪝 真实钩爪 (Grapple Hook)** - 瞄准地面：把自己拉过去（带抛物线冲量飞行）；瞄准敌人：把敌人拽过来砸地；瞄准载具：把整台载具抽飞过来！
- **💨 原力冲击波 (Force Push)** - 瞬间将周围 30m 内的所有敌人和载具向外炸飞。
- **☠️ 死亡射线 (Death Ray)** - 视线方向发射毁灭光柱，路径上的所有敌人和载具瞬间灰飞烟灭。
- **🔥 全民暴乱 (Riot Mode)** - 强行打乱全图 AI 阵营并清空旧有队伍锁定，开启全图大逃杀，原地反水互殴！
- **🔸 AI 招募 (Recruit)** - 强制洗脑准星选中的敌人，让他立刻叛变加入你的队伍。
- **💥 神风连锁** - 敌人死亡时原地爆炸，形成连环爆炸暴力清场。

## ⚙️ 传统作弊/强化功能
- **👁️ 视觉 (Visuals)**: ESP (方框、距离、血条)、雷达 (Radar小地图)、敌人高亮 (Chams)。
- **🎯 瞄准 (Aimbot)**: 自动瞄准、狂暴锁头、魔法子弹 (子弹自动追踪)、一键全屠 (瞬移子弹)。
- **🔫 武器 (Weapons)**: 无限弹药无须换弹、无后坐力/无散布、极速射击、高爆子弹、巨型子弹。
- **🛡️ 玩家 (Player)**: 无敌模式、穿墙穿地 (NoClip)、无限手雷、自动回血、加速奔跑、空中飞行、超级跳跃、巨人模式。
- **🚗 载具 (Vehicles)**: 载具无敌、UFO模式 (车辆自由离地飞行)、载具飞跃、彩虹车身。
- **🌍 世界 (World)**: 时间减速 (子弹时间)、低重力、AI失明、冻结敌人。

## ⌨️ 快捷键指南 (Hotkeys)
* Insert - 显示/隐藏作弊菜单
* F - 🪝 钩爪/飞爪 (看空地飞过去 / 看敌人拉过来 / 看载具抽过来)
* R - 💨 原力冲击波 (炸飞周围一切)
* N - 🌪️ 召唤毁灭龙卷风
* J - 🔸 招募准星处的 AI 成为队友
* M + 左键 - ☠️ 释放死亡射线
* V - 隔空取车 (将全图最近的载具瞬移到面前)
* T - 传送到准星指向的位置
* G - 索尔之锤 (在准星处引发单点爆炸)
* B - 空袭轰炸 (在准星大范围内降下连环群爆)
* H - 一键引爆全图所有敌方载具

## 📥 安装与注入 (Injection)
本项目使用 [SharpMonoInjector](https://github.com/warbler/SharpMonoInjector) 注入到 Unity Mono 游戏中。
本仓库已自带编译好的注射器 (smi.exe) 以及对应的依赖 DLL。

1. 打开游戏 **Ravenfield** 并进入战局。
2. 打开本项目所在的文件夹。
3. 打开终端 (CMD / PowerShell)，使用提供的 smi.exe 进行注入：
   ``cmd
   smi.exe inject -p "ravenfield" -a "RavenCheat.dll" -n "RavenCheat" -c "Loader" -m "Init"
   ``
4. 回到游戏，按下 Insert 键即可呼出菜单。

## 🛠️ 编译说明 (How to Build)
如果你修改了 RavenCheat.cs，可以使用以下命令重新编译。
> **注意**：Ravenfield 是基于老版本 Unity 编译的，由于 C# 编译器限制，**仅支持 C# 5.0 语法**！请务必避免使用 ?. 等 C# 6+ 语法，否则会编译失败。

``powershell
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /target:library /out:RavenCheat.dll /codepage:65001 /nostdlib+ /r:"<你的游戏目录>\ravenfield_Data\Managed\mscorlib.dll","<你的游戏目录>\ravenfield_Data\Managed\UnityEngine.dll","<你的游戏目录>\ravenfield_Data\Managed\Assembly-CSharp.dll" RavenCheat.cs
``

---
*免责声明：本项目仅供编程学习、逆向工程研究及单人游戏娱乐使用。与游戏官方无任何关联。*
