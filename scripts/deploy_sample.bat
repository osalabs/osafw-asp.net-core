@echo off
REM --- BEGIN PROJECT-SPECIFIC SETTINGS
set "APP_POOL_NAME=sitename.sample.com"
set "PROJECT_FILE=osafw-app.csproj"
set "ENVIRONMENT_NAME=Beta"
set "PROJECT_ROOT=C:\inetpub\sitename.sample.com"
REM Where the final published files actually run under IIS:
set "TARGET_FOLDER=%PROJECT_ROOT%\bin\Release\net8.0\publish"
REM We'll create a subfolder in %TEMP% for the build artifacts:
set "PUBLISH_FOLDER=%TEMP%\sitename-publish"

REM --- END PROJECT-SPECIFIC SETTINGS

echo == Switching to project root ==
cd /d "%PROJECT_ROOT%"

echo == Git Pull ==
git pull > pull_output.txt
type pull_output.txt | findstr /C:"Already up to date." > nul
if %ERRORLEVEL%==0 (
    echo No changes found. Exiting.
    goto :END
)
type pull_output.txt

echo == Publish to Temporary Folder ==
REM Remove old publish folder if it exists
if exist "%PUBLISH_FOLDER%" rmdir /S /Q "%PUBLISH_FOLDER%"
mkdir "%PUBLISH_FOLDER%"

dotnet publish "%PROJECT_FILE%" ^
  --configuration Release ^
  /p:EnvironmentName=%ENVIRONMENT_NAME% ^
  -o "%PUBLISH_FOLDER%"

if %ERRORLEVEL% NEQ 0 (
    echo ** Publish FAILED! Exiting. **
    goto :END
)

echo == Stop App Pool and Deploy ==
%windir%\system32\inetsrv\appcmd stop apppool "%APP_POOL_NAME%"

echo == Copy published files to %TARGET_FOLDER% ==
robocopy "%PUBLISH_FOLDER%" "%TARGET_FOLDER%" /E /PURGE /R:3 /W:3 /NFL /NDL

echo == Start App Pool ==
%windir%\system32\inetsrv\appcmd start apppool "%APP_POOL_NAME%"

:END
echo == Deploy script finished. ==
pause