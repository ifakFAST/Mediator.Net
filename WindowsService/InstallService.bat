set dir=%~dp0
set binpath="%dir%WinService.exe"
SC CREATE "ifakFAST" start=auto binpath=%binpath%

@echo off
echo.
PAUSE