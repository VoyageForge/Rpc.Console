#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Clean,
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'
$PackageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $PackageRoot 'CodeGen~\VoyageForge.Console.CodeGen\VoyageForge.Console.CodeGen.csproj'
$BuiltDll = Join-Path $PackageRoot 'CodeGen~\VoyageForge.Console.CodeGen\bin\Release\netstandard2.0\VoyageForge.Console.CodeGen.dll'
$AnalyzerDirectory = Join-Path $PackageRoot 'Runtime\Analyzers'
$TargetDll = Join-Path $AnalyzerDirectory 'VoyageForge.Console.CodeGen.dll'
$TargetMeta = $TargetDll + '.meta'

$buildLog = Join-Path ([IO.Path]::GetTempPath()) ('Rpc.Console-CodeGen-{0}.log' -f [guid]::NewGuid().ToString('N'))
$transcribing = $false
try {
    Start-Transcript -LiteralPath $buildLog -ErrorAction Stop | Out-Null
    $transcribing = $true
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '未找到 dotnet SDK。' }
    if (-not (Test-Path -LiteralPath $Project)) { throw "找不到项目：$Project" }

    if ($Clean) {
        & dotnet clean $Project --configuration Release --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet clean failed ($LASTEXITCODE)" }
    }

    Write-Host '========== Build VoyageForge.Console CodeGen =====================' -ForegroundColor Cyan
    $previous = $ErrorActionPreference
    $exitCode = -1
    try {
        $ErrorActionPreference = 'Continue'
        & dotnet build $Project --configuration Release --verbosity normal --nologo
        $exitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previous }

    if ($exitCode -ne 0) { throw "Console Source Generator 编译失败，dotnet exit code: $exitCode。" }
    if (-not (Test-Path -LiteralPath $BuiltDll)) { throw "没有找到预期 DLL：$BuiltDll" }
    Write-Host "[产物] $BuiltDll" -ForegroundColor Green

    New-Item -ItemType Directory -Path $AnalyzerDirectory -Force | Out-Null
    Copy-Item -LiteralPath $BuiltDll -Destination $TargetDll -Force
    $template = Join-Path $PackageRoot 'CodeGen~\Templates\VoyageForge.Console.CodeGen.dll.meta.template'
    if (-not (Test-Path -LiteralPath $template)) { throw "找不到 meta 模板：$template" }
    if (-not (Test-Path -LiteralPath $TargetMeta)) {
        $meta = (Get-Content -LiteralPath $template -Raw).Replace('__GUID__', [guid]::NewGuid().ToString('N'))
        [System.IO.File]::WriteAllText($TargetMeta, $meta)
    }
    Write-Host "[成功] DLL：$TargetDll" -ForegroundColor Green
    Write-Host "[成功] META：$TargetMeta" -ForegroundColor Green
    Write-Host '请在 Unity Inspector 中确认 DLL 的 Asset Label 为 RoslynAnalyzer。' -ForegroundColor Yellow
}
catch {
    Write-Host ("[失败] " + $_.Exception.Message) -ForegroundColor Red
    if ($transcribing) { Write-Host "构建日志：$buildLog" }
    throw
}
finally {
    if ($transcribing) { Stop-Transcript | Out-Null }
    if (-not $NoPause) { [void](Read-Host '按 Enter 键关闭窗口') }
}