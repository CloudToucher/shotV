# ShotV 迁移分析文档

## 0. 执行摘要

本文档记录从 Vue + PixiJS + localStorage 项目向 Godot 4 项目的完整迁移过程。

### 本轮目标
- 输出完整可运行的 Godot 4 项目骨架与核心系统
- 采用分批交付模式，优先保证最小可运行闭环

### 关键假设
- A1: UI 相关的 surface.ts / inventorySurface.ts / itemVisuals.ts / CombatHudController.ts 未完整阅读，用 Godot 原生方案重新实现
- A2: GameViewport.vue 作为 PixiJS 宿主容器，在 Godot 中不需要对应物
- A3: 原项目使用 localStorage，Godot 改用 FileAccess + JSON 存档
- A4: 所有视觉效果改为 Godot _draw() 程序化绘制

### 当前可落地程度
- 第一批交付：项目配置 + 数据层 + 核心游戏逻辑 + 实体 + 战斗系统
- 第二批交付：场景组装 + UI 层 + 小地图
- 第三批交付：完善面板交互 + 存档 + 调试

---

## 1. 输入整理

### 已提供资料
- 完整项目源码（32 个 game/ 文件 + 6 个 app/ 文件 + 1 个 Vue 组件）
- 设计文档 devDoc.md（807 行）
- 迁移任务说明 work.md（429 行）

### 关键系统清单
| 系统 | 原文件 | 职责 |
|------|--------|------|
| 游戏运行时 | GameRuntime.ts | PixiJS Application 初始化、tick 循环、场景切换 |
| 状态管理 | gameStore.ts | 全局状态、发布/订阅、存档持久化 |
| 存档水合 | saveState.ts | 存档版本兼容、字段容错水合 |
| 存档仓库 | saveRepository.ts | localStorage 读写 |
| 数据定义 | weapons.ts, hostiles.ts, items.ts, waves.ts | 武器/敌人/物品/波次数据 |
| 调色板 | palette.ts | 全局颜色常量 |
| 玩家实体 | PlayerAvatar.ts | 移动、冲刺、射击视觉 |
| 敌人实体 | EnemyAvatar.ts | 敌人形状、血条、模式视觉 |
| 战斗遭遇 | CombatEncounterManager.ts | 波次生成、敌人 AI、弹幕、伤害 |
| 玩家控制 | CombatPlayerController.ts | 瞄准、射击、武器切换 |
| 碰撞 | collision.ts | 圆形碰撞、射线裁剪 |
| 世界布局 | layout.ts | 程序化地图生成 |
| 路线系统 | routes.ts | 多区域路线定义与推进 |
| 背包系统 | grid.ts, inventory/types.ts | 网格背包放置/拾取/整理 |
| 战斗场景 | CombatSandboxScene.ts (2509行) | 战斗主场景，渲染+逻辑+UI |
| 基地场景 | BaseCampScene.ts (1412行) | 基地主场景，站点交互+面板 |
| 输入控制 | InputController.ts | 键鼠输入采集 |
| 会话类型 | session/types.ts | RunState, ExtractionResult 等 |

### 缺失信息
- ui/surface.ts 的完整绘制函数签名（已根据调用处推断）
- ui/CombatHudController.ts 完整实现
- ui/inventorySurface.ts 完整实现
- ui/itemVisuals.ts 完整实现

### 关键假设
- 所有 UI 绘制函数按调用参数推断并在 Godot 中重新实现
- HUD 控制器按功能需求重写

---

## 2. 最小必要旧系统解构

### 渲染层
- PixiJS 8 WebGL 渲染
- 所有视觉通过 Graphics.draw 系列方法程序化绘制
- 无外部贴图依赖
- 相机通过 Container 的 position/scale 实现

### 游戏逻辑层
- 两个主场景：BaseCampScene（基地）、CombatSandboxScene（战斗）
- 战斗逻辑集中在 CombatEncounterManager（敌人 AI、弹幕、波次）
- 玩家控制在 CombatPlayerController（瞄准、射击、武器切换）
- 世界碰撞在 collision.ts（圆体-矩形碰撞）

### UI 层
- HUD：血量、波次、武器、快捷栏、Boss 状态
- 面板：背包网格、统计信息、武器编排、路线选择
- 小地图：实时缩略图
- 提示条：交互提示、Toast 消息

### 数据与存档层
- localStorage JSON 存档
- SaveState 包含：base / inventory / world / session / settings
- 水合函数做字段级容错

### 输入与交互
- WASD 移动、鼠标瞄准/射击、Space 冲刺
- E 交互、Tab/I 面板、M 地图、1-3 武器切换
- Z/X/C/V 快捷栏使用

### 主循环
- PixiJS Ticker 每帧调用 scene.update(delta, elapsed, input)
- 场景内部分发更新到各子系统

### 状态流
- GameStore 中心化状态 → subscribe 监听模式切换
- 战斗场景定期 syncActiveRun 同步快照到 Store
- Store 写入 localStorage

### 性能热点
- CombatSandboxScene 2500+ 行，渲染+逻辑+UI 混合
- 每帧重绘 effects/minimap/groundLoot
- 敌人分离检测 O(n²)

---

## 3. Godot 迁移总设计

### 目录结构
```
godot/
├── project.godot
├── autoload/
│   ├── palette.gd              # 颜色常量
│   ├── game_data.gd            # 武器/敌人/物品/波次数据
│   ├── game_store.gd           # 全局状态管理
│   └── save_manager.gd         # 存档读写
├── data/
│   ├── weapon_def.gd           # WeaponDefinition 资源类
│   ├── hostile_def.gd          # HostileDefinition 资源类
│   └── item_def.gd             # ItemDefinition 资源类
├── combat/
│   ├── combat_math.gd          # 数学工具
│   ├── combat_encounter.gd     # 波次/敌人管理
│   └── combat_player_ctrl.gd   # 玩家战斗控制
├── entities/
│   ├── player_avatar.gd        # 玩家 Node2D
│   └── enemy_avatar.gd         # 敌人 Node2D
├── inventory/
│   ├── inventory_grid.gd       # 网格背包逻辑
│   └── inventory_types.gd      # 数据结构
├── world/
│   ├── world_layout.gd         # 程序化地图
│   ├── collision_utils.gd      # 碰撞工具
│   └── routes.gd               # 路线定义与推进
├── ui/
│   ├── combat_hud.gd           # 战斗 HUD
│   ├── minimap.gd              # 小地图
│   ├── panel_surface.gd        # 面板绘制
│   ├── inventory_surface.gd    # 背包面板
│   └── toast_overlay.gd        # Toast 消息
├── scenes/
│   ├── main.tscn               # 入口场景
│   ├── main.gd
│   ├── base_camp.tscn
│   ├── base_camp.gd
│   ├── combat_scene.tscn
│   └── combat_scene.gd
└── migration_analysis.md
```

### AutoLoad 设计
| 名称 | 脚本 | 职责 |
|------|------|------|
| Palette | autoload/palette.gd | 全局颜色常量 |
| GameData | autoload/game_data.gd | 武器/敌人/物品定义 |
| GameStore | autoload/game_store.gd | 状态管理、信号驱动 |
| SaveManager | autoload/save_manager.gd | JSON 文件存档 |

### 模块通信方式
- GameStore 使用 Godot signal 替代 JS subscribe 模式
- 场景内部用直接引用 + signal
- 跨场景用 AutoLoad 全局访问

### 高频与低频更新边界
- 高频（_process）：玩家移动、敌人 AI、弹幕、特效
- 低频（事件驱动）：UI 面板刷新、存档写入、小地图（限频 5fps）
- 极低频：存档落盘（仅状态变更时 + 最小间隔 1s）
