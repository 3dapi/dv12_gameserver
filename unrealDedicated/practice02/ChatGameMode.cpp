// ChatGameMode.cpp (서버에서만 실행된다)
void AChatGameMode::PreLogin(const FString& Options, const FString& Address,
                             const FUniqueNetIdRepl& UniqueId, FString& ErrorMessage)
{
    Super::PreLogin(Options, Address, UniqueId, ErrorMessage);
    if (!ErrorMessage.IsEmpty()) return;

    const FString Code = UGameplayStatics::ParseOption(Options, TEXT("Code"));
    const FString Name = UGameplayStatics::ParseOption(Options, TEXT("Name"));

    if (Code != JoinCode)                      { ErrorMessage = TEXT("인증 실패"); return; }
    if (Name.IsEmpty() || IsNameUsed(Name))    { ErrorMessage = TEXT("사용할 수 없는 이름"); return; }
    if (GameState->PlayerArray.Num() >= MaxPlayers) { ErrorMessage = TEXT("정원 초과"); return; }
}

bool AChatGameMode::IsNameUsed(const FString& Name) const
{
    for (APlayerState* PS : GameState->PlayerArray)
        if (PS->GetPlayerName() == Name) return true;
    return false;
}

// 접속 실패 사유 표시 (UChatWidget::NativeConstruct에서 등록)
GEngine->OnNetworkFailure().AddUObject(this, &UChatWidget::OnNetFail);   // (UWorld*, UNetDriver*, ENetworkFailure::Type, const FString&)
GEngine->OnTravelFailure().AddUObject(this, &UChatWidget::OnTravelFail); // (UWorld*, ETravelFailure::Type, const FString&)
