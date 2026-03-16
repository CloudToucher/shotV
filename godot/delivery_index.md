# ShotV Godot 迁移 - 交付索引

## 批次 1：核心可运行闭环（已完成）

| 文件路径 | 模块 | 系统归属 | 当前状态 | 后续优先级 |
|----------|------|----------|----------|------------|
| project.godot | 项目配置 | 基础设施 | 已完整实现 | - |
| autoload/palette.gd | 调色板 | 视觉 | 已完整实现 | - |
| autoload/game_data.gd | 数据定义 | 数据层 | 已完整实现 | - |
| autoload/save_manager.gd | 存档管理 | 持久化 | 已完整实现 | - |
| autoload/game_store.gd | 状态管理 | 核心 | 已完整实现 | - |
| world/routes.gd | 路线系统 | 世界 | 已完整实现 | - |
| world/collision_utils.gd | 碰撞工具 | 世界 | 已完整实现 | - |
| world/world_layout.gd | 地图生成 | 世界 | 已完整实现 | - |
| entities/player_avatar.gd | 玩家实体 | 实体 | 已完整实现 | 视觉细化 |
| entities/enemy_avatar.gd | 敌人实体 | 实体 | 已完整实现 | 视觉细化 |
| scenes/main.tscn | 入口场景 | 场景 | 已完整实现 | - |
| scenes/main.gd | 入口逻辑 | 场景 | 已完整实现 | - |
| scenes/base_camp.tscn | 基地场景 | 场景 | 已完整实现 | UI面板 |
| scenes/base_camp.gd | 基地逻辑 | 场景 | 已完整实现 | 面板交互 |
| scenes/combat_scene.tscn | 战斗场景 | 场景 | 已完整实现 | - |
| scenes/combat_scene.gd | 战斗逻辑 | 场景 | 已完整实现 | 背包/面板 |
| migration_analysis.md | 分析文档 | 文档 | 已完整实现 | - |

## 批次 2：待交付文件（下一批优先）

| 文件路径 | 模块 | 系统归属 | 说明 |
|----------|------|----------|------|
| inventory/inventory_grid.gd | 背包网格 | 背包 | 网格放置/拾取/整理逻辑 |
| ui/combat_hud.gd | 战斗HUD | UI | 独立HUD控制器(血量/武器/快捷栏/Boss) |
| ui/minimap.gd | 小地图 | UI | 限频刷新的缩略地图 |
| ui/toast_overlay.gd | Toast消息 | UI | 全局消息提示系统 |
| ui/panel_surface.gd | 面板绘制 | UI | 全屏面板框架绘制 |
| ui/inventory_surface.gd | 背包面板 | UI | 网格背包渲染与拖拽 |

## 批次 3：待交付文件（后续）

| 文件路径 | 模块 | 系统归属 | 说明 |
|----------|------|----------|------|
| combat/combat_encounter.gd | 遭遇管理 | 战斗 | 独立的遭遇管理器类（当前内联在combat_scene.gd） |
| combat/combat_player_ctrl.gd | 玩家控制 | 战斗 | 独立的玩家战斗控制器（当前内联） |
| ui/item_visuals.gd | 物品视觉 | UI | 地面掉落物程序化绘制 |
| scenes/settlement_panel.gd | 结算面板 | UI | 撤离/阵亡后的结算确认界面 |

---

## Migration Ledger（迁移账本）

| ID | 类型 | 内容 | 影响范围 | 对应文件 | 后续动作 |
|----|------|------|----------|----------|----------|
| ML-001 | 事实 | 原项目共32个game源文件+6个app源文件 | 全局 | migration_analysis.md | - |
| ML-002 | 事实 | 原项目使用localStorage存档 | 持久化 | save_manager.gd | 已改为FileAccess JSON |
| ML-003 | 事实 | 原项目使用PixiJS 8 Graphics程序化绘制 | 视觉 | 所有实体+场景 | 已改为Godot _draw() |
| ML-004 | 决策 | 战斗逻辑内联在combat_scene.gd中 | 战斗 | combat_scene.gd | 后续可拆分为独立类 |
| ML-005 | 决策 | 使用Godot Input Map替代JS InputController | 输入 | project.godot | 已完成 |
| ML-006 | 决策 | 使用Camera2D替代手动Container偏移 | 相机 | combat_scene.gd, base_camp.gd | 已完成 |
| ML-007 | 假设 | ui/surface.ts的绘制函数按调用参数推断重写 | UI | 所有场景 | 简化实现 |
| ML-008 | 假设 | CombatHudController按功能需求简化重写 | UI | combat_scene.gd | 后续独立 |
| ML-009 | 风险 | 效果层每帧重建Node会有GC压力 | 性能 | combat_scene.gd | 改为单Node2D队列绘制 |
| ML-010 | 风险 | 敌人分离检测O(n²)在大量敌人时可能卡顿 | 性能 | combat_scene.gd | 后续加空间分区 |
| ML-011 | 待补 | 背包网格系统未迁移 | 背包 | - | 批次2交付 |
| ML-012 | 待补 | 小地图未实现 | UI | - | 批次2交付 |
| ML-013 | 待补 | 面板UI系统未完整实现 | UI | - | 批次2交付 |
| ML-014 | 待补 | 掉落物拾取系统未迁移 | 战斗 | - | 批次2交付 |
| ML-015 | 待补 | 离线收益系统未实现 | 元系统 | - | MVP后期 |
| ML-016 | 决策 | AutoLoad顺序：Palette→GameData→SaveManager→GameStore | 启动 | project.godot | 已完成 |
| ML-017 | 事实 | 原项目有5条路线定义，全部迁移 | 世界 | routes.gd | 已完成 |
| ML-018 | 事实 | 原项目8种物品定义，全部迁移 | 数据 | game_data.gd | 已完成 |
| ML-019 | 事实 | 原项目4种敌人类型(含Boss)，全部迁移 | 数据 | game_data.gd | 已完成 |
| ML-020 | 事实 | 原项目3种武器类型，全部迁移 | 数据 | game_data.gd | 已完成 |

---

## 后续接力说明

### 当前已经能运行到什么程度

批次1交付后，项目可以：

1. 启动进入基地场景，显示地图网格、障碍物、标记点
2. WASD移动玩家角色，鼠标瞄准
3. 靠近出击闸门按E启动部署校验读条
4. 读条完成后进入战斗场景
5. 战斗场景自动生成程序化地图
6. 波次系统自动推进，敌人出现并追踪玩家
7. 鼠标射击（机枪/榴弹/狙击三种武器，1-2-3切换）
8. Space冲刺闪避
9. 击杀敌人到Boss阶段
10. Boss战二阶段转换
11. 清空后通过出口推进到下一区域或撤离
12. 撤离/阵亡后返回基地
13. 存档自动持久化到user://目录

### 下一批最先要输出哪些文件

1. **inventory/inventory_grid.gd** - 网格背包是核心体验
2. **ui/combat_hud.gd** - 独立HUD提升可读性
3. **ui/minimap.gd** - 小地图是导航必需
4. **ui/toast_overlay.gd** - 消息系统独立化

### 为什么这些文件优先

- 背包系统是撤离式roguelite的核心玩法支撑
- HUD独立化解耦战斗场景巨型脚本
- 小地图是大地图导航的必要工具
- Toast系统被所有场景共用

### 后续继续时应直接从哪一节接着输出

从「批次2」的 inventory_grid.gd 开始，然后依次完成 UI 层文件。
