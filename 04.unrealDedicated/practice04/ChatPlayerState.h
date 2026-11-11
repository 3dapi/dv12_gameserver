// ChatPlayerState.h
UPROPERTY(ReplicatedUsing = OnRep_VPos) FVector2D VPos;
UFUNCTION() void OnRep_VPos();
virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& Out) const override;
