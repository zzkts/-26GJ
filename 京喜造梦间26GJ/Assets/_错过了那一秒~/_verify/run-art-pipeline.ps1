param(
    [string]$ProjectRoot = 'D:\114514\GitHub\-26GJ\京喜造梦间26GJ',
    [string]$BuildDir = 'D:\gj_build'
)

# 跑美术管线：抠图 / 修补烘焙 UI / 自动裁边 / 设置导入参数。
#
# 主工程被 Unity 编辑器占用时（无头实例拿不到工程锁），改在独立的构建目录里跑，
# 跑完把产物（含 .meta，导入设置在里面）拷回主工程——这样编辑器重新导入后也是 Sprite。

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$locked = (Test-Path (Join-Path $ProjectRoot 'Temp\UnityLockfile')) -or (Get-Process -Name Unity -ErrorAction SilentlyContinue)
$work = $ProjectRoot
if ($locked) { $work = $BuildDir; Write-Output "主工程被编辑器占用 → 在 $work 里跑美术管线" }

if ($work -ne $ProjectRoot) {
    # 同步源码、编辑器脚本与源图
    foreach ($p in @('Assets\Scripts', 'Assets\Editor', 'Assets\Art\Source_Keep')) {
        $dst = Join-Path $work $p
        $src = Join-Path $ProjectRoot $p
        if (-not (Test-Path $src)) { continue }
        if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
        New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
        Copy-Item $src $dst -Recurse -Force
    }
    Copy-Item (Join-Path $ProjectRoot 'ProjectSettings\ProjectSettings.asset') `
              (Join-Path $work 'ProjectSettings\ProjectSettings.asset') -Force
}

$log = Join-Path $ProjectRoot '_build\art.log'
if (Test-Path $log) { Remove-Item $log -Force }

$unity = 'D:\Unity6\Editor\Unity.exe'
$bp = Start-Process -FilePath $unity -PassThru -Wait -ArgumentList @(
    '-batchmode', '-quit', '-nographics',
    '-projectPath', $work,
    '-logFile', $log,
    '-executeMethod', 'CuoGuo.EditorTools.ArtPipeline.RunAll'
)
Write-Output "Unity 退出码 = $($bp.ExitCode)"

Write-Output "==== 美术管线日志 ===="
Get-Content $log -Encoding UTF8 | Select-String -Pattern '\[美术管线\]|error CS|Exception|✗' | ForEach-Object { $_.Line }

# ---- 把产物拷回主工程（含 .meta，导入设置随之保留）----
if ($work -ne $ProjectRoot) {
    foreach ($p in @('Assets\Art\Processed', 'Assets\Resources\Art')) {
        $src = Join-Path $work $p
        if (-not (Test-Path $src)) { continue }
        $dst = Join-Path $ProjectRoot $p
        # 目标目录若已存在，必须先删——否则 Copy-Item 会把源目录整个套进目标里
        # 变成 Processed\Processed（踩过一次）
        if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
        New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
        Copy-Item $src $dst -Recurse -Force
        Write-Output "已拷回：$p"
    }
    $rep = Join-Path $work '_doc\ART_ASSET_AUDIT.md'
    if (Test-Path $rep) { Copy-Item $rep (Join-Path $ProjectRoot '_doc\ART_ASSET_AUDIT.md') -Force; Write-Output "已拷回：_doc\ART_ASSET_AUDIT.md" }
}

exit $bp.ExitCode
