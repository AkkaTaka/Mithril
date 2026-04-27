@echo off
setlocal enabledelayedexpansion

echo ===============================
echo Cleaning bin / obj folders
echo Root: %~dp0
echo ===============================

for /d /r "%~dp0" %%D in (bin,obj) do (
  if exist "%%D" (
    echo Removing: %%D
    rmdir /s /q "%%D"
    if exist "%%D" (
      echo [FAILED] %%D
    )
  )
)

echo ===============================
echo Done.
echo ===============================
pause
