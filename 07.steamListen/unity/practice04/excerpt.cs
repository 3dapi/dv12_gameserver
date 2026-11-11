void Start()
{
    NetworkManager.Singleton.OnClientDisconnectCallback += id =>
    {
        if (id != NetworkManager.Singleton.LocalClientId) return;       // 내 연결이 끊어진 경우만
        string reason = NetworkManager.Singleton.DisconnectReason;      // 승인 거부 사유 또는 빈 문자열
        statusText.text = string.IsNullOrEmpty(reason) ? "호스트와 연결이 끊어졌습니다" : reason;
        if (_lobby.IsValid()) SteamMatchmaking.LeaveLobby(_lobby);
    };
}
