using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// 접속 화면: Host / Client / Server 시작과 종료, 현재 상태 표시
public class NetStarter : MonoBehaviour
{
    string ip = "127.0.0.1";      // 클라이언트가 접속할 서버 주소
    string port = "7777";
    string role = "";             // 로그에 붙일 역할 이름
    string message = "";          // 화면에 보여 줄 안내와 오류
    bool connected;               // 이번 시작에서 접속에 성공했는가
    string pending = "";          // 눌린 버튼이 시킨 일("Host", "Client", "Server", "Stop"). OnGUI는 기록만 한다

    void Start()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        nm.OnClientConnectedCallback += OnConnected;
        nm.OnClientDisconnectCallback += OnDisconnected;
        nm.OnTransportFailure += OnTransportFailed;

        // 서버 전용 실행: 명령줄에 -server가 있으면 Server 버튼을 누른 것처럼 서버를 바로 시작한다.
        // 시작은 Update()에서 한다. 모든 스크립트의 Start()가 끝난 뒤여야 ApprovalGate의 설정이 끝나 있다.
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-server") >= 0)
        {
            QualitySettings.vSyncCount = 0;         // targetFrameRate는 수직 동기화가 켜져 있으면 무시된다
            Application.targetFrameRate = 30;       // 서버는 30fps면 충분하다. 제한하지 않으면 CPU를 한 코어 가득 쓴다.
            pending = "Server";
        }
    }

    // 버튼이 시킨 일은 화면을 그리는 도중(OnGUI)이 아니라 여기서 한다.
    void Update()
    {
        if (pending == "") return;
        string job = pending;
        pending = "";

        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        if (job == "Stop") nm.Shutdown();
        else Begin(nm, job);
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        nm.OnClientConnectedCallback -= OnConnected;
        nm.OnClientDisconnectCallback -= OnDisconnected;
        nm.OnTransportFailure -= OnTransportFailed;
    }

    void OnConnected(ulong clientId)
    {
        connected = true;
        Debug.Log($"[{role}] 접속 완료: ClientId {clientId}");
    }

    void OnDisconnected(ulong clientId)
    {
        Debug.Log($"[{role}] 접속 종료: ClientId {clientId}");
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsServer) return;      // 서버(호스트)에서는 다른 클라이언트의 종료도 이 함수로 온다
        message = connected
            ? "서버와의 연결이 끊어졌다"
            : "서버에 접속하지 못했다. 서버 주소·포트와 서버 실행 여부를 확인한다";
        if (!string.IsNullOrEmpty(nm.DisconnectReason))
            message += "\n사유: " + nm.DisconnectReason;     // 서버가 알려 준 사유 또는 NGO가 만든 진단 문장
    }

    void OnTransportFailed() => message = "네트워크 전송 계층에 오류가 생겨 종료했다. Console을 확인한다";

    void OnGUI()
    {
        UiKit.Apply();
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            GUILayout.Label("씬에 NetworkManager가 없다");
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 420, 420));
        if (nm.ShutdownInProgress)
            GUILayout.Label("종료하는 중...");
        else if (!nm.IsListening)
            DrawStartButtons(nm);
        else
            DrawStatus(nm);

        if (message != "") GUILayout.Label(message);
        GUILayout.EndArea();
    }

    void DrawStartButtons(NetworkManager nm)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("서버 IP", GUILayout.Width(70));
        ip = GUILayout.TextField(ip, GUILayout.Width(150));
        GUILayout.Label("포트", GUILayout.Width(40));
        port = GUILayout.TextField(port, GUILayout.Width(70));
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Host (서버 + 클라이언트)")) pending = "Host";
        if (GUILayout.Button("Client (접속)")) pending = "Client";
        if (GUILayout.Button("Server (서버만)")) pending = "Server";
    }

    void Begin(NetworkManager nm, string mode)
    {
        message = "";
        connected = false;
        if (!ushort.TryParse(port, out ushort p))
        {
            message = "포트는 0~65535 사이의 숫자여야 한다";
            return;
        }

        role = mode;
        var utp = nm.GetComponent<UnityTransport>();
        bool ok;
        if (mode == "Client")
        {
            utp.SetConnectionData(ip, p);                          // 접속할 서버의 주소와 포트
            ok = nm.StartClient();
        }
        else
        {
            utp.SetConnectionData("127.0.0.1", p, "0.0.0.0");     // 이 PC의 모든 주소에서 접속을 받는다
            ok = mode == "Host" ? nm.StartHost() : nm.StartServer();
        }

        if (!ok) message = "시작하지 못했다. Console의 오류를 확인한다";
    }

    void DrawStatus(NetworkManager nm)
    {
        string mode = nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client";
        GUILayout.Label($"모드: {mode}");

        if (nm.IsClient)
            GUILayout.Label(nm.IsConnectedClient ? $"접속됨. 내 ClientId = {nm.LocalClientId}" : "서버에 접속하는 중...");
        if (nm.IsServer)
            GUILayout.Label($"접속자 수: {nm.ConnectedClientsIds.Count}");

        if (GUILayout.Button("종료")) pending = "Stop";
    }
}
