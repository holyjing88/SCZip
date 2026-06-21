# SCZip

Unity 跨平台压缩文件管理器，界面与交互参考 WinZip Mobile。

## 快速开始

1. 使用 **Unity 2022.3 LTS**（已在 `2022.3.62f3c1` 验证）打开本目录
2. 菜单 **SCZip → Setup Main Scene** 修复场景（或运行下方批处理命令）
3. 打开 `Assets/_SCZip/Scenes/Main.unity`，Hierarchy 中应有 **UICanvas**（顶层 Canvas）和 **SCZipApp**
4. 点击 Play — 应看到蓝色 Storage 主界面（uGUI Canvas）

若场景组件显示 Missing Script，菜单 **SCZip → Setup Main Scene**，或批处理：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath "$(pwd)" \
  -executeMethod SCZip.Editor.SceneBootstrapper.SetupMainScene
```

## 自动化测试（批处理）

```bash
# 编译 + 文件系统/ZIP/TAR.GZ/场景 冒烟测试
Unity -batchmode -nographics -quit -projectPath . \
  -executeMethod SCZip.Editor.SmokeTestRunner.Run
# 期望日志: [SCZip] SMOKE_PASS all checks OK

# Play Mode UI 验收（uGUI Canvas）
Unity -batchmode -nographics -projectPath . \
  -executeMethod SCZip.Editor.PlayModeSmokeRunner.Run
# 期望: Logs/playmode-result.txt 内容为 PASS
```

首次运行会在 `Application.persistentDataPath/SCZip/` 下创建沙盒目录，并生成示例 `Welcome.txt`。

## 当前实现（MVP v0.1）

- **App Shell**：左侧抽屉、AppBar、面包屑、文件列表、底栏操作（**uGUI Canvas**）
- **导航**：Recent / My Files / Storage / Photos / Music / Settings
- **文件操作**：多选、新建文件夹、重命名、删除
- **压缩**：ZIP、TAR.GZ 创建；ZIP、TAR.GZ 解压与包内浏览
- **架构**：MVVM + `IArchiveProvider` 服务层

## 文档

- [策划文档](docs/SCZip-策划文档.md)
- [界面布局设计](docs/SCZip-界面布局设计.md)

## 项目结构

```
Assets/_SCZip/
├── Scenes/Main.unity
├── Scripts/
│   ├── UI/             # AppShellView, AppShellController (uGUI)
│   ├── Core/           # Bootstrap, AppServices, NavigationStack
│   ├── Domain/         # FileEntry, enums
│   ├── Services/       # 接口
│   ├── Infrastructure/ # ZIP/TAR.GZ providers, 文件系统
│   ├── ViewModels/     # FileBrowserViewModel
│   └── UI/             # AppShellView, AppShellController (uGUI)
├── Editor/             # UguiSceneBuilder, SceneBootstrapper, tests
└── SCZip.asmdef
```

## 开发说明

- 数据目录（Editor）：`~/Library/Application Support/DefaultCompany/SCZip/`（macOS）
- Pro 功能门控：`FeatureGate.IsPro = true` 可解锁 TAR.GZ 创建等
- 待迭代：7Z/RAR、预览、云存储、Android SAF 桥接
