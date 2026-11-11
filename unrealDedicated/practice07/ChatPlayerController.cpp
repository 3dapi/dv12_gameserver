// ChatPlayerController.cpp
void AChatPlayerController::ServerClaimBonus_Implementation()
{
    AChatGameMode* GM = GetWorld()->GetAuthGameMode<AChatGameMode>();
    if (!GM || !PlayerState) return;
    bool bAlready = false;
    GM->BonusClaimed.Add(PlayerState->GetPlayerId(), &bAlready);
    ClientBonusResult(!bAlready, bAlready ? 0 : 100);            // 요청한 클라이언트에게만 응답한다
}
void AChatPlayerController::ClientBonusResult_Implementation(bool bOk, int32 Amount)
{
    if (Widget) Widget->SetStatus(bOk ? FString::Printf(TEXT("보너스 %d 지급"), Amount) : TEXT("이미 받았습니다"));
}
