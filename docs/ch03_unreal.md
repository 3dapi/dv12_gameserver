# 3. Unreal Game Server

## 3.1 학습 목표

Unreal Engine의 멀티플레이 프레임워크를 이해하고 Dedicated Server 기반 소규모 온라인 게임을 구성한다.

### 3.1.1 이 장을 마치면

- Listen Server와 Dedicated Server의 구조 차이를 설명하고 Dedicated Server를 실행한다.
- Actor와 프로퍼티를 복제하고 RepNotify로 클라이언트의 반응을 구현한다.
- RPC(Server, Client, NetMulticast)의 호출 방향을 이해하고 클라이언트의 요청을 서버를 거쳐 전달한다.
- GameMode, GameState, PlayerState, PlayerController를 서버와 클라이언트에 존재하는 범위를 기준으로 구분해 사용한다.
- 접속 승인과 인증, 정원 제한, 접속·종료 처리로 접속자를 관리하고 UMG 채팅 GUI로 서버 기능을 검증한다.

### 3.1.2 서버의 역할: 이 장의 범위

이 장에서 Dedicated Server는 접속자 관리와 메시지 중계를 맡는다. Unreal의 복제는 서버에서 클라이언트 방향으로만 흐르므로, 클라이언트가 처리한 결과는 Server RPC로 서버에 알리고 서버가 이를 별도의 검증 없이 반영하여 복제한다. 이동은 CharacterMovementComponent가 클라이언트 예측을 제공하므로 별도의 구현이 필요하지 않다.

서버에서 요청을 검증하고 규칙을 판정하는 방식은 상용 서비스의 핵심이지만 학습 범위를 크게 넓히므로 개념으로만 소개하고, 6장에서 직접 구현한다.

### 3.1.3 선수 지식과 개발 환경

이 장의 코드는 C++로 작성한다. UCLASS, UPROPERTY, UFUNCTION 등 Unreal C++ 기본 문법과 Character 클래스를 만들 수 있어야 한다. Blueprint는 레벨과 UI 연결 정도로만 사용한다.

런처에서 설치한 엔진으로는 Server 타깃을 빌드할 수 없으므로, 패키징된 Dedicated Server 실습(실습 9)에는 GitHub 소스에서 빌드한 엔진이 필요하다. 실습 1~8은 에디터의 Play In Editor(PIE)에서 Net Mode를 Play As Client로 설정하면 Dedicated Server를 에디터 안에서 자동으로 띄워 진행할 수 있다.

- Unreal Engine: [검증 버전 기재] / Visual Studio: [버전 기재]
- 복제 시스템: 기본 복제 방식 대신 Iris를 사용할 수 있는 버전이 있으므로 사용 여부를 [버전 확인 후 기재]

## 3.2 주요 학습 내용

Listen Server와 Dedicated Server의 차이, 서버 권한(Authority)

Actor Replication, Property Replication, Server/Client/Multicast RPC

GameMode, GameState, PlayerState, PlayerController의 네트워크 역할

접속·인증, 채팅 중계, 가상 이동, 채널, 상태 복제 (UMG 채팅 GUI로 검증)

Dedicated Server 빌드·실행, 다중 클라이언트 테스트

### 3.2.1 Unreal Dedicated Server 단계별 실습

**[검증 방식]** Unity와 같은 방식으로 진행한다. 실시간 게임의 네트워크는 게임 화면에서 검증하기 어려우므로, 맵 하나(ChatMap)에 UMG 위젯으로 GUI 창(버튼과 TextBlock)을 만들어 서버의 기능을 하나씩 호출하며 검증한다. 검증된 코드는 게임 클라이언트로 옮기고 GUI만 게임 UI로 바꾼다. 이 절의 클래스는 다음과 같다.

- AChatGameMode: 서버에만 있다. 접속 승인(PreLogin), 접속자 등록(PostLogin), 채널 관리, 보너스 지급 기록을 맡는다.
- AChatPlayerController: 클라이언트의 요청을 Server RPC로 서버에 보내고, 서버의 응답을 Client RPC로 받아 위젯에 표시한다.
- AChatPlayerState: 접속자별 공개 상태(체력, 준비, 가상 좌표)를 복제한다.
- UChatWidget: 버튼과 입력창, 결과 TextBlock을 가진 GUI이다. 네트워크 코드는 이 위젯의 함수만 호출한다.

실습 1 — 맵, GUI, 네트워크 모드: 맵 하나와 채팅 위젯을 만들고, 같은 위젯이 Standalone, Dedicated Server, Client에서 각각 어떤 네트워크 모드로 실행되는지 화면에 표시한다. PIE에서 Number of Players를 3, Net Mode를 Play As Client로 설정하면 Dedicated Server가 에디터 안에서 자동으로 실행된다.

**[구현 순서]** 1. ChatMap을 만들고 World Settings에서 GameMode Override를 AChatGameMode로 지정한다. 2. UChatWidget을 부모로 하는 Widget Blueprint(WBP_Chat)를 만들고 주소·포트·이름·인증 코드 입력창, 접속·종료·전송 버튼, 상태·로그·접속자 목록 TextBlock을 같은 이름으로 배치한다. 3. PlayerController의 BeginPlay에서 로컬 컨트롤러에만 위젯을 생성한다.

```cpp
// ChatWidget.h
UCLASS()
class UChatWidget : public UUserWidget
{
    GENERATED_BODY()
public:
    UPROPERTY(meta = (BindWidget)) UEditableTextBox* AddressBox;
    UPROPERTY(meta = (BindWidget)) UEditableTextBox* PortBox;
    UPROPERTY(meta = (BindWidget)) UEditableTextBox* NameBox;
    UPROPERTY(meta = (BindWidget)) UEditableTextBox* CodeBox;
    UPROPERTY(meta = (BindWidget)) UEditableTextBox* ChatBox;
    UPROPERTY(meta = (BindWidget)) UButton* ConnectBtn;
    UPROPERTY(meta = (BindWidget)) UButton* DisconnectBtn;
    UPROPERTY(meta = (BindWidget)) UButton* SendBtn;
    UPROPERTY(meta = (BindWidget)) UTextBlock* StatusText;
    UPROPERTY(meta = (BindWidget)) UTextBlock* LogText;
    UPROPERTY(meta = (BindWidget)) UTextBlock* UserListText;

    void AddLog(const FString& Line);
    void SetUsers(const TArray<FString>& Names);
    void SetStatus(const FString& S) { StatusText->SetText(FText::FromString(S)); }

    virtual void NativeConstruct() override;
};

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
```

**[확인할 것]** 서버 창 1개와 클라이언트 창 3개를 실행하여 각 창의 상태 TextBlock이 서버/클라이언트로 올바르게 표시되는지 확인한다. 같은 위젯 클래스가 어느 프로세스에서 만들어지고 어느 프로세스에서는 만들어지지 않는지(Dedicated Server에는 로컬 컨트롤러가 없다)를 로그로 정리한다.

**[흔한 오류]** BindWidget 이름이 Widget Blueprint의 변수 이름과 다른 경우, 서버 프로세스에서도 CreateWidget을 호출하는 경우, GameMode Override를 지정하지 않아 기본 GameMode로 실행되는 경우, HasAuthority()를 서버 여부로 단정하는 경우(클라이언트가 직접 스폰한 Actor에서는 클라이언트도 Authority이다).

실습 2 — 접속과 인증: 이름과 인증 코드를 접속 주소의 옵션으로 보내고, 서버의 PreLogin에서 인증 코드, 이름 중복, 정원을 확인한다. 거부되면 사유를 클라이언트 상태 TextBlock에 표시한다.

**[구현 순서]** 접속 버튼이 눌리면 PlayerController가 ClientTravel로 서버에 접속한다. 이름과 인증 코드는 옵션 문자열(?Name=...?Code=...)에 실어 보낸다. 옵션은 URL이므로 이름은 영문과 숫자로 제한하거나 URL 인코딩한다. 서버는 AGameModeBase::PreLogin에서 ErrorMessage를 채우면 접속을 거부한다.

```cpp
// ChatWidget.cpp
void UChatWidget::OnConnect()
{
    const FString Url = FString::Printf(TEXT("%s:%s?Name=%s?Code=%s"),
        *AddressBox->GetText().ToString(), *PortBox->GetText().ToString(),
        *NameBox->GetText().ToString(), *CodeBox->GetText().ToString());
    GetOwningPlayer()->ClientTravel(Url, TRAVEL_Absolute);
}

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
```

**[확인할 것]** 올바른 인증 코드와 이름으로 접속되는지, 인증 코드가 틀린 경우와 이미 있는 이름인 경우, 정원(MaxPlayers)을 넘은 경우에 접속이 거부되고 사유가 표시되는지 확인한다. 기본 GameModeBase는 Name 옵션으로 접속자 이름을 설정한다는 점과, 거부 사유가 두 델리게이트 중 어디로 전달되는지(엔진 버전에 따라 다를 수 있다)를 로그로 확인한다.

**[흔한 오류]** PreLogin 시그니처가 엔진 버전과 다른 경우, ErrorMessage를 채우고도 Super 호출 순서를 잘못 두어 덮어쓰는 경우, 서버가 listen 옵션으로 열리지 않아 접속이 안 되는 경우, 방화벽이 UDP 7777을 차단하는 경우.

실습 3 — 채팅 중계와 접속자 목록: 클라이언트가 Server RPC로 메시지를 보내면 서버가 모든 접속자의 PlayerController에게 Client RPC로 전달한다. 접속자 목록은 서버가 입장·퇴장 때마다 모두에게 보낸다. 서버는 메시지 내용을 판단하지 않는다.

```cpp
// ChatPlayerController.h
UFUNCTION(Server, Reliable) void ServerSendChat(const FString& Msg);
UFUNCTION(Client, Reliable) void ClientReceiveChat(const FString& Line);
UFUNCTION(Client, Reliable) void ClientReceiveUsers(const TArray<FString>& Names);

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
```

**[확인할 것]** 메시지 한 줄이 모든 클라이언트 화면에 한 번씩만 표시되는지, 입장과 퇴장 알림 및 접속자 목록이 갱신되는지 확인한다. 클라이언트가 강제 종료되었을 때 Logout이 호출되어 목록에서 사라지는지 확인한다. GameMode는 서버에만 있고 PlayerController는 서버와 해당 클라이언트에만 있어서 다른 클라이언트의 PlayerController를 클라이언트에서는 얻을 수 없다는 점을 확인한다.

**[흔한 오류]** 클라이언트에서 GetAuthGameMode()가 null인 것을 모르고 사용하는 경우, 소유 클라이언트가 없는 Actor에서 Server RPC를 호출하여 전달되지 않는 경우(PlayerController는 클라이언트가 소유한다), Logout에서 PlayerArray의 제거 시점을 고려하지 않아 나간 접속자가 목록에 남는 경우.

실습 4 — 가상 이동 전달: 캐릭터 없이 좌표값만으로 이동을 검증한다. 클라이언트는 자신의 좌표를 계산하여 Server RPC로 알리고, 서버는 PlayerState의 복제 프로퍼티에 기록하여 모든 클라이언트에 전달한다. 자동 이동 버튼은 일정 간격으로 임의 방향으로 이동하는 가상 입력이다.

```cpp
// ChatPlayerState.h
UPROPERTY(ReplicatedUsing = OnRep_VPos) FVector2D VPos;
UFUNCTION() void OnRep_VPos();
virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& Out) const override;

// ChatPlayerState.cpp
void AChatPlayerState::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& Out) const
{
    Super::GetLifetimeReplicatedProps(Out);
    DOREPLIFETIME(AChatPlayerState, VPos);
}

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
```

**[확인할 것]** 한 클라이언트의 좌표가 다른 클라이언트의 목록에 같은 값으로 갱신되는지 확인한다. 중간에 접속한 클라이언트도 이미 바뀐 좌표를 처음부터 받는지 확인한다(프로퍼티 복제의 특징). 자동 이동을 켠 클라이언트 수를 늘리며 갱신이 늦어지는 시점을 기록한다. 클라이언트에서 VPos를 직접 바꾸어도 다음 복제 때 서버 값으로 덮어써지는지 확인한다. C++에서 OnRep 함수는 서버에서 값을 바꿀 때 자동 호출되지 않으므로 서버(Listen Server 호스트)의 화면은 별도로 갱신해야 한다.

**[흔한 오류]** GetLifetimeReplicatedProps에 DOREPLIFETIME을 빠뜨린 경우, UnrealNetwork.h를 include하지 않은 경우, 매 프레임 Reliable RPC를 호출하여 버퍼가 가득 차는 경우(자주 보내는 값은 Unreliable로 한다).

실습 5 — 채널(방) 생성·입장·퇴장: 서버의 GameMode가 채널 목록과 채널별 접속자를 관리하고, 채널 대화를 같은 채널 접속자에게만 전달한다. 방 기반 서버(6장)의 기초가 되는 기능이다.

```cpp
// ChatGameMode.h  (서버에만 존재하므로 클라이언트에서는 접근할 수 없다)
TMap<FString, TArray<TWeakObjectPtr<AChatPlayerController>>> Channels;
TMap<TWeakObjectPtr<AChatPlayerController>, FString> ChannelOf;

// ChatGameMode.cpp
void AChatGameMode::EnterChannel(AChatPlayerController* PC, const FString& Ch, bool bCreate)
{
    if (!Channels.Contains(Ch)) { if (!bCreate) return; Channels.Add(Ch); }
    LeaveChannel(PC);
    Channels[Ch].AddUnique(PC);
    ChannelOf.Add(PC, Ch);
    BroadcastChannels();
}

void AChatGameMode::LeaveChannel(AChatPlayerController* PC)
{
    if (!PC) return;
    if (FString* Ch = ChannelOf.Find(PC))
    {
        const FString Name = *Ch;
        Channels[Name].Remove(PC);
        if (Channels[Name].Num() == 0) Channels.Remove(Name);
        ChannelOf.Remove(PC);
    }
    BroadcastChannels();
}

void AChatGameMode::ChannelChat(AChatPlayerController* From, const FString& Msg)
{
    FString* Ch = ChannelOf.Find(From);
    if (!Ch) return;
    const FString Line = FString::Printf(TEXT("[%s] %s"), *From->PlayerState->GetPlayerName(), *Msg);
    for (auto& W : Channels[*Ch])
        if (W.IsValid()) W->ClientReceiveChannelChat(Line);   // 같은 채널에만 전달
}

// ChatPlayerController.cpp: 버튼에 연결된 Server RPC들은 GameMode 함수를 호출한다.
void AChatPlayerController::ServerCreateChannel_Implementation(const FString& Ch)
{ if (AChatGameMode* GM = GetWorld()->GetAuthGameMode<AChatGameMode>()) GM->EnterChannel(this, Ch, true); }
void AChatPlayerController::ServerJoinChannel_Implementation(const FString& Ch)
{ if (AChatGameMode* GM = GetWorld()->GetAuthGameMode<AChatGameMode>()) GM->EnterChannel(this, Ch, false); }
void AChatPlayerController::ServerLeaveChannel_Implementation()
{ if (AChatGameMode* GM = GetWorld()->GetAuthGameMode<AChatGameMode>()) GM->LeaveChannel(this); }
```

**[확인할 것]** 채널 A와 B에 접속자를 나누어 넣고 한 채널의 대화가 다른 채널에 보이지 않는지 확인한다. 채널 접속자가 접속을 끊으면 인원이 줄고 빈 채널이 사라지는지(Logout에서 LeaveChannel 호출) 확인한다. 존재하지 않는 채널에 입장을 시도할 때 서버가 오류 없이 무시하는지 확인한다. 약한 포인터(TWeakObjectPtr)를 쓰는 이유(접속 종료로 컨트롤러가 사라질 수 있다)를 설명한다.

**[흔한 오류]** 채널 자료구조를 클라이언트에서 읽으려는 경우(서버에만 있다), 접속 종료 처리에서 채널을 정리하지 않아 끊긴 접속자가 남는 경우, 삭제된 컨트롤러 포인터를 그대로 사용하는 경우.

실습 6 — 접속자 상태 복제(RepNotify): 체력과 준비 상태를 PlayerState의 복제 프로퍼티로 관리하고 값이 바뀔 때 OnRep 함수로 화면을 갱신한다. 클라이언트는 Server RPC로 변경을 요청하고 서버는 요청받은 값을 반영한다. 중간에 접속한 클라이언트도 현재 값을 받는다.

```cpp
// ChatPlayerState.h
UPROPERTY(ReplicatedUsing = OnRep_State) int32 Hp = 100;
UPROPERTY(ReplicatedUsing = OnRep_State) bool bReady = false;
UFUNCTION() void OnRep_State();                                  // 클라이언트에서 값이 바뀔 때 호출된다

// ChatPlayerState.cpp
void AChatPlayerState::OnRep_State()
{
    OnStateChanged.Broadcast();                                  // 위젯이 구독하여 목록을 다시 그린다
}

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
```

**[확인할 것]** 한 클라이언트가 체력을 바꾸면 모든 화면의 목록이 갱신되는지 확인한다. 변경을 마친 뒤 새 클라이언트를 접속시켜 현재 값이 바로 표시되는지 확인한다. 클라이언트에서 PlayerState의 값을 직접 바꾸면 다음 복제에서 서버 값으로 되돌아오는지 확인한다. 복제 프로퍼티는 서버에서 클라이언트 방향으로만 흐른다는 점을 정리한다.

**[흔한 오류]** DOREPLIFETIME을 빠뜨린 경우, OnRep을 서버에서도 자동 호출될 것으로 기대하는 경우, PlayerState를 서버에서 바꾸지 않고 클라이언트에서만 바꾸는 경우.

실습 7 — 요청과 응답(보너스 받기): 클라이언트의 요청을 서버가 처리하고 결과를 요청한 클라이언트에게만 돌려준다. 서버 로직이 바뀔 때마다 버튼 하나로 바로 검증하는 방식을 익힌다.

```cpp
// ChatPlayerController.h
UFUNCTION(Server, Reliable) void ServerClaimBonus();
UFUNCTION(Client, Reliable) void ClientBonusResult(bool bOk, int32 Amount);

// ChatGameMode.h
TSet<int32> BonusClaimed;                                        // PlayerId 기준, 서버만 사용

// ChatPlayerController.cpp
void AChatPlayerController::ServerClaimBonus_Implementation()
{
    AChatGameMode* GM = GetWorld()->GetAuthGameMode<AChatGameMode>();
    if (!GM || !PlayerState) return;
    bool bAlready = false;
    GM->BonusClaimed.Add(PlayerState->GetPlayerId(), &bAlready);
    ClientBonusResult(!bAlready, bAlready ? 0 : 100);            // 요청한 클라이언트에게만 응답한다
}
void AChatPlayerController::ClientBonusResult_Implementation(bool bOk, int32 Amount)
{
    if (Widget) Widget->SetStatus(bOk ? FString::Printf(TEXT("보너스 %d 지급"), Amount) : TEXT("이미 받았습니다"));
}
```

**[선택 기준]** 중간에 접속한 사람에게도 보여야 하는 지속 상태는 복제 프로퍼티(RepNotify), 한 번 일어나고 지나가는 사건은 RPC로 전달한다. Reliable은 반드시 도착해야 하는 요청(보너스, 구매), Unreliable은 놓쳐도 되는 연출과 자주 보내는 값에 사용한다. Server RPC 구현 함수는 이름에 _Implementation을 붙인다. 입력값을 서버에서 검사하는 WithValidation(_Validate)도 제공되지만 이 장에서는 사용하지 않는다.

**[확인할 것]** 버튼을 두 번 눌렀을 때 두 번째에 "이미 받았습니다"가 표시되는지, 다른 클라이언트의 화면은 바뀌지 않는지 확인한다. 서버 코드의 지급 조건을 바꾼 뒤 다시 실행하여 GUI만으로 바로 검증되는 것을 확인한다. Client RPC가 해당 PlayerController를 소유한 클라이언트에서만 실행된다는 점을 확인한다.

**[흔한 오류]** 응답을 모든 PlayerController에 보내는 경우, 월드 Actor에서 직접 Server RPC를 호출하고 "RPC가 안 온다"고 생각하는 경우(RPC는 소유 Connection이 있는 Actor에서만 서버로 전달된다), 재접속하면 PlayerId가 바뀐다는 점을 고려하지 않은 경우.

실습 8 — Actor 생성·제거: 서버가 복제되는 Actor를 생성하고 제거하여 모든 클라이언트에 나타나고 사라지게 한다. 클라이언트가 직접 스폰한 Actor는 서버와 다른 클라이언트에 보이지 않는다는 점도 확인한다.

```cpp
// ItemActor.cpp
AItemActor::AItemActor()
{
    bReplicates = true;                                          // 서버에서 생성하면 클라이언트에 복제된다
    SetReplicateMovement(true);
}

// ChatPlayerController.cpp   (UFUNCTION(Server, Reliable))
void AChatPlayerController::ServerSpawnItem_Implementation()
{
    if (AChatGameMode* GM = GetWorld()->GetAuthGameMode<AChatGameMode>())
        GM->SpawnItem(FVector(FMath::RandRange(-300.f, 300.f), FMath::RandRange(-300.f, 300.f), 100.f));
}

// ChatGameMode.cpp
void AChatGameMode::SpawnItem(const FVector& Loc)
{
    if (AItemActor* A = GetWorld()->SpawnActor<AItemActor>(ItemClass, Loc, FRotator::ZeroRotator))
    {
        Items.Add(A);
        NotifyItemCount();                                       // 개수를 모든 접속자에게 Client RPC로 알린다
    }
}
void AChatGameMode::DespawnItem()
{
    if (Items.Num() == 0) return;
    if (AItemActor* A = Items.Pop().Get()) A->Destroy();         // 서버에서 Destroy하면 클라이언트에서도 사라진다
    NotifyItemCount();
}
```

**[확인할 것]** 생성 버튼을 누르면 모든 클라이언트의 뷰포트에 Actor가 나타나는지, 제거하면 사라지는지 확인한다. bReplicates를 끄면 서버에서만 보이는 것을 확인한다. 클라이언트에서 직접 SpawnActor한 Actor가 다른 곳에 나타나지 않는 것을 확인한다. 이동하는 Actor의 SetReplicateMovement 효과와 NetUpdateFrequency의 영향을 관찰한다.

**[흔한 오류]** 클라이언트에서 Spawn하여 서버와 다른 클라이언트에 보이지 않는 경우, bReplicates를 켜지 않아 서버에서만 보이는 경우, ItemClass를 지정하지 않은 경우.

실습 9 — Dedicated Server 빌드와 게임 클라이언트 적용: 검증을 마친 GameMode, PlayerController, PlayerState 코드를 Dedicated Server로 빌드하고, 위젯 대신 게임 UI와 게임 캐릭터가 같은 함수를 호출하도록 옮긴다.

**[구현 순서]** 1. Server 타깃 파일(Target.cs)을 추가하고 소스 빌드 엔진에서 빌드한다. 2. 서버를 -log 옵션으로 실행하고 클라이언트 3개 이상을 접속시킨다. 3. 위젯의 네트워크 호출 부분을 PlayerController의 함수로 분리하고, 게임 UI가 같은 함수를 호출하도록 바꾼다. 4. 가상 좌표를 ACharacter와 CharacterMovementComponent로 바꾼다(이동 복제와 예측이 기본 제공된다). 5. 실습 2의 PreLogin을 그대로 사용한다.

```csharp
// MyGameServer.Target.cs
using UnrealBuildTool;
using System.Collections.Generic;

public class MyGameServerTarget : TargetRules
{
    public MyGameServerTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Server;
        DefaultBuildSettings = BuildSettingsVersion.V5;          // 엔진 버전에 맞게 확인한다
        ExtraModuleNames.Add("MyGame");
    }
}
```

**[확인할 것]** 서버 로그에 접속·종료 로그가 남는지, 서버 창이 없어도 게임이 진행되는지, 클라이언트 3개 이상이 접속하는지 확인한다. 채팅 맵에서 통과한 시험(인증 거부, 정원 초과, 채널 대화, 상태 복제, 보너스 응답)이 게임 클라이언트에서도 같은 결과로 나오는지 비교한다.

**[흔한 오류]** 런처 엔진에서 Server 타깃 빌드를 시도하는 경우, 서버 맵이 쿠킹되지 않은 경우, 방화벽이 UDP 7777을 차단하는 경우.

## 3.3 실습 프로젝트

4~8인이 접속하는 채팅 프로그램을 완성하고 게임 클라이언트에 적용한다. 3.2.1의 UMG 채팅 GUI를 하나의 프로젝트로 묶어 서버 기능을 모두 GUI로 검증한 뒤, 같은 코드를 캐릭터 이동이 있는 게임 화면에 옮긴다. 콘텐츠의 양보다 접속자 관리와 메시지 중계가 GUI에서 모두 확인되는지에 집중한다.

### 3.3.1 프로젝트 요구사항

- 접속: 최대 8명 접속, 정원 초과 시 거부, 접속 종료와 재접속
- 채팅: 전체 채팅과 채널 채팅, 접속자 목록, 입장·퇴장 공지
- 요청·응답: 보너스 받기처럼 서버가 처리하고 결과를 요청자에게만 돌려주는 기능 한 가지 이상
- 상태: 체력과 준비 상태(RepNotify), 접속자별 가상 좌표
- 오브젝트: 서버가 Actor를 생성하고 제거하여 모든 클라이언트에 반영
- 적용: 검증된 코드를 게임 클라이언트(ACharacter 이동)에 옮기고, Dedicated Server 빌드에 서로 다른 PC 또는 프로세스에서 3개 이상 클라이언트 접속

### 3.3.2 역할 분담표

프로젝트의 각 행동에 대해 처리하는 쪽, 서버의 역할, 전파 방식을 표로 작성하고 구현이 표와 일치하는지 점검한다.

- 접속과 종료: GameMode가 PreLogin에서 인증과 정원을 확인하고 PostLogin·Logout에서 접속자를 등록·제거하여 모두에게 알린다.
- 채팅: 클라이언트가 Server RPC로 보내면 서버가 대상 PlayerController에 Client RPC로 중계한다. 서버는 내용을 판단하지 않는다.
- 이동: 소유 클라이언트가 처리하고 서버는 값을 복제하여 전달한다.
- 채널: GameMode가 채널 목록을 관리하고 같은 채널의 접속자에게만 중계한다.
- 상태: 클라이언트가 변경을 요청하면 서버가 PlayerState에 반영하고 복제한다.
- 보너스: 서버가 지급 기록을 관리하고 요청한 클라이언트에게만 응답한다.

### 3.3.3 구현 순서

1. 접속과 인증 GUI  2. 채팅 중계와 접속자 목록  3. 가상 이동  4. 채널  5. 상태 복제  6. 보너스 요청·응답  7. Actor 생성·제거  8. Dedicated Server 빌드와 다중 클라이언트 테스트  9. 게임 클라이언트(ACharacter 이동)에 적용

## 3.4 접속자 관리와 지연 대응

접속자 관리와 메시지 중계를 중심으로 Dedicated Server를 구성한다. 접속 승인과 정원 제한, 중도 참여와 접속 종료 처리, 재접속을 실습하고 요청 검증과 치트 방지 원칙은 개념으로 소개한다.

### 3.4.1 접속자 관리 실습

서버가 접속자를 관리하는 상황을 만들어 보고 서버 로그와 클라이언트 화면이 일치하는지 확인한다.

- 정원(8명)을 넘는 접속 시도(PreLogin에서 거부하고 클라이언트에 거부 사유를 표시)
- 플레이 중 클라이언트 강제 종료(서버 로그와 다른 클라이언트의 접속자 목록에서 정리되는지 확인)
- 잘못된 인증 코드 또는 중복된 이름으로 접속(거부 사유가 GUI에 표시되는지 확인)
- 채널에 있던 접속자의 종료와 같은 이름으로의 재접속(채널·접속자 목록이 정리되었다가 다시 등록되는지 확인)
- 서버 종료 시 클라이언트가 접속 끊김 화면으로 돌아가는지 확인

결과는 상황, 서버의 처리, 로그 근거를 표로 작성하여 제출한다. 클라이언트 요청을 서버가 검증하는 방식(_Validate, 상태·쿨타임 검증)은 개념으로 소개하고 6장에서 구현한다.

### 3.4.2 지연과 패킷 손실 실습

에디터의 Network Emulation 설정(Editor Preferences의 Play 항목) 또는 콘솔 명령으로 지연을 만든다.

```text
Net PktLag=100         // 패킷 지연(ms)
Net PktLagVariance=30  // 지연 변동
Net PktLoss=5          // 패킷 손실(%)
stat net              // 전송량과 패킷 통계
```

**[관찰 항목]** RTT 0ms, 100ms, 300ms와 손실 0%, 5%, 10%에서 로컬 캐릭터의 반응, 다른 캐릭터의 움직임, RepNotify 도착 시점, Reliable RPC와 Unreliable RPC의 손실 차이를 기록한다.

### 3.4.3 Unreal이 제공하는 것과 직접 구현할 것

- 제공: CharacterMovementComponent의 클라이언트 예측과 서버 보정(Reconciliation), 이동 보간, Reliable/Unreliable 전송, 관련성 판단
- 직접 구현: Lag Compensation(발사 시점의 대상 위치를 과거로 되돌려 판정), 전투 입력의 예측과 되돌림, 사용자 정의 이동에 대한 예측 확장

사용하는 엔진 버전에서 예측을 돕는 별도 시스템(Network Prediction 계열 플러그인 등)의 지원 상태가 바뀔 수 있으므로 [버전 확인 후 기재]한다.

### 3.4.4 네트워크 최적화

- NetUpdateFrequency, MinNetUpdateFrequency: Actor 갱신 빈도를 필요한 만큼만 높인다.
- NetCullDistanceSquared와 관련성(Relevancy): 멀리 있는 Actor는 해당 클라이언트에 복제하지 않는다. bOnlyRelevantToOwner는 소유자에게만 필요한 Actor에 사용한다.
- Dormancy: 자주 바뀌지 않는 Actor는 휴면 상태로 두어 비교 비용을 줄인다.
- 복제 프로퍼티는 필요한 것만 선언하고, 조건 복제(DOREPLIFETIME_CONDITION)로 소유자에게만 또는 초기에만 보낸다.
- 측정: stat net, Network Profiler 또는 Unreal Insights의 Networking Insights로 전송량이 큰 Actor와 프로퍼티를 찾는다. 이 값은 6장의 Interest Management 설계와 연결된다.

네트워크 RTT 증가와 패킷 손실 상황에서 Replication 결과를 비교하고, 보간·예측·서버 보정(Server Reconciliation) 및 Lag Compensation의 개념과 엔진이 제공하는 기능의 범위를 살펴본다. 모든 고급 지연 보정 알고리즘을 직접 구현하지는 않는다.

### 3.4.5 Listen Server와 비교 정리

같은 프로젝트를 Dedicated Server와 Listen Server로 각각 실행하여 다음을 비교한다. 4장에서 Steam으로 같은 게임을 Listen Server로 연결한다.

- 서버 프로세스 위치와 호스트 플레이어의 존재 여부(서버가 곧 한 플레이어인지)
- 호스트 플레이어의 지연 없는 입력과 그에 따른 공정성 차이
- 호스트 종료 시 세션 종료
- BeginPlay 같은 공통 코드에서 서버 전용 처리와 UI 처리를 구분하는 방법(IsNetMode(NM_DedicatedServer), IsLocallyControlled())

### 3.4.6 검증 버전 기록

- Unreal Engine: [기재]  /  소스 빌드 여부: [기재]
- 복제 시스템(기본 Replication Graph 또는 Iris): [기재]
- 예제 저장소: [저장소 주소 기재]

## 3.5 Unreal 네트워킹의 내부 구조

Unreal은 서버 권한 모델이 엔진 구조에 깊이 반영되어 있어서, 구성 요소의 역할과 위치를 알아야 올바르게 설계할 수 있다. 이 절은 3.2절의 실습이 의존하는 원리를 정리한다.

### 3.5.1 연결과 복제의 계층

- NetDriver: 네트워크 통신을 총괄하는 객체이다. 서버는 클라이언트마다 NetConnection을 하나씩 만들고, 클라이언트는 서버로의 NetConnection 하나를 가진다.
- Channel: NetConnection 안에서 데이터 종류별로 나뉜 통로이다. Control 채널은 접속과 종료를, Actor 채널은 복제되는 액터마다 하나씩 열려 그 액터의 속성과 RPC를 전달한다.
- Actor와 Component: 복제는 액터 단위로 켠다(bReplicates). 컴포넌트는 그 액터와 함께 복제되고 SetIsReplicated로 개별적으로 켠다.

```text
[Server]  NetDriver
   +- NetConnection(Client A)  -- Control channel
   |                           -- Actor channel (PlayerController A)
   |                           -- Actor channel (Character A, B ...)
   +- NetConnection(Client B)  -- (같은 구조)
```

### 3.5.2 Role과 소유권

같은 액터가 서버와 클라이언트에 각각 존재하며, 어디에서 실행되느냐에 따라 Role이 다르다.

- ROLE_Authority: 이 액터의 원본이다. 서버에서 실행되는 액터는 대부분 Authority이다.
- ROLE_AutonomousProxy: 이 클라이언트가 조작하는 액터이다. 입력을 받고 예측을 수행한다.
- ROLE_SimulatedProxy: 다른 플레이어의 액터이다. 서버가 보내 준 상태를 보여 주기만 한다.

소유권(Owner)은 연결과 관련된 개념이다. PlayerController가 연결의 소유자이고, 그 컨트롤러가 빙의(Possess)한 Pawn과 PlayerState가 같은 소유자 체인에 속한다. 클라이언트가 서버로 RPC를 보내려면 해당 액터의 소유 연결이 자신이어야 한다. 소유하지 않은 액터에서 호출한 Server RPC는 버려진다. 이것이 "RPC를 호출했는데 아무 일도 일어나지 않는" 가장 흔한 원인이다.

### 3.5.3 프레임워크 클래스의 위치

어떤 클래스가 어디에 존재하는지 알아야 설계 실수를 줄일 수 있다.

- GameMode: 서버에만 존재한다. 접속 승인(PreLogin), 입장(PostLogin), 퇴장(Logout), 규칙을 담당한다. 클라이언트는 GameMode에 접근할 수 없다(없는 것이 정상이다).
- GameState: 서버와 모든 클라이언트에 존재하고 복제된다. 접속자 수나 경기 상태처럼 모두가 알아야 하는 전역 상태를 둔다.
- PlayerState: 서버와 모든 클라이언트에 존재하고 복제된다. 플레이어별 공개 정보(이름, 점수)를 둔다. 접속자 목록은 GameState의 PlayerArray에서 얻는다.
- PlayerController: 서버와 해당 플레이어의 클라이언트에만 존재한다. 다른 플레이어의 컨트롤러는 내 클라이언트에 없다. 그래서 다른 사람의 이름은 PlayerController가 아니라 PlayerState에서 읽어야 한다.
- Pawn/Character: 서버와 관련된 클라이언트에 존재하고 복제된다. 관련성(relevancy) 때문에 멀리 있는 Pawn은 클라이언트에 없을 수 있다.
- HUD/UMG 위젯: 클라이언트(화면이 있는 쪽)에만 존재한다. Dedicated Server에서는 생성하지 않는다.

### 3.5.4 속성 복제와 RPC

속성 복제는 서버의 값이 클라이언트로 단방향 전달되는 상태 동기화이다. UPROPERTY(Replicated)로 선언하고 GetLifetimeReplicatedProps에서 DOREPLIFETIME으로 등록한다. 값이 바뀐 속성만 전송되며, 복제 조건(예: COND_OwnerOnly, COND_SkipOwner)으로 받을 대상을 제한한다. ReplicatedUsing에 지정한 OnRep 함수는 클라이언트가 값을 받았을 때 호출되므로 UI 갱신에 쓴다. 서버 자신(Listen Server의 호스트 포함)에서는 OnRep이 자동 호출되지 않으므로 필요하면 직접 호출한다.

RPC는 일회성 호출이다.

- Server: 클라이언트에서 호출하여 서버에서 실행한다. 소유 액터에서만 유효하다.
- Client: 서버에서 호출하여 해당 소유 클라이언트에서만 실행한다.
- NetMulticast: 서버에서 호출하여 서버와 관련된 모든 클라이언트에서 실행한다.

각 RPC는 Reliable 또는 Unreliable로 지정한다. Reliable은 도착을 보장하지만 큐가 넘치면 연결이 끊길 수 있으므로 매 프레임 호출하면 안 된다. 서버에서 호출되는 RPC에 WithValidation을 쓰면 인자 검증 함수가 실패했을 때 해당 클라이언트가 끊긴다. 클라이언트 입력을 믿지 않는다는 1.8절의 원칙을 코드로 옮긴 장치이다.

### 3.5.5 관련성과 갱신 빈도

서버는 모든 액터를 모든 클라이언트에 보내지 않는다. 클라이언트마다 관련성(relevancy)을 판단해 필요한 액터만 보낸다.

- bAlwaysRelevant: 항상 보낸다. 게임 상태처럼 모두가 알아야 하는 액터에 쓴다.
- bOnlyRelevantToOwner: 소유자에게만 보낸다. 인벤토리처럼 개인 정보에 쓴다.
- NetCullDistanceSquared: 이 거리 밖의 액터는 보내지 않는다.
- NetUpdateFrequency, MinNetUpdateFrequency: 액터의 초당 갱신 횟수 범위를 정한다. 값이 클수록 부드럽고 전송량이 늘어난다.
- NetPriority: 대역폭이 모자랄 때 먼저 보낼 액터의 우선순위이다.

대규모 접속에서는 기본 방식의 비용이 커지므로 Replication Graph나 Iris 같은 대체 복제 시스템이 쓰인다. 이 교재는 이름과 용도만 소개하고 사용하지 않는다.

### 3.5.6 이동 예측: CharacterMovementComponent

Unreal의 Character는 2.7절의 클라이언트 예측과 서버 보정을 이미 구현하고 있다. 조종하는 클라이언트(AutonomousProxy)가 입력을 받아 즉시 이동하고, 이동 정보(SavedMove)를 서버로 보낸다. 서버는 같은 이동을 다시 계산해 결과가 허용 오차를 넘게 다르면 보정 위치를 보낸다. 그러면 클라이언트는 보정 위치로 이동한 뒤 아직 확인받지 못한 SavedMove를 다시 적용한다. 다른 플레이어(SimulatedProxy)는 복제된 위치를 보간해 보여 준다.

이 구조 덕분에 Character를 쓰면 이동 동기화를 직접 만들 필요가 없다. 반대로 Character가 아닌 Pawn이나 직접 만든 이동에는 이 예측이 적용되지 않으므로 반응이 느리게 보인다.

## 3.6 Unity NGO와 Unreal 비교

두 엔진의 같은 개념을 대응시키면 한쪽에서 배운 것을 다른 쪽에 옮기기 쉽다.

- 서버가 승인하는 지점: NGO는 ConnectionApprovalCallback, Unreal은 GameMode의 PreLogin이다.
- 상태 동기화: NGO는 NetworkVariable, Unreal은 Replicated 속성과 OnRep이다.
- 이벤트 호출: NGO는 ServerRpc/ClientRpc, Unreal은 Server/Client/NetMulticast RPC이다.
- 접속자 목록: NGO는 NetworkManager.ConnectedClients, Unreal은 GameState의 PlayerArray이다.
- 네트워크 오브젝트 식별: NGO는 NetworkObject, Unreal은 복제되는 Actor이다.
- 소유권: NGO는 OwnerClientId, Unreal은 PlayerController 중심의 Owner 체인이다.
- 이동 예측: NGO는 직접 구현이 필요하고, Unreal의 Character는 내장되어 있다.
- 서버 전용 코드: NGO는 IsServer 검사, Unreal은 HasAuthority 또는 GameMode(서버에만 존재)이다.

Unreal은 규칙이 엄격해서 처음에는 어렵지만 구조가 정해져 있어 팀 작업에서 실수가 적고, Unity는 자유도가 높아 빨리 만들 수 있지만 구조를 스스로 정해야 한다. 어느 쪽을 쓰든 서버와 클라이언트에서 같은 코드가 다르게 동작한다는 점을 늘 의식해야 한다.

## 3.7 3장 연습 문제

1. Dedicated Server에서 PlayerController, GameMode, HUD가 존재하는지 각각 답하고 이유를 설명하라.
2. 클라이언트가 Server RPC를 호출했는데 서버에서 실행되지 않았다. 원인으로 가능한 것을 세 가지 이상 적어라.
3. 다른 플레이어의 이름을 화면에 표시하려면 PlayerController가 아니라 PlayerState를 써야 하는 이유를 설명하라.
4. OnRep 함수가 Listen Server 호스트에서 자동 호출되지 않는 이유와 해결 방법을 설명하라.
5. Replicated 속성과 NetMulticast RPC 중 "중간에 접속한 클라이언트도 최신 상태를 보아야 하는 값"에는 어느 쪽을 써야 하는지 이유와 함께 답하라.
6. NetUpdateFrequency를 100으로 올렸을 때 대역폭과 서버 CPU에 일어나는 변화를 설명하라.
