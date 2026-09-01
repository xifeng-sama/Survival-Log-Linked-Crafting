# Survival Log Linked Crafting

《Survival Log》制作台仓储制作系统。

在 MC 里用惯了 AE2，回到家做东西还要挨个翻柜子找材料，实在有点受不了，于是做了这个 Mod。

## 功能

- 保留原版工作台，不影响手动制作。
- 已解锁配方旁会出现“仓储”按钮。
- 点击后直接搜索家中有效储物家具。
- 找到的材料会显示在下方仓储制作区。
- 材料不足时仍会显示配方，缺少项会标红并注明数量。
- 选配方时不会移动物品，确认制造后才从储物家具中消耗材料。

## 安装

### 完整包

1. 退出游戏。
2. 在 [Releases](https://github.com/xifeng-sama/Survival-Log-Linked-Crafting/releases) 下载 `SurvivalLog_LinkedCrafting_v1.2.4.zip`。
3. 将压缩包内的文件直接解压到游戏根目录。
4. 启动游戏，在工作台配方右侧点击“仓储”。

完整包已经包含 BepInEx 6 IL2CPP，只带本 Mod，不包含其他功能 Mod。

### 已安装 BepInEx

也可以只下载 `SurvivalLog.LinkedCrafting.dll`，放到：

```text
BepInEx\plugins\SurvivalLogLinkedCrafting\SurvivalLog.LinkedCrafting.dll
```

## 版本

- Mod：`1.2.4`
- 已验证游戏版本：`1.0.15369`

游戏更新后，原生接口可能发生变化。遇到无法使用的情况，请附上 `BepInEx\LogOutput.log` 反馈。
