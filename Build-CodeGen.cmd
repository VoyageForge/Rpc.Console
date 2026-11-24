@echo off
setlocal
cd /d "%~dp0"

rem ===========================================================================
rem  Double-click entry: build the VoyageForge.Console source generator and
rem  deploy it to Runtime/Analyzers.
rem
rem  Why this .cmd exists:
rem    Build-CodeGen.ps1 lives in the repo/archive and therefore carries a
rem    Zone.Identifier (downloaded-file marker). Windows default policy
rem    "RemoteSigned" rejects unsigned scripts that carry that marker, and the
rem    PowerShell window closes immediately -> the "flash exit" you saw.
rem    Launching with -ExecutionPolicy Bypass skips the signature check; the
rem    script is right here in the package and its content is reviewable.
rem
rem  NOTE: keep this file ASCII-only. cmd.exe parses .cmd with the active
rem  code page, so non-ASCII comments can break parsing before chcp takes
rem  effect.
rem ===========================================================================

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-CodeGen.ps1" -NoPause %*
set "EXITCODE=%ERRORLEVEL%"

echo.
if not "%EXITCODE%"=="0" (
    echo [FAILED] build script exit code %EXITCODE%
) else (
    echo [OK] build script exit code 0
)

echo.
pause
exit /b %EXITCODE%
