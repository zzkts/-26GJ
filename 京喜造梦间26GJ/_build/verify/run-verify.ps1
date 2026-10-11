param(
    [string]$ProjectRoot = 'D:\114514\GitHub\-26GJ\京喜造梦间26GJ',
    [string]$WorkDir = 'D:\114514\_uverify',
    [switch]$SkipFull
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$dotnet = 'D:\Unity6\Editor\Data\NetCoreRuntime\dotnet.exe'
$csc    = 'D:\Unity6\Editor\Data\DotNetSdkRoslyn\csc.dll'
$netstd = 'D:\Unity6\Editor\Data\NetStandard\ref\2.1.0\netstandard.dll'
$mgDir  = 'D:\Unity6\Editor\Data\Managed'
$uiDir  = 'D:\Unity6\Editor\Data\Managed\UnityEngine'
$uiDll  = 'D:\Unity6\Editor\Data\Resources\PackageManager\ProjectTemplates\libcache\com.unity.template.2d-cross-platform-2d-6.1.2\ScriptAssemblies\UnityEngine.UI.dll'

$gameSources = @(Get-ChildItem -Path (Join-Path $ProjectRoot 'Assets\Scripts') -Recurse -Filter '*.cs' | Select-Object -ExpandProperty FullName)

# 编辑器脚本也要一起编译。
# 之前只扫 Assets\Scripts，结果 SceneBaker.cs 少了一个 using 拖了十分钟才在 Unity 打包时暴露——
# 一次漏编译的代价远高于多编译这十几个文件。
$editorSources = @(Get-ChildItem -Path (Join-Path $ProjectRoot 'Assets\Editor') -Recurse -Filter '*.cs' | Select-Object -ExpandProperty FullName)

# 连续时间轴内核：这四个文件刻意不引用 UnityEngine，可以脱离引擎独立编译与运行
$coreDir = Join-Path $ProjectRoot 'Assets\Scripts\Core'
$coreSources = @(
    (Join-Path $coreDir 'TimelineModel.cs'),
    (Join-Path $coreDir 'DebtModel.cs'),
    (Join-Path $coreDir 'StoryData.cs'),
    (Join-Path $coreDir 'GameFlow.cs')
)
$program = Join-Path $ProjectRoot '_build\verify\Program.cs'

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

function Invoke-Csc {
    param([string[]]$Arguments, [string]$StdOut, [string]$StdErr)
    $p = Start-Process -FilePath $dotnet -ArgumentList $Arguments -PassThru -Wait -RedirectStandardOutput $StdOut -RedirectStandardError $StdErr
    return $p.ExitCode
}

# ---------------------------------------------------------------- 1. 全量编译（含视图层）
$fullExit = 0
if (-not $SkipFull) {
    $refs = New-Object System.Collections.Generic.List[string]
    $refs.Add('/r:' + $uiDll)
    foreach ($d in @($mgDir, $uiDir)) {
        foreach ($f in Get-ChildItem -LiteralPath $d -Filter '*.dll') { $refs.Add('/r:' + $f.FullName) }
    }

    $fullArgs = @($csc, '/noconfig', '/nostdlib+', '/target:library', '/nologo', '/langversion:9.0',
        '/nowarn:0169,0649,0414,0067,0108,0618', "/out:$WorkDir\full.dll",
        "/r:$netstd", '/utf8output') + $refs + $gameSources + $editorSources
    $fullExit = Invoke-Csc -Arguments $fullArgs -StdOut "$WorkDir\full.out" -StdErr "$WorkDir\full.err"
    Write-Output "[1/3] 全量编译（运行时 $($gameSources.Count) 个 + 编辑器 $($editorSources.Count) 个源文件 + 真实 UnityEngine/UnityEditor 程序集）退出码 = $fullExit"
    $fullOut = Get-Content "$WorkDir\full.out" -Encoding UTF8 -ErrorAction SilentlyContinue
    if ($fullOut) { $fullOut | Select-Object -First 40 | ForEach-Object { Write-Output "    $_" } }
    if ($fullExit -ne 0) { $global:verifyExit = 1; return }
} else {
    Write-Output "[1/3] 已跳过全量编译（-SkipFull）"
}

# ---------------------------------------------------------------- 2. 纯内核校验程序
$coreArgs = @($csc, '/noconfig', '/nostdlib+', '/target:exe', '/nologo', '/langversion:9.0',
    '/nowarn:0169,0649,0414,0067', "/out:$WorkDir\verify.exe",
    "/r:$netstd", '/utf8output') + $coreSources + @($program)
$coreExit = Invoke-Csc -Arguments $coreArgs -StdOut "$WorkDir\csc.out" -StdErr "$WorkDir\csc.err"
Write-Output "[2/3] 内核校验程序编译退出码 = $coreExit"
$coreOut = Get-Content "$WorkDir\csc.out" -Encoding UTF8 -ErrorAction SilentlyContinue
if ($coreOut) { $coreOut | Select-Object -First 40 | ForEach-Object { Write-Output "    $_" } }
if ($coreExit -ne 0) { $global:verifyExit = 1; return }

# ---------------------------------------------------------------- 3. 运行校验
$runtimeConfig = @'
{
  "runtimeOptions": {
    "tfm": "net6.0",
    "framework": { "name": "Microsoft.NETCore.App", "version": "6.0.0" },
    "configProperties": { "System.Reflection.Metadata.MetadataUpdater.IsSupported": false }
  }
}
'@
Set-Content -LiteralPath "$WorkDir\verify.runtimeconfig.json" -Value $runtimeConfig -Encoding UTF8

Push-Location $WorkDir
& $dotnet "$WorkDir\verify.exe" *>&1 | ForEach-Object { Write-Output $_ }
$runExit = $LASTEXITCODE
Pop-Location
Write-Output "[3/3] 校验运行退出码 = $runExit"
$global:verifyExit = $runExit
