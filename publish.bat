@echo off

::echo.
::echo clean projects ...
::dotnet clean
del /Q /S ".\dist\"

:: for win-x64
echo.
echo --- publish for win-x64 ---
echo.
dotnet publish ./eide_tools.sln -o ./dist/win-x64^
               -c Release --no-self-contained^
               --os win --arch x64^
               --framework net6.0^
               -p:PublishReadyToRun=true^
               /property:GenerateFullPaths=true^
               /consoleloggerparameters:NoSummary


:: for linux-x64
echo.
echo --- publish for linux-x64 ---
echo.
dotnet publish ./eide_tools.sln -o ./dist/linux-x64^
               -c Release --no-self-contained^
               --os linux --arch x64^
               --framework net6.0^
               /property:GenerateFullPaths=true^
               /consoleloggerparameters:NoSummary


:: for osx-x64
echo.
echo --- publish for osx-x64 ---
echo.
dotnet publish ./eide_tools.sln -o ./dist/osx-x64^
               -c Release --no-self-contained^
               -r osx-x64^
               --framework net6.0^
               /property:GenerateFullPaths=true^
               /consoleloggerparameters:NoSummary

:: for osx-arm64
echo.
echo --- publish for osx-arm64 ---
echo.
dotnet publish ./eide_tools.sln -o ./dist/osx-arm64^
               -c Release --no-self-contained^
               -r osx-arm64^
               --framework net6.0^
               /property:GenerateFullPaths=true^
               /consoleloggerparameters:NoSummary


echo.
echo all done !
echo.