# 编译补丁器 PCL2Patcher.exe
#
# 依赖：
#   1. .NET Framework 4.x 自带的 csc.exe（Windows 自带）
#   2. dnlib.dll  —— NuGet 包 dnlib 4.4.0 的 lib\net45\dnlib.dll
#      下载：https://www.nuget.org/packages/dnlib
#
# 用法：把 dnlib.dll 放到本目录，然后在本目录执行
#   powershell -ExecutionPolicy Bypass -File build.ps1

$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "找不到 csc.exe：$csc" }
if (-not (Test-Path 'dnlib.dll')) { throw "当前目录缺少 dnlib.dll（NuGet: dnlib 4.4.0 的 lib/net45/dnlib.dll）" }

& $csc /nologo /codepage:65001 /target:exe /platform:anycpu `
    /out:PCL2Patcher.exe `
    /r:dnlib.dll `
    /resource:dnlib.dll `
    Bootstrap.cs PCL2Patcher.cs

if ($LASTEXITCODE -ne 0) { throw "编译失败（$LASTEXITCODE）" }
Write-Host "已生成 PCL2Patcher.exe"
