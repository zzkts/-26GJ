param(
    [string]$ProjectRoot = 'D:\114514\GitHub\-26GJ\京喜造梦间26GJ',
    [string]$BuildDir = 'D:\gj_build',
    [switch]$SkipBuild,
    [switch]$InPlace
)

# 一条命令走完：全量编译校验 → 打包 Windows 播放器 → 在真实运行时里跑无人值守自检。
#
# 如果主工程正被 Unity 编辑器打开（Temp\UnityLockfile 被占），无头打包拿不到工程锁，
# 这时自动改用一个独立目录（默认 D:\gj_build，短 ASCII 路径）来打包，
# 再把产物拷回主工程的 Build\Windows。这样"开着编辑器调美术"和"命令行验证"互不打扰。
#
# 用 -InPlace 可以强制在工程本体里打包（需要先关掉编辑器）。

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Output "================ 1/3 编译与内核断言 ================"
$verify = [ScriptBlock]::Create((Get-Content (Join-Path $ProjectRoot '_build\verify\run-verify.ps1') -Raw -Encoding UTF8))
& $verify -ProjectRoot $ProjectRoot
$compileExit = $global:verifyExit
if ($compileExit -ne 0) {
    Write-Output "编译或内核断言失败，停止。"
    exit $compileExit
}

if ($SkipBuild) {
    Write-Output "（已跳过打包与运行时自检）"
    exit 0
}

# ---------------------------------------------------------------- 选一个能拿到锁的工程来打包
$buildRoot = $ProjectRoot
$locked = (-not $InPlace) -and ((Test-Path (Join-Path $ProjectRoot 'Temp\UnityLockfile')) -or (Get-Process -Name Unity -ErrorAction SilentlyContinue))

if ($locked) {
    $buildRoot = $BuildDir
    Write-Output "检测到主工程正被 Unity 编辑器打开 → 改用独立构建目录：$buildRoot"

    if (-not (Test-Path $buildRoot)) {
        Write-Output "构建目录不存在，从工程拷一份干净的最小副本（首次会重建 Library，之后都有缓存）。"
        New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null
        foreach ($p in @('Assets', 'Packages', 'ProjectSettings')) {
            Copy-Item (Join-Path $ProjectRoot $p) (Join-Path $buildRoot $p) -Recurse -Force
        }
    }

    # 每次只同步源码与关键设置，Library 留着做增量编译
    foreach ($p in @('Assets\Scripts', 'Assets\Editor')) {
        $dst = Join-Path $buildRoot $p
        $src = Join-Path $ProjectRoot $p
        if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
        Copy-Item $src $dst -Recurse -Force
    }

    Copy-Item (Join-Path $ProjectRoot 'ProjectSettings\ProjectSettings.asset') `
              (Join-Path $buildRoot 'ProjectSettings\ProjectSettings.asset') -Force
    Copy-Item (Join-Path $ProjectRoot 'Packages\manifest.json') `
              (Join-Path $buildRoot 'Packages\manifest.json') -Force

    # 已归档的旧版不需要拷过去（它以 ~ 结尾，Unity 本来就忽略）
    $legacy = Join-Path $buildRoot 'Assets\Legacy借你一秒~'
    if (Test-Path $legacy) { Remove-Item $legacy -Recurse -Force }
}

Write-Output "================ 2/3 打包播放器 ================"
Write-Output "打包工程：$buildRoot"
$unity = 'D:\Unity6\Editor\Unity.exe'
$buildLog = Join-Path $ProjectRoot '_build\build26.log'
if (Test-Path $buildLog) { Remove-Item $buildLog -Force }
$bp = Start-Process -FilePath $unity -PassThru -Wait -ArgumentList @(
    '-batchmode', '-quit', '-nographics',
    '-projectPath', $buildRoot,
    '-logFile', $buildLog,
    '-buildTarget', 'StandaloneWindows64',
    '-executeMethod', 'CuoGuo.EditorTools.BuildScript.PrepareAndBuildWindows'
)
Write-Output "Unity 打包退出码 = $($bp.ExitCode)"
$built = Select-String -Path $buildLog -Pattern '打包成功|打包失败' -Encoding UTF8 | Select-Object -First 1
if ($built) { Write-Output ("    " + $built.Line) }

if ($bp.ExitCode -ne 0) {
    Write-Output "打包失败。日志里最后几行："
    Get-Content $buildLog -Tail 15 -Encoding UTF8 | ForEach-Object { Write-Output ("    " + $_) }
    exit $bp.ExitCode
}

# ---------------------------------------------------------------- 把产物拷回主工程
$exeSrc = Join-Path $buildRoot 'Build\Windows'
$exeDst = Join-Path $ProjectRoot 'Build\Windows'
if ($buildRoot -ne $ProjectRoot) {
    try {
        New-Item -ItemType Directory -Force -Path $exeDst | Out-Null
        Copy-Item (Join-Path $exeSrc '*') $exeDst -Recurse -Force
        Write-Output "已把打包产物拷回：$exeDst"
    } catch {
        Write-Output "拷回产物失败（可能播放器正在运行）：$($_.Exception.Message)"
        $exeDst = $exeSrc
        Write-Output "改用副本的产物继续自检：$exeDst"
    }
}

Write-Output "================ 3/3 运行时自检 ================"
$exe = Join-Path $exeDst 'CuoGuoNaYiMiao.exe'
$checkLog = Join-Path $ProjectRoot '_build\selfcheck.log'
if (Test-Path $checkLog) { Remove-Item $checkLog -Force }
$pp = Start-Process -FilePath $exe -PassThru -Wait -ArgumentList @(
    '-batchmode', '-nographics', '-cuoguo-selfcheck', '-logFile', $checkLog
)
Write-Output "播放器退出码 = $($pp.ExitCode)"
if (Test-Path $checkLog) {
    Get-Content $checkLog -Encoding UTF8 | Select-String -Pattern 'SELFCHECK|Exception|NullReferenceException' | ForEach-Object { $_.Line }
}
exit $pp.ExitCode
