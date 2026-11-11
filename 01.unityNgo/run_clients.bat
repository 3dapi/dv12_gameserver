@echo off
rem 빌드한 클라이언트를 3개 띄우고 로그를 따로 저장한다. 이 파일은 프로젝트 폴더(Builds 폴더가 있는 곳)에 둔다.
if not exist "%~dp0logs" mkdir "%~dp0logs"
start "" "%~dp0Builds\Client\NgoChat.exe" -logFile "%~dp0logs\client1.log"
start "" "%~dp0Builds\Client\NgoChat.exe" -logFile "%~dp0logs\client2.log"
start "" "%~dp0Builds\Client\NgoChat.exe" -logFile "%~dp0logs\client3.log"
