using Steamworks;
using UnityEngine;
using UnityEngine.UI;

public class SteamLobbyPanel : MonoBehaviour
{
    [SerializeField] Button createBtn, findBtn, joinBtn, inviteBtn;
    [SerializeField] Text listText, statusText;

    CallResult<LobbyCreated_t> _created;
    CallResult<LobbyMatchList_t> _list;
    Callback<LobbyEnter_t> _entered;
    Callback<GameLobbyJoinRequested_t> _joinReq;
    CSteamID _lobby, _found;
    public ulong HostSteamId { get; private set; }

    void Start()
    {
        _created = CallResult<LobbyCreated_t>.Create(OnCreated);
        _list    = CallResult<LobbyMatchList_t>.Create(OnList);
        _entered = Callback<LobbyEnter_t>.Create(OnEntered);
        _joinReq = Callback<GameLobbyJoinRequested_t>.Create(r => SteamMatchmaking.JoinLobby(r.m_steamIDLobby));   // 친구 초대 수락

        createBtn.onClick.AddListener(() =>
            _created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4)));
        findBtn.onClick.AddListener(() =>
        {
            SteamMatchmaking.AddRequestLobbyListStringFilter("game", "chat", ELobbyComparison.k_ELobbyComparisonEqual);
            _list.Set(SteamMatchmaking.RequestLobbyList());
        });
        joinBtn.onClick.AddListener(() => { if (_found.IsValid()) SteamMatchmaking.JoinLobby(_found); });
        inviteBtn.onClick.AddListener(() => SteamFriends.ActivateGameOverlayInviteDialog(_lobby));
    }

    void OnCreated(LobbyCreated_t r, bool ioFail)
    {
        if (ioFail || r.m_eResult != EResult.k_EResultOK) { statusText.text = "로비 생성 실패"; return; }
        _lobby = new CSteamID(r.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_lobby, "game", "chat");
        SteamMatchmaking.SetLobbyData(_lobby, "host", SteamUser.GetSteamID().m_SteamID.ToString());
        HostSteamId = SteamUser.GetSteamID().m_SteamID;
        statusText.text = "로비 생성: " + _lobby;
    }
    void OnList(LobbyMatchList_t r, bool ioFail)
    {
        listText.text = "";
        for (int i = 0; i < r.m_nLobbiesMatching; i++)
        {
            _found = SteamMatchmaking.GetLobbyByIndex(i);          // 첫 번째 로비를 선택한다
            listText.text += _found + "  " + SteamMatchmaking.GetNumLobbyMembers(_found) + "명\n";
        }
    }
    void OnEntered(LobbyEnter_t e)
    {
        _lobby = new CSteamID(e.m_ulSteamIDLobby);
        ulong.TryParse(SteamMatchmaking.GetLobbyData(_lobby, "host"), out var host);
        HostSteamId = host;
        statusText.text = "로비 입장, host=" + host;
    }
}
