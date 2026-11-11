// ChatPlayerState.cpp
void AChatPlayerState::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& Out) const
{
    Super::GetLifetimeReplicatedProps(Out);
    DOREPLIFETIME(AChatPlayerState, VPos);
}
