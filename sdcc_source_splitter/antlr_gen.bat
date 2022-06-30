@echo off

echo input antlr grammar file: %1
echo.

echo check file suffix
for /F "delims=" %%i IN ("%1") DO set FILE_SUFFIX=%%~xi
IF NOT "%FILE_SUFFIX%" == ".g4" (
	echo error grammar file suffix: "%FILE_SUFFIX%", exit.
	exit 1
) ELSE (
	echo ok
)
echo.

echo delete old parser
for /F "delims=" %%i IN ("%1") DO set FILE_NAME=%%~ni
IF NOT "%FILE_NAME%" == "" (
	echo del /S /F /Q %FILE_NAME%*.cs %FILE_NAME%*.interp %FILE_NAME%*.tokens
	del /S /F /Q %FILE_NAME%*.cs %FILE_NAME%*.interp %FILE_NAME%*.tokens
)
echo.

echo start generate parser ...
java org.antlr.v4.Tool -Dlanguage=CSharp %1
echo done !
echo.