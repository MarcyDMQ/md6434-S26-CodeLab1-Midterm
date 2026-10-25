# SocialSim MVP

## 自然同行与拖放入组

- 小组保留共同 Utility 决策和 2 秒活动计时。每次出发由最靠近目标区域的成员提供路线参考，所有人共同向区域移动；抵达附近时靠近已到达的同伴，参考成员离组后自动接替。
- 没有聚集点、环形站位或个人落点表。带路者使用临时导航目的地前往区域；成员满足区域距离和同伴距离后原地停下。平均位置仅用于统计组内跨度，不是导航目标。
- **Group Settings → Follow Distance** 默认 0.9，用于区域附近与同伴会合；**Companion Arrival Distance** 默认 1.5，**Max Group Spread** 默认 2.5。到达必须靠近活动区域、与已到达的同伴相近，并限制成员间跨度；NavMesh 直通检查防止隔墙判定为已经会合。
- **Moving Separation / Resume Separation** 默认 2 / 1.5，每 0.15 秒检查实际距离，用于领先成员减速和恢复。过度重叠时短暂让开，其他避让交给 NavMeshAgent。
- 行走采用松散群聚：成员沿活动区域的路径方向共同前进，并叠加组内吸引、近距离分离和稳定的左右偏向。空地会展开，窄处允许短暂收成队列；没有固定队形或站位。
- **Cohesion Weight**（0.85）控制靠拢，**Separation Weight**（1.3）控制让开，**Side Bias Weight**（0.4）控制横向展开。**Cohesion Distance**（1）与 **Separation Distance**（0.8）控制各自作用距离，**Steering Look Ahead**（1.5）控制临时短程导航目标长度。
- 领先成员平滑降低速度而非频繁停车，匹配较慢同伴；**Minimum Speed Multiplier**（0.3）与 **Speed Change Rate**（4）控制减速下限和变化速度。明显走散超过 **Wait Separation**（3.5）才暂停等待。离组、拖动或到达时恢复自己的原速度，窄路受阻时回退到普通 NavMesh 寻路。
- Study/Home 使用 **Arrival Distance** 判定。Playground 的 **Require Surface Contact** 已开启：脚下必须命中 **Activity Surfaces** 引用的三个 Capsule 的真实表面，才算到达并开始减压；不使用扩大的包围盒判定。**Surface Height Tolerance** 默认 0.12，仅容忍 NavMesh 烘焙的高度误差；**Surface Goal Inset** 默认 0.15，为临时导航目标留出边缘余量。
- **Drop Join Distance** 默认 1.5：拖到组员附近松开，立即加入最近的未满小组，绕过需求匹配、扫描和冷却。满员（默认 5 人）、紧急状态、不可达或关闭小组功能时不强制加入。活动中的小组保留原有计时，新成员靠近后参与剩余时间。
- 第一名到达后最多等待 3 秒，未到成员离组；拖放新成员有独立的到达宽限。放到小组范围外恢复个人决策，窗口失焦结束拖动不触发入组。
- 不同组可以自然混在同一区域，实际邻居数仍影响支持和拥挤压力。头顶显示组号、当前带路者编号及小组阶段，Scene 连线只显示成员关系。

## 邻居、变色与压力爆炸

- Manager 的 **Stress Material** 默认引用现有 `red`，**Explode Prefab** 默认引用 `explodeR`。两者都可在 Inspector 替换；原资产保持不变。
- **Fatigue Material** 默认引用 `blueskin`。**Settings → Fatigue Tint Threshold** 默认 50：疲劳不超过阈值时保持原色，超过后开始渐变，75 时渐变一半，100 时完全接近它；休息后随疲劳降低而恢复。先计算疲劳颜色，再叠加压力颜色；压力达到爆炸阈值时以压力材质为主。每个学生使用独立材质实例，原材质资产不修改。
- **Neighbor Radius** 默认 3：统计三维距离内的其他存活且启用的学生，不受墙体遮挡，也不会因为多个 Collider 重复计数。拖动中的学生同样参与。
- **Support Min/Max Neighbors** 默认 2 / 4：在此区间，低成绩压力增长乘 **Supported Stress Gain Multiplier**（0.5），Playground 减压速度乘 **Supported Recovery Multiplier**（1.5）。
- 超过 4 个邻居时，失去上述帮助；每多一个邻居，每秒增加 **Crowd Stress Per Neighbor**（1）点压力。此压力不要求成绩低于阈值。在操场实际执行减压活动时免疫拥挤压力，2–4 名同伴的减压加成仍生效；低成绩压力照常计算，前往操场、集合等待或拖动时不享受这项豁免。
- **Stress Explosion Threshold** 默认 90：压力越接近它，外观越接近 `red` 的现有颜色；减压后恢复原色。颜色使用每个学生独立的运行时材质，不修改共享资源。`red` 原本的颜色未强制改成纯红。
- **Explosion Grace Period** 默认 15：从本轮模拟启动起，前 15 秒所有学生都不会因压力爆炸，压力和其他行为正常更新。保护结束时，仍达到爆炸阈值的学生立即爆炸，已经减到阈值以下的继续存活。使用游戏时间，暂停时不倒计时；设为 0 可关闭保护。
- 达到阈值立即消失，原地生成一次独立爆炸，**Explosion Lifetime** 默认 5 秒后清理。不补充学生，Manager 的学生列表随之减少。拖动期间达到阈值也会爆炸。
- 头顶标签新增 **Neighbors** 和 **Normal / Supported / Crowded**。当只有 5 个学生时，每人最多 4 个邻居；观察拥挤效果需要至少 6 个学生，并配置相应出生点。当前场景的 Agent Count 保持为 15。
- 新测试覆盖人数边界、支持/拥挤压力叠加、邻居去重和禁用排除、独立材质渐变与清理，以及拖动中死亡、爆炸唯一性和清理。

打开 `Assets/Scenes/SocialSim.unity`，等待 Unity 编译后按 Play。
场景启动时先由 Campus Navigation 的 NavMeshSurface 构建导航，再按 Agent Count 生成学生。
不需要先手动 Bake。已将 SocialSim 加入 Build Settings，SampleScene 仍保留。

## Inspector 配置

- **SocialManager**：Visual Prefab 可替换角色，默认 goat1-run；Student Animator 使用学生专用 idle/run 控制器。替换其他骨架角色时同时替换兼容的控制器，或清空该字段以保留角色自己的控制器。
- **Settings**：调整成绩下降、学习提升、疲劳增加、休息恢复、移动速度、停止距离及停留时间（默认 2 秒）。参数在运行时也可调整。
- **压力参数**：Stress Academic Threshold 默认 50，成绩低于它时每秒增加 Stress Gain（默认 2）的压力；在 Playground 执行行为时，每秒降低 Playground Stress Recovery（默认 8）。低成绩时两个效果叠加，没有邻居修正时 Playground 默认净减压每秒 6。压力初始为 0，范围 0–100，成绩恢复后停止低成绩引起的增长，但已有压力需要在 Playground 缓解，拥挤仍可增加压力。
- **Study/Home/Playground Activity**：Target 引用现有区域中心（Playground 使用中央 Capsule）。Arrival Distance 控制走到多近后停下，Study/Home 默认 2 单位，Playground 默认 0.65。选中 Activity 对象显示到达范围。每次移动自动生成范围内的行走目标，不需要布置停留点。
- **Spawn Points**：只需配置五个出生点，学生按点 1→2→3→4→5 循环分配；增加 Agent Count 不需要扩充数组。15 人时每点 3 人，不能整除时各点人数最多差 1。重复的 Transform 引用会被忽略，避免某个点被重复加权。
- **Spawn Spacing** 默认 0.8：同一出生点附近自动错开，使用可达 NavMesh 位置并检查所有初始位置之间的间距，防止出生重叠。
- **Show Debug Labels**：决定启动时是否生成头顶标签。运行中的 StudentAgent Inspector 可调整成绩与疲劳来观察决策。
- **Enable Mouse Dragging**：启动时启用鼠标拖放。在 Game 视图用左键按住学生并拖动，松开时放置在鼠标指向的 NavMesh 表面（包括 Playground 平台），随后重新自主决策。不可行走表面或场景外的位置不接受放置，保留最近的有效位置。拖动期间继续自然成绩下降和低成绩压力增长，但暂停学习、休息和减压效果。窗口失去焦点时自动结束拖动。
- **Mouse Pick Padding Pixels** 默认 16：点击不必精确命中小型 CapsuleCollider；优先使用直接命中，其次在角色可见外观的屏幕范围周围增加像素容差，选择最近的可见学生。建筑遮挡仍有效，不修改角色碰撞体或压力材质。鼠标按下时保留抓取偏移，点在角色边缘不会立刻把它拉向光标。

## 行为

成绩、疲劳和压力限制在 0–100。成绩持续自然下降；低成绩会持续增加压力，包括移动时。到达后学习增加成绩与疲劳，Home 恢复疲劳，Playground 缓解压力。每次停留 2 秒结束后重新评分，同一行为胜出时原地继续。Playground 的内部探索计时随时间增长，完成停留后重置；其评分为 Min(100, 探索基础分 + 压力) × (1 − 疲劳/100)，压力越高越倾向前往 Playground，高疲劳仍优先休息。

学生进入区域的到达距离后就停下，依靠 NavMesh 局部避让减少重叠。路径不完整或连续 5 秒没有进展时重新选择，并在 5 秒内跳过失败区域。无可达区域时每秒重试。Playground 三个 Capsule 使用贴合模型的 MeshCollider，导航包含其表面，学生可以走上这些低矮平台。导航尺寸在 Project Settings 的 Navigation Agent 设置中配置；NavMeshAgent 的实际半径、高度与其保持相容。

原 goat prefab 未修改。学生生成时关闭实例中的旧 BulletReverse / CharacterController，禁用额外 NavMeshAgent，将 Rigidbody 设为运动学。新增组件与模拟代码均在 SocialSim 命名空间中。

## 测试

在 Unity Test Runner 中运行 SocialSim.EditMode 与 SocialSim.PlayMode：

- EditMode：Utility 评分、探索上限、移动/学习数值变化、0–100 边界，以及三个区域和三个 Capsule 表面的导航连通性。
- PlayMode：固定随机种子运行 180 秒模拟时间，检查配置的学生人数、数值范围与三种行为均被执行；检查拖动期间暂停自主行为、有效位置放置、拒绝无效位置以及松开后恢复决策。

视觉检查：观察头顶文本（包含 Stress）、跑步/站立动画、局部避让和区域范围内的自然停留。此 MVP 不包含 Belonging、Relationships 或社交互动。
