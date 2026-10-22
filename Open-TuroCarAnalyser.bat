@echo off
setlocal
set "ROOT=%~dp0"
set "DOTNET_CLI_HOME=%ROOT%"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "APPDATA=%ROOT%.appdata\Roaming"
set "LOCALAPPDATA=%ROOT%.appdata\Local"
set "NUGET_PACKAGES=%ROOT%.nuget\packages"

if not exist "%APPDATA%" mkdir "%APPDATA%"
if not exist "%LOCALAPPDATA%" mkdir "%LOCALAPPDATA%"
if not exist "%NUGET_PACKAGES%" mkdir "%NUGET_PACKAGES%"

dotnet run --project "%ROOT%TuroCarAnalyser\TuroCarAnalyser.csproj"

if errorlevel 1 (
    echo.
    echo The app did not start successfully.
    pause
)

endlocal
