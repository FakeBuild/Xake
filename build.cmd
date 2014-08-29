@echo off

if exist "%VS110COMNTOOLS%vsvars32.bat" (
  @call "%VS110COMNTOOLS%vsvars32.bat"
  goto build
)

if exist "%VS100COMNTOOLS%vsvars32.bat" (
  @call "%VS100COMNTOOLS%vsvars32.bat"
  goto build
)

echo Requires VS2012 or VS2010 to be installed
goto exit

:build
msbuild xake.sln /t:Rebuild /p:Configuration=Debug

:exit