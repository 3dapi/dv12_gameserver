// ChatPlayerController.cpp   (UFUNCTION(Server, Reliable))
void AChatPlayerController::ServerChangeState_Implementation(int32 DHp, bool bToggleReady)
{
    if (AChatPlayerState* PS = GetPlayerState<AChatPlayerState>())
    {
        PS->Hp += DHp;                                           // 서버가 요청받은 값을 반영한다
        if (bToggleReady) PS->bReady = !PS->bReady;
        PS->OnRep_State();                                       // 서버(호스트) 화면은 직접 갱신한다
    }
}
