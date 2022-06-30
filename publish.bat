@echo off

::echo.
::echo clean projects ...
::dotnet clean

:: for win-x64
echo.
echo --- publish for win-x64 ---
echo.
dotnet publish ./eide_tools.sln -o ./dist/win-x64 ^
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
dotnet publish ./eide_tools.sln -o ./dist/linux-x64 ^
               -c Release --no-self-contained^
               --os linux --arch x64^
               --framework net6.0^
               /property:GenerateFullPaths=true^
               /consoleloggerparameters:NoSummary

echo.
echo all done !
echo.