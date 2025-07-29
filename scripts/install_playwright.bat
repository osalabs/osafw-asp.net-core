@echo off
echo One-time setup for a shared Playwright browser cache - RUN AS ADMINISTRATOR
set "PW=C:\Program Files\pw-browsers"
mkdir "%PW%"
icacls "%PW%" /grant "IIS_IUSRS:(OI)(CI)M" /T
setx PLAYWRIGHT_BROWSERS_PATH "%PW%" /M
echo Re-start IIS (or reboot) so worker processes pick up the new variable.

pause