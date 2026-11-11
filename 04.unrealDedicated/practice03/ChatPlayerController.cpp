// ChatPlayerController.cpp
void AChatPlayerController::ServerSendChat_Implementation(const FString& Msg)
{
    const FString Line = FString::Printf(TEXT("[%s] %s"), *PlayerState->GetPlayerName(), *Msg);   // 호출자 식별
    for (FConstPlayerControllerIterator It = GetWorld()->GetPlayerControllerIterator(); It; ++It)
        if (AChatPlayerController* PC = Cast<AChatPlayerController>(It->Get()))
            PC->ClientReceiveChat(Line);                       // 서버는 전달만 한다
}
void AChatPlayerController::ClientReceiveChat_Implementation(const FString& Line)
{
    if (Widget) Widget->AddLog(Line);
}
void AChatPlayerController::ClientReceiveUsers_Implementation(const TArray<FString>& Names)
{
    if (Widget) Widget->SetUsers(Names);
}
