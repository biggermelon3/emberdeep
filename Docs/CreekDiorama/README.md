# Creek Beautiful Corner / 模块化环境美术

根据参考图制作的原创低多边形溪谷沙盘。当前交付是独立的 beautiful corner 美术样板和可自由摆放的模块。桥的展示路线仅用于构图，实际路线、障碍、台阶和碰撞由关卡作者安排。

## 打开与交接

- `Assets/CreekDiorama/Scenes/BeautifulCorner.unity`：起点、终点、木桥、岩岸、松树、动态溪水、瀑布、落水泡沫和可选传送门的独立展示场景。
- `Assets/CreekDiorama/Prefabs/BeautifulCorner.prefab`：完整美术样板；摄影相机、灯光和展示地面不包含在此 Prefab 中。
- `Assets/CreekDiorama/Scenes/CreekModuleGallery.unity`：20 个模块的实际 Prefab 实例陈列。
- `Assets/CreekDiorama/Prefabs/Modules/`：可拖入关卡的独立模块，均无 Collider、无运行时行为，不依赖玩法资产。
- `ArtSource/CreekDiorama/CreekModularKit.blend`：模块的 Blender 4.3 可编辑源文件，部件按行摆开。
- `ArtSource/CreekDiorama/CreekDiorama.blend`：原始完整沙盘源文件，保留分组。
- `BeautifulCorner-Botanical.png`、`Botanical-WaterfallDetail.png`：植物与岩石升级后的 Unity 实际预览；`BotanicalGallery.png` 展示新装饰模块，`ModuleGallery.png` 展示基础桥梁模块。原始 `BeautifulCorner.png` 保留用于对照。

## 模块尺寸与摆放

单位为米，默认前进方向为局部 +Z，桥面宽 0.40 m。建议使用 0.20 m 基础网格，精确连接时对齐 Anchor_Entry 与上一块的 Anchor_Exit；这些空物体只标记位置，不包含逻辑。桥面顶面入口位于 y=0，可整体缩放。

| 模块 | 尺寸 / 用途 |
| --- | --- |
| Deck_Straight_040 / 080 | 长 0.40 / 0.80，宽 0.40 |
| Deck_Ramp_080_Rise020 | 长 0.80，上升 0.20 |
| Deck_Stairs_080_Rise020 | 长 0.80，上升 0.20，共 8 级 |
| Stair_Tread_040 | 单级踏板，宽 0.40、深 0.10 |
| Deck_Turn90_080 | 0.80 × 0.80 占地；入口 (0,0,0)，出口 (0.60,0,0.60)，出口朝向 +X |
| Support_Pier_020 / 040 / 060 | 桥墩高度 0.20 / 0.40 / 0.60，含 Foot / Deck 对接点 |
| Railing_040 | 长 0.40 的独立绳栏杆 |
| Landing_Platform_080 | 0.80 × 0.80 木平台 |
| Marker_Start / Goal | 蓝色起点、红色终点标牌 |
| Portal_Frame | 木门框与可单独隐藏的青蓝漩涡 |
| Water_Tile_080 | 0.80 × 0.80 水面 |
| Waterfall_020x040 | 宽 0.20、高 0.40，含 Lip / Impact 对接点 |
| Cliff_Terrace_040 | 0.40 岩岸草地模块 |
| Station_Start / Build / Goal | 起点、拼车平台与终点组合模块 |

Station_Build 是 Station_Start 的 **Prefab Variant**，共用同一木平台、门框、蓝色标牌和材质；修改基础起点 Prefab 会同步到拼车平台。Anchor_AssemblyCenter、Anchor_Spawn、Anchor_Portal 供后续玩法接线，当前没有车辆移动或传送逻辑。Goal 使用同款平台与红色标牌，传送门默认隐藏。完整 beautiful corner 中平台缩放为 0.6，模块库保持原尺寸。

关卡作者可以组合桥段、台阶和支架，按实际车宽缩放并添加自己的碰撞体。桥墩的 Deck 对接点对应桥面高度。

## 植被、岩石和装饰升级

`Prefabs/Botanical/`（相对于 Assets/CreekDiorama）包含 18 个独立模块：三种层状岩块、河石、碎石组、柔软草丛、蕨类、阔叶灌木、垂藤、苔藓边缘、雏菊、羽扇豆、分枝松树、圆冠树、灯笼、木桶、木箱和浮叶。植物叶片使用真实几何，不依赖透明裁剪贴图。

`ArtSource/CreekDiorama/CreekBotanicalKit.blend` 是可编辑源文件。`Tools/CreekDiorama/build_botanical_kit.py` 同时生成模型、石灰岩与木纹的无缝颜色/法线贴图。原始参考图没有作为贴图使用。纹理仅用于细小表面变化，岩石的大面、缺口和植物轮廓来自模型。

叶片材质使用 `Shaders/CreekFoliage.shader`，根据叶片正反面修正受光，并增加柔和背光，减少薄叶背面过黑的问题；支持 URP 阴影与立体渲染宏。模型本身没有风动画。

独立 BeautifulCorner 场景与 Prefab 使用新的分层岩壁、苔藓边缘、垂藤、花簇和分枝树冠；桥面与平台尺寸沿用原模块。`CreekBotanicalGallery.unity` 展示全部新模块。场景中 `10 Botanical dressing/Render batches` 为实际显示的合并网格；其旁边隐藏的 `Editable placement` 保留所有嵌套 Prefab 摆放。手工调整时先关闭 Render batches，再启用 Editable placement，避免重复显示。

Unity 菜单 **MetaHackthone > Creek Art > Build botanical dressing** 可从已生成 FBX 重新导入并摆放，运行前保存打开的场景。此操作覆盖本套生成的摆放；手动摆放应先另存自己的场景。基础模块重新生成时，也会保留并重新应用已安装的植物岩石套件。

最新画面：`BeautifulCorner-Botanical.png`、`Botanical-WaterfallDetail.png`、`BotanicalGallery.png`。模型和场景检查结果记录在 `Botanical-Verification.txt`；新增装饰无碰撞体和玩法脚本，尚未做头显性能测试。

## 水体材质

下列路径均相对于 Assets/CreekDiorama/：

- Shaders/CreekWater.shader、Materials/Living river.mat：流动波纹、深浅水色、移动焦散、细碎反光与岸边泡沫。
- Materials/Modular river.mat：开启 World Space，使用世界坐标计算流动和波浪，相邻水面块保持连续；同一水面保持水平和相同高度。
- Shaders/CreekWaterfall.shader、Materials/Silk waterfall.mat：向下流动的水丝与白沫。
- Shaders/CreekImpact.shader、Materials/Waterfall impact.mat：扩散落水涟漪。
- Shaders/CreekSpray.shader、Materials/Waterfall spray.mat：落水飞溅水珠。
- Shaders/CreekPortal.shader、Materials/Portal cyan.mat：可选的程序化传送门效果。

Shader 使用内置时间，不需要行为脚本；Blender 保留静态建模材质。单独瀑布模块只包含流水网格，落水泡沫和飞溅可从完整样板的水体分组中复用、按 Impact 点放置。

URP 相机需要开启 **Depth Texture** 和 **Opaque Texture**，以启用接触泡沫与轻微透底；展示场景已配置。若不能启用，将水材质的 **Camera depth and opaque textures enabled** 设为 0，仍保留基础水色、波纹和反光。

早期模块展示截图临时启用了阴影以便观察体积，捕获后恢复原管线。项目原有阴影距离为 0，因此直接打开场景时投影可能与这些截图不同。本次云层与拼装台交付的 PC HDR 管线配置见下文。材质动画曾在 Play 模式连续帧中验证，尚未做头显设备验证。

## 重新生成

1. Blender 后台运行 Tools/CreekDiorama/build_modular_kit.py，生成模块 .blend、FBX 和 module_anchors.json。
2. Unity 菜单 **MetaHackthone > Creek Art > Build independent beautiful corner and modules** 生成原生模块网格、20 个 Prefab、独立样板与陈列场景。先保存打开的场景。
3. 批处理入口 CreekDiorama.Editor.CreekArtKitAuthoring.BuildAndCapture 生成并输出截图，结束时退出 Editor。

这些命令会覆盖本套生成资产，手工调整前请复制为自己的文件。原始完整沙盘仍使用 build_creek.py 与 CreekEnvironmentImporter 工作流；无需为摆放模块重新生成原始沙盘。

模型和配色由本项目脚本生成。云的主形状纹理由内置 imagegen 生成，提示词见 CloudTexture-Prompt.txt；外围噪声使用用户提供的 Luos Noise V2 纹理包中的 T_Lu_Noise_12 与 T_Lu_Noise_01，已复制到项目的 Textures/Clouds 下。

## 验证与现有项目状态

2026-11-02：Unity 6000.3.21f1 中生成 20 个模块，检查无 Collider、无运行时行为、无缺失材质、无玩法资产依赖；确认标准直桥宽 0.40 m、FBX 坐标正确、拼车平台为起点平台变体。记录见 ArtKit-Verification.txt。

2026-11-03：上传前更新到 Plastic /main cs:57。队友的 DownhillSandbox 关卡保留该版本原样，美术只在独立场景中展示。旧关卡接入脚本、旧路线网格和实验截图不包含在这次交付中。

2026-11-05：上传前更新到 Plastic /main cs:68。本次交付包含上述美术目录，以及 QualitySettings 的 PC 渲染管线引用，以启用独立 BeautifulCornerPreviewPipeline 的 HDR 辉光；Mobile 配置不变。

## 写实云层和拼装台

- BeautifulCorner 场景及同名 Prefab 已包含分层 Billboard 云、透明边界、网格台面和方向标识。
- 云的主形状使用 RealisticCumulus.png，外围由 CloudPerimeterNoise.png 和 CloudWispNoise.png 控制扰动与薄雾。可在云材质中调整 Edge turbulence、Peripheral vapor 和 Noise drift。
- AssemblyBounds.prefab 是复用入口。修改根对象 Scale XYZ 可同时调整透明框和网格台面；不要单独缩放台面子对象。台面与边界使用同一单位空间范围。
- 中央箭头和圆环缩为上一版的 52%；在 Assembly tabletop 材质中调整 Direction emblem scale。
- 发光预览需要 HDR、相机 Post Processing 和 Assembly bloom VolumeProfile。PC 质量档已指向专用预览管线；移动端实际辉光与性能仍需实机验证。
- 最终预览见 BeautifulCorner-CloudNoise.png。已在三种长宽高下检查台面与边框的 XZ 范围一致，相关 Shader 编译无错误。

美术更新入口为 CreekDiorama.Editor.CreekCloudNoiseAuthoring.Apply；重新生成完整氛围可调用 CreekAtmosphereV2.Apply。调用会保存场景及 Prefab，手工摆放后应先备份。
