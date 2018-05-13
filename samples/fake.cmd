@echo off
REM Install .NET Core (https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script)
REM @powershell -NoProfile -ExecutionPolicy unrestricted -Command "&([scriptblock]::Create((Invoke-WebRequest -useb 'https://dot.net/v1/dotnet-install.ps1'))) -Channel Current"
REM SET PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%
dotnet restore dotnet-fake.csproj -v:quiet
dotnet fake run %*