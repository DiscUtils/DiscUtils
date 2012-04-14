@echo off

echo Instructions
echo ------------
echo.
echo 1. Run this batch file as Administrator
echo 2. Select 'Install content from disk'
echo 3. Select the file 'HelpContentSetup.msha' from this folder
echo 4. Select 'Add' for 'DiscUtils Class Library'
echo 5. Click 'Update'
echo 6. Confirm you want to proceed


"%ProgramFiles%\Microsoft Help Viewer\v1.0\HelpLibManager.exe" /product "VS" /version "100" /locale en-us
