# 2. Unity Game Server

## 2.1 학습 목표

Unity Netcode for GameObjects를 통해 네트워크 객체의 생성, 소유권, 상태 동기화, 원격 호출을 이해하고 소규모 멀티플레이 게임을 구현한다. 이 Part는 엔진이 제공하는 네트워킹 기술을 활용하는 단계이며 별도의 순수 C++ 서버를 직접 제작하지 않는다.

### 2.1.1 이 장을 마치면

- Host, Client, Server의 역할을 구분하고 접속·종료·재접속 흐름을 구현한다.
- NetworkObject의 생성·제거와 소유권 이전을 서버 기준으로 처리한다.
- 지속적인 상태는 NetworkVariable로, 일회성 사건은 RPC로 전달하는 기준을 설명한다.
- 접속 승인과 정원 제한, 접속자 목록, 입장·퇴장 통지로 접속자를 관리한다.
- 지연 상황에서 동기화 결과를 관찰한다.

### 2.1.2 서버의 역할: 이 장의 범위

이 장에서 서버(또는 호스트)는 접속자 관리와 메시지 중계만 맡는다. 이동과 상호작용 같은 게임 규칙은 소유한 클라이언트가 처리하고, 그 결과를 NetworkVariable과 RPC로 서버를 거쳐 다른 클라이언트에 전달한다. 이 방식은 구현이 단순한 대신 클라이언트의 조작에 취약하다는 점을 인지하고 진행한다.

NGO에서 NetworkObject의 생성·제거(Spawn, Despawn)는 서버만 호출할 수 있다. NetworkTransform은 기본적으로 서버 권한으로 동작하며, 소유 클라이언트가 자신의 이동을 직접 결정하게 하려면 Owner 권한으로 바꾸어야 한다. 설정 방법은 사용하는 NGO 버전에 따라 다르다([버전 확인 후 기재]).

### 2.1.3 C#과 Unity 선수 지식 점검

이 장의 코드는 C#으로 작성한다. 대상 학습자는 C++ 경험이 있으므로 다음 차이만 확인하고 시작한다.

- 클래스는 참조 타입이고 struct는 값 타입이다. 메모리는 가비지 컬렉터가 관리하므로 delete에 해당하는 코드를 작성하지 않는다.
- 이벤트와 델리게이트는 +=로 핸들러를 등록하고 -=로 해제한다. NetworkVariable.OnValueChanged와 NetworkManager 콜백이 이 방식이며, 등록한 핸들러는 오브젝트가 제거될 때 해제해야 한다.
- 네트워크로 전송하는 값은 int, float, bool, enum, struct 같은 값 타입을 주로 사용한다. 문자열은 FixedString 같은 전용 타입을 사용한다.
- MonoBehaviour의 생명주기(Awake, Start, Update, OnDestroy)를 이해한다. 네트워크 오브젝트에는 OnNetworkSpawn, OnNetworkDespawn이 추가된다.
- 프리팹, 씬, 컴포넌트, Inspector에서의 참조 연결([SerializeField])을 사용할 수 있어야 한다. Unity 입문이 필요한 학습자는 이 장을 시작하기 전에 [Unity 기초 학습 자료 기재]를 먼저 학습한다.

## 2.2 주요 학습 내용

Host / Client / Server의 역할 및 접속 흐름

NetworkManager, NetworkObject, NetworkBehaviour와 네트워크 객체의 생성·제거

NetworkVariable과 RPC를 이용한 상태 및 이벤트 동기화

플레이어 이동, 소유권, 서버 권한, 지연 및 연결 종료 처리

### 2.2.1 Netcode for GameObjects 단계별 실습

**[검증 방식]** 실시간 게임의 네트워크는 게임 화면에서 검증하기가 매우 어렵다. 입력, 렌더링, 물리가 함께 동작하면 문제가 네트워크에서 생겼는지 게임 코드에서 생겼는지 구분하기 어렵기 때문이다. 그래서 실무에서는 먼저 채팅 씬 하나에 GUI 창(버튼과 Text)을 만들어 서버의 기능을 하나씩 호출하며 검증하고, 검증된 네트워크 코드를 게임 클라이언트에 옮겨 적용한다. 이 절의 실습은 모두 이 방식을 따른다.

**[작업 순서]** 1. ChatScene 하나에 서버 기능마다 버튼(요청)과 Text(응답·상태)를 만든다. 2. 서버(호스트)와 이 GUI 클라이언트를 여러 개 실행하여 기능별로 검증한다. 3. 검증된 네트워크 코드를 게임 클라이언트에 그대로 옮기고, GUI 부분만 게임 UI로 교체한다.

**[GUI로 검증할 기능]**

- 초기화와 접속: 주소·포트 입력, Host / Client / Server / 종료 버튼, 상태 Text (실습 2)
- 인증: 이름과 인증 코드 전송, 승인·거부 사유 표시 (실습 2)
- 접속자 목록: 입장·퇴장 알림, 접속자 목록 Text (실습 2)
- 채널(방) 생성·입장·퇴장: 채널 이름 입력, 생성·입장·퇴장 버튼, 채널별 접속자 Text (실습 4)
- 위치·상태 전달: 방향 버튼과 자동 이동, 수신 좌표 Text (실습 3), 체력 증감·준비 버튼과 상태 목록 Text (실습 5)
- 요청과 응답: 보너스 받기 같은 요청 버튼과 결과 Text (실습 6)
- 오브젝트 생성·제거: 생성·제거 버튼과 목록 Text (실습 7)

이 씬은 게임이 아니라 서버 검증 도구이다. 실습 2의 예제는 단순화를 위해 UI와 네트워크 코드가 한 스크립트에 있으며, 게임 클라이언트로 옮길 때는 UI 부분만 교체한다. 씬에 uGUI 패널을 만드는 방법은 선수 지식으로 다룬다.

 1 — 패키지 설치 및 개발 환경: Package Manager에서 Netcode for GameObjects, Unity Transport, Multiplayer Tools를 확인하고 동일 프로젝트의 Host·Client·Server 구동 환경을 준비한다.

**[구현 순서]**

1. 새 프로젝트를 만들고 Package Manager에서 Netcode for GameObjects를 설치한다. Unity Transport가 함께 설치되었는지 확인하고 Multiplayer Tools를 추가한다.
2. 빈 오브젝트를 만들어 NetworkManager 컴포넌트를 추가하고, Network Transport 항목에 Unity Transport가 지정되었는지 확인한다.
3. 같은 프로젝트를 두 개 이상 동시에 실행하는 방법을 정한다. Editor와 빌드본을 함께 실행하거나, 사용하는 Editor 버전에서 지원하면 Multiplayer Play Mode를 사용한다.

**[확인할 것]** Editor와 빌드본이 각각 오류 없이 실행되고 같은 씬(ChatScene 하나)을 로드한다.

**[흔한 오류]** 씬에 NetworkManager가 둘 이상 있는 경우, Network Transport 항목이 비어 있는 경우, Build Settings의 씬 목록에 씬이 없는 경우, 방화벽이 접속을 차단하는 경우.

실습 2 — Host / Client / Server 접속과 인증(채팅 패널): NetworkManager, UnityTransport 및 StartHost / StartClient / StartServer 버튼을 구성한다. 버튼과 입력창으로 초기화·접속·인증을 하고, 호스트와 별도 클라이언트 인스턴스에서 연결·접속 종료·재접속을 검증한다.

**[구현 순서]** 이 절의 모든 실습은 하나의 씬(ChatScene)에서 진행한다. 씬을 만들고 Build Settings에 등록한 뒤, 아래 순서로 UI를 구성한다.

1. Canvas를 만들고 접속 패널을 구성한다. 주소(기본 127.0.0.1)·포트(기본 7777)·이름·인증 코드 InputField 4개, Host / Client / Server / 종료 버튼 4개, 상태 표시용 Text 1개를 둔다. EventSystem이 씬에 있어야 한다.
2. 채팅 패널을 구성한다. 채팅 로그 Text(ScrollRect 안), 접속자 목록 Text, 채팅 입력 InputField와 보내기 버튼을 둔다. 예제는 legacy UI(Text, InputField)를 사용하며 TextMeshPro를 쓰면 타입만 바꾼다.
3. 빈 오브젝트에 ConnectionPanel을 붙이고 UI를 연결한다. NetworkManager 오브젝트의 Connection Approval은 코드에서 켠다. 다른 빈 오브젝트에 NetworkObject와 ChatHub를 붙이고 UI를 연결한다(ChatHub는 씬에 배치된 NetworkObject여야 한다).

접속 흐름은 "초기화(주소·포트 지정) → 접속(StartHost / StartClient / StartServer) → 인증(이름과 인증 코드 확인, 정원 확인) → 채팅" 순서이다. 클라이언트는 접속 요청에 이름과 인증 코드를 실어 보내고, 서버(호스트)는 Connection Approval 콜백에서 이를 확인한다. 아래 두 스크립트가 이 흐름의 전부이다. UDP 포트 기본값과 SetConnectionData 사용법은 Unity Transport 기준이다.

```csharp
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionPanel : MonoBehaviour
{
    [SerializeField] InputField addressInput, portInput, nameInput, codeInput;
    [SerializeField] Button hostButton, clientButton, serverButton, stopButton;
    [SerializeField] Text statusText;

    const int MaxPlayers = 4;
    const string JoinCode = "1234";                              // 실습용 인증 코드
    public readonly Dictionary<ulong, string> Names = new();     // 서버(호스트)가 관리하는 접속자 목록

    NetworkManager nm;

    void Start()
    {
        nm = NetworkManager.Singleton;
        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback = Approve;                 // 서버(호스트)에서 호출된다
        nm.OnClientConnectedCallback += id => statusText.text = $"접속: {id} (현재 {nm.ConnectedClientsIds.Count}명)";
        nm.OnClientDisconnectCallback += id => statusText.text = string.IsNullOrEmpty(nm.DisconnectReason)
            ? $"종료: {id}" : $"거부: {nm.DisconnectReason}";

        hostButton.onClick.AddListener(() =>
        {
            SetTransport();
            Names[NetworkManager.ServerClientId] = nameInput.text;   // 호스트 자신도 접속자 목록에 등록
            nm.StartHost();
        });
        clientButton.onClick.AddListener(() =>
        {
            SetTransport();
            nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(nameInput.text + "|" + codeInput.text);
            nm.StartClient();
        });
        serverButton.onClick.AddListener(() => { SetTransport(); nm.StartServer(); });
        stopButton.onClick.AddListener(() => { nm.Shutdown(); Names.Clear(); statusText.text = "종료"; });
    }

    void SetTransport()                                          // 초기화: 주소와 포트 지정
    {
        nm.GetComponent<UnityTransport>().SetConnectionData(addressInput.text, ushort.Parse(portInput.text));
    }

    void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
    {
        var parts = Encoding.UTF8.GetString(req.Payload).Split('|');
        string user = parts.Length > 0 ? parts[0] : "";
        string code = parts.Length > 1 ? parts[1] : "";

        string reason = "";
        if (code != JoinCode) reason = "인증 실패";
        else if (string.IsNullOrEmpty(user) || Names.ContainsValue(user)) reason = "사용할 수 없는 이름";
        else if (nm.ConnectedClientsIds.Count >= MaxPlayers) reason = "정원 초과";

        res.Approved = reason == "";
        res.CreatePlayerObject = false;                          // 채팅 실습에서는 플레이어 오브젝트를 만들지 않는다
        res.Reason = reason;
        if (res.Approved) Names[req.ClientNetworkId] = user;
    }
}
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ChatHub : NetworkBehaviour                          // 씬에 배치한 NetworkObject에 붙인다
{
    [SerializeField] ConnectionPanel panel;
    [SerializeField] InputField chatInput;
    [SerializeField] Button sendButton;
    [SerializeField] Text logText, userListText;

    public override void OnNetworkSpawn()
    {
        sendButton.onClick.AddListener(() =>
        {
            if (chatInput.text == "") return;
            SendChatRpc(chatInput.text);                         // 서버에 보낸다
            chatInput.text = "";
        });

        if (IsServer)                                            // 접속자 관리는 서버(호스트)만 한다
        {
            NetworkManager.OnClientConnectedCallback += OnJoin;
            NetworkManager.OnClientDisconnectCallback += OnLeave;
            UsersRpc(string.Join("\n", panel.Names.Values));
        }
    }

    void OnJoin(ulong id)
    {
        ChatRpc($"* {panel.Names[id]} 입장");
        UsersRpc(string.Join("\n", panel.Names.Values));
    }

    void OnLeave(ulong id)
    {
        if (panel.Names.Remove(id, out var user)) ChatRpc($"* {user} 퇴장");
        UsersRpc(string.Join("\n", panel.Names.Values));
    }

    [Rpc(SendTo.Server)]
    void SendChatRpc(string msg, RpcParams rpcParams = default)
    {
        string who = panel.Names[rpcParams.Receive.SenderClientId];   // 호출자 식별
        ChatRpc($"[{who}] {msg}");                                    // 서버는 전달만 한다
    }

    [Rpc(SendTo.Everyone)] void ChatRpc(string line) => logText.text += line + "\n";
    [Rpc(SendTo.Everyone)] void UsersRpc(string list) => userListText.text = list;
}
```

**[확인할 것]** Host를 먼저 실행하고 Client를 접속시켜 상태 Text와 접속자 목록이 갱신되는지 확인한다. 인증 코드가 틀린 경우, 이미 있는 이름인 경우, 정원(4명)을 넘은 경우에 접속이 거부되고 사유가 클라이언트 상태 Text에 표시되는지 확인한다. 채팅 한 줄이 모든 접속자 화면에 한 번씩만 표시되는지, Client 종료 시 "퇴장"이 표시되는지, Host 종료 시 Client가 접속 끊김 상태가 되는지 확인한다. StartServer는 서버 역할만 하며 로컬 사용자가 없다는 점을 StartHost와 비교한다.

**[흔한 오류]** Client를 Host보다 먼저 실행한 경우, 같은 포트를 두 인스턴스가 사용하는 경우, Shutdown 없이 다시 StartClient를 호출하는 경우, ChatHub를 NetworkObject 없이 붙이거나 런타임에 만든 경우, UI 참조를 연결하지 않아 NullReferenceException이 나는 경우.

**[설명]** 이 채팅 예제가 서버의 역할 전부이다. 서버는 메시지의 내용을 판단하지 않고, 누가 접속했는지 관리(승인, 이름, 정원, 입장·퇴장 통지)하고 받은 메시지를 모두에게 전달한다. 인증 코드와 이름은 실습용 단순 확인이며, 실제 계정 인증은 5장(Game Database)과 6장에서 다룬다. 실습 3부터 플레이어 오브젝트가 필요하므로 이 씬의 Approve에서 CreatePlayerObject를 true로 바꾸고 Player Prefab을 등록한다. 실습 9의 GameManager는 이 Approve에 게임 단계 검사를 더한 것으로 대체한다.

실습 3 — 가상 이동 전달: 캐릭터 오브젝트 없이 좌표값만으로 이동을 검증한다. 각 클라이언트는 자신의 좌표를 직접 계산하고(방향 버튼 또는 자동 이동), 서버는 받은 좌표를 다른 접속자에게 중계한다. 이동 로직이 바뀌어도 GUI에서 바로 확인할 수 있다.

**[구현 순서]** 상·하·좌·우 버튼 4개, 자동 이동 Toggle, 좌표 목록 Text를 씬에 추가하고 아래 스크립트를 ChatHub 오브젝트에 붙인다. 자동 이동은 0.1초마다 임의 방향으로 이동하는 가상 입력이며, 클라이언트 여러 개를 켜 두면 이동 중계 부하도 함께 시험할 수 있다.

```csharp
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class VirtualMove : NetworkBehaviour
{
    [SerializeField] Button upBtn, downBtn, leftBtn, rightBtn;
    [SerializeField] Toggle autoToggle;
    [SerializeField] Text positionText;

    Vector2 myPos;                                                // 내 좌표는 내가 계산한다
    readonly Dictionary<ulong, Vector2> positions = new();
    float nextAuto;

    void Start()
    {
        upBtn.onClick.AddListener(() => Move(Vector2.up));
        downBtn.onClick.AddListener(() => Move(Vector2.down));
        leftBtn.onClick.AddListener(() => Move(Vector2.left));
        rightBtn.onClick.AddListener(() => Move(Vector2.right));
    }

    void Update()
    {
        if (!IsSpawned || !autoToggle.isOn || Time.time < nextAuto) return;
        nextAuto = Time.time + 0.1f;
        Move(Random.insideUnitCircle);                            // 가상 입력
    }

    void Move(Vector2 d)
    {
        if (!IsSpawned) return;
        myPos += d;
        PosRpc(myPos);                                            // 서버에 알린다
    }

    [Rpc(SendTo.Server)]
    void PosRpc(Vector2 pos, RpcParams rpcParams = default)
        => PosRelayRpc(rpcParams.Receive.SenderClientId, pos);    // 서버는 전달만 한다

    [Rpc(SendTo.Everyone)]
    void PosRelayRpc(ulong id, Vector2 pos)
    {
        positions[id] = pos;
        positionText.text = string.Join("\n", positions.Select(kv => $"{kv.Key}: ({kv.Value.x:F1}, {kv.Value.y:F1})"));
    }
}
```

**[확인할 것]** 클라이언트 둘의 좌표가 서로의 화면에 같은 값으로 갱신되는지 확인한다. 중간에 접속한 클라이언트는 이전 좌표를 받지 못한다는 점을 확인하고, 이를 실습 5의 NetworkList로 해결한다. 자동 이동을 켠 클라이언트 수를 늘리며 갱신 지연이 생기는 시점을 기록한다.

**[흔한 오류]** 스폰되기 전(접속 전)에 버튼을 눌러 RPC를 호출하는 경우, 같은 스크립트를 NetworkObject가 없는 오브젝트에 붙인 경우.

실습 4 — 채널(방) 생성·입장·퇴장: 서버가 채널 목록과 채널별 접속자를 관리하고, 채널 안의 메시지만 같은 채널 접속자에게 중계한다. 방 기반 서버(6장)의 기초가 되는 기능이다.

**[구현 순서]** 채널 이름 InputField, 생성·입장·퇴장·전송 버튼, 채널 목록 Text, 채널 대화 Text를 추가한다. 서버는 채널과 접속자의 관계를 자료구조로 관리하고 변경될 때마다 목록을 모두에게 알린다. 특정 접속자들에게만 보내는 RPC는 SendTo.SpecifiedInParams와 RpcTarget을 사용하며, 이 부분의 API 형식은 사용하는 NGO 버전에서 확인한다.

```csharp
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ChannelHub : NetworkBehaviour
{
    [SerializeField] InputField channelInput, chatInput;
    [SerializeField] Button createBtn, joinBtn, leaveBtn, sendBtn;
    [SerializeField] Text channelListText, channelLogText;

    readonly Dictionary<string, HashSet<ulong>> channels = new();   // 서버만 사용
    readonly Dictionary<ulong, string> where = new();               // 접속자가 속한 채널 (서버만 사용)

    void Start()
    {
        createBtn.onClick.AddListener(() => CreateRpc(channelInput.text));
        joinBtn.onClick.AddListener(() => JoinRpc(channelInput.text));
        leaveBtn.onClick.AddListener(() => LeaveRpc());
        sendBtn.onClick.AddListener(() => ChannelChatRpc(chatInput.text));
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) NetworkManager.OnClientDisconnectCallback += Exit;   // 접속이 끊기면 채널에서 제거
    }

    [Rpc(SendTo.Server)]
    void CreateRpc(string ch, RpcParams rpcParams = default)
    {
        if (string.IsNullOrEmpty(ch)) return;
        if (!channels.ContainsKey(ch)) channels[ch] = new HashSet<ulong>();
        Enter(rpcParams.Receive.SenderClientId, ch);
    }

    [Rpc(SendTo.Server)]
    void JoinRpc(string ch, RpcParams rpcParams = default)
    {
        if (channels.ContainsKey(ch)) Enter(rpcParams.Receive.SenderClientId, ch);
    }

    [Rpc(SendTo.Server)]
    void LeaveRpc(RpcParams rpcParams = default) => Exit(rpcParams.Receive.SenderClientId);

    void Enter(ulong id, string ch)
    {
        Exit(id);
        channels[ch].Add(id);
        where[id] = ch;
        Publish();
    }

    void Exit(ulong id)
    {
        if (where.Remove(id, out var ch))
        {
            channels[ch].Remove(id);
            if (channels[ch].Count == 0) channels.Remove(ch);
        }
        Publish();
    }

    void Publish() => ListRpc(string.Join("\n", channels.Select(kv => $"{kv.Key}: {kv.Value.Count}명")));

    [Rpc(SendTo.Server)]
    void ChannelChatRpc(string msg, RpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;
        if (!where.TryGetValue(id, out var ch)) return;
        LogRpc($"[{id}] {msg}", RpcTarget.Group(channels[ch], RpcTargetUse.Temp));   // 같은 채널에만 중계
    }

    [Rpc(SendTo.Everyone)]
    void ListRpc(string list) => channelListText.text = list;

    [Rpc(SendTo.SpecifiedInParams)]
    void LogRpc(string line, RpcParams rpcParams) => channelLogText.text += line + "\n";
}
```

**[확인할 것]** 채널 A와 B에 접속자를 나누어 넣고 한 채널의 메시지가 다른 채널에 보이지 않는지 확인한다. 채널 접속자가 접속을 끊으면 목록의 인원수가 줄고 빈 채널이 사라지는지 확인한다. 존재하지 않는 채널에 입장을 시도할 때 서버가 오류 없이 무시하는지 확인한다.

**[흔한 오류]** 서버의 자료구조를 클라이언트에서 읽으려는 경우(서버에서만 값이 있다), 접속 종료 콜백을 등록하지 않아 끊긴 접속자가 채널에 남는 경우.

실습 5 — 접속자 상태 목록(NetworkList): 접속자별 체력과 준비 상태를 서버가 하나의 목록으로 관리하고, 중간에 접속한 클라이언트도 현재 목록을 받게 한다. 클라이언트는 변경을 요청하고, 서버는 요청받은 값을 반영한다.

**[구현 순서]** 체력 증가·감소 버튼, 준비 토글 버튼, 상태 목록 Text를 추가한다. 목록은 NetworkList로 선언하며 서버만 쓸 수 있다. NetworkList는 Awake에서 생성해야 한다.

```csharp
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public struct UserState : INetworkSerializable, IEquatable<UserState>
{
    public ulong Id; public int Hp; public bool Ready;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Id); s.SerializeValue(ref Hp); s.SerializeValue(ref Ready);
    }
    public bool Equals(UserState o) => Id == o.Id && Hp == o.Hp && Ready == o.Ready;
}

public class StateHub : NetworkBehaviour
{
    [SerializeField] Button hpUpBtn, hpDownBtn, readyBtn;
    [SerializeField] Text stateText;

    NetworkList<UserState> states;

    void Awake() => states = new NetworkList<UserState>();

    void Start()
    {
        hpUpBtn.onClick.AddListener(() => ChangeRpc(+10, false));
        hpDownBtn.onClick.AddListener(() => ChangeRpc(-10, false));
        readyBtn.onClick.AddListener(() => ChangeRpc(0, true));
    }

    public override void OnNetworkSpawn()
    {
        states.OnListChanged += _ => Redraw();
        if (IsServer)
        {
            foreach (var id in NetworkManager.ConnectedClientsIds) Add(id);
            NetworkManager.OnClientConnectedCallback += Add;
            NetworkManager.OnClientDisconnectCallback += Remove;
        }
        Redraw();                                                   // 중간 참여자도 현재 목록으로 화면을 맞춘다
    }

    void Add(ulong id) => states.Add(new UserState { Id = id, Hp = 100, Ready = false });

    void Remove(ulong id)
    {
        for (int i = 0; i < states.Count; i++)
            if (states[i].Id == id) { states.RemoveAt(i); break; }
    }

    [Rpc(SendTo.Server)]
    void ChangeRpc(int dHp, bool toggleReady, RpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Id != id) continue;
            var s = states[i];
            s.Hp += dHp;
            if (toggleReady) s.Ready = !s.Ready;
            states[i] = s;                                          // 서버가 요청받은 값을 반영한다
            break;
        }
    }

    void Redraw()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var s in states) sb.AppendLine($"{s.Id}: HP {s.Hp}  {(s.Ready ? "준비" : "대기")}");
        stateText.text = sb.ToString();
    }
}
```

**[확인할 것]** 한 클라이언트가 체력을 바꾸면 모든 화면의 목록이 갱신되는지 확인한다. 변경을 마친 뒤 새 클라이언트를 접속시켜 현재 값이 바로 표시되는지 확인한다. 클라이언트에서 목록에 직접 값을 쓰려고 하면 쓰기 권한 때문에 거부되는지 확인한다. 단일 값 하나는 NetworkVariable, 접속자마다 있는 값의 모음은 NetworkList로 둔다는 차이를 정리한다.

**[흔한 오류]** NetworkList를 필드 초기화나 OnNetworkSpawn에서 생성하는 경우, 구조체에 Equals를 구현하지 않아 컴파일 오류가 나는 경우, OnListChanged 핸들러를 해제하지 않는 경우.

실습 6 — 요청과 응답(보너스 받기): 클라이언트의 요청을 서버가 처리하고 그 결과를 요청한 클라이언트에게만 돌려준다. 실무에서 서버 로직이 바뀔 때마다 GUI 버튼 하나로 바로 검증하는 방식을 익힌다.

**[구현 순서]** 보너스 받기 버튼과 결과 Text를 추가한다. 서버는 접속 중 한 번만 지급하도록 받은 사람을 기억하고, 결과를 요청한 클라이언트에게만 보낸다.

```csharp
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class BonusHub : NetworkBehaviour
{
    [SerializeField] Button bonusBtn;
    [SerializeField] Text resultText;

    readonly HashSet<ulong> claimed = new();                        // 서버만 사용

    void Start() => bonusBtn.onClick.AddListener(() => ClaimRpc());

    [Rpc(SendTo.Server)]
    void ClaimRpc(RpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;
        bool ok = claimed.Add(id);                                  // 이미 받았으면 false
        ResultRpc(ok, ok ? 100 : 0, RpcTarget.Single(id, RpcTargetUse.Temp));   // 요청한 사람에게만 응답
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void ResultRpc(bool ok, int amount, RpcParams rpcParams)
        => resultText.text = ok ? $"보너스 {amount} 지급" : "이미 받았습니다";
}
```

**[선택 기준]** 중간에 접속한 사람에게도 보여야 하는 지속 상태는 NetworkVariable 또는 NetworkList, 한 번 일어나고 지나가는 사건과 개별 응답은 RPC로 전달한다. RPC는 SendTo.Server, Everyone, NotOwner, SpecifiedInParams 등 대상 지정에 따라 실행되는 위치가 달라진다. 이전 자료의 [ServerRpc], [ClientRpc]는 각각 서버로 보내는 RPC, 클라이언트들에게 보내는 RPC에 대응하는 이전 방식이므로 사용하는 NGO 버전에서 지원 여부를 확인한다.

**[확인할 것]** 버튼을 두 번 눌렀을 때 두 번째에 "이미 받았습니다"가 표시되는지, 다른 클라이언트의 화면에는 응답이 표시되지 않는지 확인한다. 서버 코드의 지급 조건을 바꾸고 다시 실행하여 GUI만으로 바로 검증되는 것을 확인한다.

**[흔한 오류]** 응답을 SendTo.Everyone으로 보내 다른 접속자에게도 보이는 경우, 접속을 끊었다 다시 접속한 사용자를 같은 사용자로 볼지 정하지 않은 경우(clientId는 재접속 시 바뀐다).

실습 7 — NetworkObject 생성·제거: 서버가 NetworkObject를 Spawn하고 Despawn하여 모든 클라이언트에 복제한다. 클라이언트가 임의로 생성한 오브젝트는 복제되지 않는다는 점도 함께 확인한다.

**[구현 순서]** 큐브 모양 Item 프리팹에 NetworkObject를 붙이고 NetworkManager의 Network Prefabs 목록에 등록한다. 생성·제거 버튼과 개수 Text를 추가한다. 생성과 제거는 서버에서만 호출하며 클라이언트는 RPC로 요청한다.

```csharp
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SpawnHub : NetworkBehaviour
{
    [SerializeField] NetworkObject itemPrefab;
    [SerializeField] Button spawnBtn, despawnBtn;
    [SerializeField] Text countText;

    readonly List<NetworkObject> items = new();                     // 서버만 사용

    void Start()
    {
        spawnBtn.onClick.AddListener(() => SpawnRpc());
        despawnBtn.onClick.AddListener(() => DespawnRpc());
    }

    [Rpc(SendTo.Server)]
    void SpawnRpc(RpcParams rpcParams = default)
    {
        var o = Instantiate(itemPrefab, Random.insideUnitSphere * 3f, Quaternion.identity);
        o.SpawnWithOwnership(rpcParams.Receive.SenderClientId);     // 서버가 생성하면 모든 클라이언트에 복제된다
        items.Add(o);
        CountRpc(items.Count);
    }

    [Rpc(SendTo.Server)]
    void DespawnRpc()
    {
        if (items.Count == 0) return;
        var o = items[items.Count - 1];
        items.RemoveAt(items.Count - 1);
        o.Despawn();                                                // 제거도 서버에서만 한다
        CountRpc(items.Count);
    }

    [Rpc(SendTo.Everyone)]
    void CountRpc(int n) => countText.text = $"아이템 {n}개";
}
```

**[확인할 것]** 생성 버튼을 누르면 모든 클라이언트의 Scene 뷰에 큐브가 나타나는지, 제거하면 모두에서 사라지는지 확인한다. 클라이언트에서 Instantiate만 호출한 오브젝트가 다른 클라이언트에 나타나지 않는 것을 확인한다. 소유자를 확인하고(OwnerClientId) 소유권 이전(ChangeOwnership)을 시험한다.

**[흔한 오류]** 프리팹을 Network Prefabs 목록에 등록하지 않은 경우, Spawn 없이 Instantiate만 한 경우, Despawn 대신 Destroy를 호출한 경우.

실습 8 — 가상 부하와 지연 시험: 실습 3의 자동 이동과 Ping 버튼, 네트워크 시뮬레이터로 지연과 부하 상황을 GUI에서 재현한다. 실제 게임 화면 없이도 서버 로직이 지연이나 다수 접속에서 어떻게 반응하는지 확인할 수 있다.

**[구현 순서]** Ping 버튼과 RTT 표시 Text를 추가한다. 서버가 Ping에 즉시 응답하고 클라이언트가 왕복 시간을 측정한다. 지연은 Multiplayer Tools의 Network Simulator 또는 Unity Transport의 시뮬레이터 설정으로 주고, 사용하는 버전의 설정 방법을 확인한다.

```csharp
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PingTest : NetworkBehaviour
{
    [SerializeField] Button pingBtn;
    [SerializeField] Text pingText;

    float sentTime;

    void Start()
    {
        pingBtn.onClick.AddListener(() => { sentTime = Time.realtimeSinceStartup; PingRpc(); });
    }

    [Rpc(SendTo.Server)]
    void PingRpc(RpcParams rpcParams = default)
        => PongRpc(RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));

    [Rpc(SendTo.SpecifiedInParams)]
    void PongRpc(RpcParams rpcParams)
        => pingText.text = $"RTT {(Time.realtimeSinceStartup - sentTime) * 1000f:F0} ms";
}
```

**[확인할 것]** 지연을 0, 100, 300ms로 바꾸며 RTT가 이에 맞게 표시되는지 확인한다. 지연 상황에서 실습 3의 좌표 갱신, 실습 4의 채널 대화, 실습 6의 보너스 응답이 어떻게 달라지는지 기록한다. 자동 이동 클라이언트를 늘리며 RTT가 증가하기 시작하는 접속자 수를 기록한다.

**[흔한 오류]** Editor와 빌드본 사이의 로컬 접속에서 지연을 설정하지 않고 결과를 일반화하는 경우, 패킷 손실 설정을 켠 채로 Reliable 전송 결과를 비교하는 경우.

실습 9 — 게임 클라이언트에 적용: 채팅 씬에서 검증한 네트워크 코드를 게임 클라이언트로 옮긴다. 옮길 때는 네트워크 코드와 GUI를 분리하고, GUI 대신 게임 UI와 게임 오브젝트가 같은 이벤트를 받도록 바꾼다.

**[구현 순서]** 1. 네트워크 코드에서 Text와 Button 참조를 제거하고 이벤트로 결과를 알리도록 바꾼다. 2. 채팅 씬의 GUI 스크립트는 이 이벤트를 구독하는 검증용 UI로 남긴다. 3. 게임 씬에서는 게임 UI가 같은 이벤트를 구독한다. 4. 가상 좌표를 실제 오브젝트로 바꾼다.

```csharp
using System;
using Unity.Netcode;

public class ChatService : NetworkBehaviour                        // 네트워크 코드: UI를 모른다
{
    public event Action<string> OnLog;

    public void Send(string msg) { if (IsSpawned) SendChatRpc(msg); }

    [Rpc(SendTo.Server)]
    void SendChatRpc(string msg, RpcParams rpcParams = default)
        => ChatRpc($"[{rpcParams.Receive.SenderClientId}] {msg}");

    [Rpc(SendTo.Everyone)]
    void ChatRpc(string line) => OnLog?.Invoke(line);
}

// 채팅 씬(검증용)과 게임 씬이 같은 ChatService를 사용하고, UI만 다르다.
// void Start() { service.OnLog += line => logText.text += line + "\n"; }
```

가상 좌표를 실제 캐릭터로 바꿀 때는 캡슐 모양의 Player Prefab에 NetworkObject, NetworkTransform, 이동 스크립트를 붙이고 NetworkManager의 Player Prefab 항목에 등록한다. 실습 2의 Approve에서 CreatePlayerObject를 true로 바꾼다. 이 장의 기본 방식은 소유 클라이언트가 직접 이동하고 서버가 위치를 중계하는 것이므로 NetworkTransform을 Owner 권한으로 동작시킨다(NGO 1.x는 ClientNetworkTransform, 2.x는 Authority Mode를 Owner로 설정하며 사용하는 버전의 문서를 확인한다).

```csharp
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerMove : NetworkBehaviour
{
    [SerializeField] float speed = 5f;

    void Update()
    {
        if (!IsOwner) return;                                       // 내 플레이어만 입력을 읽는다
        var dir = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        transform.position += dir.normalized * speed * Time.deltaTime;
    }
}

public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;     // NGO 1.x 방식
}
```

**[핵심 개념]** 같은 Player 오브젝트가 접속자 수만큼의 인스턴스로 존재하므로 코드가 어느 인스턴스에서 실행되는지 구분해야 한다. IsServer는 서버 로직을 실행하는 인스턴스(Host 포함), IsClient는 클라이언트 역할의 인스턴스, IsHost는 둘 다인 경우, IsOwner는 이 오브젝트의 소유자가 이 인스턴스의 클라이언트인 경우, IsLocalPlayer는 이 인스턴스의 로컬 플레이어 오브젝트인 경우이다. 소유권이나 NetworkVariable 값에 의존하는 초기화는 Awake와 Start가 아니라 OnNetworkSpawn에서 수행한다.

**[시험 시나리오]** 아래 상황에서 먼저 채팅 씬의 GUI로 서버가 의도한 대로 동작하는지 확인하고, 게임 클라이언트에서도 같은 결과가 나오는지 비교한다.

- 정원을 넘은 접속 시도, 인증 코드가 틀린 접속 시도
- 채널 대화 중 접속자가 강제 종료되는 경우
- 상태 변경 후 새 클라이언트가 접속하는 경우
- 플레이 중 Host가 종료되는 경우
- RTT 300ms 환경에서 이동과 채널 대화를 진행하는 경우

실습 진행 원칙: 참고 글의 기능별 학습 흐름을 따르되 예제 코드는 직접 작성한다. 2024년 글에 등장하는 API 명칭·NetworkPrefab 등록 방식·권한 설정은 교재에서 확정한 NGO 패키지 버전을 기준으로 다시 검증한다.

## 2.3 실습 프로젝트

2.2.1의 채팅 GUI 씬을 하나의 프로젝트로 묶고, 검증된 코드를 2~4인 규모의 게임 클라이언트(캐릭터 이동)에 적용한다. 플레이어 접속, 이동·상호작용 동기화, 게임 진행을 접속자 관리와 메시지 중계 기준으로 검증한다.

지연·패킷 손실 시험에는 Multiplayer Tools의 통계 및 네트워크 시뮬레이션 기능을 사용 가능한 범위에서 활용한다. 단순 위치 보간과 클라이언트 예측/서버 재조정은 서로 다르며, NGO가 모든 지연 보정 알고리즘을 자동 제공한다고 설명하지 않는다.

이 장의 프로젝트는 이동·공격 같은 게임 규칙을 소유 클라이언트가 처리하고, 호스트(서버)는 접속자 관리와 중계를 맡는다. NetworkTransform의 Owner Authority나 NetworkVariable의 Owner 쓰기 권한은 편의 기능이지만, 경쟁형 게임의 최종 판정을 클라이언트에 맡겨도 안전하다는 의미는 아니다.

### 2.3.1 프로젝트 요구사항

- 접속: Host와 Client 접속, 최대 인원 제한, 접속 종료와 재접속
- 상태: 플레이어 체력과 준비 상태, 게임 단계(Lobby, Playing, Result)
- 상호작용: 아이템 획득 같은 상호작용 한 가지 이상을 소유 클라이언트가 처리하고 결과를 중계
- 오브젝트: 투사체 또는 아이템을 서버가 Spawn과 Despawn
- 진행: 모든 플레이어가 준비하면 시작하고 종료 조건을 만족하면 결과 표시

### 2.3.2 역할 분담표

프로젝트의 각 행동에 대해 처리하는 쪽, 서버의 역할, 전파 방식을 표로 작성하고 구현이 표와 일치하는지 점검한다.

- 이동: 소유 클라이언트가 처리하고 서버는 위치를 중계한다. 전파는 NetworkTransform이다.
- 공격: 소유 클라이언트가 결과를 계산하여 RPC로 서버에 알리고, 서버가 다른 클라이언트에 중계한다. 전파는 체력 NetworkVariable과 이펙트 RPC이다.
- 아이템 획득: 소유 클라이언트가 충돌을 판단하여 서버에 알리고, 서버가 Despawn한다.
- 접속과 종료: 서버가 접속을 승인하고 접속자 목록을 관리하며 입장·퇴장을 모든 클라이언트에 알린다.
- 게임 시작과 종료: 서버가 접속자의 준비 상태를 확인하고 게임 단계를 전환한다.

### 2.3.3 구현 순서와 점검

1. 채팅 GUI 씬에서 접속·채널·상태·보너스 검증  2. 게임 씬에 접속과 플레이어 생성  3. 이동 동기화  4. 준비 상태와 게임 단계  5. 상호작용  6. 오브젝트 생성·제거  7. 종료와 결과 화면  8. 접속 예외 상황 시험

접속자 관리 점검을 위해 정원 초과 접속, 게임 시작 후 중간 접속, 플레이 중 클라이언트 강제 종료, Host 종료, 재접속을 시험한다. 각 상황에서 서버(호스트)의 접속자 목록과 클라이언트 화면이 일치하는지 기록하여 역할 분담표와 함께 제출한다.

## 2.4 네트워크 지연과 동기화 품질

RTT·지터·패킷 손실이 게임 상태 표현에 미치는 영향을 테스트한다. 보간(Interpolation), 외삽(Extrapolation), 추측항법(Dead Reckoning), 클라이언트 예측과 서버 재조정의 개념을 비교하고, Unity Netcode에서 제공하는 동기화·전송 기능과 직접 구현해야 할 기능을 구분한다.

학습자가 소규모 게임의 지연 상황을 재현하고 캐릭터 움직임 및 상태 갱신의 차이를 관찰하는 것을 필수 실습으로 삼는다.

### 2.4.1 지연 시뮬레이션 실습

**[구현 순서]** Multiplayer Tools가 제공하는 Network Simulator 등 사용하는 버전에서 지원하는 도구로 지연, 지터, 패킷 손실을 설정한다. 조건은 RTT 0ms, 100ms, 300ms와 손실률 0%, 5%, 10%를 조합한다.

**[관찰 항목]** 입력 후 내 캐릭터가 반응하는 시간, 다른 플레이어의 움직임이 끊기거나 튀는 정도, NetworkVariable 갱신 지연, RPC 도착 순서를 조건별로 기록한다.

### 2.4.2 개념 정리

- 보간(Interpolation): 과거에 받은 두 상태 사이를 부드럽게 채운다. 약간 과거를 보여 주는 대신 끊김을 줄인다.
- 외삽(Extrapolation): 마지막으로 받은 상태와 속도를 이용해 미래 위치를 추정한다. 방향이 바뀌면 오차가 커진다.
- 추측항법(Dead Reckoning): 속도와 방향으로 위치를 예측하고, 예측과 실제의 차이가 클 때만 갱신을 보낸다.
- 클라이언트 예측: 내 입력을 서버 응답 전에 로컬에서 먼저 적용한다.
- 서버 재조정(Reconciliation): 서버 결과가 예측과 다르면 서버 상태로 되돌리고 그 이후의 입력을 다시 적용한다.

### 2.4.3 NGO가 제공하는 것과 직접 구현할 것

- 제공: NetworkTransform의 보간과 전송 임계값, 틱 기반 상태 전송, 신뢰성 있는 RPC와 NetworkVariable
- 직접 구현: 입력 순번 관리, 클라이언트 예측과 서버 재조정, 지연 보상(Lag Compensation)

사용하는 NGO 버전에 따라 예측을 돕는 기능이 추가되어 있을 수 있으므로 버전 문서에서 제공 범위를 확인하고, 제공되지 않는 부분만 직접 구현한다고 정리한다.

## 2.5 Steam 연동과 참고자료

본 장에서는 Unity Transport를 사용하여 NGO의 네트워크 객체·게임 규칙을 먼저 완성한다. 4장 Steam Listen Server에서는 동일한 Unity 프로젝트에 Steam 로비·친구 초대 기능을 추가하고, Unity Netcode와 호환되는 Steam 기반 Transport를 적용한다. Unity Transport만 설정했다고 Steam P2P/Relay 연결이 자동으로 되는 것은 아니다.

실습 참고자료: 고라니 유니티 강좌, 「멀티 서버 게임오브젝트 넷코드 마스터하기」(2024-12-27), https://goranitv.tistory.com/30 — 설치, 접속, 위치·변수·RPC·오브젝트 동기화의 단계별 학습 순서를 참고한다. 예제 코드와 설명을 그대로 복제하지 않고 교재용 프로젝트에 맞추어 재구성한다.

후속 참고자료: Unity Gaming Services Lobby — https://goranitv.tistory.com/31 / Unity Gaming Services Relay — https://goranitv.tistory.com/32. 두 서비스는 4장의 Steamworks Lobby/Steam Datagram Relay와 다른 플랫폼이며, 본 장에서는 선택 확장으로만 소개한다.

공식 기술 기준: Unity Netcode for GameObjects 패키지 문서 https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@latest/ . 교재 원고와 코드에는 Unity Editor, NGO, Unity Transport, Multiplayer Tools의 실제 검증 버전을 함께 기재한다. 2024년 블로그의 패키지 설정과 코드 동작은 최신 프로젝트에서 재확인한다.

### 2.5.1 검증 버전 기록

- Unity Editor: [기재]  /  Netcode for GameObjects: [기재]
- Unity Transport: [기재]  /  Multiplayer Tools: [기재]
- 4장에서 사용할 Steam 기반 Transport 구현체와 호환 버전: [확인 후 기재]

## 2.6 NGO의 내부 구조와 동기화 원리

앞의 실습은 NGO가 제공하는 기능을 사용하는 방법이었다. 이 절은 그 기능이 내부에서 어떻게 동작하는지 설명한다. 원리를 알면 "왜 값이 안 바뀌는가", "왜 RPC가 호출되지 않는가" 같은 문제를 추측이 아니라 근거로 해결할 수 있다.

### 2.6.1 구성 요소와 메시지 흐름

- NetworkManager: 연결의 시작과 종료, 접속자 목록(ConnectedClients), 승인 콜백, 틱 시스템을 관리한다. 씬에 하나만 둔다.
- Transport: 실제 바이트를 주고받는 계층이다. 기본은 Unity Transport(UDP 기반)이며 교체할 수 있다. 4장에서 Steam 연결로 바꿀 때 교체되는 부분이 이 계층이다.
- NetworkObject: 네트워크로 동기화할 게임 오브젝트에 붙는 식별 컴포넌트이다. 각 오브젝트에 NetworkObjectId가 부여되고, 소유자(OwnerClientId)가 정해진다. 프리팹을 NetworkManager의 Network Prefabs 목록에 등록해야 Spawn할 수 있다.
- NetworkBehaviour: 네트워크 기능(NetworkVariable, RPC)을 쓰는 스크립트의 부모 클래스이다. OnNetworkSpawn과 OnNetworkDespawn에서 초기화와 정리를 하며, Awake나 Start 시점에는 네트워크 상태가 확정되지 않았을 수 있다.

```text
[Client A]                         [Host/Server]                       [Client B]
 NetworkBehaviour  -- RPC/Var -->   NetworkManager  -- Spawn/Var -->   NetworkBehaviour
     |                                 |   (승인, 소유, 틱)                  |
  Transport  <============ UDP =======> Transport <===== UDP =======>  Transport
```

Spawn은 서버만 할 수 있다. 서버가 NetworkObject를 Spawn하면 접속해 있는 모든 클라이언트에게 생성 메시지가 가고, 나중에 접속한 클라이언트에게도 현재 존재하는 오브젝트의 생성 메시지가 전달된다. 그래서 중도 참여자도 같은 장면을 보게 된다.

### 2.6.2 NetworkVariable의 동작

NetworkVariable은 값이 바뀔 때만 전송되는 상태 동기화 수단이다. 쓰기 권한(Write Permission)을 가진 쪽(기본은 서버)이 값을 바꾸면 틱 경계에서 변경 사항이 모아져 전송되고, 읽는 쪽은 OnValueChanged 콜백으로 변화를 받는다.

- 쓰기 권한이 없는 쪽이 값을 바꾸면 예외가 발생한다. 소유자가 직접 값을 바꾸게 하려면 Write Permission을 Owner로 지정한다.
- 같은 틱에 여러 번 바꾸면 마지막 값만 전달된다. 중간 값이 모두 필요하면 NetworkVariable이 아니라 RPC나 NetworkList를 쓴다.
- 새 클라이언트가 접속하면 현재 값을 한 번에 받는다. 이 점이 RPC와 가장 큰 차이이다. RPC는 호출 시점에 있던 클라이언트에게만 전달되고, 나중에 들어온 클라이언트는 과거 호출을 받지 못한다.
- 값 타입은 직렬화 가능한 구조여야 한다. 사용자 구조체는 INetworkSerializable을 구현하고, 문자열은 FixedString처럼 크기가 고정된 형식을 쓰는 것이 일반적이다.

### 2.6.3 RPC의 종류와 신뢰성

RPC(Remote Procedure Call)는 한 번 일어난 일을 원격에서 실행하게 하는 호출이다. 서버로 보내는 ServerRpc, 클라이언트로 보내는 ClientRpc로 나뉘고, 사용하는 NGO 버전에 따라 속성(attribute) 이름과 지정 방식이 다르므로 설치한 버전의 문서를 확인한다.

- 기본은 신뢰성 있는 전송으로, 순서와 도착이 보장된다. 위치처럼 낡으면 의미가 없는 데이터에 쓰면 HOL Blocking을 겪을 수 있으므로 비신뢰 전송(Unreliable)을 선택할 수 있다.
- ServerRpc는 기본적으로 오브젝트의 소유자만 호출할 수 있다. 소유권이 없는 오브젝트에서 호출하려면 소유권 요구를 끄는 옵션을 쓰되, 그때는 서버에서 호출자를 검증해야 한다.
- RPC 안에서는 호출한 클라이언트의 ID(ServerRpcParams)를 얻을 수 있다. 채팅 서버에서 보낸 사람의 이름을 서버가 붙여 전달하는 이유는, 클라이언트가 보내는 이름을 믿지 않고 서버가 아는 접속자 정보를 쓰기 위해서이다.
- 호스트는 서버이면서 클라이언트이므로 자신이 호출한 ServerRpc가 같은 프로세스에서 실행된다. 호스트에서만 시험하면 이 차이가 드러나지 않으므로 반드시 별도 클라이언트로도 시험한다.

### 2.6.4 NetworkTransform과 틱

NetworkTransform은 위치·회전·크기를 NetworkVariable과 비슷한 방식으로 동기화한다. 기본은 서버 권한이므로 소유 클라이언트가 직접 움직이려면 소유자 권한을 쓰는 설정(버전에 따라 방법이 다르다)이 필요하다.

- 변화량이 임계값(threshold)보다 작으면 전송하지 않아 대역폭을 줄인다. 임계값이 크면 움직임이 계단처럼 보이고, 작으면 전송량이 늘어난다.
- Interpolate를 켜면 받은 값 사이를 부드럽게 이어서 보여 준다. 대신 화면에 보이는 위치가 실제 값보다 약간 과거의 값이 된다.
- 네트워크 틱은 NetworkManager 설정의 Tick Rate로 정한다. 초당 30틱이 기본값으로 알려져 있으며, 위의 변경 모으기와 전송이 이 틱에 맞춰 일어난다. 게임 프레임(Update)과 네트워크 틱은 서로 다른 주기이다.

## 2.7 지연 보정 기법

서버가 값을 정하고 클라이언트가 그것을 받아 보여 주는 구조에서는 모든 동작에 왕복 지연이 끼어든다. RTT가 100ms이면 키를 누르고 화면이 반응하기까지 최소 100ms가 걸리므로, 상용 게임은 아래 기법으로 이를 숨긴다. 이 교재의 엔진 파트는 소유자 권한 이동을 쓰므로 이런 보정이 필요 없지만, 서버 권한 게임(6장 이후)에서는 필수이다.

### 2.7.1 보간(Interpolation)

다른 플레이어의 움직임은 전송 간격(예: 33ms)마다 도착한다. 도착한 값으로 바로 이동시키면 순간이동하듯 끊겨 보이므로, 보여 주는 시각을 일부러 조금 과거로(예: 100ms 전) 두고 받은 두 스냅샷 사이를 선형 보간한다. 패킷 하나가 늦거나 사라져도 다음 값이 있으므로 부드럽게 보인다.

```text
서버 시각:    t0     t1     t2     t3
스냅샷:       S0     S1     S2     S3  ...
화면 시각:                 ^ (현재 - 보간 지연)
                      S1 <----- 보간 ----> S2
```

보간 지연은 전송 간격의 2~3배가 흔히 쓰이며, 지연이 클수록 부드럽지만 상대의 모습이 더 과거가 된다. 이 값이 클수록 상대 화면의 반응이 늦어지는 것이 보간의 대가이다.

### 2.7.2 외삽(Extrapolation)과 추측 항법

다음 스냅샷이 아직 도착하지 않았을 때 마지막 속도로 위치를 계속 예측하는 방식이 외삽(추측 항법, dead reckoning)이다. 직선으로 달리는 자동차에는 잘 맞지만 방향을 급하게 바꾸는 캐릭터에는 틀리기 쉽다. 틀리면 새 스냅샷이 도착했을 때 위치가 튀므로 시간 제한을 두거나 부드럽게 보정한다.

### 2.7.3 클라이언트 예측과 서버 보정

서버 권한 방식에서 내 캐릭터가 입력 후 100ms 뒤에야 움직이면 조작감이 나쁘다. 그래서 내 클라이언트는 입력을 받자마자 결과를 예측해 먼저 움직이고(client-side prediction), 서버에는 입력을 보낸다. 서버가 계산한 결과가 돌아오면 예측과 비교해 다르면 서버 값으로 되돌린다(reconciliation).

1. 클라이언트가 입력에 순번(예: 입력 #57)을 붙여 서버로 보내고, 같은 입력을 자신에게도 바로 적용한다. 보낸 입력은 목록에 보관한다.
2. 서버는 입력을 순서대로 처리하고, 결과 상태와 "마지막으로 처리한 입력 번호(#57)"를 클라이언트에게 보낸다.
3. 클라이언트는 서버가 처리한 번호까지의 입력을 목록에서 지우고, 서버가 보낸 상태에서 시작해 아직 확인되지 않은 입력(#58, #59...)을 다시 적용한다.
4. 예측이 맞았으면 화면에 변화가 없고, 틀렸으면 위치가 보정된다.

이 방식에서는 서버와 클라이언트의 시뮬레이션 코드가 같은 입력에 같은 결과를 내야 한다. 그래서 이동 계산을 공통 함수로 만들어 양쪽이 같이 쓴다. Unreal의 CharacterMovementComponent가 이 구조를 이미 구현해 두었다(3.5절).

### 2.7.4 지연 보상(Lag Compensation)

사격 게임에서 내 화면의 적을 맞췄는데 서버에서는 적이 이미 이동한 뒤일 수 있다. 서버가 발사 요청을 받으면 그 클라이언트가 본 시점(요청 시각 - 그 클라이언트의 지연 - 보간 지연)으로 적의 위치를 되돌려 놓고 명중을 판정하는 것이 지연 보상이다. 맞은 쪽에서는 벽 뒤로 숨은 뒤 맞는 것처럼 느껴질 수 있으므로, 보상 범위를 제한(예: 최대 200ms)한다.

이 기법들의 핵심은 지연을 없앨 수는 없으니 보이는 방식으로 감춘다는 점과, 그만큼 화면에 보이는 상태와 서버의 실제 상태가 달라질 수 있다는 점이다.

## 2.8 관심 영역과 대역폭 관리

접속자가 늘면 서버가 모든 클라이언트에게 모든 오브젝트의 상태를 보내는 방식은 곧 한계에 닿는다. 100명이 서로의 상태를 모두 받으면 한 틱에 100×100 = 10,000건이 오간다. 그래서 각 플레이어에게 의미 있는 오브젝트만 보낸다. 이를 관심 영역 관리(Interest Management)라고 한다.

- 거리 기반: 플레이어 주변 일정 반경 안의 오브젝트만 전송한다.
- 격자(grid) 기반: 월드를 칸으로 나누고 같은 칸과 인접 칸의 오브젝트만 전송한다. 거리 계산 비용을 줄인다.
- 방 단위: 같은 방에 있는 플레이어끼리만 전송한다. 6장의 구조이다.

NGO에서는 NetworkObject의 가시성(visibility)을 서버가 지정하는 기능(NetworkShow, NetworkHide, CheckObjectVisibility)으로 구현할 수 있다. 보이지 않는 오브젝트는 해당 클라이언트에 생성되지 않으므로 대역폭뿐 아니라 월핵 방지에도 도움이 된다.

그 밖에 전송량을 줄이는 방법은 1.6절의 양자화와 델타 압축, 갱신 빈도를 거리나 중요도에 따라 달리하는 방법(가까운 상대는 30Hz, 먼 상대는 5Hz)이 있다.

## 2.9 2장 연습 문제

1. NetworkVariable과 ClientRpc의 차이를 "나중에 접속한 클라이언트가 받는 것"을 기준으로 설명하라.
2. 보간 지연을 100ms로 두면 상대 플레이어는 화면에서 어떻게 보이는지, 지연을 0으로 하면 어떤 문제가 생기는지 설명하라.
3. 서버 틱이 30Hz이고 RTT가 80ms일 때, 클라이언트 예측이 없는 서버 권한 이동은 입력 후 화면 반응까지 최소 몇 ms가 걸리는가?
4. 클라이언트 예측과 서버 보정에서 입력에 순번을 붙이는 이유를 쓰고, 서버 보정 후 클라이언트가 해야 할 일을 순서대로 나열하라.
5. 채팅 프로그램에서 서버가 보낸 사람 이름을 직접 붙여 중계해야 하는 이유를 신뢰 경계 관점에서 설명하라.
6. 100명이 같은 방에 있을 때 모두에게 모든 위치를 보내는 방식의 전송량을 구하고, 거리 기반 관심 영역으로 줄이는 방법을 제시하라.
