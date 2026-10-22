[![](https://img.shields.io/badge/powered_by-dsh-4D6BFE?style=flat-square&logo=deepseek&logoColor=white)](https://github.com/deepseek-ai/deepseek-harness)

# PCL2 主题解锁

解锁 PCL 启动器的全部主题，并关闭自动更新与公告拉取。

提供两种用法：**打补丁**（推荐，保留你自己那份 exe）或**直接用成品 exe**。

## 改了什么

- 解锁全部主题（含隐藏主题），去掉灰色遮罩层与赞助/等级校验
- 主题配色与标题栏渐变按 PCL 2.14.0.0 的官方色表还原
- 移除自动更新、自动获取公告与联网配置（保留本地缓存读取）
- 「检查更新」按钮改为提示文字

## 关于版本号

PCL 正式版安装包里的 exe，其 `AssemblyVersion` 可能**低于**发布号。例如 `PCL-2.14.0.0.zip` 里 `最新正式版.zip` 解出来的 `Plain Craft Launcher 2.exe`：

| 项 | 值 |
|---|---|
| AssemblyVersion | 2.13.1.1 |
| SHA256 | `30EB93C62FC51EDE0898648E67455478A3D5A4F279938AE65AEC77B84CB9703F` |
| 界面/日志显示 | 正式版 2.13.1.1 (408) |

补丁器**不再硬卡版本号**，改为结构校验：检查目标里是否存在补丁需要的类型与方法，缺失的补丁点会自动跳过并提示。因此 2.13.1.1 与 2.14.0.0 安装包内附带的这个 exe 都可以直接打。

## 用法一：补丁器（推荐）

1. 关闭 PCL
2. 把 `PCL2Patcher.exe` 放进 PCL 目录（和 `Plain Craft Launcher 2.exe` 同级），双击
3. 它会自动定位同目录的 exe、备份为 `*.bak_日期时间`、写入补丁

也可以把 `Plain Craft Launcher 2.exe` 直接拖到补丁器上；或用命令行：

```
PCL2Patcher.exe "D:\某个目录\Plain Craft Launcher 2.exe"
```

已经打过补丁的文件会自动跳过；结构不匹配时加 `-f` 可强制继续（缺失点跳过）。

## 用法二：直接覆盖

备份你原来的 `Plain Craft Launcher 2.exe`，用仓库里的同名文件覆盖，然后启动。

## 文件信息

| 文件 | 大小 | SHA256 |
|---|---|---|
| `PCL2Patcher.exe` | 1,203,712 | `C2B23D6E2489185EF7B171E42FA11A6345DC3D3FEE80D6A7C85D362A4A6FEC4F` |
| `Plain Craft Launcher 2.exe` | 5,738,496 | `7E9EE68114C96D25702C4DD8A870ED07FA0CAB0C915BDE14FA27BD2D8FD3A069` |

原版与补丁后的 exe 均未签名。

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
