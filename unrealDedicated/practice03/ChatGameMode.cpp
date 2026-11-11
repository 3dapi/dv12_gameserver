// ChatGameMode.cpp
void AChatGameMode::PostLogin(APlayerController* NewPlayer)
{
    Super::PostLogin(NewPlayer);
    BroadcastUsers(nullptr);
    Notice(FString::Printf(TEXT("* %s 입장"), *NewPlayer->PlayerState->GetPlayerName()));
}

void AChatGameMode::Logout(AController* Exiting)
{
    const FString Name = Exiting->PlayerState ? Exiting->PlayerState->GetPlayerName() : TEXT("?");
    LeaveChannel(Cast<AChatPlayerController>(Exiting));
    Super::Logout(Exiting);
    BroadcastUsers(Exiting->PlayerState);                      // 나가는 접속자는 목록에서 뺀다
    Notice(FString::Printf(TEXT("* %s 퇴장"), *Name));
}

void AChatGameMode::BroadcastUsers(APlayerState* Except)
{
    TArray<FString> Names;
    for (APlayerState* PS : GameState->PlayerArray)
        if (PS != Except) Names.Add(PS->GetPlayerName());
    for (FConstPlayerControllerIterator It = GetWorld()->GetPlayerControllerIterator(); It; ++It)
        if (AChatPlayerController* PC = Cast<AChatPlayerController>(It->Get()))
            PC->ClientReceiveUsers(Names);
}

void AChatGameMode::Notice(const FString& Line)
{
    for (FConstPlayerControllerIterator It = GetWorld()->GetPlayerControllerIterator(); It; ++It)
        if (AChatPlayerController* PC = Cast<AChatPlayerController>(It->Get()))
            PC->ClientReceiveChat(Line);
}
