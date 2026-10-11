@echo off
chcp 65001 >nul
setlocal
set PROJECT=%~dp0..
set WORK=D:\114514\_uverify
echo ============================================
echo  《借你一秒》一键校验
echo  1) 全量编译 Assets\Scripts 下所有源码
echo  2) 编译脱离引擎的流程内核校验程序
echo  3) 运行 23 组断言
echo ============================================
powershell -NoProfile -Command "$c = Get-Content '%PROJECT%\_build\verify\run-verify.ps1' -Raw -Encoding UTF8; Invoke-Expression $c; exit $global:verifyExit"
echo.
echo 退出码 %ERRORLEVEL% （0 = 全部通过）
pause
