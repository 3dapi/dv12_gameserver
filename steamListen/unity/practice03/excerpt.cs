// 호스트: 로비 생성 직후
NetworkManager.Singleton.StartHost();

// 클라이언트: 로비 입장 직후 (Transport 클래스와 필드 이름은 사용하는 구현체의 문서를 따른다)
var transport = NetworkManager.Singleton.GetComponent<MySteamTransport>();
transport.TargetSteamId = steamLobbyPanel.HostSteamId;
NetworkManager.Singleton.StartClient();
