// ChatPlayerState.cpp
void AChatPlayerState::OnRep_State()
{
    OnStateChanged.Broadcast();                                  // 위젯이 구독하여 목록을 다시 그린다
}
