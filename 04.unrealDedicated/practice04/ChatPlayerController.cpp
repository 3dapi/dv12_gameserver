// ChatPlayerController.cpp
void AChatPlayerController::MoveVirtual(FVector2D Delta)       // 버튼 또는 자동 이동 타이머에서 호출
{
    MyPos += Delta;                                            // 내 좌표는 내가 계산한다
    ServerReportPos(MyPos);
}
void AChatPlayerController::ServerReportPos_Implementation(FVector2D Pos)   // UFUNCTION(Server, Unreliable)
{
    if (AChatPlayerState* PS = GetPlayerState<AChatPlayerState>())
        PS->VPos = Pos;                                        // 서버는 기록하고 복제한다
}

// 좌표 목록 표시: 위젯이 타이머로 GameState->PlayerArray의 VPos를 읽어 TextBlock을 갱신한다.
