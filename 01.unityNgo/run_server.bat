@echo off
rem 화면 없는 서버를 실행한다. 로그는 logs\server.log에 남는다. 포트를 바꾸려면 -port 값을 바꾼다.
if not exist "%~dp0logs" mkdir "%~dp0logs"
start "" "%~dp0Builds\Client\NgoChat.exe" -batchmode -nographics -logFile "%~dp0logs\server.log" -server -port 7777
