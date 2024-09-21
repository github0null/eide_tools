@echo off

set DIST_DIR="D:\Code_Project\TypeScript\eide\res\tools"
if not exist %DIST_DIR% (
    set DIST_DIR=".\dist"
)

echo.
echo =======================================
echo  Output Folder: %DIST_DIR%
echo =======================================

::echo.
::echo clean projects ...
::dotnet clean
::del /Q /S ".\dist\"

:: for win-x64
echo.
echo --- publish for win-x64 ---
echo.
dotnet publish ./eide_tools.sln -o %DIST_DIR%/win32/unify_builder^
               -c Release --no-self-contained^
               -r win-x64^
               --framework net6.0^
               -p:PublishReadyToRun=true^
               /property:GenerateFullPaths=true^
               /consoleloggerparameters:NoSummary


:: for linux-x64
echo.
echo --- publish for linux-x64 ---
echo.
dotnet publish ./eide_tools.sln -o %DIST_DIR%/linux/unify_builder^
               -c Release --no-self-contained^
               -r linux-x64^
               --framework net6.0^
               /property:GenerateFullPaths=true^
               /consoleloggerparameters:NoSummary


:: for osx-x64
echo.
echo --- publish for osx-x64 ---
echo.
dotnet publish ./eide_tools.sln -o %DIST_DIR%/darwin/unify_builder/x86_64^
               -c Release --no-self-contained^
               -r osx-x64^
               --framework net6.0^
               /property:GenerateFullPaths=true^
               /consoleloggerparameters:NoSummary

:: for osx-arm64
echo.
echo --- publish for osx-arm64 ---
echo.
dotnet publish ./eide_tools.sln -o %DIST_DIR%/darwin/unify_builder/arm64^
               -c Release --no-self-contained^
               -r osx-arm64^
               --framework net6.0^
               /property:GenerateFullPaths=true^
               /consoleloggerparameters:NoSummary


echo.
echo all done !
echo.