param(
    [string]$ProjectRoot = 'D:\114514\GitHub\-26GJ\京喜造梦间26GJ',
    [string]$BuildDir = 'D:\gj_build'
)

# 为《借你一秒》旧版出包。
#
# 主工程被 Unity 编辑器占着（拿不到工程锁），所以在独立构建目录里跑：
# 把旧版的 Assets 镜像过去 → 用它自己的 ProjectSetup 生成入口场景 → 用它自己的 BuildScript 打包
# → 把产物拷回主工程的 Build/Windows。
#
# 注意：新版《错过了那一秒》的 Assets 在 Assets\_错过了那一秒~ 里，Unity 会忽略，
# 所以这里镜像时排除掉它和 Art（旧版用不到那 87 MB 素材）。

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Output "==== 1/4 同步旧版 Assets 到构建目录 ===="
New-Item -ItemType Directory -Force -Path "$BuildDir\Assets" | Out-Null
$null = robocopy "$ProjectRoot\Assets" "$BuildDir\Assets" /MIR /XD "Art" "_错过了那一秒~" /NFL /NDL /NJH /NJS /NP
$scriptCount = (Get-ChildItem "$BuildDir\Assets\Scripts" -Recurse -Filter *.cs -ErrorAction SilentlyContinue).Count
$editorCount = (Get-ChildItem "$BuildDir\Assets\Editor" -Recurse -Filter *.cs -ErrorAction SilentlyContinue).Count
Write-Output "构建目录里的脚本数：$scriptCount（编辑器 $editorCount）"

Copy-Item "$ProjectRoot\ProjectSettings\ProjectSettings.asset" "$BuildDir\ProjectSettings\ProjectSettings.asset" -Force
Copy-Item "$ProjectRoot\Packages\manifest.json" "$BuildDir\Packages\manifest.json" -Force

$unity = 'D:\Unity6\Editor\Unity.exe'
$log = "$ProjectRoot\_build\old-scene.log"
if (Test-Path $log) { Remove-Item $log -Force }

Write-Output "==== 2/4 用旧版的 ProjectSetup 生成入口场景 ===="
$p1 = Start-Process -FilePath $unity -PassThru -Wait -ArgumentList @(
    '-batchmode', '-quit', '-nographics', '-projectPath', $BuildDir, '-logFile', $log,
    '-executeMethod', 'JieNiYiMiao.EditorTools.ProjectSetup.SetupScene')
Write-Output "退出码 = $($p1.ExitCode)"
Get-Content $log -Encoding UTF8 | Select-String -Pattern "借你一秒|error CS" | Select-Object -First 5 | ForEach-Object { "    " + $_.Line }

Write-Output "==== 3/4 打包 ===="
$log2 = "$ProjectRoot\_build\old-build.log"
if (Test-Path $log2) { Remove-Item $log2 -Force }
$p2 = Start-Process -FilePath $unity -PassThru -Wait -ArgumentList @(
    '-batchmode', '-quit', '-nographics', '-projectPath', $BuildDir, '-logFile', $log2,
    '-buildTarget', 'StandaloneWindows64',
    '-executeMethod', 'JieNiYiMiao.EditorTools.BuildScript.BuildWindows')
Write-Output "退出码 = $($p2.ExitCode)"
Get-Content $log2 -Encoding UTF8 | Select-String -Pattern "打包成功|打包失败|error CS" | ForEach-Object { "    " + $_.Line }

if ($p2.ExitCode -ne 0) { exit $p2.ExitCode }

Write-Output "==== 4/4 拷回产物 ===="
$dst = "$ProjectRoot\Build\Windows_借你一秒"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item "$BuildDir\Build\Windows\*" $dst -Recurse -Force
Write-Output "产物目录：$dst"
Get-ChildItem $dst -Filter *.exe | ForEach-Object { "    " + $_.Name + "  " + [math]::Round($_.Length/1KB,0) + " KB" }
