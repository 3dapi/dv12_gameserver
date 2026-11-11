// ChatSessionSubsystem.cpp
#include "OnlineSubsystem.h"
#include "OnlineSessionSettings.h"
#include "Interfaces/OnlineSessionInterface.h"

IOnlineSessionPtr UChatSessionSubsystem::Sess() const
{
    IOnlineSubsystem* Sub = IOnlineSubsystem::Get();
    return Sub ? Sub->GetSessionInterface() : nullptr;
}

void UChatSessionSubsystem::CreateRoom()
{
    IOnlineSessionPtr S = Sess(); if (!S.IsValid()) return;
    FOnlineSessionSettings Set;
    Set.NumPublicConnections = 4;                                     // 정원
    Set.bShouldAdvertise = true;
    Set.bUsesPresence = true;
    Set.bUseLobbiesIfAvailable = true;
    Set.bAllowJoinInProgress = true;
    Set.bAllowJoinViaPresence = true;
    Set.bIsLANMatch = IOnlineSubsystem::Get()->GetSubsystemName() == "NULL";
    Set.Set(FName("GAME"), FString("Chat"), EOnlineDataAdvertisementType::ViaOnlineService);
    S->OnCreateSessionCompleteDelegates.AddUObject(this, &UChatSessionSubsystem::OnCreated);
    S->CreateSession(0, NAME_GameSession, Set);
}
void UChatSessionSubsystem::OnCreated(FName, bool bOk)
{
    if (bOk) GetWorld()->ServerTravel(TEXT("/Game/Maps/ChatMap?listen"));   // 호스트가 서버가 된다
    else OnStatus.Broadcast(TEXT("세션 생성 실패"));
}

void UChatSessionSubsystem::FindRooms()
{
    IOnlineSessionPtr S = Sess(); if (!S.IsValid()) return;
    Search = MakeShared<FOnlineSessionSearch>();
    Search->MaxSearchResults = 20;
    Search->bIsLanQuery = IOnlineSubsystem::Get()->GetSubsystemName() == "NULL";
    Search->QuerySettings.Set(SEARCH_PRESENCE, true, EOnlineComparisonOp::Equals);
    S->OnFindSessionsCompleteDelegates.AddUObject(this, &UChatSessionSubsystem::OnFound);
    S->FindSessions(0, Search.ToSharedRef());
}
void UChatSessionSubsystem::OnFound(bool bOk)
{
    FString Text;
    if (bOk && Search.IsValid())
        for (int32 i = 0; i < Search->SearchResults.Num(); ++i)
            Text += FString::Printf(TEXT("[%d] %s (%d명 참가 가능)\n"), i,
                *Search->SearchResults[i].Session.OwningUserName,
                Search->SearchResults[i].Session.NumOpenPublicConnections);
    OnList.Broadcast(Text);                                           // 위젯의 목록 TextBlock에 표시
}

void UChatSessionSubsystem::JoinRoom(int32 Index)
{
    IOnlineSessionPtr S = Sess();
    if (!S.IsValid() || !Search.IsValid() || !Search->SearchResults.IsValidIndex(Index)) return;
    S->OnJoinSessionCompleteDelegates.AddUObject(this, &UChatSessionSubsystem::OnJoined);
    S->JoinSession(0, NAME_GameSession, Search->SearchResults[Index]);
}
void UChatSessionSubsystem::OnJoined(FName Name, EOnJoinSessionCompleteResult::Type R)
{
    FString Addr;
    if (R == EOnJoinSessionCompleteResult::Success && Sess()->GetResolvedConnectString(Name, Addr))
        GetWorld()->GetFirstPlayerController()->ClientTravel(Addr, TRAVEL_Absolute);   // 이후는 3장 접속 흐름
    else OnStatus.Broadcast(TEXT("참가 실패"));
}
