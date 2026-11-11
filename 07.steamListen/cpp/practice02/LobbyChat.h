// LobbyChat.h — Steamworks SDK (steam_api.h). 클래스 이름과 시그니처는 사용하는 SDK 버전의 헤더로 확인한다.
#include "steam/steam_api.h"
#include <iostream>
#include <string>

class LobbyChat
{
public:
    bool Init() { return SteamAPI_Init(); }
    void Update() { SteamAPI_RunCallbacks(); }                       // 루프마다 호출한다

    void Create()                                                    // 호스트: 로비 생성
    {
        SteamAPICall_t h = SteamMatchmaking()->CreateLobby(k_ELobbyTypeFriendsOnly, 4);
        m_Created.Set(h, this, &LobbyChat::OnCreated);
    }
    void Join(CSteamID id)                                           // 클라이언트: 로비 참가
    {
        SteamAPICall_t h = SteamMatchmaking()->JoinLobby(id);
        m_Entered.Set(h, this, &LobbyChat::OnEntered);
    }
    void Say(const std::string& s)
    {
        SteamMatchmaking()->SendLobbyChatMsg(m_Lobby, s.c_str(), (int)s.size() + 1);
    }
    std::string HostId() const { return SteamMatchmaking()->GetLobbyData(m_Lobby, "host"); }

private:
    CSteamID m_Lobby;
    CCallResult<LobbyChat, LobbyCreated_t> m_Created;
    CCallResult<LobbyChat, LobbyEnter_t>   m_Entered;
    CCallback<LobbyChat, LobbyChatMsg_t>    m_ChatMsg{ this, &LobbyChat::OnChatMsg };
    CCallback<LobbyChat, LobbyChatUpdate_t> m_ChatUpd{ this, &LobbyChat::OnChatUpdate };
    CCallback<LobbyChat, GameLobbyJoinRequested_t> m_JoinReq{ this, &LobbyChat::OnJoinReq };

    void OnCreated(LobbyCreated_t* p, bool ioFail)
    {
        if (ioFail || p->m_eResult != k_EResultOK) { std::cout << "create failed\n"; return; }
        m_Lobby = CSteamID(p->m_ulSteamIDLobby);
        SteamMatchmaking()->SetLobbyData(m_Lobby, "game", "chat");
        SteamMatchmaking()->SetLobbyData(m_Lobby, "host", std::to_string(SteamUser()->GetSteamID().ConvertToUint64()).c_str());
        std::cout << "lobby " << m_Lobby.ConvertToUint64() << " created\n";
    }
    void OnEntered(LobbyEnter_t* p, bool ioFail)
    {
        if (ioFail || p->m_EChatRoomEnterResponse != k_EChatRoomEnterResponseSuccess) { std::cout << "join failed\n"; return; }
        m_Lobby = CSteamID(p->m_ulSteamIDLobby);
        std::cout << "joined, host=" << HostId() << "\n";
    }
    void OnChatMsg(LobbyChatMsg_t* p)                                // 보낸 사람 자신에게도 전달된다
    {
        char buf[1024]; CSteamID user; EChatEntryType type;
        int n = SteamMatchmaking()->GetLobbyChatEntry(CSteamID(p->m_ulSteamIDLobby), p->m_iChatID, &user, buf, sizeof buf, &type);
        if (n > 0) std::cout << SteamFriends()->GetFriendPersonaName(user) << ": " << buf << "\n";
    }
    void OnChatUpdate(LobbyChatUpdate_t*)                            // 입장·퇴장 때 접속자 목록을 다시 그린다
    {
        int n = SteamMatchmaking()->GetNumLobbyMembers(m_Lobby);
        std::cout << "USERS";
        for (int i = 0; i < n; ++i)
            std::cout << (i ? "," : " ") << SteamFriends()->GetFriendPersonaName(SteamMatchmaking()->GetLobbyMemberByIndex(m_Lobby, i));
        std::cout << "\n";
    }
    void OnJoinReq(GameLobbyJoinRequested_t* p) { Join(p->m_steamIDLobby); }   // 친구 초대 수락
};
