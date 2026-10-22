# PCL2 主题解锁

基于 **PCL2 2.13.1.1（正式版 408）**：解锁全部启动器主题，关闭自动更新与公告拉取。

提供两种用法：**打补丁**（推荐，保留你自己那份 exe）或**直接用成品 exe**。

## 改了什么

- 解锁全部主题（含隐藏主题），去掉灰色遮罩层与赞助/等级校验
- 主题配色与标题栏渐变按 **PCL 2.14.0.0** 的官方色表还原
- 移除自动更新、自动获取公告与联网配置（保留本地缓存读取）
- 「检查更新」按钮改为提示文字

## 用法一：补丁器（推荐）

1. 关闭 PCL
2. 把 `PCL2Patcher.exe` 放进 PCL 目录（和 `Plain Craft Launcher 2.exe` 同级），双击
3. 它会自动定位同目录的 exe、校验版本、备份为 `*.bak_日期时间`、写入补丁

也可以把 `Plain Craft Launcher 2.exe` 直接拖到补丁器上；或用命令行：

```
PCL2Patcher.exe "D:\某个目录\Plain Craft Launcher 2.exe"
```

版本不符时会拒绝执行，加 `-f` 可强制。已经打过补丁的文件会自动跳过。

## 用法二：直接覆盖

备份你原来的 `Plain Craft Launcher 2.exe`，用仓库里的同名文件覆盖，然后启动。

## 文件信息

| 文件 | 大小 | SHA256 |
|---|---|---|
| `PCL2Patcher.exe` | 1,200,128 | `111831009AEA0A1546AF0462D837DA99E85725BBEA28D22772696A5D0A291E06` |
| `Plain Craft Launcher 2.exe` | 5,738,496 | `7E9EE68114C96D25702C4DD8A870ED07FA0CAB0C915BDE14FA27BD2D8FD3A069` |

原版为 2.13.1.1 (408)，未签名；补丁后的 exe 同样是未签名文件。

## 源码

补丁器源码在 [`src/`](src)：

| 文件 | 说明 |
|---|---|
| `src/PCL2Patcher.cs` | 全部补丁逻辑：11 处 IL 改动、每主题配色表、标题栏渐变表 |
| `src/Bootstrap.cs` | 启动引导，把内嵌的 dnlib.dll 交给运行时加载 |
| `src/build.ps1` | 编译脚本 |

编译方式：把 `dnlib.dll`（NuGet 包 `dnlib` 4.4.0 里的 `lib/net45/dnlib.dll`）放进 `src/`，然后执行

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

## 说明

- 只改了 11 处 IL，其余字节与原版一致；内嵌的依赖程序集（MeloongCore / PCLCS 等）完全未改动
- 补丁器只读写本地文件，不联网、不上传任何数据
- 觉得 PCL 好用请支持作者：<https://afdian.net/a/LTCat>
- 仅供学习交流，使用风险自负

## 还原

用备份文件（`*.bak_*`）或你自己的原版 exe 覆盖回去即可。

[![](https://img.shields.io/badge/powered_by-dsh-4D6BFE?style=flat-square&logo=deepseek&logoColor=white)](https://github.com/deepseek-ai/deepseek-harness)
