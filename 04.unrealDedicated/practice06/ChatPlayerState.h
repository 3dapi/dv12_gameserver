// ChatPlayerState.h
UPROPERTY(ReplicatedUsing = OnRep_State) int32 Hp = 100;
UPROPERTY(ReplicatedUsing = OnRep_State) bool bReady = false;
UFUNCTION() void OnRep_State();                                  // 클라이언트에서 값이 바뀔 때 호출된다
