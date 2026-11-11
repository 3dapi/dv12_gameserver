// ChatPlayerController.cpp
void AChatPlayerController::BeginPlay()
{
    Super::BeginPlay();
    if (IsLocalController() && WidgetClass)                       // 서버 프로세스에는 화면이 없다
    {
        Widget = CreateWidget<UChatWidget>(this, WidgetClass);
        Widget->AddToViewport();
        bShowMouseCursor = true;
        const ENetMode Mode = GetNetMode();
        Widget->SetStatus(Mode == NM_Standalone ? TEXT("Standalone") :
                          Mode == NM_Client ? TEXT("Client") : TEXT("Server"));
    }
}
