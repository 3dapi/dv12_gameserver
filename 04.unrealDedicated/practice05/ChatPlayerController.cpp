// ChatPlayerController.cpp: 버튼에 연결된 Server RPC들은 GameMode 함수를 호출한다.
void AChatPlayerController::ServerCreateChannel_Implementation(const FString& Ch)
{ if (AChatGameMode* GM = GetWorld()->GetAuthGameMode<AChatGameMode>()) GM->EnterChannel(this, Ch, true); }
void AChatPlayerController::ServerJoinChannel_Implementation(const FString& Ch)
{ if (AChatGameMode* GM = GetWorld()->GetAuthGameMode<AChatGameMode>()) GM->EnterChannel(this, Ch, false); }
void AChatPlayerController::ServerLeaveChannel_Implementation()
{ if (AChatGameMode* GM = GetWorld()->GetAuthGameMode<AChatGameMode>()) GM->LeaveChannel(this); }
