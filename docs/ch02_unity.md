# 2. Unity Game Server

## 2.1 학습 목표

Unity Netcode for GameObjects를 통해 네트워크 객체의 생성, 소유권, 상태 동기화, 원격 호출을 이해하고 소규모 멀티플레이 게임을 구현한다. 이 Part는 엔진이 제공하는 네트워킹 기술을 활용하는 단계이며 별도의 순수 C++ 서버를 직접 제작하지 않는다.

이 장은 네트워크와 C++ 지식은 있지만 Unity를 처음 쓰는 학습자가 교재만 보고 혼자서 따라 해도 Netcode for GameObjects(이하 NGO)를 쓸 수 있도록 구성했다. 에디터 설치부터 시작하여, 개념을 그림으로 먼저 잡고 바로 실습하는 순서로 진행한다.

### 2.1.1 이 장을 마치면

- Host, Client, Server의 역할을 구분하고 접속·종료·재접속 흐름을 구현한다.
- NetworkObject의 생성·제거와 소유권 이전을 서버 기준으로 처리한다.
- 지속적인 상태는 NetworkVariable로, 일회성 사건은 RPC로 전달하는 기준을 설명한다.
- 접속 승인과 정원 제한, 접속자 목록, 입장·퇴장 통지로 접속자를 관리한다.
- 지연 상황에서 동기화 결과를 관찰한다.
- NGO가 한 프레임 안에서 메시지를 처리하는 순서와 틱을 설명하고, 지연을 숨기는 기법(보간, 클라이언트 예측과 서버 보정, 지연 보상)과 관심 영역으로 전송량을 줄이는 방법을 설명한다.
- Unity 6.3 LTS에 NGO를 설치하고, 한 대의 PC에서 에디터와 빌드로 서버 하나와 클라이언트 여러 개를 실행하여 시험한다.
- 스스로 작성한 NetworkBehaviour 스크립트에서 IsOwner, IsServer 같은 플래그로 "누가 이 코드를 실행하는가"를 구분한다.
- 채팅 GUI 하나로 접속, 인증, 접속자 목록, 채널, 요청과 응답, 오브젝트 생성·제거를 검증하는 도구를 완성한다.
- 오류가 났을 때 로그와 오류 사전을 보고 원인을 좁힌다.

### 2.1.2 서버의 역할: 이 장의 범위

이 장에서 서버(또는 호스트)는 접속자 관리와 메시지 중계만 맡는다. 이동과 상호작용 같은 게임 규칙은 소유한 클라이언트가 처리하고, 그 결과를 NetworkVariable과 RPC로 서버를 거쳐 다른 클라이언트에 전달한다. 이 방식은 구현이 단순한 대신 클라이언트의 조작에 취약하다는 점을 인지하고 진행한다.

NGO에서 NetworkObject의 생성·제거(Spawn, Despawn)는 서버만 호출할 수 있다. NetworkTransform은 기본적으로 서버 권한으로 동작하며, 소유 클라이언트가 자신의 이동을 직접 결정하게 하려면 Owner 권한으로 바꾸어야 한다. NGO 2.x에서는 NetworkTransform의 Authority Mode를 Owner로 바꾸면 된다(2.7절). NGO 1.x 자료에 나오는 ClientNetworkTransform 클래스를 직접 만드는 방법은 2.x에서는 필요하지 않다.

### 2.1.3 C#과 Unity 선수 지식 점검

이 장의 코드는 C#으로 작성한다. 대상 학습자는 C++ 경험이 있으므로 다음 차이만 확인하고 시작한다.

- 클래스는 참조 타입이고 struct는 값 타입이다. 메모리는 가비지 컬렉터가 관리하므로 delete에 해당하는 코드를 작성하지 않는다.
- 이벤트와 델리게이트는 +=로 핸들러를 등록하고 -=로 해제한다. NetworkVariable.OnValueChanged와 NetworkManager 콜백이 이 방식이며, 등록한 핸들러는 오브젝트가 제거될 때 해제해야 한다.
- 네트워크로 전송하는 값은 int, float, bool, enum, struct 같은 값 타입을 주로 사용한다. 문자열은 FixedString 같은 전용 타입을 사용한다.
- MonoBehaviour의 생명주기(Awake, Start, Update, OnDestroy)를 이해한다. 네트워크 오브젝트에는 OnNetworkSpawn, OnNetworkDespawn이 추가된다.
- 프리팹, 씬, 컴포넌트, Inspector에서의 참조 연결([SerializeField])을 사용할 수 있어야 한다. Unity를 처음 쓰는 학습자는 2.2절과 2.3절을 따라 하면 이 장을 진행할 수 있다.

### 2.1.4 주요 학습 내용

Host / Client / Server의 역할 및 접속 흐름

NetworkManager, NetworkObject, NetworkBehaviour와 네트워크 객체의 생성·제거

NetworkVariable과 RPC를 이용한 상태 및 이벤트 동기화

플레이어 이동, 소유권, 서버 권한, 지연 및 연결 종료 처리

### 2.1.5 이 장의 진행 방식

이 장은 크게 세 덩어리로 이루어진다.

- 2.2절~2.3절: Unity 6.3 LTS 설치, 에디터 사용법, C# 개발자를 위한 Unity 기본기. Unity를 이미 아는 학습자는 건너뛴다.
- 2.4절~2.14절: NGO를 단계별로 익히는 실습. 하나의 프로젝트(NgoChat)를 계속 키워 가며, 앞 실습의 결과 위에 다음 실습을 쌓는다.
- 2.15절~2.20절: 설계 이론과 마무리. NGO의 내부 구조(2.15절), 지연과 지연 보정 기법(2.16절), 관심 영역(2.17절), 실습 프로젝트(2.18절), 참고자료와 4장으로의 연결(2.19절), 연습 문제(2.20절).

모든 실습은 같은 형식을 따른다. 무엇을 만드는지(목표), 따라 할 순서(단계), 실행하여 확인할 것, 그 뒤에서 일어난 일(원리), 자주 나는 오류 순이다. 단계는 번호를 따라 그대로 진행하면 되도록 썼다. 에디터는 영어 인터페이스를 기준으로 설명하고 메뉴는 `File > Build Profiles`처럼 > 로 이어 적는다.

실습 번호는 0번부터 이어진다.

- 실습 0(2.3절): 키보드로 움직이는 캡슐. Unity 기본 조작.
- 실습 1(2.5절): 버튼으로 Host, Client, Server를 시작한다.
- 실습 2(2.6절): 접속한 클라이언트마다 플레이어 캡슐이 생긴다.
- 실습 3(2.7절): 내 캡슐만 키보드로 움직이고 모두에게 보인다.
- 실습 4(2.8절): NetworkVariable로 닉네임과 색을 동기화한다.
- 실습 5(2.9절): RPC로 핑을 주고받아 왕복 시간을 잰다.
- 실습 6~7(2.10절): 접속·종료 이벤트와 접속 승인.
- 실습 8~12(2.11절): 채팅 프로그램 완성(접속자 목록과 입장·퇴장 알림, 채팅·귓속말·공지, 채널, 상태 목록, 요청과 응답).
- 실습 13(2.12절): 서버가 오브젝트를 생성·제거하고 소유권을 넘긴다.
- 실습 14(2.13절): 지연과 패킷 손실을 걸고 이동, 상태, 채팅이 어떻게 달라지는지 관찰한다.
- 실습 15(2.14절): 서버 전용 빌드를 만들어 다른 PC에서 접속한다(선택).
- 실습 16(2.17절): 서버가 가까운 캡슐만 보이게 하여 전송 범위를 줄인다(선택).

2.18절의 실습 프로젝트는 번호 없이, 지금까지 만든 것을 합쳐 소규모 멀티플레이 게임 한 판을 만든다.

### 2.1.6 준비물과 버전 기록

- 운영체제: Windows 10 또는 11 64비트.
- 하드웨어: 메모리 16GB 이상 권장. 에디터와 프로젝트, 빌드 결과를 합쳐 디스크 여유 공간 20GB 이상.
- 계정: Unity 계정(무료 Personal 라이선스로 진행 가능하다. 라이선스 조건은 바뀔 수 있으므로 Unity Hub의 안내를 따른다).
- 코드 편집기: Visual Studio 2022 Community 또는 Visual Studio Code, JetBrains Rider 중 하나. Unity Hub에서 에디터를 설치할 때 Visual Studio를 함께 설치할 수 있다.
- 네트워크: 같은 PC에서 모든 실습이 가능하다. 다른 PC와 연결하는 실습(2.14절)은 선택이다.

이 장은 다음 버전을 기준으로 작성했다. 학습 시점의 버전이 다르면 이 기록과 비교하여 차이를 확인한다. 어디까지 확인했는지는 2.19.2절에 적었다.

- Unity Editor: Unity 6.3 LTS (버전 번호 6000.3.x). Hub에 표시되는 정확한 번호는 각자 기록해 둔다.
- Netcode for GameObjects: 2.x 계열. 이 교재의 API와 Inspector 설명은 2.13.3의 공식 문서·소스로 확인했다.
- Unity Transport: 2.6.0 (NGO 2.13.3이 함께 설치한다).
- Multiplayer Tools: 선택 설치. Network Simulator와 Runtime Net Stats Monitor에 사용한다(2.13절).
- Multiplayer Play Mode: 한 프로젝트를 여러 에디터 창으로 동시에 실행하는 패키지(2.13절).

**[버전 주의]** NGO 3.x 계열은 Unity 6000.7 이상과 Netcode for Entities를 요구하므로 Unity 6.3 LTS에는 설치하지 않는다. 또 NGO 1.x는 Unity 6000.3부터 지원되지 않는다. 인터넷에 있는 NGO 강좌와 블로그 글은 상당수가 1.x 기준이므로, 코드를 그대로 가져오지 말고 2.9.6절의 대응을 참고하여 바꾸어 쓴다.

## 2.2 Unity 6.3 LTS 시작하기

이 절은 Unity를 처음 설치하고 첫 프로젝트를 여는 과정이다. 이미 Unity 6.3 LTS와 프로젝트가 준비된 학습자는 2.2.4절의 설정 확인만 하고 2.3절로 넘어간다.

### 2.2.1 Unity Hub와 에디터 설치

Unity Hub는 에디터 설치와 프로젝트 목록을 관리하는 프로그램이다. 에디터(Editor)는 실제로 게임을 만드는 프로그램이다. 둘을 따로 설치한다.

1. Unity 공식 사이트(unity.com)의 다운로드 페이지에서 Unity Hub를 받아 설치한다.
2. Hub를 실행하여 Unity 계정으로 로그인한다. 계정이 없으면 Hub의 안내에 따라 만든다. 처음 로그인하면 라이선스(Personal) 활성화 안내가 나오면 그대로 진행한다.
3. Hub 왼쪽 메뉴에서 **Installs**를 열고 **Install Editor**를 누른다.
4. 목록에서 **Unity 6.3 LTS (6000.3.x)** 를 고른다. LTS(Long Term Support)는 장기간 안정화 업데이트를 받는 버전이다. 같은 6.3 안에서 x는 계속 올라가므로 가장 위의 최신 6000.3.x를 고르면 된다. 고른 정확한 번호는 2.1.6절의 버전 기록에 적어 둔다.
5. 설치할 모듈 선택 화면에서 다음을 확인한다.
   - 코드 편집기: **Microsoft Visual Studio Community 2022**를 체크하면 Unity 연동 설정까지 함께 설치된다. 이미 쓰는 편집기가 있으면 체크하지 않아도 된다.
   - **Windows Build Support**: Windows용 빌드(.exe)를 만들기 위한 항목이다. 이 교재의 빌드 실습에 사용한다. Windows 에디터에서는 기본으로 들어 있는 경우가 많으니 목록에 있으면 체크 상태를 유지한다.
   - 나머지(Android, iOS, WebGL 등)는 필요 없다. 나중에 필요해지면 Hub의 **Installs**에서 해당 에디터의 **Add modules**로 추가할 수 있다.
6. **Install**을 누르고 설치가 끝날 때까지 기다린다. 용량이 크므로 시간이 걸린다.

### 2.2.2 새 프로젝트 만들기

1. Hub의 **Projects**에서 **New project**를 누른다.
2. 위쪽 에디터 버전 선택에서 방금 설치한 6000.3.x가 선택되어 있는지 확인한다.
3. 템플릿은 **Universal 3D**를 고른다(Hub 버전에 따라 "3D (URP)"처럼 표시될 수 있다). 처음 선택하면 템플릿 다운로드가 먼저 진행된다. 이 교재의 실습은 렌더링 파이프라인과 상관이 없으므로 3D 템플릿이면 무엇이든 괜찮다.
4. **Project name**에 `NgoChat`을 입력한다.
5. **Location**은 경로에 한글과 공백이 없는 짧은 폴더로 정한다(예: `D:\Unity`). 경로가 길거나 특수문자가 있으면 일부 도구에서 문제가 생긴다.
6. Unity Cloud에 연결하는 체크 항목이 보이면 지금은 해제한다. 2.14절에서 Relay를 다룰 때만 필요하다.
7. **Create project**를 누른다. 처음 열 때는 프로젝트 캐시(Library 폴더)를 만드느라 몇 분이 걸린다.

프로젝트가 열리면 `SampleScene`이라는 씬이 열려 있다. 씬은 게임 오브젝트를 배치해 저장한 파일이다(2.3절).

### 2.2.3 에디터 화면 읽기

![그림 2-1 Unity 에디터 화면 구성(개념도)](images/ch02_fig01_editor_layout.png)

에디터는 여러 창(패널)으로 나뉜다. 이 교재에서 자주 쓰는 창은 다음과 같다.

- **Hierarchy**: 현재 씬에 있는 오브젝트의 목록이다. 오브젝트를 만들거나 선택하고 부모·자식 관계를 정한다.
- **Scene**: 오브젝트를 눈으로 보며 배치하는 편집 화면이다.
- **Game**: 실제로 실행했을 때 카메라가 비추는 화면이다.
- **Inspector**: 선택한 오브젝트에 붙은 컴포넌트와 설정값을 보여 주고 고친다. NetworkManager 설정도 여기서 한다.
- **Project**: 프로젝트의 파일(에셋)을 보여 준다. 스크립트, 프리팹, 씬 파일이 여기에 있다.
- **Console**: 로그와 오류가 나오는 창이다. 네트워크 실습의 거의 모든 확인을 여기서 한다. 보이지 않으면 `Window > General > Console`로 연다.
- **재생 버튼(▶)**: 위쪽 가운데에 있다. 누르면 Play 모드가 되어 게임이 실행되고, 다시 누르면 멈춘다.

기본 조작은 다음만 알면 된다.

- Scene 뷰 이동: 마우스 오른쪽 버튼을 누른 채 W, A, S, D로 이동하고 마우스를 움직여 둘러본다. 휠로 확대·축소한다.
- 오브젝트 선택 후 F: 그 오브젝트로 시점이 이동한다.
- 이동·회전·크기 도구: Scene 뷰에서 W(이동), E(회전), R(크기). Inspector의 Transform에서 숫자로 직접 입력해도 된다.
- 오브젝트 만들기: Hierarchy에서 마우스 오른쪽 버튼 > **3D Object** > Cube, Capsule, Plane 등. 메뉴 `GameObject > 3D Object`로도 같다.
- 저장: **Ctrl + S**는 씬을 저장한다. 에디터를 닫기 전에 항상 저장한다.

**[Play 모드의 규칙]** Play 모드 중에 Inspector에서 값을 바꾸거나 오브젝트를 만들고 지우면 Play를 멈추는 순간 모두 원래대로 돌아간다. 시험 삼아 값을 바꿔 보는 데는 편하지만, 저장하고 싶은 수정은 반드시 Play를 멈춘 뒤에 한다. 스크립트 파일(코드) 수정은 파일이므로 유지된다. 실수를 막으려면 `Edit > Preferences > Colors`에서 Playmode tint를 눈에 띄는 색으로 바꾸어 Play 중임을 알아볼 수 있게 한다.

### 2.2.4 꼭 해 둘 프로젝트 설정

**폴더 만들기.** Project 창의 `Assets`에서 마우스 오른쪽 버튼 > **Create** > **Folder**로 `Scenes`, `Scripts`, `Prefabs` 세 폴더를 만든다. 이미 `Scenes`가 있으면 그대로 쓴다. 앞으로 스크립트는 `Assets/Scripts`, 프리팹은 `Assets/Prefabs`에 둔다.

**Active Input Handling 확인.** `Edit > Project Settings`를 열고 왼쪽에서 **Player**를 선택한 뒤 **Other Settings** 아래 **Configuration**의 **Active Input Handling**을 본다. Unity 6의 최신 템플릿은 새 Input System 패키지를 기본으로 쓰는 경우가 많다. 이 설정이 "Input System Package (New)"인데 코드에서 예전 방식인 `Input.GetAxis`를 부르면 다음 예외가 난다.

```text
InvalidOperationException: You are trying to read Input using the UnityEngine.Input class,
but you have switched active Input handling to Input System package in Player Settings.
```

이 교재는 2.3절의 `SimpleInput` 도우미 하나로 입력을 읽어, 설정이 New, Old, Both 어느 것이어도 동작하게 한다. 따라서 이 설정은 바꾸지 않아도 된다. 다만 인터넷에서 가져온 예제가 이 예외를 내면 위 설정이 원인이다. 설정을 **Both**로 바꾸면 에디터를 재시작하라는 안내가 나오고, 재시작 후 두 방식이 함께 동작한다.

**Company Name과 Product Name.** 같은 Player 설정의 맨 위에서 **Company Name**과 **Product Name**을 정한다(예: `MyCompany`, `NgoChat`). 빌드한 프로그램의 로그 파일 위치가 이 이름으로 정해진다(2.13절).

**창 크기.** 같은 화면에 빌드한 프로그램을 여러 개 띄워야 하므로 **Resolution and Presentation**(Unity 버전에 따라 항목 이름이 조금 다를 수 있다)에서 전체 화면 모드를 **Windowed**, 크기를 1280×720 안팎으로 정한다.

**Run In Background.** 서로 다른 창을 오가며 시험하면 포커스를 잃은 창은 멈추기 쉽고, 서버가 멈추면 클라이언트 연결이 끊어진다. NetworkManager의 **Run In Background** 항목이 기본으로 켜져 있어 이를 막아 준다(2.5절). 이 항목을 끄지 않는다.

### 2.2.5 스크립트 편집기와 컴파일

`Edit > Preferences > External Tools`의 **External Script Editor**에서 사용할 편집기(예: Visual Studio 2022)를 고른다. Hub로 편집기를 함께 설치했다면 이미 지정되어 있다. Project 창에서 스크립트를 두 번 누르면 편집기가 열린다.

코드를 저장하고 Unity 창으로 돌아오면 에디터가 자동으로 컴파일한다. 컴파일 중에는 오른쪽 아래에 회전 표시가 나온다. 컴파일 오류가 있으면 Console에 빨간 줄로 나오고, 오류를 고치기 전에는 Play 버튼이 동작하지 않는다.

```text
All compiler errors have to be fixed before you can enter playmode!
```

오류 메시지의 `Assets/Scripts/Mover.cs(12,25)`는 파일 이름과 줄·열 번호이다. Console의 오류를 두 번 누르면 해당 줄이 편집기에서 열린다.

### 2.2.6 Console 읽기

Console에는 세 종류의 메시지가 나온다. `Debug.Log`는 일반 정보, `Debug.LogWarning`은 노란 경고, `Debug.LogError`는 빨간 오류이다. 예외(Exception)가 발생해도 빨간 오류로 나오며, 어느 파일 몇째 줄에서 났는지 호출 순서(스택 트레이스)가 함께 나온다.

- **Clear on Play**: 켜 두면 Play를 시작할 때 이전 로그를 지운다. 켜 두는 것이 읽기 편하다.
- **Collapse**: 같은 메시지를 하나로 묶어 개수만 보여 준다. 같은 오류가 수천 번 반복될 때 유용하다.
- **Error Pause**: 켜 두면 오류가 나는 순간 Play를 일시정지한다. 원인을 찾을 때 켠다.

네트워크 실습에서는 서버와 클라이언트가 각각 다른 에디터 창(또는 빌드)에서 돌아가므로 Console이 창마다 따로 있다. 어느 창의 로그인지 구분하려고 로그 앞에 `[서버]`, `[클라이언트]` 같은 접두어를 붙이는 습관을 들인다.

### 2.2.7 Git으로 관리할 때

프로젝트를 Git으로 관리한다면 Unity용 `.gitignore`를 쓴다. 최소한 `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `Builds/` 폴더는 저장소에 넣지 않는다. `Library`는 에디터가 다시 만들 수 있는 캐시이고 용량이 매우 크다. `Assets`, `Packages`, `ProjectSettings` 폴더만 저장하면 다른 PC에서도 같은 프로젝트를 열 수 있다.

## 2.3 C# 개발자를 위한 Unity 기본기

이 절은 NGO 실습에 필요한 Unity의 기본 개념과 조작만 다룬다. Unity를 이미 쓸 줄 안다면 2.3.7절의 SimpleInput 스크립트만 만들어 두고 2.4절로 넘어간다.

### 2.3.1 씬, 게임 오브젝트, 컴포넌트

Unity에서 화면에 보이거나 동작하는 모든 것은 게임 오브젝트(GameObject)이다. 게임 오브젝트는 그 자체로는 이름과 위치 정보만 가진 빈 상자이고, 여기에 컴포넌트(Component)를 붙여서 기능을 준다.

- **GameObject**: Hierarchy 창의 한 줄이다. 이름, 활성 여부, 위치·회전·크기(Transform)를 가진다.
- **Component**: 게임 오브젝트에 붙는 기능 하나이다. 화면에 모양을 그리는 MeshRenderer, 충돌 범위를 정하는 Collider, 우리가 작성한 스크립트가 모두 컴포넌트이다. 한 오브젝트에 여러 컴포넌트를 붙일 수 있다.
- **Scene**: 게임 오브젝트를 배치해서 저장한 파일(`.unity`)이다. 게임의 한 장면, 한 레벨에 해당한다.
- **Transform**: 모든 게임 오브젝트가 반드시 가지는 컴포넌트이다. 위치(Position), 회전(Rotation), 크기(Scale)를 담는다. 오브젝트를 다른 오브젝트 아래로 끌어다 놓으면 부모-자식 관계가 되고, 자식의 위치는 부모를 기준으로 한 값(로컬 좌표)이 된다.

C++의 클래스 상속으로 "Player는 Character의 한 종류"를 표현하던 것과 달리, Unity는 "Player는 Transform, 모양, 이동 스크립트, 네트워크 컴포넌트를 조립한 것"으로 표현한다. 상속 대신 조립(composition)이다. NGO도 같은 방식이어서, 어떤 오브젝트를 네트워크로 동기화하려면 NetworkObject 컴포넌트를 붙인다.

좌표계는 다음과 같다. Y축이 위쪽이고 바닥은 X-Z 평면이다. 길이 1은 1미터로 취급한다. 이 교재의 실습은 바닥 위에서 X, Z 방향으로 움직이는 캡슐만 사용한다.

### 2.3.2 스크립트는 컴포넌트다

Unity의 C# 스크립트는 `MonoBehaviour`를 상속한 클래스이며, 게임 오브젝트에 붙이면 컴포넌트가 된다.

```csharp
using UnityEngine;

public class Hello : MonoBehaviour
{
    [SerializeField] string message = "안녕";   // Inspector에 나타나는 설정값

    void Start()
    {
        Debug.Log(message);                      // Console 창에 출력된다
    }
}
```

반드시 지켜야 할 규칙이 몇 가지 있다.

- **파일 이름은 클래스 이름과 같아야 한다.** `Hello` 클래스는 `Hello.cs`에 있어야 하고 대소문자도 같아야 한다. 다르면 스크립트를 오브젝트에 붙일 수 없고, 붙이려 하면 다음 메시지가 나온다.

```text
No MonoBehaviour scripts in the file, or their names do not match the file name.
```

- **생성자를 쓰지 않는다.** `new Hello()`로 만들지 않는다. 오브젝트에 붙이면 Unity가 생성하고, 초기화 코드는 아래의 `Awake`나 `Start`에 쓴다.
- **`[SerializeField]`를 붙인 private 필드(또는 public 필드)는 Inspector에 나타난다.** 코드를 고치지 않고 Inspector에서 값을 바꿀 수 있고, 다른 오브젝트나 컴포넌트를 끌어다 연결하는 참조도 이 필드로 한다. C++에서 설정 파일이나 의존성 주입으로 하던 일을 Inspector가 맡는다.
- **스크립트는 `Assets/Scripts` 폴더에 둔다.** Project 창에서 `Scripts` 폴더를 열고 마우스 오른쪽 버튼 > **Create** > **Scripting** > **MonoBehaviour Script**(Unity 버전에 따라 **Create > C# Script**)를 고르면 새 스크립트가 만들어진다. 만든 직후 파일 이름을 입력하는 상태가 되므로, 원하는 클래스 이름을 입력하고 Enter를 누른다. 이름을 나중에 바꾸면 파일명만 바뀌고 안의 클래스 이름은 그대로이므로 둘을 함께 고쳐야 한다.
- **스크립트를 오브젝트에 붙이는 방법**은 두 가지이다. Project 창의 스크립트를 Hierarchy의 오브젝트나 Inspector 창으로 끌어다 놓거나, 오브젝트를 선택하고 Inspector 아래의 **Add Component**를 눌러 스크립트 이름을 검색한다.

### 2.3.3 생명주기: Unity가 부르는 함수들

MonoBehaviour에 특정 이름의 함수를 만들어 두면 Unity가 알맞은 때에 호출해 준다. 직접 호출하지 않는다. 다음은 이 교재에서 쓰는 것들이다.

- **Awake()**: 오브젝트가 만들어질 때 한 번 호출된다. 자기 자신의 초기화에 쓴다. 다른 스크립트의 Awake가 먼저 실행되었다고 가정하면 안 된다.
- **OnEnable() / OnDisable()**: 컴포넌트가 켜지고 꺼질 때 호출된다.
- **Start()**: 첫 Update 직전에 한 번 호출된다. 다른 오브젝트를 참조하는 초기화는 여기서 한다.
- **Update()**: 매 프레임 호출된다. 입력을 읽고 오브젝트를 움직이는 코드를 여기에 쓴다.
- **OnDestroy()**: 오브젝트가 제거될 때 호출된다. `+=`로 등록한 이벤트 핸들러를 `-=`로 해제한다.

Update는 컴퓨터마다 호출 횟수가 다르다. 한 대는 초당 60번, 다른 대는 144번 호출한다. 그래서 이동량은 `속도 × Time.deltaTime`으로 계산한다. `Time.deltaTime`은 지난 프레임이 걸린 시간(초)이므로, 프레임이 많든 적든 1초 동안 이동한 거리가 같아진다.

NetworkBehaviour에는 여기에 `OnNetworkSpawn()`과 `OnNetworkDespawn()`이 더해진다. 이 두 함수가 네트워크 코드의 진짜 시작점이다(2.6절).

### 2.3.4 자주 쓰는 기능

- `Debug.Log("글")`, `Debug.LogWarning("글")`, `Debug.LogError("글")`: Console 창에 출력한다. 문자열 보간(`$"값 {x}"`)을 쓸 수 있다.
- `transform.position`: 이 오브젝트의 월드 좌표이다. `Vector3`(x, y, z)이다. `transform.position += new Vector3(1, 0, 0);`처럼 더할 수 있다.
- `GetComponent<T>()`: 같은 오브젝트에 붙은 다른 컴포넌트를 찾는다. 예를 들어 `GetComponent<Renderer>()`.
- `Instantiate(프리팹, 위치, 회전)`: 프리팹으로부터 오브젝트를 새로 만든다. 네트워크 오브젝트는 만든 뒤에 NGO의 Spawn을 호출해야 한다(2.12절).
- `Destroy(오브젝트)`: 오브젝트를 제거한다. 네트워크 오브젝트는 Destroy 대신 Despawn을 쓴다.
- `Random.Range(a, b)`, `Mathf.Clamp(v, min, max)`, `Time.time`(게임 시작 후 경과 시간) 같은 수학·시간 도우미.

### 2.3.5 프리팹

프리팹(Prefab)은 게임 오브젝트를 설정째 파일로 저장해 둔 설계도이다. 씬에 놓인 프리팹의 복사본을 인스턴스라고 한다.

- 만드는 방법: Hierarchy의 오브젝트를 Project 창의 `Assets/Prefabs` 폴더로 끌어다 놓는다. 만들어지면 Hierarchy의 그 오브젝트 이름이 파란색으로 바뀐다.
- 프리팹을 고치면 모든 인스턴스에 반영된다. 프리팹을 더블클릭하면 그 프리팹만 보이는 편집 화면(Prefab Mode)으로 들어가고, 왼쪽 위의 화살표로 씬으로 돌아온다.
- 씬의 인스턴스 하나만 값을 바꿀 수도 있다. 바꾼 항목은 Inspector에 굵은 글씨와 파란 줄로 표시된다(override). Inspector 위쪽의 **Overrides** 메뉴에서 그 변경을 프리팹에 반영(Apply)하거나 되돌릴(Revert) 수 있다.
- 코드에서는 `[SerializeField] GameObject prefab;`처럼 필드에 프리팹을 연결해 두고 `Instantiate(prefab)`로 만든다.

NGO에서 프리팹은 특히 중요하다. 서버가 오브젝트를 스폰하면 모든 접속자의 PC에서 같은 프리팹으로 오브젝트가 만들어진다. 이 때문에 서버와 클라이언트가 같은 프리팹을 가지고 있어야 하고, NetworkManager에 그 프리팹을 등록해 두어야 한다(2.5절~2.6절).

### 2.3.6 UnityEngine.Object의 null

Unity가 관리하는 오브젝트(GameObject, 컴포넌트 등)는 `Destroy`된 뒤에도 C# 객체 자체는 메모리에 남아 있다. 그래서 Unity는 `==` 연산자를 다시 정의하여, 이미 제거된 오브젝트를 `null`과 비교하면 같다고 답하게 만들었다.

- `if (target == null)`처럼 비교하면 제거된 오브젝트도 null로 판정한다. 이것이 올바른 검사이다.
- `target?.Foo()`나 `target ?? other`처럼 C#의 null 조건 연산자를 쓰면 이 재정의를 거치지 않으므로 제거된 오브젝트를 null로 보지 못한다. Unity 오브젝트에는 이 연산자를 쓰지 않는다.

### 2.3.7 키보드 입력과 SimpleInput

Unity에는 입력을 읽는 방식이 두 가지 있다. 예전 방식인 Input Manager(`Input.GetAxis`, `Input.GetKey`)와 새 방식인 Input System 패키지(`Keyboard.current.wKey.isPressed`)이다. Unity 6의 새 프로젝트는 새 방식을 기본으로 쓰는 경우가 많고, 이때 예전 방식의 함수를 부르면 2.2.4절의 `InvalidOperationException`이 난다. 인터넷의 예제는 대부분 예전 방식을 쓰므로 그대로 붙여 넣으면 이 오류를 만난다.

이 교재는 어느 설정에서도 동작하도록 입력 읽기를 스크립트 하나에 모아 둔다. 이후 모든 실습은 이 스크립트의 `SimpleInput.Move()`로 방향키를 읽는다.

`Assets/Scripts/SimpleInput.cs`:

```csharp
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// WASD 또는 방향키 입력을 읽는다. 입력 설정(Active Input Handling)이 무엇이어도 동작한다.
public static class SimpleInput
{
    public static Vector2 Move()
    {
        float x = 0f, y = 0f;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;                       // 새 Input System
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    y += 1f;
        }
#else
        x = Input.GetAxisRaw("Horizontal");              // 예전 Input Manager
        y = Input.GetAxisRaw("Vertical");
#endif
        var v = new Vector2(x, y);
        return v.sqrMagnitude > 1f ? v.normalized : v;   // 대각선 속도가 빨라지지 않게 길이를 1로 제한
    }
}
```

`#if ENABLE_INPUT_SYSTEM`은 C++의 `#if`와 같은 전처리 지시문이다. Unity가 Player 설정에 따라 이 기호를 정의해 주므로, 설정이 Input System이면 위쪽 코드가, 예전 방식이면 아래쪽 코드가 컴파일된다. `static class`는 객체를 만들지 않고 `SimpleInput.Move()`로 바로 부르는 클래스이다.

### 2.3.8 실습 0: 키보드로 움직이는 캡슐

**목표.** 네트워크 없이 Unity의 기본 조작(오브젝트 만들기, 스크립트 붙이기, 프리팹)을 연습한다. 마지막에 같은 캡슐을 여러 개 놓았을 때 생기는 문제를 직접 본다. 이 문제가 NGO의 출발점이다.

**단계.**

1. 2.2절에서 만든 `NgoChat` 프로젝트를 열고 Project 창에서 `Assets/Scenes/SampleScene`을 더블클릭하여 연다. 이 교재의 모든 실습은 이 씬 하나에서 진행한다. Hierarchy에 기본으로 들어 있는 `Main Camera`와 `Directional Light`는 그대로 둔다. 그 밖에 템플릿이 만든 오브젝트가 있어도 지우지 않아도 된다.
2. 바닥을 만든다. Hierarchy에서 마우스 오른쪽 버튼 > **3D Object** > **Plane**을 고른다. 이름을 `Ground`로 바꾼다(오브젝트를 선택하고 F2). Inspector의 Transform에서 Position을 `0, 0, 0`, Scale을 `3, 1, 3`으로 입력한다. 가로세로 30미터짜리 바닥이 된다.
3. 캡슐을 만든다. 마우스 오른쪽 버튼 > **3D Object** > **Capsule**을 고르고 이름을 `Player`로 바꾼다. Position을 `0, 1, 0`으로 입력한다. 캡슐의 높이가 2미터이므로 중심이 1미터에 있으면 바닥에 서 있게 된다.
4. 카메라를 바닥 위에서 내려다보도록 맞춘다. `Main Camera`를 선택하고 Position을 `0, 14, -8`, Rotation을 `60, 0, 0`으로 입력한다. Game 창에 바닥과 캡슐이 보이면 된다.
5. 2.3.7절의 `SimpleInput.cs`를 만든다. `Assets/Scripts`에 새 스크립트를 만들어 이름을 `SimpleInput`으로 하고, 파일을 열어 내용을 위 코드로 모두 바꾼다.
6. 이동 스크립트 `Mover.cs`를 같은 방법으로 만들어 아래 코드로 바꾼다.

```csharp
using UnityEngine;

public class Mover : MonoBehaviour
{
    [SerializeField] float speed = 4f;      // 초당 이동 거리(미터)

    void Update()
    {
        Vector2 dir = SimpleInput.Move();
        Vector3 move = new Vector3(dir.x, 0f, dir.y) * (speed * Time.deltaTime);
        transform.position += move;
    }
}
```

7. 저장한 뒤 Unity 창으로 돌아와 컴파일이 끝나기를 기다린다. Console에 빨간 오류가 없어야 한다.
8. `Mover`를 `Player` 오브젝트에 붙인다(스크립트를 Hierarchy의 `Player`로 끌어다 놓는다). `Player`를 선택하면 Inspector에 `Mover (Script)`와 `Speed` 항목이 보여야 한다.
9. 재생 버튼(▶)을 누르고 Game 창을 한 번 클릭한 뒤 W, A, S, D 또는 방향키를 누른다. 캡슐이 움직여야 한다. 재생 중에 Inspector에서 `Speed`를 10으로 바꾸면 즉시 빨라진다. 재생을 멈추면 4로 돌아온다(2.2.3절의 Play 모드 규칙).
10. 프리팹을 만든다. 재생을 멈추고, Hierarchy의 `Player`를 `Assets/Prefabs` 폴더로 끌어다 놓는다. `Player`의 이름이 파란색이 되면 프리팹이 만들어진 것이다.
11. Project 창의 `Player` 프리팹을 Hierarchy의 빈 곳으로 두 번 끌어다 놓아 캡슐을 두 개 더 만든다. 각각 Position을 `3, 1, 0`과 `-3, 1, 0`으로 바꾼다.
12. 다시 재생하고 키를 누른다. 캡슐 세 개가 모두 함께 움직인다.
13. 재생을 멈추고, Hierarchy에서 `Player` 세 개를 모두 지운다(선택하고 Delete). 프로젝트의 `Prefabs/Player` 프리팹은 남겨 둔다. 다음 절부터는 플레이어를 직접 놓지 않고 NGO가 접속자마다 만든다. 씬을 저장한다(Ctrl + S).

**확인할 것.** 12번에서 캡슐 세 개가 함께 움직였다. 키보드는 하나이고 캡슐은 셋이므로 "어느 캡슐이 내 것인가"를 코드가 알 방법이 없다. 하나의 컴퓨터에서 하나의 키보드만 읽는 프로그램이라면 문제가 없어 보이지만, 네트워크 게임에서는 접속한 사람 수만큼 캡슐이 있고 각 컴퓨터에는 모든 캡슐의 복사본이 있다. 내 PC에서 내 키보드가 움직여야 할 캡슐은 그 가운데 내 것 하나뿐이다. NGO에서 이 구분을 하는 것이 `IsOwner`이다(2.7절).

**그 뒤에서 일어난 일.** `Mover.Update()`는 `Mover`가 붙은 오브젝트마다 매 프레임 호출된다. 프리팹 인스턴스 세 개는 같은 스크립트를 가졌으므로 같은 입력을 읽고 같은 방향으로 움직였다. 지금 만든 `Player` 프리팹은 이 실습에서만 쓴다. 이후에는 NGO가 접속자마다 플레이어를 만들어 주는 방식으로 바꾸고, 이동 스크립트는 "내 것일 때만 키보드를 읽는" 형태로 바꾼다. `SimpleInput.cs`는 계속 사용하므로 지우지 않는다.

**흔한 오류.**

- `No MonoBehaviour scripts in the file, or their names do not match the file name`: 파일 이름과 클래스 이름이 다르다. `Mover.cs` 안의 클래스가 `Mover`인지 확인한다.
- 캡슐이 움직이지 않는다: Game 창을 클릭해서 포커스를 주었는지, `Mover`가 `Player`에 붙어 있는지(Inspector 확인), Console에 컴파일 오류가 없는지 확인한다. 재생 중이 아닐 때는 움직이지 않는다.
- `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class...`: 2.2.4절에서 설명한 입력 설정 문제이다. 예전 `Input` 클래스를 직접 쓴 코드가 있는지 찾고, `SimpleInput.Move()`로 바꾼다.
- `The type or namespace name 'InputSystem' does not exist in the namespace 'UnityEngine'`: Input System 패키지가 설치되지 않은 프로젝트이다. `Window > Package Manager`에서 **Unity Registry**를 선택하고 Input System을 설치하거나, `Edit > Project Settings > Player > Other Settings`의 **Active Input Handling**을 **Input Manager (Old)**로 바꾼다.
- 재생 중에 고친 값이 사라진다: 정상이다. 저장할 변경은 재생을 멈춘 뒤에 한다.
- 스크립트를 고쳤는데 반영되지 않는다: 편집기에서 파일을 저장했는지(Ctrl + S) 확인하고 Unity 창으로 돌아와 컴파일이 끝나기를 기다린다.

## 2.4 Netcode for GameObjects 한눈에 보기

코드를 쓰기 전에 NGO가 무엇을 해 주고 개발자가 무엇을 정해야 하는지 큰 그림을 잡는다. 이 절은 설명 위주이고 실습은 2.5절부터 시작한다. 용어가 낯설어도 괜찮다. 이후 실습을 하면서 이 절의 그림으로 돌아와 확인하면 된다.

### 2.4.1 NGO가 해 주는 일

Netcode for GameObjects(NGO)는 Unity가 공식으로 제공하는 네트워킹 라이브러리이다. 이름의 GameObject가 말해 주듯 Unity의 게임 오브젝트와 MonoBehaviour 방식 위에서 동작한다. (Unity에는 ECS 방식 오브젝트를 위한 Netcode for Entities도 있지만 이 교재에서는 다루지 않는다.)

소켓으로 게임 서버를 직접 만든다고 생각해 보자. 소켓을 열고, 접속을 받고, 패킷 형식을 정하고, 값을 바이트로 바꾸고, 접속자를 관리하고, 누락과 순서를 처리하고, 오브젝트마다 번호를 붙여 서로 같은 것을 가리키게 하고, 접속이 끊어졌는지 감지해야 한다. NGO는 이 대부분을 이미 구현해 두었다. 개발자는 "어떤 오브젝트를 동기화할지, 누가 값을 바꿀 수 있는지, 어떤 데이터를 누구에게 보낼지"만 정하면 된다. 6장에서는 이 일을 C++로 직접 구현하므로, NGO가 대신해 주는 것이 무엇인지 지금 눈여겨 두면 6장에서 비교가 된다.

NGO가 해 주는 일:

- 접속 관리: 서버 시작, 클라이언트 접속과 승인, 접속 종료 감지
- 오브젝트 복제: 서버가 오브젝트를 만들면 모든 접속자의 PC에 같은 오브젝트가 만들어진다(스폰). 제거도 마찬가지이다(디스폰).
- 오브젝트 번호와 소유자 관리: 오브젝트마다 번호(NetworkObjectId)와 소유한 클라이언트(OwnerClientId)를 가진다.
- 직렬화: int, float, Vector3 같은 값을 바이트로 바꾸어 보내고 되돌린다.
- 상태 동기화: 값이 바뀌면 알아서 전파하는 NetworkVariable, 위치를 전파하는 NetworkTransform
- 원격 호출: 다른 PC에서 함수를 실행시키는 RPC
- 씬 동기화: 서버가 로드한 씬을 클라이언트도 로드한다.
- 틱: 일정한 주기로 변경 사항을 모아서 보낸다.

NGO가 해 주지 않는 일:

- 계정 인증, 매치메이킹, 로비, 데이터 저장(DB)은 별도의 서비스나 코드가 필요하다. 이 교재에서는 4장(Steam 로비), 5장(DB), 6장(C++ 서버)에서 다룬다.
- 치트 방지: 이 장에서 쓰는 소유자 권한 방식은 클라이언트가 보낸 값을 서버가 그대로 믿는다(2.4.5절).
- 클라이언트 예측과 서버 보정, 지연 보상 같은 고급 지연 숨김 기법은 기본 기능만으로 완성되지 않는다(2.16절).

### 2.4.2 구성 요소 지도

![그림 2-2 NGO 구성 요소 지도](images/ch02_fig02_components.png)

그림 2-2는 NGO의 구성 요소를 두 덩어리로 나눈 것이다. 왼쪽은 연결을 담당하는 NetworkManager 쪽이고, 오른쪽은 동기화되는 오브젝트 쪽이다. 각 이름이 의미하는 바는 다음과 같다.

- **NetworkManager**: 씬에 하나만 두는 컴포넌트이다. 서버·호스트·클라이언트를 시작하고 종료하는 함수(`StartHost`, `StartClient`, `StartServer`, `Shutdown`)를 가지고 있고, 접속 승인, 접속자 목록, 틱 설정을 모두 여기서 한다. 오브젝트의 스폰을 관리하는 SpawnManager와 씬 로드를 맞추는 SceneManager도 NetworkManager 안에 들어 있다.
- **Unity Transport**: 실제로 바이트를 주고받는 계층이다. UDP 위에서 동작하며 신뢰성(재전송과 순서 보장)이 필요한 메시지를 위한 기능도 가지고 있다. 접속할 주소와 포트를 여기에 설정한다(기본값 `127.0.0.1`, 포트 `7777`).
- **NetworkObject**: 동기화될 오브젝트에 붙이는 컴포넌트이다. 이것이 붙어야 NGO가 그 오브젝트를 알아본다. 오브젝트마다 번호(NetworkObjectId)와 소유자(OwnerClientId)를 가진다. 프리팹의 가장 바깥 오브젝트에 하나만 붙인다.
- **NetworkBehaviour**: 우리가 작성하는 스크립트의 부모 클래스이다. `MonoBehaviour`를 확장한 것으로, `IsOwner`, `IsServer` 같은 플래그와 `OnNetworkSpawn()` 함수를 제공한다. NetworkVariable과 RPC는 NetworkBehaviour 안에서만 쓸 수 있다.
- **NetworkVariable**: 값이 바뀌면 NGO가 알아서 다른 접속자에게 전달하는 변수이다. 이름, 체력, 점수처럼 계속 유지되는 상태에 쓴다. 나중에 접속한 사람도 현재 값을 받는다(2.8절).
- **RPC**: Remote Procedure Call. 다른 PC에서 함수를 실행시키는 호출이다. 채팅 한 줄, 요청, 알림처럼 한 번 일어나고 지나가는 사건에 쓴다(2.9절).
- **NetworkTransform**: 오브젝트의 위치·회전·크기를 자동으로 동기화하는 컴포넌트이다(2.7절).

기억할 연결 관계는 이렇다. NetworkManager가 연결을 맡고, 연결된 뒤 서버가 NetworkObject를 스폰하며, 스폰된 오브젝트 안에서 우리 스크립트(NetworkBehaviour)가 NetworkVariable과 RPC로 데이터를 주고받는다.

### 2.4.3 Host, Server, Client

![그림 2-3 Host, Server, Client의 구성](images/ch02_fig03_modes.png)

NGO에서 프로그램은 세 가지 방식 중 하나로 시작한다.

- **`StartHost()`**: 서버와 클라이언트가 한 프로세스에 함께 있는 방식이다. 이 프로그램을 실행한 사람은 플레이어로 게임에 참여하면서 다른 사람의 접속도 받는다. 방장이 있는 게임, 곧 리슨 서버(Listen Server)이며 4장의 Steam 방식이 이것이다. 호스트의 클라이언트 번호(ClientId)는 항상 0이다.
- **`StartServer()`**: 서버 기능만 실행한다. 화면에 보이는 플레이어가 없고 접속 승인, 스폰, 중계만 한다. 전용 서버(Dedicated Server)라고 하며 3장 언리얼의 방식이 이것이다. 화면 없이(`-batchmode -nographics`) 실행할 수도 있다(2.14절).
- **`StartClient()`**: 서버나 호스트에 접속한다.

세 방식의 공통점은 클라이언트가 서버 하나하고만 연결된다는 것이다. 클라이언트끼리는 직접 통신하지 않는다. 클라이언트 A가 클라이언트 B에게 보내는 메시지도 서버를 거쳐 간다. 이것이 NGO의 기본 구조인 클라이언트-서버 방식이다. 채팅 한 줄도 같은 길을 따른다(2.11절).

호스트와 서버 전용의 차이는 시험할 때 중요하다. 호스트는 서버 코드와 클라이언트 코드가 같은 프로세스에서 실행되므로, 서버가 클라이언트에게 보내는 메시지가 실제 네트워크를 거치지 않고 지나갈 수 있다. 호스트 하나만 켜고 시험하면 잘 되던 코드가 별도의 클라이언트가 접속하는 순간 실패하는 일이 흔하다. 그래서 이 장의 모든 실습은 호스트와 별도 클라이언트를 함께 띄워 시험한다.

접속자에게는 번호가 붙는다. 서버(호스트)는 0이고, 접속하는 클라이언트는 접속 순서대로 1, 2, 3...을 받는다. 접속을 끊었다가 다시 접속하면 같은 번호를 받지 못하고 새 번호를 받는다. 이 점은 접속자 관리를 설계할 때 중요하다(2.10절).

### 2.4.4 하나의 오브젝트, 여러 개의 복제본

![그림 2-4 같은 오브젝트가 모든 컴퓨터에 하나씩 존재한다](images/ch02_fig04_replication.png)

처음 NGO를 쓰는 사람이 가장 많이 헷갈리는 부분이다. 그림 2-4에서 플레이어 A는 서버, 클라이언트 1, 클라이언트 2 세 컴퓨터에 모두 존재한다. 같은 번호(NetworkObjectId)를 가진 서로 다른 복사본이다. 서버가 오브젝트를 스폰하면 NGO가 모든 접속자의 PC에 복사본을 만든다.

그러면 `Update()` 같은 스크립트 코드는 어느 복사본에서 실행될까? 모든 복사본에서 실행된다. 클라이언트 1의 컴퓨터에서는 플레이어 A와 B 두 개의 복사본이 각각 `Update()`를 실행한다. 코드가 같으니 "플레이어 A는 내 키보드를 읽는다"고 쓰면 B도 내 키보드를 읽게 된다(2.3절 실습 0에서 본 현상이다). 그래서 NGO는 코드가 지금 어느 복사본에서 실행되는지 알려 주는 플래그를 제공한다.

- **IsServer**: 이 컴퓨터가 서버 역할을 하고 있는가. 호스트이면 true이다.
- **IsClient**: 이 컴퓨터가 클라이언트 역할을 하고 있는가. 호스트이면 true이다.
- **IsHost**: 서버이면서 클라이언트인가.
- **IsOwner**: 이 복사본의 소유자가 이 컴퓨터의 클라이언트인가. 그림에서 노란 상자가 IsOwner가 true인 복사본이다.
- **IsLocalPlayer**: 이 복사본이 이 컴퓨터의 클라이언트가 조종하는 플레이어 오브젝트인가. 플레이어 오브젝트에서는 IsOwner와 같다고 보면 된다.
- **OwnerClientId**: 이 오브젝트의 소유자 클라이언트 번호이다.

실전에서 가장 먼저 쓰게 될 코드는 다음 두 줄 꼴이다.

```csharp
void Update()
{
    if (!IsOwner) return;       // 내 것이 아닌 복사본은 입력을 읽지 않는다
    // ... 키보드를 읽어 움직인다
}
```

이 한 줄이 2.3절 실습 0의 문제를 해결한다. 모든 복사본에서 `Update()`가 실행되지만, 소유자의 컴퓨터에서 소유자의 복사본만 키보드를 읽는다. 읽은 결과는 NetworkTransform이 다른 컴퓨터의 복사본에 전달한다(2.7절).

### 2.4.5 권한과 소유권

![그림 2-5 권한(Authority)과 소유권(Ownership)](images/ch02_fig05_authority.png)

**권한(Authority)** 은 어떤 값을 결정할 수 있는 쪽을 뜻한다. NGO의 기본 모델은 서버 권한이다. 서버가 세계의 진짜 상태를 가지고 있고, 오브젝트를 스폰하거나 디스폰하거나 소유자를 바꾸는 일은 서버만 할 수 있다. 클라이언트가 직접 `Instantiate`로 오브젝트를 만들어도 다른 컴퓨터에는 나타나지 않는다.

**소유권(Ownership)** 은 서버가 오브젝트마다 지정하는 소유 클라이언트이다. 플레이어 오브젝트는 그 플레이어가 소유한다. 소유자는 서버로부터 일부 권한을 위임받을 수 있다.

- NetworkTransform: 위치를 누가 정할지 **Authority Mode**로 선택한다. Server이면 서버가 위치를 정해서 소유자에게도 내려보내고(그림 2-5 왼쪽), Owner이면 소유자가 직접 위치를 정해서 서버에게 올려보낸다(그림 2-5 오른쪽).
- NetworkVariable: 쓰기 권한(Write Permission)을 Server 또는 Owner로 선택한다.
- RPC: 소유자만 호출할 수 있게 제한하거나, 누구나 호출할 수 있게 허용한다.

그림 2-5의 두 방식은 장단점이 뚜렷하다. 서버 권한 방식은 서버가 모든 이동을 검증하므로 조작에 강하지만, 키를 누르고 화면이 반응하기까지 서버를 한 번 다녀와야 하므로 느리게 느껴진다. 소유자 권한 방식은 내 컴퓨터에서 바로 움직이므로 반응이 빠르지만, 해킹된 클라이언트가 거짓 위치를 보내면 서버가 그대로 믿는다.

이 교재의 엔진 파트(2장~4장)는 소유자 권한 방식을 쓴다. 이동과 상호작용 같은 게임 규칙은 소유자의 클라이언트가 처리하고, 서버는 접속자 관리와 메시지 중계만 맡는다. 구현이 단순하고 반응이 빠른 대신 클라이언트의 조작에 취약하다는 점을 알고 쓴다. 서버가 규칙을 모두 판정하는 방식은 6장의 C++ 서버에서 구현한다.

### 2.4.6 상태와 사건: NetworkVariable과 RPC

서버와 클라이언트 사이에 데이터를 전달하는 수단은 두 가지이다.

- **상태**: 계속 유지되는 값이다. 접속자의 이름, 체력, 점수, 색깔. 상태는 NetworkVariable로 전달한다. 값이 바뀔 때마다 모든 접속자의 복사본에 반영되고, 나중에 접속한 사람도 접속하는 순간 현재 값을 받는다.
- **사건**: 한 번 일어나고 지나가는 일이다. 채팅 한 줄, "보너스를 받겠다"는 요청, 폭발 효과. 사건은 RPC로 전달한다. 호출한 순간에 접속해 있던 쪽에만 전달되고, 나중에 접속한 사람에게는 전달되지 않는다.

둘 중 무엇을 쓸지 고르는 질문은 하나이다. **"나중에 접속한 사람에게도 보여야 하는가?"** 그렇다면 NetworkVariable이고, 아니라면 RPC이다. 채팅 로그는 늦게 들어온 사람이 지난 대화를 받지 못해도 되므로 RPC, 접속자 이름은 늦게 들어온 사람도 알아야 하므로 NetworkVariable이다. 2.8절과 2.9절에서 직접 실험해서 이 차이를 눈으로 확인한다.

### 2.4.7 틱

NGO는 변경 사항을 바로바로 보내지 않고 일정한 주기로 모아서 보낸다. 이 주기를 틱(tick)이라고 하며 기본은 초당 30번이다(NetworkManager의 Tick Rate). 게임 화면을 그리는 프레임(`Update`)은 이와 별개의 주기이다. 같은 틱 안에서 NetworkVariable을 여러 번 바꾸면 마지막 값만 전달된다. 이 때문에 생기는 현상은 2.15절과 2.16절에서 설명한다.

### 2.4.8 NGO의 버전

인터넷에서 NGO 자료를 찾을 때 가장 중요한 주의점은 버전이다.

- **NGO 1.x**: 2022~2024년 강좌와 블로그 글의 대부분이 이 버전이다. Unity 6000.3(6.3 LTS)부터는 지원되지 않는다.
- **NGO 2.x**: Unity 6.3 LTS에서 쓰는 버전이다. 이 교재의 기준이다. 1.x와 달라진 대표적인 부분은 RPC를 `[ServerRpc]`, `[ClientRpc]` 대신 `[Rpc(SendTo.Server)]` 같은 방식으로 선언하는 것, 소유자 권한 이동을 `ClientNetworkTransform` 클래스를 만드는 대신 NetworkTransform의 Authority Mode로 설정하는 것이다. 1.x 방식과의 대응은 2.9.6절에 정리했다.
- **NGO 3.x**: Unity 6000.7 이상과 Netcode for Entities를 요구한다. Unity 6.3 LTS에는 설치하지 않는다. Package Manager에서 최신 버전 표시가 3.x이더라도 2.x를 설치한다(2.5절).

### 2.4.9 용어 정리

- **스폰(Spawn) / 디스폰(Despawn)**: 서버가 네트워크 오브젝트를 모든 접속자에게 만들고 제거하는 일. `Destroy`가 아니라 `Despawn`을 쓴다.
- **복제(Replication)**: 서버의 상태를 다른 접속자의 컴퓨터에 똑같이 만들어 두는 일.
- **소유자(Owner)**: 오브젝트를 소유한 클라이언트. 서버가 지정한다.
- **권한(Authority)**: 값을 정하는 쪽. 서버 권한과 소유자 권한이 있다.
- **페이로드(Payload)**: 접속을 요청할 때 함께 보내는 데이터(이름, 접속 코드 등)이다. 서버는 이것을 보고 접속을 승인하거나 거절한다(2.10절).
- **중도 참여자(Late Joiner)**: 게임이 이미 진행되는 중에 접속한 클라이언트. NetworkVariable과 스폰이 이 클라이언트에게 어떻게 전달되는지가 설계의 핵심이다.
- **틱(Tick)**: NGO가 변경 사항을 모아서 보내는 주기. 기본 초당 30번.

**이 절의 핵심.**

- NGO는 "오브젝트를 복제하는" 라이브러리이다. 패킷을 직접 만들지 않고 오브젝트와 변수를 동기화한다.
- 같은 오브젝트가 접속자 모두의 컴퓨터에 하나씩 있고, 코드는 모든 복사본에서 실행된다. `IsOwner`, `IsServer`로 구분한다.
- 스폰과 디스폰은 서버만 한다. 이동은 소유자가 하도록 설정할 수 있다.
- 지속되는 값은 NetworkVariable, 한 번 일어나는 일은 RPC이다.
- 호스트만 시험하지 말고 항상 별도 클라이언트를 함께 띄워 시험한다.

## 2.5 NGO 설치와 첫 연결

이 절에서 처음으로 서버와 클라이언트를 실행한다. 순서는 패키지 설치, NetworkManager 만들기, 버튼 화면 만들기, 한 대의 PC에서 여러 개를 실행하여 접속시키기이다. 이 절을 마치면 호스트에 클라이언트가 접속하는 것을 화면과 Console에서 확인할 수 있다.

### 2.5.1 패키지 설치

NGO는 Unity에 기본 포함되어 있지 않으므로 Package Manager로 설치한다.

1. `Window > Package Manager`를 연다.
2. 창 왼쪽 위의 **+** 버튼을 누르고 **Install package by name...**을 고른다(Unity 버전에 따라 **Add package by name...**으로 표시된다).
3. 이름 칸에 다음을 입력한다. 버전 칸은 비워 둔다.

```text
com.unity.netcode.gameobjects
```

4. **Install**(또는 **Add**)을 누른다. 설치와 컴파일이 끝나기를 기다린다. 오른쪽 아래에 회전 표시가 나타났다 사라진다.
5. Package Manager 왼쪽 위의 드롭다운을 **In Project**로 바꾸어 **Netcode for GameObjects**가 목록에 있는지 확인하고, 선택하여 오른쪽에 나오는 **버전이 2로 시작하는지** 확인한다(예: 2.13.3). 이 목록에 **Unity Transport**도 함께 보여야 한다. NGO가 필요로 하는 패키지여서 자동으로 설치된다.

Console에 빨간 오류가 없어야 한다. 이 교재의 코드는 NGO 2.7.0과 2.13.3의 소스에서 사용한 API 이름을 확인했다. 설치된 버전이 이보다 많이 낮은 2.x이면 일부 API 이름이 다를 수 있으므로 Package Manager에서 버전을 올린다.

이후 2.13절에서 두 가지 선택 패키지를 더 설치한다. **Multiplayer Tools**(Network Simulator와 통계 화면), **Multiplayer Play Mode**(에디터 창을 여러 개 띄우는 기능)이다. 지금은 필요 없다. NetworkManager의 Inspector에 "Multiplayer Tools 패키지를 설치하라"는 안내 문구가 보일 수 있는데 정상이다.

Unity 6에는 `Window > Multiplayer > Multiplayer Center`라는 안내 창도 있어 게임 종류에 맞는 패키지를 골라 설치해 준다. 이 교재는 필요한 패키지를 직접 설치하는 방식으로 진행한다.

### 2.5.2 NetworkManager 만들기

NetworkManager는 연결을 시작하고 종료하며 접속자와 스폰을 총괄하는 컴포넌트이다(2.4.2절). 씬에 하나만 둔다.

1. Hierarchy의 빈 곳에서 마우스 오른쪽 버튼 > **Create Empty**를 고르고 이름을 `NetworkManager`로 바꾼다.
2. 이 오브젝트를 선택하고 Inspector 아래의 **Add Component**를 누른다. 검색 칸에 `Network Manager`를 입력하고 **Network Manager**(Netcode 항목 아래)를 고른다.
3. Inspector의 **Network Transport** 칸 아래에 노란 경고("You have no transport selected...")와 **Select transport...** 드롭다운이 보인다. 드롭다운에서 **UnityTransport**를 고른다. 같은 오브젝트에 **Unity Transport** 컴포넌트가 추가되고, Network Transport 칸에 `NetworkManager (Unity Transport)`가 들어간다.
4. 다른 항목은 지금 바꾸지 않는다. **Network Prefabs Lists**에는 `DefaultNetworkPrefabs`가 자동으로 들어가 있고, Project 창의 `Assets` 폴더에 `DefaultNetworkPrefabs.asset` 파일이 생겨 있다. 이 목록은 네트워크 오브젝트로 쓸 프리팹을 모아 둔 목록이다(2.6절).

**Unity Transport** 컴포넌트에는 접속 정보가 있다. 기본값은 Address `127.0.0.1`, Port `7777`이다. `127.0.0.1`은 자기 자신의 컴퓨터를 가리키는 주소(루프백)여서 같은 PC에서 하는 시험에 알맞다. 포트 7777은 UDP 포트이다. 이 값들은 다음에 만들 화면에서 코드로 지정하므로 Inspector에서 고칠 필요가 없다.

### 2.5.3 NetworkManager 설정 읽기

![그림 2-6 NetworkManager Inspector 읽는 법](images/ch02_fig06_networkmanager.png)

그림 2-6은 NetworkManager Inspector의 항목을 이 교재에서 쓰는 관점으로 설명한 개념도이다. 실제 화면은 NGO 버전에 따라 항목 이름과 순서가 조금 다를 수 있다. 노란 줄로 표시한 항목이 이 교재에서 직접 다루는 것들이다.

- **Run In Background**: 켜 둔다. 창이 포커스를 잃어도 프로그램이 멈추지 않는다. 한 PC에서 창 여러 개를 오가며 시험할 때 필수이다. 꺼 두면 포커스를 잃은 서버가 멈추고 클라이언트의 연결이 끊어진다.
- **Log Level**: NGO가 Console에 남기는 로그의 상세 정도이다. 기본 Normal로 두고, 문제를 추적할 때 Developer로 바꾼다.
- **Protocol Version**: 서버와 클라이언트가 같은 값이어야 접속된다. 두 빌드를 서로 접속하지 못하게 만들고 싶을 때 바꾼다. 기본 0.
- **Network Transport**: 실제 통신을 맡는 컴포넌트이다. 비어 있으면 아무것도 시작되지 않는다.
- **Tick Rate**: 초당 네트워크 틱 수이다. 기본 30.
- **Spawn Time Out**: 아직 만들어지지 않은 오브젝트에 대한 메시지를 이 시간(초)만큼 보관한다. 기본값을 쓴다.
- **Connection Approval**: 접속 승인 콜백을 쓸지 정한다. 2.10절에서 켠다.
- **NetworkVariable Length Safety**, **Recycle Network Ids**, **Rpc Hash Size**: 기본값을 쓴다.
- **Force Same Prefabs**: 켜 둔다. 서버와 클라이언트의 네트워크 프리팹 목록이 같을 때만 접속을 허용한다. 클라이언트를 빌드한 뒤에 프리팹을 추가하고 다시 빌드하지 않으면 접속이 거부된다(2.5.8절).
- **Default Player Prefab**: 접속한 클라이언트마다 자동으로 만들 플레이어 프리팹을 넣는 칸이다. 2.6절에서 사용한다.
- **Network Prefabs Lists**: 네트워크 오브젝트로 쓸 프리팹의 목록이다. 기본으로 `DefaultNetworkPrefabs`가 지정되어 있고, 프로젝트에서 NetworkObject를 붙인 프리팹은 이 목록에 자동으로 추가된다.
- **Enable Scene Management**: 켜 둔다. 서버가 씬을 로드하면 클라이언트도 같은 씬을 로드한다. 이 기능을 쓰려면 그 씬이 빌드의 씬 목록(Scene List)에 있어야 한다(2.5.5절).
- **Start Host / Start Server / Start Client**: Play 모드에서만 누를 수 있는 버튼이다. 이 교재는 코드의 버튼으로 시작하지만, 간단히 시험할 때는 이 버튼을 써도 된다.

한 가지 주의가 있다. 서버와 클라이언트는 다음 설정이 같아야 접속이 된다. Protocol Version, Tick Rate, Connection Approval, Force Same Prefabs(그리고 그 프리팹 목록), Enable Scene Management, NetworkVariable Length Safety, Rpc Hash Size. 서로 다르면 서버의 Console에 `NetworkConfig mismatch. The configuration between the server and client does not match` 경고가 나오고 접속이 거부된다. 같은 프로젝트에서 빌드했더라도 설정을 고친 뒤 한쪽만 다시 빌드하면 이 일이 생긴다.

### 2.5.4 실습 1: 버튼으로 Host, Client, Server 시작하기

**목표.** 화면의 버튼으로 호스트, 클라이언트, 서버를 시작하고 종료한다. 접속 상태가 화면과 Console에 나타나게 한다. 이 화면이 이 장 내내 쓰는 접속 화면의 첫 버전이다.

이 교재는 화면(UI)을 Unity의 IMGUI 방식인 `OnGUI`로 만든다. 버튼과 글자를 코드 한 줄로 그릴 수 있어서 씬에 Canvas나 EventSystem을 만들 필요가 없고, NGO 공식 샘플도 같은 방식을 쓴다. 대신 모양이 단순하다. 완성된 채팅 화면을 Canvas 기반의 uGUI로 만드는 방법은 2.11.9절에서 다룬다.

**단계.**

1. 한글을 표시하는 도우미를 만든다. `Assets/Scripts`에 `UiKit.cs`를 만들고 아래 코드로 바꾼다. IMGUI의 기본 글꼴이 한글을 제대로 그리지 못할 때를 대비하여 윈도우의 맑은 고딕을 지정하고 글자 크기를 키운다.

```csharp
using UnityEngine;

// IMGUI 화면에서 한글이 보이도록 글꼴과 글자 크기를 정해 주는 도우미
public static class UiKit
{
    static Font font;

    // 글자 입력 칸에 키보드 포커스가 있으면 true. 이름을 입력하는 동안 W, A, S, D가 이동 키로도 쓰이지 않게 할 때 쓴다.
    public static bool Typing { get; private set; }

    // 모든 OnGUI 함수의 맨 처음에 호출한다.
    public static void Apply()
    {
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 16);

        var skin = GUI.skin;
        skin.font = font;
        skin.label.fontSize = 16;
        skin.button.fontSize = 16;
        skin.textField.fontSize = 16;
        skin.toggle.fontSize = 16;
        skin.box.fontSize = 16;

        // IMGUI는 입력 칸 바깥을 클릭해도 키보드 포커스를 빼 주지 않는다.
        // 아직 처리되지 않은 클릭이면 포커스를 먼저 빼 둔다. 입력 칸을 클릭한 것이면 그 칸이 포커스를 다시 가져간다.
        if (Event.current.type == EventType.MouseDown) GUIUtility.keyboardControl = 0;
        Typing = GUIUtility.keyboardControl != 0;
    }
}
```

`Typing`과 마지막 두 줄은 2.7절부터 쓰는 기능이다. 지금 화면의 입력 칸은 서버 IP와 포트뿐이지만, 이후 닉네임이나 채팅을 입력할 때 그 글자가 캐릭터 이동 입력으로도 들어가는 것을 막기 위해 "지금 글자를 입력하는 중인가"를 `UiKit.Typing`에 기록해 둔다.

2. 같은 폴더에 `NetStarter.cs`를 만들고 아래 코드로 바꾼다.

```csharp
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
```

3. Hierarchy에서 **Create Empty**로 새 오브젝트를 만들고 이름을 `NetStarter`로 바꾼다. `NetStarter.cs`를 이 오브젝트에 붙인다.
4. 씬을 저장한다(Ctrl + S).
5. ▶를 눌러 Game 창을 본다. 왼쪽 위에 서버 IP와 포트 입력칸, Host, Client, Server 버튼이 보이면 성공이다. 한글이 깨져 보이면 2.5.8절을 본다.

**코드를 읽는 법.**

- `OnGUI()`는 Unity가 화면을 그릴 때 호출하는 함수로, 한 프레임에 여러 번 호출될 수 있다. 여기서 `GUILayout.Button("글")`을 쓰면 버튼이 그려지고, 눌렸을 때 true를 돌려준다. 그래서 `if (GUILayout.Button(...))` 안에 누르면 할 일을 쓴다. 다만 이 교재의 화면 코드는 누르면 할 일을 그 자리에서 하지 않고 `pending`에 적어 두기만 하고, 실제로는 `Update()`에서 실행한다. `OnGUI`는 한 프레임 안에서 "배치 계산", "입력 처리", "그리기"로 나뉘어 여러 번 불리는데, 매번 같은 순서로 같은 개수의 컨트롤을 그려야 하기 때문이다. `StartHost()`처럼 상태를 바꾸는 일을 `OnGUI` 안에서 하면 같은 프레임의 다음 호출에서 그릴 내용이 달라져 `Getting control N's position in a group with only N controls when doing Repaint` 같은 오류가 Console에 나올 수 있다. 화면은 다음 프레임부터 정상이 되므로 치명적이지는 않지만, 빨간 오류를 일부러 보고 갈 이유가 없다. 이 규칙은 뒤의 모든 화면 코드에 똑같이 적용한다.
- `NetworkManager.Singleton`은 씬의 NetworkManager를 가리키는 정적 변수이다. 어디서든 `NetworkManager.Singleton`으로 접근한다.
- 호스트와 서버는 `utp.SetConnectionData("127.0.0.1", p, "0.0.0.0")`로 시작한다. 세 번째 인자가 **서버가 접속을 받는 주소**이다. `0.0.0.0`은 "이 PC의 모든 네트워크 카드"를 뜻하므로 같은 PC의 클라이언트도, 같은 네트워크의 다른 PC도 접속할 수 있다. 이 인자를 주지 않으면 서버는 자기 PC 안에서만 접속을 받는다. 이 점은 다른 PC와 연결할 때(2.14절) 중요하다.
- 클라이언트는 `utp.SetConnectionData(ip, p)`로 접속할 서버의 주소와 포트를 지정한다. `StartClient()`를 부르면 접속을 시도한다.
- `StartHost()`, `StartClient()`, `StartServer()`는 시작에 성공하면 true를 돌려준다. 같은 포트가 이미 쓰이고 있는 경우처럼 시작하지 못하면 false를 돌려주고 Console에 이유가 나온다.
- `nm.IsConnectedClient`는 클라이언트가 서버의 승인을 받고 초기 동기화까지 마쳤을 때 true가 된다. `nm.IsClient`는 접속을 시도하는 중에도 true이므로 둘을 구분한다.
- `OnClientConnectedCallback`과 `OnClientDisconnectCallback`은 접속과 종료 때 NGO가 불러 주는 이벤트이다. 서버(호스트)에서는 클라이언트가 접속하고 종료할 때마다, 클라이언트에서는 자기 자신의 접속 완료와 종료 때 호출된다. 2.10절에서 자세히 다룬다.
- `+=`로 등록한 핸들러는 `OnDestroy`에서 `-=`로 해제한다(2.1.3절).

### 2.5.5 한 대의 PC에서 여러 인스턴스 띄우기

![그림 2-7 한 대의 PC로 멀티플레이를 시험하는 세 가지 방법](images/ch02_fig07_test_setups.png)

멀티플레이를 시험하려면 서버와 클라이언트 프로그램이 동시에 떠 있어야 한다. 한 대의 PC에서 하는 방법은 그림 2-7과 같이 세 가지이다. 이 절에서는 B(플레이어 빌드와 에디터)를 따라 하고, A(Multiplayer Play Mode)는 설치하면 더 편하다. C(전용 서버 빌드)는 2.14절에서 한다.

**방법 B: 플레이어를 빌드하여 에디터와 함께 실행한다.** 가장 확실한 방법이다. 빌드한 프로그램은 실제로 배포하는 프로그램과 같은 조건에서 실행된다.

1. `File > Build Profiles`를 연다.
2. 왼쪽 목록에서 **Windows**를 선택한다. 활성 플랫폼이 Windows가 아니면 오른쪽의 **Switch Platform**을 누른다.
3. **Scene List**에 `Scenes/SampleScene`이 체크되어 있는지 본다. 목록에 없으면 `SampleScene`을 연 상태에서 **Add Open Scenes**를 누른다(버튼 이름은 버전에 따라 조금 다를 수 있다). 이 목록에 없는 씬은 빌드에 들어가지 않고, 씬 관리 기능이 켜져 있으면 접속이 실패한다.
4. **Build** 버튼을 누르고, 폴더 선택 창에서 프로젝트 폴더 안에 `Builds` 폴더를 만들어 그 안에 `Client` 폴더를 만들어 선택한다. 처음 빌드는 몇 분 걸린다.
5. 빌드가 끝나면 폴더가 열린다. `NgoChat.exe`(Product Name으로 정한 이름)를 실행한다. Windows 보안 경고가 나오면 **개인 네트워크**만 허용한다. 같은 PC에서만 시험할 때는 허용하지 않아도 동작한다.
6. 에디터에서 ▶를 눌러 **Host**를 시작하고, 빌드한 프로그램에서 **Client**를 시작한다. 프로그램을 한 번 더 실행하면 클라이언트를 하나 더 만들 수 있다.

코드나 NetworkManager 설정, 프리팹 목록을 바꿀 때마다 빌드를 다시 해야 한다. 이 번거로움이 이 방법의 단점이다. 빌드한 프로그램의 로그는 `C:\Users\사용자이름\AppData\LocalLow\<Company Name>\<Product Name>\Player.log` 파일에 남는다(2.13절).

**방법 A: Multiplayer Play Mode(MPPM).** 같은 프로젝트를 여러 에디터 창으로 동시에 실행하는 패키지이다. 빌드 없이 코드를 고치고 바로 시험할 수 있다. 이 기능은 Unity와 패키지 버전에 따라 메뉴 이름과 화면 배치가 조금씩 다르므로 순서만 요약한다. 이 방법이 잘 동작하지 않으면 방법 B로 모든 실습을 할 수 있다.

1. Package Manager 왼쪽 위의 드롭다운을 **Unity Registry**로 바꾸고 **Multiplayer Play Mode**를 찾아 설치한다(패키지 이름 `com.unity.multiplayer.playmode`, **Install package by name**으로 입력해도 된다).
2. `Window` 메뉴에서 **Multiplayer Play Mode** 창을 연다(버전에 따라 `Window > Multiplayer` 아래에 있을 수 있다).
3. 창에서 **Player 2**(필요하면 Player 3, 4)를 활성화한다. 가상 플레이어는 같은 프로젝트를 여는 별도의 에디터 프로세스이다. **반드시 Play 모드에 들어가기 전에 활성화한다.** Play 중에 켜면 이번 실행에는 참여하지 못한다.
4. 메인 에디터에서 ▶를 누르면 활성화한 가상 플레이어도 함께 Play에 들어간다. 가상 플레이어의 Game 화면과 Console은 Multiplayer Play Mode 창에서 열 수 있다.
5. 메인 에디터의 화면에서 **Host**를, 가상 플레이어의 화면에서 **Client**를 누른다.

### 2.5.6 실행하여 확인하기

다음 순서로 시험하고, 각 단계에서 화면과 Console에 나타나는 것을 확인한다.

1. 호스트 하나만 먼저 시작한다. 화면에 `모드: Host`와 `접속자 수: 1`이 나온다. 호스트 자신도 접속자 하나로 센다. Console에는 `[Host] 접속 완료: ClientId 0`이 찍힌다.
2. 다른 인스턴스에서 **Client**를 누른다. 클라이언트 화면에 `접속됨. 내 ClientId = 1`이 나오고, 호스트 화면의 접속자 수가 2가 된다. 호스트 Console에 `[Host] 접속 완료: ClientId 1`, 클라이언트 Console에는 `[Client] 접속 완료: ClientId 1`이 찍힌다.
3. 클라이언트를 하나 더 접속시킨다. ClientId가 2이다.
4. 클라이언트 하나에서 **종료**를 누른다. 호스트의 접속자 수가 하나 줄고 Console에 `접속 종료: ClientId 1`이 찍힌다.
5. 방금 종료한 인스턴스에서 다시 **Client**를 누른다. 이번에는 ClientId 3을 받는다. 접속 번호는 재사용하지 않고 계속 늘어난다.
6. 호스트를 종료한다. 호스트는 접속자에게 끊는다고 알리고 나서 종료하므로 `종료하는 중...`이 최대 5초 정도 표시될 수 있다. 남은 클라이언트 화면에는 "서버와의 연결이 끊어졌다"와 함께 사유로 `Disconnected due to host shutting down.`이 나오고 시작 버튼 화면으로 돌아간다. 서버가 끊을 때 이유를 함께 알려 주면 클라이언트는 `NetworkManager.DisconnectReason`으로 그것을 읽을 수 있다(2.10절).
7. Host 없이 Client만 눌러 본다. 한동안 `서버에 접속하는 중...`이 표시된 뒤(기본 설정에서 대략 1분 안팎) 접속에 실패했다는 안내가 나오고 Console에 `Failed to connect to server.` 오류가 찍힌다.
8. 이번에는 **Server**(서버만)로 시작한다. 접속자 수가 0이다. 클라이언트가 접속하면 1이 된다. 호스트와 달리 서버 자신은 접속자가 아니다.
9. 클라이언트가 접속한 상태에서 프로그램 창을 닫지 않고 작업 관리자로 강제 종료해 본다. 서버는 바로 알아채지 못하고 한참 뒤에 접속 종료로 처리한다. 클라이언트가 정상 종료 절차 없이 사라진 경우 서버는 일정 시간(Unity Transport의 Disconnect Timeout, 기본 30초 안팎) 동안 신호가 없어야 끊어진 것으로 판단한다. 실제 서비스에서 흔한 상황이다.

### 2.5.7 그 뒤에서 일어난 일

`StartHost()`를 호출하면 NetworkManager가 Unity Transport에게 UDP 포트 7777을 열라고 시키고, 서버 역할과 클라이언트 역할을 동시에 시작한다. 호스트의 클라이언트는 네트워크를 거치지 않고 같은 프로세스 안에서 서버에 바로 연결되며 ClientId 0을 받는다.

`StartClient()`를 호출하면 Unity Transport가 지정한 주소로 UDP 연결을 시도한다. 연결이 되면 클라이언트가 접속 요청(Connection Request)을 보내고, 서버가 이를 승인하면 현재 씬과 오브젝트 정보를 보내 주고, 클라이언트가 동기화를 마치면 `OnClientConnectedCallback`이 호출된다. 이 과정의 자세한 순서는 2.6절의 그림 2-8에서 본다. 접속 승인을 쓰지 않으면(Connection Approval이 꺼진 기본 상태) 서버는 모든 접속 요청을 자동으로 승인한다.

ClientId는 서버가 접속 순서대로 1, 2, 3...으로 부여하며, 접속이 끊어졌다가 다시 접속하면 새 번호를 받는다. 번호는 컴퓨터나 사람이 아니라 연결 하나를 가리킨다는 점을 기억해 둔다. 사람을 구분하려면 이름 같은 별도의 정보가 필요하다(2.10절).

### 2.5.8 흔한 오류

- **버튼은 보이는데 한글이 네모로 보인다.** `UiKit.Apply()`를 `OnGUI`의 첫 줄에서 호출하는지 확인한다. 그래도 같으면 `UiKit.cs`의 글꼴 이름 목록에 PC에 설치된 한글 글꼴을 추가한다. IMGUI의 한글 입력(IME)은 에디터와 환경에 따라 조합 중인 글자가 늦게 보일 수 있으므로, 입력이 이상하면 영문으로 시험한다.
- **화면이 아무것도 나오지 않는다.** `NetStarter`가 씬의 오브젝트에 붙어 있는지, 오브젝트가 활성 상태(Inspector 맨 위 체크)인지 확인한다. 화면에 `씬에 NetworkManager가 없다`가 나오면 2.5.2절의 NetworkManager 오브젝트를 만들지 않은 것이다.
- **`Host`를 눌렀는데 `시작하지 못했다`가 나온다.** Console을 확인한다. `Server failed to bind. This is usually caused by another process being bound to the same port.`이면 같은 포트를 다른 프로세스가 쓰고 있다. 이전에 켠 빌드나 에디터가 아직 서버로 남아 있지 않은지 확인하고, 없으면 포트를 바꾼다. NetworkManager의 Network Transport 칸이 비어 있어도 시작되지 않는다.
- **클라이언트가 접속은 하는데 곧바로 끊어진다.** 서버 쪽 Console에 `NetworkConfig mismatch. The configuration between the server and client does not match`가 있으면 2.5.3절의 설정이 서로 다른 것이다. 빌드한 프로그램이 에디터보다 오래된 것은 아닌지 확인하고 다시 빌드한다.
- **클라이언트가 한참 접속 중이다가 실패한다.** 서버(호스트)를 먼저 시작했는지, 서버 IP와 포트가 맞는지 확인한다. 다른 PC로 접속할 때는 방화벽도 확인한다(2.14절).
- **한 번 종료한 뒤 다시 시작이 안 된다.** `Shutdown()`은 바로 끝나지 않으므로 `ShutdownInProgress`가 true인 동안은 다시 시작하지 않는다. 이 교재의 화면은 그 동안 `종료하는 중...`을 표시하고 버튼을 숨긴다.
- **재생 중에 NetworkManager 설정을 바꿨더니 다음 실행에서 사라진다.** 정상이다(2.2.3절). 설정은 Play를 멈춘 뒤에 바꾼다.

**이 절의 정리.**

- NGO 설치: Package Manager에서 `com.unity.netcode.gameobjects`, 버전이 2.x인지 확인한다.
- NetworkManager 오브젝트 + Unity Transport 컴포넌트가 씬에 하나 있어야 한다.
- 접속은 호스트나 서버를 먼저, 그다음 클라이언트 순서이다.
- 에디터와 빌드를 함께 띄우고, 클라이언트를 빌드한 뒤 설정을 바꾸면 다시 빌드한다.

## 2.6 플레이어 오브젝트와 스폰

2.5절에서는 접속만 했고 화면에 보이는 것이 없었다. 이 절에서는 클라이언트가 접속하면 서버가 그 클라이언트의 플레이어 캡슐을 만들어 모든 접속자의 화면에 나타나게 한다. 이 과정에서 NGO의 핵심 개념인 스폰(Spawn), 소유자, `OnNetworkSpawn()`을 처음 만난다.

### 2.6.1 스폰이란 무엇인가

![그림 2-8 클라이언트가 접속할 때 일어나는 일](images/ch02_fig08_connect_spawn.png)

**스폰(Spawn)** 은 서버가 네트워크 오브젝트를 만들어 모든 접속자의 PC에 같은 오브젝트가 생기게 하는 일이다. 반대로 **디스폰(Despawn)** 은 서버가 오브젝트를 제거하면 모든 접속자의 PC에서 사라지게 하는 일이다. 스폰과 디스폰은 서버만 할 수 있다(2.4.5절). 클라이언트가 `Instantiate`로 만든 오브젝트는 그 컴퓨터에만 존재하는 평범한 오브젝트일 뿐이다.

네트워크 오브젝트는 두 종류가 있다.

- **동적 스폰 오브젝트**: 프리팹으로 정의해 두고 게임 중에 서버가 스폰한다. 플레이어, 아이템, 투사체가 이 방식이다. 프리팹에는 NetworkObject 컴포넌트가 있어야 하고, 프리팹이 NetworkManager의 Network Prefabs 목록에 등록되어 있어야 한다.
- **씬에 미리 배치한 오브젝트(in-scene placed)**: 씬 안에 처음부터 놓아 둔 NetworkObject이다. 서버가 시작하면 자동으로 스폰되고, 나중에 접속하는 클라이언트는 씬을 받으면서 함께 받는다. 2.11절의 채팅 허브가 이 방식이다.

**플레이어 오브젝트**는 접속한 클라이언트마다 하나씩 만들어지는 특별한 네트워크 오브젝트이다. NetworkManager의 **Default Player Prefab** 칸에 프리팹을 넣어 두면 NGO가 클라이언트가 접속할 때마다(접속 승인에서 플레이어 오브젝트 생성을 허용한 경우) 그 프리팹으로 오브젝트를 만들어 스폰하고, 그 클라이언트를 소유자로 지정한다. 그림 2-8은 이 과정이 접속 과정 어디에서 일어나는지 보여 준다. 서버가 승인 응답을 보낸 뒤 현재 씬과 이미 있는 오브젝트들(다른 접속자의 플레이어 포함)을 클라이언트에게 보내고, 클라이언트가 이를 모두 받아 처리하면 `OnClientConnectedCallback`이 호출된다.

### 2.6.2 실습 2: 접속한 클라이언트마다 플레이어 캡슐 만들기

**목표.** 클라이언트가 접속하면 캡슐이 하나씩 생기고, 모든 접속자의 화면에 같은 캡슐들이 보이게 한다. 각 캡슐이 어느 컴퓨터에서 어떻게 스폰되는지를 Console 로그로 읽는다.

**단계.**

1. 플레이어 프리팹을 만든다. Hierarchy에서 마우스 오른쪽 버튼 > **3D Object** > **Capsule**을 만들고 이름을 `NetPlayer`로 바꾼다. Position은 `0, 1, 0`이다.
2. `NetPlayer`를 선택하고 **Add Component**에서 `Network Object`를 검색해 **Network Object**(Netcode 항목)를 추가한다. 이 컴포넌트가 있어야 NGO가 이 오브젝트를 동기화한다.
3. `NetPlayer`를 Project 창의 `Assets/Prefabs` 폴더로 끌어다 프리팹으로 만든다. 그런 다음 Hierarchy에 남은 `NetPlayer`를 지운다. 플레이어는 이제 NGO가 만들어 주므로 씬에 직접 놓지 않는다.
4. `Assets/Prefabs`의 `NetPlayer` 프리팹을 Project 창에서 끌어다 `NetworkManager` 오브젝트의 **Default Player Prefab** 칸에 놓는다. 반드시 Project 창의 프리팹 파일을 끌어와야 한다. Hierarchy의 오브젝트를 놓으면 안 된다.
5. Project 창의 `Assets/DefaultNetworkPrefabs.asset`을 선택해 보면 Inspector의 목록에 `NetPlayer`가 자동으로 들어 있다. 이렇게 NetworkObject를 가진 프리팹은 기본 목록에 자동 등록된다.
6. 플레이어가 생길 위치를 정하는 `ApprovalGate.cs`를 `Assets/Scripts`에 만든다. 승인 콜백 안에서 위치를 정하는 것이 NGO가 권장하는 방법이다. 이 스크립트는 2.10절에서 접속 승인 규칙을 더해 완성한다. 지금은 모두 승인하고 위치만 정한다.

```csharp
using Unity.Netcode;
using UnityEngine;

// 접속 승인 콜백. 지금은 모두 승인하고 플레이어가 생길 위치만 정한다. (2.10절에서 검사 규칙을 더한다)
public class ApprovalGate : MonoBehaviour
{
    void Start()
    {
        var nm = NetworkManager.Singleton;
        nm.NetworkConfig.ConnectionApproval = true;     // 접속 승인 콜백을 사용한다
        nm.ConnectionApprovalCallback = Approve;        // 서버(호스트)에서 호출된다
    }

    void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;                                    // 접속을 승인한다
        response.CreatePlayerObject = true;                          // Default Player Prefab으로 플레이어를 만든다
        response.Position = SpawnPoint(request.ClientNetworkId);     // 플레이어가 생길 위치
        response.Rotation = Quaternion.identity;
    }

    // ClientId마다 가로로 2미터씩 떨어진 자리를 준다.
    static Vector3 SpawnPoint(ulong clientId) => new Vector3((int)(clientId % 8) * 2f - 7f, 1f, 0f);
}
```

7. `ApprovalGate.cs`를 2.5절에서 만든 `NetStarter` 오브젝트에 붙인다(Hierarchy의 `NetStarter`를 선택하고 스크립트를 Inspector로 끌어다 놓는다). 이제 이 오브젝트에는 `NetStarter`와 `ApprovalGate` 두 스크립트가 붙어 있다.
8. 플레이어에서 일어나는 일을 로그로 보여 주는 `PlayerInfo.cs`를 만든다.

```csharp
using Unity.Netcode;
using UnityEngine;

// 플레이어 오브젝트가 어디서 어떻게 만들어지는지 Console에 출력한다.
public class PlayerInfo : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        string where = IsHost ? "Host" : IsServer ? "Server" : "Client";
        Debug.Log($"[{where}] 스폰: NetworkObjectId={NetworkObjectId}, 소유자={OwnerClientId}, " +
                  $"IsOwner={IsOwner}, IsLocalPlayer={IsLocalPlayer}");
    }

    public override void OnNetworkDespawn()
    {
        string where = IsHost ? "Host" : IsServer ? "Server" : "Client";
        Debug.Log($"[{where}] 디스폰: NetworkObjectId={NetworkObjectId}, 소유자={OwnerClientId}");
    }
}
```

9. `PlayerInfo.cs`를 `NetPlayer` 프리팹에 붙인다. Project 창에서 프리팹을 더블클릭하여 Prefab Mode로 열고, 스크립트를 Inspector로 끌어다 놓은 뒤 왼쪽 위의 화살표로 돌아온다. 프리팹에서 NetworkObject 컴포넌트가 스크립트보다 위에 있는지 확인한다. 이후에 NetworkBehaviour 스크립트를 붙일 때도 NetworkObject가 있는 오브젝트에 붙여야 한다. NetworkBehaviour를 먼저 붙이면 NetworkObject가 자동으로 함께 추가되기도 한다.
10. 씬을 저장하고 에디터를 Host로, 빌드(또는 가상 플레이어)를 Client로 실행한다. 빌드한 프로그램은 코드와 프리팹이 바뀌었으므로 다시 빌드해야 한다(2.5.5절).

**확인할 것.** 다음 순서로 확인한다.

1. 에디터에서 **Host**를 누른다. Game 창 왼쪽에 캡슐이 하나 보이고, Hierarchy에 `NetPlayer(Clone)`이 하나 생긴다. 접속 승인 콜백이 호스트 자신의 접속에도 불렸고, 그 결과 ClientId 0의 플레이어가 `SpawnPoint(0)`인 x = −7에 만들어졌다. Console에는 다음과 같은 줄이 나온다(번호는 환경에 따라 다를 수 있다).

```text
[Host] 스폰: NetworkObjectId=1, 소유자=0, IsOwner=True, IsLocalPlayer=True
```

2. 빌드한 프로그램에서 **Client**를 누른다. 호스트 화면에 캡슐이 하나 더 나타난다(x = −5). 호스트 Console에는 새 플레이어가 호스트에서 스폰된 줄이 나온다. 이번에는 `IsOwner=False`이다. 이 캡슐은 ClientId 1의 것이고 호스트는 소유자가 아니기 때문이다.

```text
[Host] 스폰: NetworkObjectId=2, 소유자=1, IsOwner=False, IsLocalPlayer=False
```

3. 클라이언트 화면에도 캡슐 두 개가 보인다. 클라이언트의 Console(또는 빌드의 Player.log)에는 `스폰:` 줄이 두 개 나온다. 접속하기 전부터 있던 호스트의 플레이어(소유자=0, `IsOwner=False`)와 자기 플레이어(소유자=1, `IsOwner=True`, `IsLocalPlayer=True`)이다. 접속하자마자 이미 있던 오브젝트들이 클라이언트에 만들어졌다는 점이 중요하다. 중간에 접속한 사람도 같은 장면을 보게 하는 것이 NGO의 스폰 동기화이다.
4. 클라이언트를 하나 더 접속시킨다. 캡슐이 세 개가 된다(x = −3). 모든 컴퓨터의 Hierarchy에서 `NetPlayer(Clone)`이 접속자 수만큼 있는지 센다.
5. Play 중에 Hierarchy에서 `NetPlayer(Clone)` 하나를 선택하고 Inspector의 **Network Object** 컴포넌트를 본다. `NetworkObjectId`, `OwnerClientId`, `IsSpawned`, `IsLocalPlayer`, `IsOwner`, `IsPlayerObject` 같은 값이 읽기 전용으로 표시되고, 서버(호스트)에서는 이 오브젝트를 볼 수 있는 접속자 목록(Observers)도 보인다. 같은 오브젝트가 컴퓨터마다 다른 값을 가진다는 것(2.4.4절의 그림 2-4)을 호스트와 클라이언트에서 번갈아 확인한다.
6. 클라이언트 하나를 **종료**한다. 호스트와 다른 클라이언트의 Hierarchy에서 그 클라이언트의 `NetPlayer(Clone)`이 사라지고 `디스폰:` 로그가 나온다. 접속을 끊은 클라이언트가 소유하던 오브젝트는 기본적으로 서버가 함께 제거한다(NetworkObject의 **Don't Destroy With Owner**가 꺼진 상태).
7. 같은 클라이언트가 다시 접속하면 새 ClientId를 받으므로 위치도 달라진다. ClientId가 3이면 x = −1이다.

**그 뒤에서 일어난 일.**

1. 클라이언트가 접속 요청을 보내면 서버는 접속 승인 콜백(`Approve`)을 호출한다. 콜백이 `CreatePlayerObject = true`와 `Position`을 채워 주었으므로, 서버는 Default Player Prefab을 그 위치에 `Instantiate`한다.
2. 서버는 그 오브젝트를 해당 클라이언트가 소유하는 플레이어 오브젝트로 스폰한다. 서버에서 `OnNetworkSpawn()`이 호출된다.
3. 접속한 클라이언트에게는 씬 정보와 이미 스폰된 모든 오브젝트의 정보(위치 포함)를 보낸다. 클라이언트가 같은 프리팹으로 오브젝트들을 만들고 각각 `OnNetworkSpawn()`을 호출한다.
4. 기존 접속자들에게는 새 플레이어의 생성 정보를 보내어 그 컴퓨터에도 오브젝트를 만들게 한다.

플레이어 오브젝트에 접근하는 방법도 알아 두자. 코드에서 내 플레이어는 `NetworkManager.Singleton.LocalClient.PlayerObject`이고, 서버에서는 `NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject`로 특정 접속자의 플레이어를 얻는다. 이 값들은 접속이 완료되기 전에는 비어 있다.

### 2.6.3 OnNetworkSpawn: 네트워크 코드의 시작점

`PlayerInfo`의 `OnNetworkSpawn()`은 이 오브젝트가 네트워크에 스폰된 직후, 그 컴퓨터마다 한 번씩 호출된다. 이 시점부터 `IsOwner`, `IsServer`, `OwnerClientId`, NetworkVariable 값이 확정되어 있다. 반대로 `Awake()`와 `Start()` 시점에는 아직 확정되지 않은 경우가 있다.

- 동적으로 스폰되는 오브젝트의 호출 순서: `Awake()` → `OnNetworkSpawn()` → `Start()`
- 씬에 미리 배치한 오브젝트의 호출 순서: `Awake()` → `Start()` → `OnNetworkSpawn()`

순서가 다르므로 "`IsOwner`가 필요한 초기화는 `OnNetworkSpawn()`에서 한다"는 한 가지 규칙만 지키면 어느 쪽이든 안전하다. `OnNetworkSpawn()`과 짝이 되는 `OnNetworkDespawn()`은 오브젝트가 네트워크에서 제거되기 직전에 호출된다. `OnNetworkSpawn()`에서 등록한 이벤트 핸들러는 `OnNetworkDespawn()`에서 해제한다. `Update()`에서는 `IsSpawned`가 true일 때만 네트워크 값을 쓴다.

`OnDestroy()`를 직접 정의할 때는 반드시 `base.OnDestroy()`를 함께 호출한다. NetworkBehaviour가 정리 작업을 `OnDestroy()`에서 하기 때문이다.

### 2.6.4 흔한 오류

- **Host를 눌러도 캡슐이 생기지 않는다.** NetworkManager의 **Default Player Prefab**이 비어 있지 않은지, `ApprovalGate`가 씬의 오브젝트에 붙어 있는지 확인한다. `ApprovalGate`의 승인 콜백에서 `CreatePlayerObject`를 true로 하지 않아도 캡슐이 생기지 않는다. 승인 콜백을 쓰면 `Approved`와 `CreatePlayerObject`의 기본값은 false이므로 코드에서 직접 true로 만들어야 한다.
- **Default Player Prefab 칸에 Hierarchy의 오브젝트를 넣었다.** Project 창의 프리팹 파일을 넣어야 한다. 씬의 오브젝트를 넣으면 클라이언트에서 같은 프리팹을 찾지 못한다.
- **프리팹에 NetworkObject가 없다.** 프리팹을 열어 가장 바깥 오브젝트에 **Network Object** 컴포넌트가 있는지 확인한다. 자식 오브젝트에 붙이면 안 된다. NetworkManager 오브젝트에는 NetworkObject를 붙이지 않는다(붙이면 경고가 난다).
- **캡슐이 모두 같은 자리에 겹쳐 보인다.** `ApprovalGate`가 붙어 있지 않아 위치가 프리팹의 위치로 쓰였을 가능성이 있다. NetStarter 오브젝트의 컴포넌트를 확인한다.
- **클라이언트가 접속은 되는데 캡슐이 안 보이거나 접속이 거부된다.** 빌드한 프로그램이 오래된 것일 수 있다. 서버의 Console에 `NetworkConfig mismatch`가 있는지 보고 클라이언트를 다시 빌드한다. 프리팹 목록이 서버와 클라이언트에서 달라도 같은 오류가 난다(Force Same Prefabs).
- **`No ConnectionApprovalCallback defined. Connection approval will timeout` 경고가 나고 접속이 되지 않는다.** NetworkManager의 Connection Approval 체크는 켜져 있는데 콜백이 없다. `ApprovalGate`가 씬에 붙어 있는지 확인하거나, Inspector에서 직접 켠 체크를 끈다.
- **`ConnectionApprovalCallback is defined but ConnectionApproval is disabled` 경고가 난다.** 콜백은 등록했는데 Connection Approval이 꺼진 상태로 시작했다는 뜻이다. `ApprovalGate.Start()`에서 `ConnectionApproval = true`를 하는 줄이 있는지 확인한다.

**이 절의 정리.**

- 플레이어 오브젝트 = NetworkObject가 붙은 프리팹을 NetworkManager의 Default Player Prefab에 등록하면, 접속자마다 서버가 하나씩 스폰한다.
- 스폰되는 위치는 접속 승인 콜백의 `response.Position`으로 정한다.
- 스폰된 오브젝트는 모든 접속자의 PC에 각각 하나씩 존재하고, `OnNetworkSpawn()`이 그 컴퓨터마다 한 번씩 호출된다.
- 접속을 끊으면 그 클라이언트가 소유한 오브젝트는 서버가 제거한다.

## 2.7 이동 동기화와 소유권

2.6절에서 접속자마다 캡슐이 생겼지만 움직이지는 않는다. 이 절에서는 각자 자기 캡슐만 키보드로 움직이고 그 움직임이 모든 접속자의 화면에 보이게 한다. 2.3절 실습 0에서 본 "캡슐 셋이 함께 움직이는 문제"를 NGO의 소유권으로 해결하는 단계이다.

### 2.7.1 이동을 동기화하는 두 부분

플레이어의 이동 동기화는 두 가지 일로 나뉜다.

- **누가 움직일지 정하기**: 소유자의 컴퓨터에서만 키보드를 읽고 캡슐을 움직인다. `IsOwner`로 구분하는 일이고, 우리가 작성하는 `PlayerMove` 스크립트가 맡는다.
- **움직임을 다른 컴퓨터에 전달하기**: 소유자 컴퓨터에서 바뀐 위치를 서버와 다른 클라이언트의 복사본에 반영하는 일이다. **NetworkTransform** 컴포넌트가 이 일을 해 준다.

NetworkTransform은 오브젝트의 위치, 회전, 크기를 틱마다 살펴서 달라졌으면 전달하고, 받는 쪽에서는 부드럽게 이어서 보여 준다. 누가 위치를 정하는지(권한)는 **Authority Mode**로 고른다(2.4.5절의 그림 2-5).

- **Server**(기본값): 서버가 위치를 정한다. 클라이언트의 로컬 이동은 서버 값으로 덮어써진다. 소유자가 직접 움직이게 하려면 이동 요청을 RPC로 서버에 보내야 한다.
- **Owner**: 소유자가 위치를 정한다. 소유자가 로컬에서 움직이면 NetworkTransform이 그 위치를 서버에 올려 보내고, 서버가 다른 클라이언트에게 전달한다.

이 교재는 Owner를 쓴다. 이동 코드가 단순하고 입력에 바로 반응한다. 대신 서버가 이동을 검증하지 않으므로, 소유자 클라이언트가 어떤 위치든 보낼 수 있다는 점을 인지하고 쓴다(2.1.2절).

### 2.7.2 실습 3: 내 캡슐만 키보드로 움직이고 모두에게 보이게 하기

**목표.** 각 접속자가 자기 캡슐을 WASD와 방향키로 움직이고, 모든 컴퓨터에서 그 움직임이 보인다. 키보드 없이도 움직임을 시험할 수 있는 자동 이동 기능을 함께 만든다.

**단계.**

1. 이동 스크립트 `PlayerMove.cs`를 `Assets/Scripts`에 만든다.

```csharp
using Unity.Netcode;
using UnityEngine;

// 소유자의 컴퓨터에서만 입력을 읽어 움직인다. 위치를 다른 컴퓨터에 전달하는 일은 NetworkTransform이 한다.
public class PlayerMove : NetworkBehaviour
{
    public static bool AutoWalk;               // true이면 키보드 대신 원을 그리며 걷는다(가상 입력)

    [SerializeField] float speed = 4f;         // 초당 이동 거리(미터)

    void Update()
    {
        if (!IsSpawned || !IsOwner) return;    // 내 것이 아닌 복사본은 입력을 읽지 않는다

        // 글자를 입력하는 중(UiKit.Typing)에는 키보드 이동을 하지 않는다. 입력 칸은 2.8절부터 생긴다.
        Vector2 dir = AutoWalk ? AutoDirection() : (UiKit.Typing ? Vector2.zero : SimpleInput.Move());
        transform.position += new Vector3(dir.x, 0f, dir.y) * (speed * Time.deltaTime);
    }

    // 시간에 따라 방향이 계속 바뀌어 원을 그린다. 접속자마다 시작 방향이 다르다.
    Vector2 AutoDirection()
    {
        float t = Time.time + (float)OwnerClientId;
        return new Vector2(Mathf.Cos(t), Mathf.Sin(t));
    }
}
```

2. 자동 이동을 켜고 끄는 체크 상자 `AutoWalkToggle.cs`를 만든다. 화면 오른쪽 위에 그려진다.

```csharp
using UnityEngine;

// 화면 오른쪽 위의 체크 상자. 켜면 이 컴퓨터의 플레이어가 키보드 없이 원을 그리며 걷는다.
public class AutoWalkToggle : MonoBehaviour
{
    void OnGUI()
    {
        UiKit.Apply();
        var rect = new Rect(Screen.width - 270, 10, 260, 30);
        PlayerMove.AutoWalk = GUI.Toggle(rect, PlayerMove.AutoWalk, "자동 이동 (가상 입력)");
    }
}
```

3. `AutoWalkToggle.cs`를 `NetStarter` 오브젝트에 붙인다.
4. `NetPlayer` 프리팹을 더블클릭하여 Prefab Mode로 연다. `Add Component`로 `Network Transform`(Netcode 항목)을 추가한다. 반드시 **NetworkObject가 붙은 같은 오브젝트**(프리팹의 가장 바깥)에 붙인다.
5. 추가한 **Network Transform**의 Inspector에서 **Authority** 아래의 **Authority Mode**를 **Owner**로 바꾼다. 이 한 줄이 이 실습의 핵심이다.
6. (선택) **Axis to Synchronize**에서 Rotation의 X, Y, Z와 Scale의 X, Y, Z 체크를 끈다. 이 캡슐은 회전하거나 커지지 않으므로 전송할 필요가 없다. Position의 Y도 끌 수 있지만 이후 실습에서 높이가 바뀔 수 있으므로 켜 둔다.
7. `PlayerMove.cs`를 `NetPlayer` 프리팹에 붙인다(`PlayerInfo`는 그대로 둔다). 프리팹에는 이제 Network Object, Network Transform, Player Info, Player Move가 붙어 있다. Prefab Mode를 닫는다.
8. 씬을 저장하고 에디터를 Host로, 빌드를 Client로 실행한다. 빌드는 프리팹이 바뀌었으므로 다시 해야 한다. 한 대의 PC에서 시험할 때 키보드 입력은 **포커스가 있는 창 하나에만** 들어간다는 점을 기억한다.

**확인할 것.**

1. Host와 Client를 접속시킨다. 호스트 창을 클릭하고 W, A, S, D를 누른다. 호스트의 캡슐만 움직이고, 클라이언트 창에서도 호스트의 캡슐이 같은 방향으로 움직인다.
2. 클라이언트 창을 클릭하고 키를 누른다. 클라이언트의 캡슐만 움직이고 호스트 창에서도 그 움직임이 보인다. 호스트의 캡슐은 가만히 있다. 같은 키보드를 눌러도 내 것만 움직이는 것이 `IsOwner` 덕분이다. 실습 0의 문제가 해결되었다.
3. **자동 이동 (가상 입력)** 체크 상자를 호스트와 클라이언트 양쪽에서 켠다. 두 캡슐이 각각 원을 그리며 걷는다. 포커스가 없는 창에서도 움직이므로 두 창을 나란히 놓고 관찰할 수 있다(NetworkManager의 **Run In Background**가 켜져 있어서 가능하다).
4. 클라이언트를 하나 더 접속시킨다. 이미 움직이고 있던 다른 캡슐들의 현재 위치가 새 클라이언트의 화면에 그대로 나타난다. 오브젝트가 스폰될 때 NetworkObject의 **Synchronize Transform**(기본 켜짐)이 현재 위치까지 함께 전달하기 때문이다.
5. **Authority Mode를 Server로 바꿔 비교한다.** Play를 멈추고 `NetPlayer` 프리팹의 **Authority Mode**를 **Server**로 바꾼 뒤, 클라이언트 쪽도 같은 프로젝트 상태로 다시 실행한다(빌드를 쓰면 다시 빌드한다). 클라이언트의 캡슐은 키를 눌러도 움직이지 않거나 제자리로 되돌아간다. 서버가 위치를 정하는 방식이므로 소유자의 로컬 이동이 서버 값으로 덮어써지기 때문이다. 반면 호스트 자신의 캡슐은 서버이면서 소유자이므로 여전히 움직인다. 확인했으면 Authority Mode를 **Owner**로 되돌린다.

**그 뒤에서 일어난 일.**

`PlayerMove.Update()`는 모든 컴퓨터의 모든 `NetPlayer` 복사본에서 실행되지만, `IsOwner`가 false인 복사본은 곧바로 돌아간다. 그래서 키보드 입력은 소유자 컴퓨터의 소유자 복사본만 움직인다. 이 복사본의 위치가 바뀌면 NetworkTransform이 다음 틱에 변화를 알아채고(기본 임계값 이상 바뀐 경우에만) 위치를 서버에 보낸다. 서버는 그 복사본을 소유하지 않은 모든 접속자에게 위치를 전달한다. 받은 쪽의 NetworkTransform은 받은 위치로 복사본을 옮기는데, **Interpolate**(기본 켜짐)가 켜져 있으면 받은 위치들 사이를 부드럽게 이어서 보여 준다. 그래서 다른 접속자의 캡슐은 실제 위치보다 아주 약간 과거에 보인다(2.16절).

Authority Mode가 Owner일 때 서버의 복사본은 위치를 직접 정하지 않고 소유자가 보낸 값을 받는다. 호스트가 아닌 전용 서버(StartServer)에서도 마찬가지이다. 이것이 서버가 이동 규칙을 모른 채 중계만 한다는 2.1.2절의 범위에 해당한다.

**그 밖에 알아 둘 것.**

- 큰 위치 변화를 보간 없이 한 번에 반영하려면 `transform.position`에 값을 대입하는 대신 `GetComponent<NetworkTransform>().Teleport(위치, 회전, 크기)`를 소유자(권한이 있는 쪽)에서 호출한다. 순간이동이나 리스폰에 쓴다.
- 물리(`Rigidbody`)를 쓰는 오브젝트는 NetworkTransform과 함께 NetworkRigidbody를 붙인다. 이 장의 캡슐은 물리를 쓰지 않으므로 필요 없다.
- NGO 1.x 자료에는 소유자 권한 이동을 위해 `ClientNetworkTransform`이라는 클래스를 직접 만들어 쓰는 방법이 나온다. NGO 2.x에서는 필요 없고 위의 Authority Mode만 바꾸면 된다(2.9.6절).

### 2.7.3 흔한 오류

- **키를 누르면 모든 캡슐이 함께 움직인다.** `PlayerMove`의 `if (!IsSpawned || !IsOwner) return;`을 빠뜨렸다. 2.3절의 `Mover` 스크립트가 `NetPlayer` 프리팹에 붙어 있어도 같은 증상이 난다. `Mover`를 `NetPlayer`에서 제거한다.
- **내 캡슐은 움직이는데 다른 컴퓨터에서는 안 움직인다.** `NetPlayer` 프리팹에 NetworkTransform이 없거나 Authority Mode가 Server이다. 프리팹을 고치고 저장했는지, 빌드를 다시 했는지 확인한다.
- **키를 눌러도 아무 캡슐도 움직이지 않는다.** 해당 창을 클릭해서 포커스를 주었는지, `PlayerMove`가 프리팹에 붙어 있는지, Console에 컴파일 오류나 Input 예외가 없는지 확인한다.
- **다른 컴퓨터의 캡슐이 끊겨서 움직인다.** Interpolate가 꺼져 있거나 창이 가려져 프레임이 낮은 경우가 많다. NetworkTransform의 **Interpolate** 체크를 확인한다. 지연이 클 때 어떻게 보이는지는 2.13절과 2.16절에서 다룬다.
- **자동 이동 체크 상자를 켰는데 움직이지 않는다.** 체크 상자는 이 컴퓨터의 플레이어에게만 적용된다. 접속한 상태에서 켰는지, `AutoWalkToggle`이 씬의 오브젝트에 붙어 있는지 확인한다.
- **프리팹이나 스크립트를 고쳤는데 클라이언트에서는 예전처럼 동작한다.** 빌드한 프로그램은 빌드한 시점의 프리팹과 코드를 가지고 있다. 고친 뒤에는 빌드를 다시 한다. 서버 Console에 `NetworkConfig mismatch`가 나오면 양쪽의 프리팹 목록이 달라진 것이다(2.5.8절).

**이 절의 정리.**

- 이동 코드는 `if (!IsOwner) return;`으로 소유자의 컴퓨터에서만 실행한다.
- 위치 전달은 NetworkTransform이 하고, 소유자가 움직이려면 **Authority Mode = Owner**로 바꾼다.
- 한 PC에서 여러 창을 시험할 때는 자동 이동(가상 입력)으로 키보드 포커스 제약을 피한다.

## 2.8 NetworkVariable: 상태를 동기화한다

2.7절까지 만든 것은 "캡슐이 어디에 있는가"라는 상태를 NetworkTransform이 알아서 동기화해 주는 것이었다. 닉네임, 체력, 점수, 팀 같은 값은 직접 동기화할 값으로 선언해야 한다. 그때 쓰는 것이 **NetworkVariable**이다. 이 절에서는 플레이어의 닉네임과 색을 NetworkVariable로 만들고, 캡슐 위에 닉네임을 표시한다.

### 2.8.1 NetworkVariable이란

NetworkVariable은 "모든 접속자의 복사본에 같은 값을 유지해 주는 변수"이다. 일반 C# 변수처럼 선언하되 `NetworkVariable<T>`로 감싼다.

```csharp
NetworkVariable<int> hp = new NetworkVariable<int>(100);   // 초기값 100

hp.Value = 80;                    // 값을 쓴다 (쓸 권한이 있는 쪽에서만)
int now = hp.Value;               // 값을 읽는다
hp.OnValueChanged += (previous, current) => { /* 값이 바뀌었을 때 */ };
```

NetworkVariable에는 세 가지가 붙어 있다.

- **값(Value)**: 읽고 쓰는 부분이다. `hp = 80`이 아니라 `hp.Value = 80`으로 쓴다.
- **변경 알림(OnValueChanged)**: 값이 바뀌었을 때 호출되는 이벤트이다. 이전 값과 새 값을 받는다.
- **권한(Permission)**: 누가 읽고 누가 쓸 수 있는지. 기본은 "모두 읽을 수 있고, 서버만 쓸 수 있다"이다.

선언할 때 지켜야 할 것은 다음과 같다.

- NetworkVariable은 **NetworkBehaviour를 상속한 스크립트의 필드**로 선언한다. 그 스크립트는 NetworkObject가 붙은 오브젝트(또는 그 자식)에 붙어 있어야 한다. 일반 MonoBehaviour에서는 쓸 수 없다.
- 필드를 선언하는 자리에서 `new NetworkVariable<T>(...)`로 만든다.
- 값은 **오브젝트가 스폰된 뒤**(`OnNetworkSpawn` 이후)에 쓴다. 스폰되기 전에 쓰면 `NetworkVariable is written to, but doesn't know its NetworkBehaviour yet.` 경고가 날 수 있다.

NetworkVariable의 값은 두 경우에 다른 접속자에게 전달된다.

- 값이 **바뀌었을 때**: 바뀐 값만 틱마다 모아서 보낸다(2.4.7절). 같은 틱 안에서 여러 번 바꾸면 마지막 값만 전달된다.
- **오브젝트가 스폰될 때**: 중간에 접속한 클라이언트가 그 오브젝트를 만들 때 현재 값을 함께 받는다. 그래서 늦게 들어온 사람도 이미 정해진 닉네임을 본다. NetworkVariable이 RPC와 다른 가장 큰 점이다(2.9절).

### 2.8.2 실습 4: 닉네임과 색을 동기화하기

**목표.** 플레이어 캡슐마다 닉네임과 색이 있다. 색은 서버가 접속 순서대로 정하고, 닉네임은 각자 입력해서 바꾼다. 캡슐 위에 닉네임이 보이고, 늦게 접속한 클라이언트도 현재의 닉네임과 색을 본다.

**단계.**

1. `Assets/Scripts`에 `PlayerProfile.cs`를 만든다. 닉네임(소유자가 쓴다)과 색(서버가 쓴다)을 NetworkVariable으로 가진다.

```csharp
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// 플레이어의 닉네임과 색을 NetworkVariable으로 동기화하고, 캡슐 위에 닉네임을 그린다.
public class PlayerProfile : NetworkBehaviour
{
    // 닉네임: 모두 읽을 수 있고, 이 오브젝트의 소유자만 쓸 수 있다. (값, 읽기 권한, 쓰기 권한)
    NetworkVariable<FixedString64Bytes> nick = new NetworkVariable<FixedString64Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // 색: 권한을 적지 않으면 "모두 읽기, 서버만 쓰기"이다.
    NetworkVariable<Color> tint = new NetworkVariable<Color>(Color.white);

    public static PlayerProfile Local;      // 이 컴퓨터에서 내가 조종하는 플레이어

    string nickText = "";                   // 화면에 그릴 닉네임(NetworkVariable 값의 사본)

    string Where => IsHost ? "Host" : IsServer ? "Server" : "Client";

    public override void OnNetworkSpawn()
    {
        if (IsOwner) Local = this;

        // 색은 서버가 접속 순서(ClientId)에 따라 정한다.
        if (IsServer) tint.Value = Color.HSVToRGB(((float)OwnerClientId * 0.17f) % 1f, 0.65f, 0.95f);

        // 1) 지금 값을 한 번 반영한다. 늦게 접속했어도 이 시점에는 현재 값이 이미 와 있다.
        nickText = nick.Value.ToString();
        ApplyTint(tint.Value);

        // 2) 이후의 변화는 알림으로 받는다.
        nick.OnValueChanged += OnNickChanged;
        tint.OnValueChanged += OnTintChanged;
    }

    public override void OnNetworkDespawn()
    {
        nick.OnValueChanged -= OnNickChanged;
        tint.OnValueChanged -= OnTintChanged;
        if (Local == this) Local = null;
    }

    void OnNickChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        nickText = current.ToString();
        Debug.Log($"[{Where}] 닉네임 변경: 소유자 {OwnerClientId}, '{previous}' → '{current}'");
    }

    void OnTintChanged(Color previous, Color current) => ApplyTint(current);

    void ApplyTint(Color c) => GetComponent<Renderer>().material.color = c;

    // ---- 화면의 버튼에서 호출한다 ----

    // 소유자가 호출한다. 한글 16자는 48바이트이므로 FixedString64Bytes(61바이트)에 들어간다.
    public void SetNick(string text)
    {
        text = text.Trim();
        if (text.Length > 16) text = text.Substring(0, 16);
        nick.Value = new FixedString64Bytes(text);
    }

    // 일부러 권한이 없는 쪽에서 써 보는 시험용 함수이다. 서버(호스트)에서만 성공한다.
    public void TryChangeTint()
    {
        tint.Value = Random.ColorHSV(0f, 1f, 0.6f, 0.8f, 0.9f, 1f);
    }

    // ---- 캡슐 위에 닉네임 그리기 ----

    // 다른 스크립트도 쓸 수 있는 이름. 닉네임이 비어 있으면 "Player 번호"를 쓴다.
    public string PlayerName => nickText.Length > 0 ? nickText : $"Player {OwnerClientId}";

    string DisplayName => IsOwner ? PlayerName + " (나)" : PlayerName;

    void OnGUI()
    {
        if (!IsSpawned) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 p = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
        if (p.z <= 0f) return;                          // 카메라 뒤에 있으면 그리지 않는다

        UiKit.Apply();
        string text = DisplayName;
        Vector2 size = GUI.skin.label.CalcSize(new GUIContent(text));
        // 월드→화면 변환의 y는 아래가 0이고 IMGUI는 위가 0이다.
        var rect = new Rect(p.x - size.x / 2f, Screen.height - p.y - size.y, size.x, size.y);

        Color old = GUI.color;
        GUI.color = Color.black;                        // 글자 그림자
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = old;
        GUI.Label(rect, text);
    }
}
```

2. 같은 폴더에 `NickPanel.cs`를 만든다. 화면 왼쪽 아래의 닉네임 입력 패널이다. 내 플레이어가 스폰된 뒤에만 보인다.

```csharp
using UnityEngine;

// 화면 왼쪽 아래의 닉네임 입력 패널
public class NickPanel : MonoBehaviour
{
    string input = "";

    void OnGUI()
    {
        UiKit.Apply();
        PlayerProfile me = PlayerProfile.Local;
        if (me == null) return;

        GUILayout.BeginArea(new Rect(10, Screen.height - 150, 420, 140), GUI.skin.box);
        GUILayout.Label("닉네임 (최대 16자)");
        input = GUILayout.TextField(input, 16);
        if (GUILayout.Button("닉네임 바꾸기")) me.SetNick(input);
        if (GUILayout.Button("내 색 바꾸기 (서버만 가능)")) me.TryChangeTint();
        GUILayout.EndArea();
    }
}
```

3. `NickPanel.cs`를 씬의 `NetStarter` 오브젝트에 붙인다.
4. `NetPlayer` 프리팹을 더블클릭하여 연다. `Add Component`로 `PlayerProfile`을 붙이고 Prefab Mode를 닫는다. 프리팹에는 이제 Network Object, Network Transform, Player Info, Player Move, Player Profile이 붙어 있다.
5. 씬을 저장하고 에디터를 Host로, 빌드를 Client로 실행한다. 프리팹과 코드가 바뀌었으므로 빌드를 다시 한다.

**확인할 것.**

1. Host와 Client를 접속시킨다. 호스트 화면의 호스트 캡슐 위에는 `Player 0 (나)`, 클라이언트 캡슐 위에는 `Player 1`이 보인다. 클라이언트 화면에서는 반대로 `Player 0`과 `Player 1 (나)`이다. 두 캡슐의 색이 서로 다르고, 같은 캡슐은 두 화면에서 같은 색이다. 색은 서버가 정해서 모두에게 전달한 것이다.
2. 호스트 화면 왼쪽 아래의 입력칸에 `서버장`을 입력하고 **닉네임 바꾸기**를 누른다. 호스트와 클라이언트 양쪽 화면에서 `서버장`으로 바뀐다. 양쪽 Console에 `닉네임 변경: 소유자 0, '' → '서버장'` 로그가 나온다. 값을 쓴 컴퓨터에서도 `OnValueChanged`가 호출된다는 점을 확인한다.
3. 클라이언트 화면에서 `철수`를 입력하고 버튼을 누른다. 클라이언트가 자기 값을 썼는데도 호스트 화면의 클라이언트 캡슐이 `철수`로 바뀐다. 클라이언트가 서버로 보내고 서버가 다시 다른 접속자에게 전달한 것이다.
4. 입력칸에 `wasd`를 입력해 본다. 글자가 입력되는 동안 캡슐은 움직이지 않는다. 입력칸 밖을 클릭하면 다시 움직인다(2.5절 `UiKit.Typing`).
5. **늦게 접속한 클라이언트.** 세 번째 인스턴스를 클라이언트로 접속시킨다. 접속한 순간 화면에 `서버장`, `철수` 닉네임과 두 캡슐의 색이 이미 표시되어 있다. 닉네임을 바꾸는 이벤트가 없었는데도 보이는 이유는 오브젝트가 스폰될 때 NetworkVariable의 현재 값이 함께 전달되기 때문이다.
6. **권한 시험.** 클라이언트 화면에서 **내 색 바꾸기 (서버만 가능)** 를 누른다. 색은 바뀌지 않고, 그 클라이언트의 Console에 빨간 오류가 나온다.

```text
|Client-1|NetPlayer(Clone)|tint| Write permissions (Server) for this client instance is not allowed!
```

같은 버튼을 호스트 화면에서 누르면 호스트 캡슐의 색이 바뀌고, 모든 화면에 반영된다. `tint`는 쓰기 권한이 Server이므로 호스트(서버)만 쓸 수 있기 때문이다. 권한이 없는 쪽이 써도 프로그램이 멈추지는 않지만, 값은 바뀌지 않고 오류 로그만 남는다.

**그 뒤에서 일어난 일.**

닉네임을 바꾸는 클라이언트(소유자)를 기준으로 따라가 본다.

1. `nick.Value = ...`를 실행하면 그 클라이언트 안의 값이 즉시 바뀌고 `OnValueChanged`가 호출된다. 그와 동시에 이 NetworkVariable에 "바뀌었음" 표시가 붙는다.
2. 다음 틱에 클라이언트는 바뀐 변수만 담은 메시지를 서버로 보낸다.
3. 서버는 먼저 이 클라이언트에게 쓰기 권한이 있는지 확인한다. 권한이 있으면 값을 받아 자기 복사본을 바꾸고 `OnValueChanged`를 호출한 뒤, 같은 변경을 이 오브젝트를 볼 수 있는 다른 클라이언트에게 곧바로 다시 보낸다.
4. 다른 클라이언트들이 값을 받아 복사본을 바꾸고 `OnValueChanged`를 호출한다. `OnNickChanged`가 `nickText`를 바꾸므로 다음 프레임의 `OnGUI`가 새 닉네임을 그린다.

서버가 값을 쓰는 `tint`는 2~3단계 없이, 서버에서 값이 바뀐 뒤 다음 틱에 모든 클라이언트에게 전달된다. 소유자가 쓰는 값은 소유자 쪽 틱을 한 번 기다린 뒤 서버를 거쳐 가므로, 네트워크 전송 시간 외에 기본 설정에서 최대 약 33ms(30틱/초)의 간격이 더해진다.

3단계의 권한 확인은 서버가 하는 일이다. 쓰기 권한이 없는 클라이언트가 값을 보내더라도(예를 들어 변조된 클라이언트) 서버는 그 값을 받아들이지 않는다.

### 2.8.3 읽기 권한과 쓰기 권한

NetworkVariable의 생성자는 값과 두 가지 권한을 받는다.

```csharp
NetworkVariable<int> score = new NetworkVariable<int>(
    0,                                       // 초기값
    NetworkVariableReadPermission.Everyone,  // 읽기 권한
    NetworkVariableWritePermission.Server);  // 쓰기 권한
```

- **읽기 권한**: `Everyone`(기본값)은 모든 접속자가 값을 받는다. `Owner`는 소유자와 서버만 값을 받는다. 다른 클라이언트에게 보이면 안 되는 값(소유자만 보는 인벤토리 같은 값)에 쓴다.
- **쓰기 권한**: `Server`(기본값)는 서버만 쓸 수 있다. `Owner`는 **소유자만** 쓸 수 있다.

쓰기 권한에서 헷갈리기 쉬운 점이 있다. 쓰기 권한이 `Owner`이면 정말로 소유자만 쓸 수 있고, 서버도 남이 소유한 오브젝트의 이 값은 쓰지 못한다. 서버가 소유한 오브젝트(예: 호스트 자신의 플레이어, 서버가 스폰한 소유자 없는 오브젝트)는 서버가 소유자이므로 쓸 수 있다.

어떤 권한으로 둘지는 "누가 이 값의 주인인가"로 정한다.

- 서버가 정하는 값(점수, 체력, 색, 팀): 쓰기 권한 `Server`. 클라이언트가 바꾸고 싶다면 서버에게 요청하는 RPC를 보내고 서버가 검사한 뒤 바꾼다(2.9절).
- 소유 클라이언트가 직접 정하는 값(닉네임, 선택한 장비): 쓰기 권한 `Owner`. 구현이 쉽지만 서버가 검증하지 않으므로, 클라이언트가 어떤 값이든 보낼 수 있다는 점을 알고 쓴다(2.1.2절).

### 2.8.4 OnValueChanged와 초기값

`OnValueChanged`와 초기값 처리에서 흔히 실수하는 부분을 정리한다.

- **등록은 `OnNetworkSpawn()`에서, 해제는 `OnNetworkDespawn()`에서 한다.** `PlayerProfile`이 그렇게 한다. 해제하지 않으면 오브젝트가 사라진 뒤에도 핸들러가 남아 있다.
- **처음 접속할 때의 값은 알림으로 오지 않는다.** 스폰될 때 이미 최신 값이 들어와 있고 `OnNetworkSpawn()`이 호출된 뒤에는 `.Value`를 바로 읽을 수 있다. 그래서 `PlayerProfile`은 `OnNetworkSpawn()`에서 `nick.Value`와 `tint.Value`를 직접 한 번 반영하고(1단계), 이후의 변화만 알림으로 받는다(2단계). 알림만 믿으면 늦게 접속한 클라이언트가 현재 닉네임과 색을 놓친다.
- **값을 쓴 쪽에서도 `OnValueChanged`가 호출된다.** 값을 쓰는 순간 호출되므로, 값을 쓰는 코드와 알림을 받는 코드에 같은 처리를 두 번 쓰지 않도록 한다.
- **같은 틱 안의 여러 번 변경은 합쳐진다.** 값을 쓴 쪽에서는 쓸 때마다 알림이 호출되지만, 받는 쪽은 마지막 값에 대해 한 번 알림을 받는다. 중간 값이 전부 필요하다면 NetworkVariable이 아니라 RPC를 쓴다.
- **값이 빠르게 계속 바뀌는 데이터에 쓰지 않는다.** 위치처럼 매 프레임 바뀌는 값은 NetworkTransform에 맡긴다. NetworkVariable은 체력이나 점수처럼 가끔 바뀌는 상태에 어울린다.

### 2.8.5 NetworkVariable에 넣을 수 있는 타입

NetworkVariable에는 네트워크로 보낼 수 있는 타입만 넣을 수 있다.

- C#의 기본 값 타입: `bool`, `byte`, `int`, `uint`, `long`, `ulong`, `short`, `float`, `double` 등.
- Unity의 기본 구조체: `Vector2`, `Vector3`, `Vector4`, `Quaternion`, `Color`, `Color32`, `Ray` 등.
- `enum`.
- 고정 길이 문자열: `FixedString32Bytes`, `FixedString64Bytes`, `FixedString128Bytes`, `FixedString512Bytes`, `FixedString4096Bytes`. 사용하려면 `using Unity.Collections;`가 필요하다.
- `INetworkSerializable`을 구현한 구조체(2.11절에서 직접 만든다).

`string`은 넣을 수 없다. 길이가 일정하지 않고 가비지를 만들기 때문이다. 문자열은 `FixedString`을 쓴다. `FixedString64Bytes`는 UTF-8 61바이트까지 담는다. 영어는 한 글자가 1바이트이고 한글은 3바이트이므로 한글 약 20자까지이다. 실습의 닉네임을 16자로 제한한 이유이다. 용량이 더 필요하면 `FixedString128Bytes`로 바꾸면 된다. 용량이 커도 전송할 때는 실제로 쓰인 바이트만 보낸다.

자주 필요한 `FixedString64Bytes`와 `string`의 변환은 다음과 같다.

```csharp
FixedString64Bytes f = new FixedString64Bytes("철수");   // string → FixedString
string s = f.ToString();                                 // FixedString → string
```

값 하나가 아니라 "여러 개의 목록"(접속자 목록 같은 것)은 `List<T>`가 아니라 `NetworkList<T>`를 쓴다. 2.11절에서 접속자 목록을 만들 때 다룬다.

### 2.8.6 흔한 오류

- **`Write permissions (Server) for this client instance is not allowed!`가 난다.** 쓰기 권한이 없는 쪽에서 값을 썼다. 권한 선언과 값을 쓰는 코드의 위치(서버에서만 쓰는지)를 확인한다. 클라이언트가 서버에게 값을 바꿔 달라고 부탁하려면 RPC를 쓴다(2.9절).
- **`NetworkVariable is written to, but doesn't know its NetworkBehaviour yet.` 경고가 난다.** 오브젝트가 스폰되기 전에(`Awake`, `Start`, 또는 스폰 전의 어떤 시점) 값을 썼다. 값은 `OnNetworkSpawn()`이 호출된 뒤에 쓴다.
- **한쪽 컴퓨터에서는 바뀌는데 다른 컴퓨터에서는 그대로이다.** 값을 쓴 쪽이 쓰기 권한이 있는지, 받는 쪽의 읽기 권한이 `Everyone`인지 확인한다. 클라이언트 빌드가 오래된 것은 아닌지도 확인한다.
- **`OnValueChanged`가 호출되지 않는다.** 구독하는 코드가 변경보다 늦게 실행되었거나, 받는 쪽에서는 구독하지 않았다. 구독은 `OnNetworkSpawn()`에서 한다.
- **늦게 접속한 클라이언트에서만 기본값(빈 닉네임, 흰색)으로 보인다.** `OnNetworkSpawn()`에서 현재 값을 직접 반영하지 않고 변경 알림에만 의존했다.
- **구조체의 필드 하나만 고쳤더니 전달되지 않는다.** NetworkVariable에 담은 구조체는 `Value`를 꺼내어 고친 뒤 다시 `Value`에 대입해야 변화가 전달된다. 목록은 `List<T>`를 NetworkVariable에 담지 말고 `NetworkList<T>`를 쓴다(2.11절).
- **한글 닉네임이 잘리거나 오류가 난다.** `FixedString64Bytes`의 용량(61바이트)을 넘었다. 입력 글자 수를 제한하거나 더 큰 `FixedString`을 쓴다.
- **닉네임을 입력하는데 캡슐이 움직인다.** `PlayerMove`에 `UiKit.Typing` 검사가 없거나, `UiKit.cs`가 2.5절의 최신 코드가 아니다.

**이 절의 정리.**

- 모든 접속자가 같은 값을 가져야 하고, 늦게 접속한 사람도 알아야 하는 값은 NetworkVariable로 만든다.
- 권한은 (읽기, 쓰기)로 정한다. 기본은 모두 읽기, 서버만 쓰기이고, 쓰기를 소유자에게 줄 수도 있다. 권한 없는 쪽이 쓰면 오류 로그만 나고 값은 바뀌지 않는다.
- 값은 `OnNetworkSpawn()` 이후에 쓰고, 처음 값은 `OnNetworkSpawn()`에서 직접 읽어 반영한다. 이후의 변화는 `OnValueChanged`로 받는다.
- 문자열은 `FixedString`을 쓴다. 한글은 글자당 3바이트이다.

## 2.9 RPC: 사건을 전달한다

2.8절의 NetworkVariable은 "계속 유지되는 상태"를 전달했다. 이 절의 RPC는 "한 번 일어나고 지나가는 사건"과 "누군가에게 보내는 요청"을 전달한다. 채팅 한 줄, "보너스를 받겠다"는 요청, 총을 쏘았다는 알림이 여기에 해당한다. 이 절의 마지막에서 핑을 주고받아 왕복 시간을 재고, 외치기 기능으로 RPC의 대상과 권한을 직접 확인한다.

### 2.9.1 RPC란 무엇인가

RPC(Remote Procedure Call)는 **한쪽 컴퓨터에서 함수를 부르면 다른 컴퓨터에서 그 함수의 본문이 실행되는 호출**이다. 함수 호출처럼 쓰지만 실제로는 메시지가 네트워크로 전송된다. NGO에서는 NetworkBehaviour 안의 함수에 `[Rpc(...)]` 표시를 붙이면 RPC가 된다.

```csharp
[Rpc(SendTo.Server)]
void BonusRequestRpc(int amount)
{
    // 이 본문은 서버에서 실행된다
}

void OnButton()
{
    BonusRequestRpc(10);   // 클라이언트에서 부르면 메시지가 서버로 간다
}
```

네트워크 지식이 있는 학습자라면 gRPC나 직접 만든 패킷 처리 함수를 떠올리면 된다. 다만 NGO의 RPC는 몇 가지가 다르다.

- 패킷의 종류를 정의하거나 직렬화 코드를 작성하지 않는다. 함수의 매개변수가 그대로 직렬화되어 전송된다.
- **반환값이 없다.** 호출은 메시지를 보내고 바로 돌아오며 응답을 기다리지 않는다. 응답이 필요하면 상대가 응답 RPC를 따로 호출하도록 만든다(2.9.4절의 Ping과 Pong).
- 기본은 신뢰성 있는 전송이고, 같은 오브젝트에서 호출한 순서대로 실행된다.

![그림 2-9 NetworkVariable과 RPC: 늦게 접속한 클라이언트](images/ch02_fig09_nv_vs_rpc.png)

그림 2-9는 2.4.6절에서 말한 차이를 시간 순서로 보여 준다. 서버가 값을 바꾸고 사건을 알린 뒤에 클라이언트 B가 접속한다고 하자. NetworkVariable은 오브젝트가 스폰될 때 현재 값을 B에게 전달하므로 B도 현재 상태를 안다. RPC는 호출한 순간 접속해 있던 쪽에만 전달되므로 B는 지나간 사건을 받지 못한다. 어느 쪽을 쓸지는 이 질문 하나로 정한다. "나중에 접속한 사람에게도 보여야 하는가?"

### 2.9.2 RPC를 선언하고 호출하는 규칙

RPC 선언에는 지켜야 할 규칙이 있다. 규칙을 어기면 스크립트를 저장할 때 Console에 오류가 나온다.

- NetworkBehaviour를 상속한 클래스 안의 **인스턴스 함수**여야 한다. static, abstract, 제네릭, async 함수와 지역 함수에는 쓸 수 없다.
- 반환 타입은 `void`이다.
- **함수 이름이 `Rpc`로 끝나야 한다.** `PingRpc`, `ShoutToServerRpc`처럼 쓴다. 이름만 보고 "이것은 원격 호출이다"를 알아보게 하려는 규칙이며, 지키지 않으면 `Rpc method must end with 'Rpc' suffix!` 오류가 난다.
- `[Rpc(SendTo.X)]`처럼 **기본 대상을 반드시 적는다**. 대상은 2.9.3절에서 설명한다.
- 매개변수는 NetworkVariable에 넣을 수 있는 타입(2.8.5절)과 같다. RPC에서는 `string`과 배열도 쓸 수 있다. 큰 데이터를 RPC로 보내지 않는다.
- 마지막 매개변수로 `RpcParams`를 받을 수 있다. 받은 쪽에서는 보낸 사람의 정보를, 보내는 쪽에서는 대상 지정을 담는 용도이다.

NGO는 스크립트가 컴파일된 직후에 RPC 함수를 자동으로 고쳐 쓴다. 호출하는 자리에서는 "메시지를 만들어 보내는 코드"가 실행되고, 우리가 작성한 본문은 받는 쪽에서 실행되도록 옮겨진다. 이름 규칙은 이 자동 작업이 RPC를 찾는 단서이기도 하다.

호출할 때 기억할 것은 세 가지이다.

- **스폰된 오브젝트에서만 호출한다.** 스폰되기 전이나 디스폰된 뒤에 호출하면 `RpcException: The NetworkBehaviour must be spawned before calling this method.` 예외가 난다. `OnNetworkSpawn()` 이후에 호출한다.
- 호출한 코드는 **본문이 끝나기를 기다리지 않는다.** `BonusRequestRpc(10);` 다음 줄은 서버가 본문을 실행하기도 전에 실행된다. 결과가 필요하면 서버가 NetworkVariable을 바꾸거나 응답 RPC를 부르게 한다.
- **호출한 컴퓨터에서 본문이 실행되는지는 대상에 달려 있다.** `SendTo.Server` RPC를 클라이언트가 부르면 본문은 서버에서만 실행된다. 같은 RPC를 호스트(서버)가 부르면 메시지를 보내지 않고 그 자리에서 바로 실행된다.

### 2.9.3 보낼 대상: SendTo

`[Rpc(SendTo.X)]`의 X는 "이 RPC의 본문이 어디에서 실행되는가"를 정한다.

![그림 2-10 RPC 대상(SendTo)별 실행 위치](images/ch02_fig10_rpc_targets.png)

그림 2-10에 모든 값의 실행 위치를 정리했다. 자주 쓰는 것부터 설명한다.

- **`SendTo.Server`**: 서버에서 실행된다. 클라이언트가 서버에게 요청할 때 쓴다. 소유권과 상관없이 서버로 간다. 서버(호스트)가 부르면 그 자리에서 실행된다.
- **`SendTo.ClientsAndHost`**: 모든 클라이언트에서 실행되고, 호스트로 실행 중이면 호스트에서도 실행된다. 전용 서버에서는 서버를 제외한 클라이언트에서만 실행된다. 서버가 모든 접속자에게 알릴 때 쓴다.
- **`SendTo.Everyone`**: 서버를 포함한 모든 접속자에서 실행되고, 호출한 쪽에서도 실행된다.
- **`SendTo.Owner`**: 이 오브젝트의 소유자에서 실행된다.
- **`SendTo.NotServer`**: 서버를 제외한 모든 클라이언트에서 실행된다. 호스트는 서버로 취급하므로 **호스트에서는 실행되지 않는다.** 호스트에서도 실행하려면 `ClientsAndHost`를 쓴다.
- **`SendTo.NotOwner`**, **`SendTo.NotMe`**: 소유자를 뺀 모두, 호출한 쪽을 뺀 모두.
- **`SendTo.Me`**: 호출한 컴퓨터에서만 실행된다. 보통 함수 호출과 같다.
- **`SendTo.SpecifiedInParams`**: 대상을 선언에서 정하지 않고, **호출할 때 정한다.** 특정 클라이언트 한 명이나 몇 명에게만 보낼 때 쓴다.

`Everyone`, `NotServer`처럼 여러 접속자를 대상으로 하는 값은 그 오브젝트를 볼 수 있는 접속자(관찰자)에게만 보낸다. 기본은 모든 접속자가 관찰자이다.

**클라이언트끼리의 RPC도 가능하다.** 클라이언트 사이에는 직접 연결이 없으므로 서버가 중계한다. 그렇다고 클라이언트가 임의의 상대에게 마음대로 보내게 두는 것은 위험하므로, 이 교재는 "클라이언트 → 서버, 서버 → 클라이언트" 두 방향으로만 설계한다.

**특정 접속자에게만 보내기.** 대상을 호출할 때 정하려면 `SendTo.SpecifiedInParams`로 선언하고 마지막 매개변수로 `RpcParams`를 받는다. 호출할 때 `RpcTarget`으로 대상을 만들어 마지막 인자로 넘긴다.

```csharp
[Rpc(SendTo.SpecifiedInParams)]
void WhisperRpc(string text, RpcParams rpcParams = default) { /* 받은 쪽에서 실행 */ }

// 한 명에게
WhisperRpc("안녕", RpcTarget.Single(clientId, RpcTargetUse.Temp));
// 여러 명에게
WhisperRpc("안녕", RpcTarget.Group(new ulong[] { 1, 3 }, RpcTargetUse.Temp));
// 한 명만 빼고 모두에게
WhisperRpc("안녕", RpcTarget.Not(clientId, RpcTargetUse.Temp));
```

`RpcTarget`은 NetworkBehaviour 안에서 바로 쓸 수 있는 도우미이다. 마지막 매개변수 `RpcTargetUse.Temp`는 "이 대상을 한 번만 쓰고 버린다"는 뜻이다. 대상을 변수에 보관해 두었다가 나중에 다시 쓰면 안 된다(내부에서 재사용하는 객체라서 값이 바뀐다). 바로 호출에 넘기는 이 형태만 쓰면 된다.

**받은 쪽에서 보낸 사람 알기.** 서버에서 실행되는 RPC의 마지막 매개변수 `RpcParams`에는 보낸 클라이언트의 ID가 들어 있다.

```csharp
[Rpc(SendTo.Server)]
void BonusRequestRpc(int amount, RpcParams rpcParams = default)
{
    ulong sender = rpcParams.Receive.SenderClientId;   // 이 RPC를 보낸 클라이언트의 ClientId
}
```

이 값은 서버가 연결 정보로 채우므로 클라이언트가 조작할 수 없다. 보낸 사람이 누구인지는 "내 ID는 5입니다" 하고 매개변수로 받지 말고, 반드시 이 값을 쓴다.

### 2.9.4 실습 5: 핑과 외치기

**목표.** RPC 두 가지를 직접 만든다.

- **핑(Ping)**: 클라이언트가 서버에 핑을 보내고, 서버가 보낸 클라이언트에게만 퐁(Pong)을 돌려준다. 왕복 시간을 화면에 표시한다. "클라이언트 → 서버 → 그 클라이언트" 흐름이다.
- **외치기(Shout)**: 클라이언트가 한마디를 서버에 보내면 서버가 모든 접속자에게 알린다. 받은 말이 화면의 목록에 쌓인다. "클라이언트 → 서버 → 모두" 흐름이다. 채팅의 가장 단순한 형태이다.

**단계.**

1. `Assets/Scripts`에 `PlayerRpc.cs`를 만든다. 닉네임은 2.8절의 `PlayerProfile.PlayerName`을 쓴다.

```csharp
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// RPC 연습: 핑(클라이언트 → 서버 → 그 클라이언트)과 외치기(클라이언트 → 서버 → 모두)
public class PlayerRpc : NetworkBehaviour
{
    public static PlayerRpc Local;                                       // 이 컴퓨터에서 내가 조종하는 플레이어
    public static readonly List<string> ShoutLog = new List<string>();   // 이 컴퓨터가 받은 외침 목록

    public float LastRttMs { get; private set; } = -1f;                  // 마지막으로 잰 왕복 시간(ms). 아직 없으면 -1

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        Local = this;
        ShoutLog.Clear();
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this) Local = null;
    }

    // ---------- 1) 핑: 클라이언트 → 서버 → 그 클라이언트 ----------

    public void SendPing() => PingRpc(Time.realtimeSinceStartup);

    // 호출은 보내는 쪽에서 하고, 본문은 서버에서 실행된다. 이 오브젝트의 소유자만 호출할 수 있다.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void PingRpc(float sentAt, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;        // 보낸 클라이언트의 ClientId
        Debug.Log($"[서버] Ping 받음: 보낸 클라이언트 {sender}");
        PongRpc(sentAt, RpcTarget.Single(sender, RpcTargetUse.Temp));   // 보낸 클라이언트에게만 되돌린다
    }

    // 대상은 호출할 때 정한다(SpecifiedInParams). 본문은 그 대상의 컴퓨터에서 실행된다.
    [Rpc(SendTo.SpecifiedInParams)]
    void PongRpc(float sentAt, RpcParams rpcParams = default)
    {
        LastRttMs = (Time.realtimeSinceStartup - sentAt) * 1000f;
    }

    // ---------- 2) 외치기: 클라이언트 → 서버 → 모두 ----------

    public void Shout(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        ShoutToServerRpc(text);
    }

    // 클라이언트 → 서버. 이 오브젝트의 소유자만 호출할 수 있으므로, 이 오브젝트의 주인이 곧 보낸 사람이다.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void ShoutToServerRpc(string text)
    {
        if (text.Length > 60) text = text.Substring(0, 60);        // 서버가 한 번 더 검사한다
        ShoutToAllRpc(GetComponent<PlayerProfile>().PlayerName, text);
    }

    // 서버 → 모든 클라이언트(호스트 포함). 클라이언트가 직접 부르지 못하도록 서버 전용으로 막는다.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    void ShoutToAllRpc(string who, string text)
    {
        ShoutLog.Add($"{who}: {text}");
        if (ShoutLog.Count > 8) ShoutLog.RemoveAt(0);
    }

    // 시험용: 클라이언트가 서버 전용 RPC를 직접 부르면 어떻게 되는지 본다.
    public void FakeShout()
    {
        ShoutToAllRpc("서버", "클라이언트가 서버인 척 보낸 메시지");
    }
}
```

2. 같은 폴더에 `RpcPanel.cs`를 만든다. 화면 오른쪽 위(자동 이동 체크 상자 아래)에 그려지는 패널이다.

```csharp
using UnityEngine;

// 화면 오른쪽의 RPC 시험 패널: Ping 버튼, 외치기 입력칸, 받은 외침 목록
public class RpcPanel : MonoBehaviour
{
    string input = "";
    bool autoPing;
    float nextPing;
    System.Action pending;                // 눌린 버튼이 시킨 일. OnGUI는 기록만 하고 Update에서 실행한다(2.5.4절)

    void Update()
    {
        PlayerRpc me = PlayerRpc.Local;
        if (me == null)
        {
            pending = null;
            return;
        }

        if (pending != null)
        {
            System.Action job = pending;
            pending = null;
            job();
        }

        if (autoPing && Time.unscaledTime >= nextPing)
        {
            nextPing = Time.unscaledTime + 1f;
            me.SendPing();
        }
    }

    void OnGUI()
    {
        UiKit.Apply();
        PlayerRpc me = PlayerRpc.Local;
        if (me == null) return;

        GUILayout.BeginArea(new Rect(Screen.width - 350, 50, 340, 360), GUI.skin.box);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping", GUILayout.Width(80))) pending = me.SendPing;
        autoPing = GUILayout.Toggle(autoPing, "1초마다");
        GUILayout.EndHorizontal();
        GUILayout.Label(me.LastRttMs < 0f ? "왕복 시간: -" : $"왕복 시간: {me.LastRttMs:F1} ms");

        GUILayout.Space(8);
        input = GUILayout.TextField(input, 60);
        if (GUILayout.Button("외치기")) { string text = input; input = ""; pending = () => me.Shout(text); }
        if (GUILayout.Button("서버인 척 외치기 (시험)")) pending = me.FakeShout;

        GUILayout.Space(8);
        foreach (string line in PlayerRpc.ShoutLog) GUILayout.Label(line);

        GUILayout.EndArea();
    }
}
```

3. `RpcPanel.cs`를 씬의 `NetStarter` 오브젝트에 붙인다.
4. `NetPlayer` 프리팹을 열어 `PlayerRpc`를 붙이고 닫는다. 프리팹에는 이제 Network Object, Network Transform, Player Info, Player Move, Player Profile, Player Rpc가 붙어 있다.
5. 씬을 저장하고 프리팹과 코드가 바뀌었으므로 빌드를 다시 한다. 에디터를 Host로 시작하고, 빌드한 프로그램을 두 번 실행하여 Client를 둘 만든다(2.5.5절).

**확인할 것: 핑.**

1. 클라이언트 화면에서 **Ping**을 누른다. `왕복 시간: 12.3 ms`처럼 숫자가 나온다. 같은 PC인데도 0이 아닌 이유는 아래 "그 뒤에서 일어난 일"에서 설명한다. 숫자는 환경마다 다르다.
2. 호스트 화면에서 **Ping**을 누른다. `0.0 ms`가 나온다. 호스트는 서버이므로 `SendTo.Server` RPC가 메시지 전송 없이 그 자리에서 실행되었기 때문이다.
3. 서버(호스트)의 Console에 `[서버] Ping 받음: 보낸 클라이언트 1` 로그가 나온다. 호스트가 누른 경우에는 `보낸 클라이언트 0`이다. 서버가 `rpcParams.Receive.SenderClientId`로 보낸 사람을 알아낸 것이다.
4. 클라이언트가 둘이면 한쪽에서만 **Ping**을 누른다. 누른 클라이언트의 왕복 시간만 갱신되고, 다른 클라이언트의 화면은 그대로이다. 서버가 퐁을 `RpcTarget.Single`로 보낸 클라이언트에게만 돌려주기 때문이다.
5. **1초마다** 체크를 켜면 매초 핑을 보내므로 숫자가 계속 갱신된다. 2.13절에서 네트워크 지연을 일부러 주입한 뒤 이 숫자가 어떻게 변하는지 본다.

**확인할 것: 외치기.**

6. 한 클라이언트에서 `안녕하세요`를 입력하고 **외치기**를 누른다. 호스트와 모든 클라이언트의 목록에 `철수: 안녕하세요`(닉네임은 2.8절에서 정한 것)가 나타난다. 보낸 클라이언트의 화면에도 나타난다. `ClientsAndHost`는 보낸 클라이언트도 대상에 포함하기 때문이다.
7. 호스트에서도 외쳐 본다. 호스트 자신의 목록에도 나타난다. `NotServer`였다면 호스트 화면에는 나타나지 않았을 것이다(2.9.3절).
8. **늦게 접속한 클라이언트.** 몇 마디를 외친 뒤에 클라이언트를 하나 더 접속시킨다. 새 클라이언트의 목록은 **비어 있다.** 지나간 외침은 다시 오지 않는다. 그러나 화면에는 다른 접속자의 닉네임과 색이 이미 표시되어 있다(2.8절). 같은 접속 직후 상황에서 NetworkVariable은 전달되고 RPC는 전달되지 않는 차이를 눈으로 확인한다.
9. **서버인 척 외치기 (시험)** 를 **클라이언트**에서 누른다. 메시지는 어디에도 나타나지 않고, 그 클라이언트의 Console에 빨간 예외가 나온다.

```text
RpcException: This RPC can only be sent by the server.
```

   `ShoutToAllRpc`를 `InvokePermission = RpcInvokePermission.Server`로 선언했기 때문에, 서버가 아닌 쪽에서 부르면 보내기 전에 막힌다. 같은 버튼을 **호스트**에서 누르면 `서버: 클라이언트가 서버인 척 보낸 메시지`가 모든 목록에 나타난다. 호스트는 서버이기 때문이다.

**그 뒤에서 일어난 일.**

핑의 흐름을 프레임 단위로 따라가 본다. 한 번의 왕복에는 세 번의 "프레임 경계"가 끼어 있다.

1. 클라이언트에서 `SendPing()`이 실행되고 `PingRpc`가 호출된다. 메시지는 바로 전송되지 않고 보낼 메시지 대기열에 쌓인다.
2. 그 **프레임의 끝**(PostLateUpdate 단계)에 대기열이 비워지며 메시지가 서버로 전송된다.
3. 서버는 **다음 프레임의 맨 앞**(EarlyUpdate 단계)에서 받은 메시지를 처리한다. 먼저 호출한 사람에게 이 RPC를 부를 권한이 있는지 확인하고(`Owner` 권한), 맞으면 서버의 `PingRpc` 본문을 실행한다. 본문이 `PongRpc`를 호출하면 퐁 메시지가 대기열에 쌓인다.
4. 서버 프레임이 끝날 때 퐁 메시지가 클라이언트로 전송된다.
5. 클라이언트는 다음 프레임의 맨 앞에서 퐁을 받아 `PongRpc` 본문을 실행하고, 현재 시각과 처음 보낸 시각의 차이를 왕복 시간으로 저장한다.

그래서 같은 PC에서도 왕복 시간은 서로 다른 프로그램의 프레임 간격이 몇 번 더해진 값이 된다. 프레임이 60fps이면 한 프레임이 약 16.7ms이므로 수십 ms가 나오는 것이 정상이다. 네트워크 경로가 길어지면 여기에 실제 전송 지연이 더해진다. 에디터 창이 가려지거나 포커스를 잃어 프레임이 느려지면 숫자가 커질 수 있다.

외치기는 같은 구조이다. 클라이언트가 `ShoutToServerRpc`를 부르면 서버에서 본문이 실행되고, 서버가 `ShoutToAllRpc`를 부르면 서버가 모든 클라이언트(호스트면 자기 자신 포함)로 메시지를 보낸다. 받은 컴퓨터마다 그 플레이어 오브젝트의 `ShoutToAllRpc` 본문이 실행되어 이 컴퓨터의 `ShoutLog`에 한 줄을 추가한다. 목록은 컴퓨터마다 따로 있는 평범한 C# 리스트이다. 서버가 모든 접속자의 목록을 대신 가지고 있지 않으므로, 늦게 접속한 사람은 지난 외침을 가져올 방법이 없다.

### 2.9.5 RPC 설계 지침

RPC를 쓸 때 지킬 원칙을 정리한다. 채팅(2.11절)과 6장의 서버 프로그램까지 이어지는 원칙이다.

- **요청은 RPC로, 결과는 NetworkVariable로.** 클라이언트는 서버에게 요청 RPC를 보내고, 서버가 검사한 뒤 NetworkVariable을 바꾸면 모든 접속자에게 결과가 전달된다. 늦게 접속한 사람도 결과를 본다.

```csharp
NetworkVariable<bool> isOpen = new NetworkVariable<bool>(false);   // 상태: 서버만 쓴다

public void RequestToggle() => ToggleRpc();                         // 클라이언트가 부르는 함수

[Rpc(SendTo.Server)]
void ToggleRpc(RpcParams rpcParams = default)
{
    ulong sender = rpcParams.Receive.SenderClientId;                // 보낸 사람은 매개변수가 아니라 이 값으로 확인한다
    if (sender != OwnerClientId) return;                            // 서버가 검사한다: 이 오브젝트의 소유자만 바꿀 수 있다
    isOpen.Value = !isOpen.Value;                                   // 바뀐 상태는 NetworkVariable이 전달한다
}
```

- **서버는 클라이언트가 보낸 값을 믿지 않는다.** 값의 범위, 호출 횟수, 대상이 존재하는지를 서버에서 검사한다. 보낸 사람은 매개변수가 아니라 `rpcParams.Receive.SenderClientId`로 확인한다. 이 교재의 엔진 파트는 서버가 게임 규칙을 판정하지 않으므로 검사를 최소한으로만 한다(2.1.2절). 6장의 서버에서는 모든 요청을 서버가 검증한다.
- **호출 권한 `InvokePermission`을 쓴다.** 누가 이 RPC를 부를 수 있는지를 선언에 적는다.
  - `Everyone`(기본값): 접속한 누구나 부를 수 있다.
  - `Owner`: 이 오브젝트의 소유자만 부를 수 있다. 플레이어 오브젝트의 RPC에 쓴다.
  - `Server`: 서버만 부를 수 있다. 서버가 모든 접속자에게 알리는 RPC에 쓴다.
  
  권한은 보내는 쪽에서 한 번(위 실습의 `This RPC can only be sent by the server.`), 받는 쪽(서버)에서 한 번 더 확인한다. 받는 쪽 확인에서 걸리면 서버 Console에 `Rpc message received from a client without permission to perform this operation!. Dropping RPC message` 오류가 나고 메시지는 버려진다. 변조된 클라이언트가 서버 전용 RPC를 직접 보내려 해도 서버가 막는다(오류 문구는 버전에 따라 조금 다를 수 있다).
- **클라이언트가 소유하지 않은 오브젝트의 RPC는 `Everyone`이어야 한다.** 씬에 미리 둔 오브젝트(예: 2.11절의 채팅 허브)는 서버가 소유하므로 `Owner` 권한을 쓰면 클라이언트가 부를 수 없다. 이 경우 `Everyone`으로 두고 `SenderClientId`로 보낸 사람을 확인한다.
- **자주 부르지 않는다.** RPC는 이동처럼 매 프레임 변하는 값에는 어울리지 않는다. 그런 값은 NetworkTransform이나 NetworkVariable에 맡긴다.
- **순서와 신뢰성.** 기본값 `RpcDelivery.Reliable`은 유실 없이 호출한 순서대로 실행된다. 일부 유실되어도 괜찮은 빈번한 알림(위치 표시 효과 같은 것)은 `Delivery = RpcDelivery.Unreliable`로 선언할 수 있지만 순서와 도착을 보장하지 않는다. 이 교재는 기본값만 쓴다.
- **호스트의 재귀 호출.** 호스트에서는 RPC가 그 자리에서 실행되므로, RPC 안에서 서로를 부르는 구조를 만들면 호출이 끝없이 쌓일 수 있다. 이런 구조가 필요하면 `[Rpc(..., DeferLocal = true)]`로 로컬 실행을 다음 프레임으로 미룬다. 이 교재의 실습에서는 필요하지 않다.

### 2.9.6 NGO 1.x 자료를 읽을 때

인터넷의 NGO 강좌와 블로그 글은 대부분 NGO 1.x 기준이라 `[ServerRpc]`와 `[ClientRpc]`를 쓴다. 2.x에서도 이 표기는 동작하지만 공식 문서는 `[Rpc]`를 쓰도록 권한다. 1.x 자료를 보면서 따라 할 때는 다음 대응으로 읽는다.

- `[ServerRpc]` 함수(이름이 `…ServerRpc`로 끝남) → `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]`. 1.x의 `[ServerRpc]`는 기본적으로 **소유자만** 부를 수 있었다. 새 방식의 기본 `InvokePermission`은 `Everyone`이므로 소유자 제한이 필요하면 직접 적어야 한다.
- `[ServerRpc(RequireOwnership = false)]` → `[Rpc(SendTo.Server)]`.
- `[ClientRpc]` 함수 → 모든 클라이언트에서 실행하려면 `[Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]`. 서버가 호스트인지에 따른 실행 위치가 1.x와 새 방식에서 조금 다를 수 있으므로, 대상을 `SendTo`로 명시해서 확인한다.
- `ServerRpcParams`, `ClientRpcParams` → `RpcParams`. 보낸 사람은 `rpcParams.Receive.SenderClientId`로 같다. `ClientRpcParams.Send.TargetClientIds`로 대상을 지정하던 것은 `SendTo.SpecifiedInParams`와 `RpcTarget.Single/Group/Not`으로 바꾼다.
- RPC 반환값은 예전 버전에는 있었으나 지금은 지원되지 않는다. 요청 RPC와 응답 RPC 한 쌍으로 만든다(핑과 퐁).
- `ClientNetworkTransform` 클래스를 직접 만들어 소유자 권한 이동을 구현하던 자료 → NetworkTransform의 **Authority Mode를 Owner**로 바꾸면 된다(2.7절).
- `NetworkManager.Singleton.StartHost()`, `NetworkVariable<T>`, `NetworkObject.Spawn()`, `OnNetworkSpawn()`은 그대로이다.
- `OnClientConnectedCallback`과 `OnClientDisconnectCallback`도 동작한다. 2.x에서는 접속과 종료를 하나로 묶은 `OnConnectionEvent`가 새로 추가되었고, 공식 문서는 앞의 콜백이 나중에 폐기될 수 있다고 안내한다(2.10절).

### 2.9.7 흔한 오류

- **`Rpc method must end with 'Rpc' suffix!`** 함수 이름이 `Rpc`로 끝나지 않는다. 이름을 고친다.
- **`RPC method must return …`, `RPC method must not be static!`, `RPC method must not be generic!`** RPC로 선언할 수 없는 함수이다. 반환값이 있거나 static이거나 제네릭인 함수에는 `[Rpc]`를 붙이지 않는다.
- **`SendTo.SpecifiedInParams cannot be used without a final parameter of type …RpcParams.`** `SpecifiedInParams`로 선언했으면서 마지막 매개변수를 `RpcParams`로 받지 않았다. `RpcParams rpcParams = default`를 추가한다.
- **`RpcException: The NetworkBehaviour must be spawned before calling this method.`** 스폰되기 전이나 디스폰된 뒤에 RPC를 불렀다. `OnNetworkSpawn()` 이후에 부른다. 접속이 끊긴 뒤에는 부르지 않는다.
- **`RpcException: This RPC can only be sent by the server.` / `… by its owner.`** 호출 권한에 맞지 않는 쪽에서 불렀다. 호출하는 코드의 위치(서버에서만 부르는가, 소유자만 부르는가)를 확인한다.
- **`RpcException: This method requires a runtime-specified send target.`** `SendTo.SpecifiedInParams` RPC를 대상 없이 불렀다. 마지막 인자로 `RpcTarget.Single(...)` 같은 대상을 넘긴다.
- **`RpcException: Target override is not allowed for this method.`** `SpecifiedInParams`가 아닌 RPC에 대상을 넘겼다. 대상을 바꾸고 싶다면 선언에 `AllowTargetOverride = true`를 추가하거나 `SpecifiedInParams`로 선언한다.
- **호스트 화면에만 메시지가 안 나온다.** `SendTo.NotServer`로 선언했다. 호스트는 서버로 취급되므로 실행되지 않는다. `SendTo.ClientsAndHost`나 `Everyone`으로 바꾼다.
- **RPC를 불렀는데 아무 일도 일어나지 않는다.** 대상이 자기 자신인지(Server RPC를 클라이언트가 불렀다면 본문은 서버에서만 실행된다), 서버 Console에 `without permission` 오류가 있는지, 서버가 그 오브젝트를 볼 수 있는지 확인한다.
- **`RpcTarget.Single(...)`을 변수에 넣어 두었다가 나중에 썼더니 엉뚱한 대상에게 갔다.** `RpcTargetUse.Temp` 대상은 호출에 바로 넘겨 쓰는 용도이다. 보관하려면 `RpcTargetUse.Persistent`로 만들고 쓴 뒤 `Dispose()`해야 하지만, 이 교재에서는 쓰지 않는다.

**이 절의 정리.**

- RPC = 한쪽에서 부르면 다른 쪽에서 본문이 실행되는 호출. 한 번 일어나고 지나가는 사건과 요청에 쓰고, 늦게 접속한 사람에게는 전달되지 않는다.
- `[Rpc(SendTo.X)]`, 이름은 `Rpc`로 끝, 반환값 없음. 스폰된 오브젝트에서만 부른다.
- 대상은 `SendTo`로 정한다. 클라이언트 → 서버는 `Server`, 서버 → 모두는 `ClientsAndHost`, 특정 접속자는 `SpecifiedInParams`와 `RpcTarget.Single`.
- 보낸 사람은 `rpcParams.Receive.SenderClientId`로 확인하고, 호출 권한은 `InvokePermission`으로 제한한다.
- 요청은 RPC, 결과는 NetworkVariable이라는 조합이 기본 설계이다.

## 2.10 접속 관리와 연결 승인

지금까지는 누구든 주소만 알면 접속할 수 있었다. 실제 서버는 접속하는 사람을 확인하고, 정원을 제한하고, 거절할 때는 이유를 알려 주어야 한다. 이 절에서는 접속과 종료 이벤트를 받는 방법, 접속 요청에 정보를 실어 보내는 방법, 서버가 접속을 승인하거나 거절하는 방법, 접속자를 내보내는 방법을 익힌다. 6장의 C++ 서버가 접속할 때 하는 일(HELLO 메시지로 이름과 접속 코드를 보내고 서버가 확인하는 일)을 NGO에서 먼저 해 보는 단계이다.

### 2.10.1 접속 관리가 하는 일

접속 관리는 크게 다섯 가지 일이다.

- **인증**: 접속 코드가 맞는 사람만 들어오게 한다.
- **정원 제한**: 정해진 인원이 차면 더 받지 않는다.
- **이름 중복 방지**: 같은 이름이 둘이 되지 않게 한다.
- **거절 사유 알림**: 거절당한 사용자가 왜 거절당했는지 알 수 있게 한다.
- **입장·퇴장 통지와 강제 퇴장**: 누가 들어오고 나갔는지 알리고, 필요하면 서버가 내보낸다.

NGO는 이 일을 위한 도구를 세 가지 준다. 접속·종료 **이벤트**, 접속 요청에 붙이는 **ConnectionData**, 서버가 승인 여부를 판단하는 **ConnectionApprovalCallback**이다.

### 2.10.2 접속과 종료 이벤트

`NetworkManager`는 접속과 종료 때 이벤트를 발생시킨다. 2.5절에서 두 가지(`OnClientConnectedCallback`, `OnClientDisconnectCallback`)를 썼다. 어디에서 호출되는지를 정확히 알아 두어야 한다.

- **`OnClientConnectedCallback(ulong clientId)`**: 서버(호스트)에서는 클라이언트가 접속을 마칠 때마다 그 클라이언트의 ID로 호출된다. 클라이언트에서는 **자기 자신**의 접속이 끝났을 때 한 번 호출된다. 다른 클라이언트의 접속은 클라이언트에게 알려 주지 않는다. 호스트는 시작이 끝날 때 자기 ID(0)로 호출된다.
- **`OnClientDisconnectCallback(ulong clientId)`**: 서버에서는 클라이언트가 나갈 때마다, 클라이언트에서는 자기 연결이 끊어졌을 때(접속하지 못했을 때도) 호출된다.
- **`OnConnectionEvent(NetworkManager, ConnectionEventData)`**: 위 두 가지를 하나로 묶고 "다른 클라이언트의 입장과 퇴장"까지 클라이언트에 알려 주는 새 이벤트이다. 공식 문서는 앞의 두 콜백이 나중에 폐기될 수 있다고 안내하며 이쪽을 권한다. `ConnectionEventData`의 `EventType`은 네 가지이다.
  - `ClientConnected`: 서버에서는 어떤 클라이언트가 접속했다는 뜻이고, 접속한 클라이언트에서는 자기 접속이 끝났다는 뜻이다. `ClientId`는 각각 그 클라이언트의 ID, 자기 자신의 ID이다.
  - `PeerConnected`: **이미 접속해 있는 클라이언트**가 받는다. 새로 접속한 다른 클라이언트의 ID가 `ClientId`에 들어 있다.
  - `ClientDisconnected`, `PeerDisconnected`: 위와 같은 구조로, 나간 쪽 자신과 서버, 남은 클라이언트에게 알린다.

```csharp
void OnEnable()  { NetworkManager.Singleton.OnConnectionEvent += OnConnectionEvent; }
void OnDisable() { if (NetworkManager.Singleton != null) NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent; }

void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
{
    switch (data.EventType)
    {
        case ConnectionEvent.ClientConnected:    Debug.Log($"접속 완료: {data.ClientId}"); break;
        case ConnectionEvent.PeerConnected:      Debug.Log($"다른 클라이언트가 접속했다: {data.ClientId}"); break;
        case ConnectionEvent.ClientDisconnected: Debug.Log($"연결 종료: {data.ClientId}"); break;
        case ConnectionEvent.PeerDisconnected:   Debug.Log($"다른 클라이언트가 나갔다: {data.ClientId}"); break;
    }
}
```

이 교재는 접속자 목록을 NetworkVariable(NetworkList)로 모든 접속자에게 동기화하므로(2.11절), 클라이언트가 다른 접속자의 입장을 이벤트로 받아야 할 일이 거의 없다. 서버 쪽 정리에는 `OnClientDisconnectCallback`으로 충분하여 그 콜백을 계속 쓴다.

### 2.10.3 연결 승인(Connection Approval) 흐름

![그림 2-11 접속 승인(Connection Approval) 흐름](images/ch02_fig11_approval.png)

그림 2-11의 순서를 따라가 본다.

1. 클라이언트가 `StartClient()`를 부르기 **전에** `NetworkManager.NetworkConfig.ConnectionData`에 바이트 배열을 넣는다. 이름, 접속 코드 같은 값이다. 이 데이터를 **페이로드(payload)** 라고 한다.
2. `StartClient()`가 연결을 만들고, 연결 요청 메시지에 페이로드를 실어 서버로 보낸다.
3. 서버에서 `ConnectionApprovalCallback`이 호출된다. 콜백은 `request.Payload`를 읽어 승인할지 정한다. 결과는 `response`에 쓴다.
   - `response.Approved`: 승인하면 true, 거절하면 false(기본값은 false).
   - `response.Reason`: 거절 사유 문자열.
   - `response.CreatePlayerObject`: 승인한 클라이언트의 플레이어 오브젝트를 만들지(기본값은 false).
   - `response.Position`, `response.Rotation`: 플레이어가 생길 위치와 방향.
4. 승인하면 서버가 플레이어 오브젝트를 만들고 씬과 오브젝트를 동기화한 뒤 `OnClientConnectedCallback`을 부른다. 거절하면 사유를 클라이언트에게 보내고 연결을 끊는다. 클라이언트는 `OnClientDisconnectCallback`에서 `NetworkManager.DisconnectReason`으로 사유를 읽는다.

알아 둘 점이 여럿 있다.

- 이 기능을 쓰려면 **서버와 클라이언트 양쪽 모두** `NetworkConfig.ConnectionApproval`이 켜져 있어야 한다(2.5.3절의 설정 해시에 포함된다). 코드에서 켜는 방법과 Inspector 체크 중 어느 쪽이든 된다. 이 교재는 `ApprovalGate.Start()`에서 코드로 켠다.
- `ConnectionApprovalCallback`은 **서버(호스트)에서만** 호출된다. 클라이언트에서는 호출되지 않는다.
- `NetworkManager.ConnectionApprovalCallback`은 이벤트가 아니라 하나의 함수만 넣을 수 있는 속성이다. `+=`로 두 번 연결하면 `Only one ConnectionApprovalCallback can be registered at a time.` 예외가 난다. `=`로 대입한다.
- **호스트 자신의 접속도 이 콜백을 거친다.** `StartHost()` 안에서 호출되고, `ClientNetworkId`는 0이다. 호스트는 거절할 수 없다. 거절로 응답해도 `You cannot decline the host connection. The connection was automatically approved.` 경고가 나고 승인된다.
- 승인 판단에 시간이 걸리면(예: 데이터베이스 조회) 콜백에서 `response.Pending = true`로 두고 판단이 끝난 뒤에 false로 바꾼다. 그 사이 서버는 다른 일을 계속한다. 대기 시간 제한은 NetworkManager의 **Client Connection Buffer Timeout**(기본 10초)이다.
- **페이로드는 암호화되지 않는다.** 접속 코드를 그대로 보내는 이 교재의 방식은 연습용이다. 실제 서비스에서는 비밀번호를 평문으로 보내지 않고, 로그인 서비스가 발급한 토큰을 보내고 서버가 검증한다. 6장의 서버도 같은 구조(HELLO 메시지의 이름과 코드)로 인증을 처리한다.

### 2.10.4 실습 6: 접속 코드, 정원, 이름 중복 검사

**목표.** 접속하기 전에 이름과 접속 코드를 입력하게 한다. 서버는 접속 코드, 정원(4명), 이름 중복을 검사하여 조건에 맞지 않으면 사유와 함께 거절한다. 승인된 사용자의 이름이 캡슐 위의 닉네임이 된다.

**단계.**

1. `Assets/Scripts`에 `JoinPanel.cs`를 만든다. 이름과 접속 코드를 입력받아 접속 요청에 실을 `ConnectionData`를 만든다.

```csharp
using System.Text;
using Unity.Netcode;
using UnityEngine;

// 접속할 때 서버에 함께 보낼 정보. 다른 스크립트(PlayerProfile 등)도 읽는다.
public static class JoinInfo
{
    public static string Name = "";
    public static string Code = "";
}

// 화면 오른쪽 위의 접속 정보 입력 패널. 접속하기 전에만 보인다.
public class JoinPanel : MonoBehaviour
{
    string lastSent;                       // 마지막으로 ConnectionData에 넣은 내용

    void OnGUI()
    {
        UiKit.Apply();
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || nm.IsListening || nm.ShutdownInProgress) return;     // 접속을 시작하면 숨긴다

        GUILayout.BeginArea(new Rect(Screen.width - 350, 50, 340, 130), GUI.skin.box);
        GUILayout.Label("이름 (최대 16자)");
        JoinInfo.Name = GUILayout.TextField(JoinInfo.Name, 16);
        GUILayout.Label("접속 코드");
        JoinInfo.Code = GUILayout.TextField(JoinInfo.Code, 16);
        GUILayout.EndArea();

        // 입력한 내용이 바뀔 때마다 접속 요청에 실을 데이터를 갱신한다. 이름 안의 '|'는 구분자와 겹치므로 뺀다.
        string text = JoinInfo.Name.Replace("|", "") + "|" + JoinInfo.Code;
        if (text != lastSent)
        {
            lastSent = text;
            nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(text);
        }
    }
}
```

2. `JoinPanel.cs`를 씬의 `NetStarter` 오브젝트에 붙인다.
3. 2.6절에서 만든 `ApprovalGate.cs`를 아래 코드로 **통째로 바꾼다.** 같은 파일이고 같은 오브젝트에 붙어 있으므로 다시 붙일 필요가 없다.

```csharp
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

// 접속 승인: 접속 코드, 정원, 이름 중복을 검사하고 플레이어가 생길 위치를 정한다. (서버에서 실행된다)
public class ApprovalGate : MonoBehaviour
{
    public const string AccessCode = "1234";   // 교재용 접속 코드. 실제 서비스에서는 이렇게 코드에 적어 두지 않는다.
    public const int MaxPlayers = 4;           // 호스트를 포함한 최대 접속자 수

    // 승인된 접속자의 ClientId → 이름. 서버(호스트)에서만 채워진다.
    public static readonly Dictionary<ulong, string> Names = new Dictionary<ulong, string>();

    NetworkManager nm;

    void Start()
    {
        nm = NetworkManager.Singleton;
        nm.NetworkConfig.ConnectionApproval = true;       // 서버와 클라이언트 양쪽이 켜야 한다
        nm.ConnectionApprovalCallback = Approve;          // 하나만 넣을 수 있으므로 += 가 아니라 = 이다
        nm.OnClientDisconnectCallback += OnClientDisconnected;
        nm.OnServerStopped += OnServerStopped;
    }

    void OnDestroy()
    {
        if (nm == null) return;
        nm.OnClientDisconnectCallback -= OnClientDisconnected;
        nm.OnServerStopped -= OnServerStopped;
    }

    void OnClientDisconnected(ulong clientId) { Names.Remove(clientId); }
    void OnServerStopped(bool wasClient) { Names.Clear(); }

    void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        ulong clientId = request.ClientNetworkId;

        // 클라이언트가 접속 요청에 실어 보낸 "이름|코드"를 읽는다.
        string[] parts = Encoding.UTF8.GetString(request.Payload ?? new byte[0]).Split('|');
        string playerName = parts.Length > 0 ? parts[0].Trim() : "";
        string code = parts.Length > 1 ? parts[1] : "";

        // 호스트 자신(ClientId 0)은 거절할 수 없다. 이름이 비어 있으면 기본 이름을 쓴다.
        bool isHost = clientId == NetworkManager.ServerClientId;
        if (isHost && playerName.Length == 0) playerName = $"Player {clientId}";

        string reason = isHost ? null : Validate(playerName, code);
        if (reason != null)
        {
            response.Approved = false;
            response.Reason = reason;          // 접속하려던 클라이언트의 NetworkManager.DisconnectReason으로 전달된다
            Debug.Log($"[서버] 접속 거절: '{playerName}' ({reason})");
            return;
        }

        Names[clientId] = playerName;
        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = SpawnPoint(clientId);
        response.Rotation = Quaternion.identity;
        Debug.Log($"[서버] 접속 승인: '{playerName}' (ClientId {clientId})");
    }

    // 거절 사유를 돌려준다. 통과하면 null이다.
    string Validate(string playerName, string code)
    {
        if (code != AccessCode) return "접속 코드가 틀렸다.";
        if (playerName.Length == 0) return "이름을 입력해야 한다.";
        if (playerName.Length > 16) return "이름은 16자까지 쓸 수 있다.";     // 입력칸은 16자로 막지만, 고친 클라이언트는 더 길게 보낼 수 있다
        if (Names.Count >= MaxPlayers) return $"정원({MaxPlayers}명)이 찼다.";
        foreach (KeyValuePair<ulong, string> pair in Names)
            if (pair.Value == playerName) return "이미 사용 중인 이름이다.";
        return null;
    }

    static Vector3 SpawnPoint(ulong clientId) => new Vector3((int)(clientId % 8) * 2f - 7f, 1f, 0f);
}
```

4. 승인된 이름이 닉네임이 되게 한다. 2.8절에서 만든 `PlayerProfile.cs`를 열어 `OnNetworkSpawn()`의 첫 줄을 찾는다.

```csharp
        if (IsOwner) Local = this;
```

   이 줄을 아래 두 줄로 바꾼다.

```csharp
        if (IsOwner) Local = this;
        if (IsOwner && JoinInfo.Name.Length > 0) SetNick(JoinInfo.Name);    // 접속할 때 쓴 이름이 첫 닉네임이다
```

5. 씬을 저장하고 빌드를 다시 한다.

**확인할 것.**

1. 에디터에서 이름 `교수`, 접속 코드 `1234`를 입력하고 **Host**를 누른다. 캡슐 위에 `교수 (나)`가 나타난다. Console에 `[서버] 접속 승인: '교수' (ClientId 0)`이 나온다. 호스트가 시작되면 접속 정보 패널은 사라진다.
2. 빌드한 클라이언트에서 접속 코드를 비워 두거나 `9999`를 입력하고 **Client**를 누른다. 접속하지 못하고 화면 아래 메시지에 사유가 나온다.

```text
서버에 접속하지 못했다. …
사유: 접속 코드가 틀렸다.
```

   서버 Console에는 `[서버] 접속 거절: '…' (접속 코드가 틀렸다.)`가 나온다. 사유 문자열은 서버의 `response.Reason`이 클라이언트의 `NetworkManager.DisconnectReason`으로 전달된 것이다.
3. 올바른 코드로 이름을 비우고 접속해 본다. `이름을 입력해야 한다.`로 거절된다. 이름을 `교수`로 입력하면 `이미 사용 중인 이름이다.`로 거절된다.
4. 이름 `철수`, 코드 `1234`로 접속한다. 승인되어 캡슐이 생기고 닉네임이 `철수 (나)`로 보인다. 호스트 화면에서는 `철수`로 보인다. 2.8절처럼 입력칸으로 닉네임을 바꿀 수도 있다.
5. 호스트를 포함해 4명이 접속한 상태에서 다섯 번째가 접속하면 `정원(4명)이 찼다.`로 거절된다. 창을 여러 개 띄우기 어려우면 `MaxPlayers`를 2로 줄여서 시험한다(빌드를 다시 한다).
6. 거절을 몇 번 겪은 뒤 접속해 본다. 승인된 클라이언트의 ClientId가 1, 2, 3으로 이어지지 않고 **건너뛴다.** 접속을 시도한 연결마다 ClientId가 하나씩 발급되기 때문에, 거절당한 시도도 번호를 하나 쓴다.

**그 뒤에서 일어난 일.**

`StartClient()`가 서버와 전송 계층 연결을 맺는 순간 서버는 그 연결에 ClientId를 발급한다. 이어서 클라이언트가 접속 요청 메시지를 보내고, 서버는 그 안의 페이로드를 `ConnectionApprovalCallback`에 넘긴다. 콜백이 `response`에 결과를 채우고 돌아오면 서버는 같은 프레임 또는 다음 프레임에 그 결과를 처리한다. 승인이면 플레이어 오브젝트를 `response.Position`에 스폰하고, 거절이면 사유를 담은 메시지를 보낸 뒤 연결을 끊는다.

`Names` 사전은 서버만 채운다. 클라이언트에서는 승인 콜백이 호출되지 않으므로 비어 있다. 클라이언트가 다른 접속자의 이름을 알려면 NetworkVariable이나 RPC로 받아야 하고, 2.11절에서 접속자 목록을 NetworkList로 만들어 해결한다.

### 2.10.5 실습 7: 접속자 목록 보기와 내보내기

**목표.** 서버(호스트) 화면에 접속자 목록을 보여 주고, 버튼으로 클라이언트를 내보낸다. 내보내진 클라이언트는 사유를 화면에서 볼 수 있다.

**단계.**

1. `ServerPanel.cs`를 만든다.

```csharp
using Unity.Netcode;
using UnityEngine;

// 서버(호스트) 화면 아래쪽의 접속자 목록과 내보내기 버튼
public class ServerPanel : MonoBehaviour
{
    ulong kickId = ulong.MaxValue;         // 내보내기로 정한 클라이언트(없으면 MaxValue)

    // 접속을 끊는 일은 화면을 그리는 도중이 아니라 Update에서 한다.
    void Update()
    {
        if (kickId == ulong.MaxValue) return;
        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer && nm.ConnectedClients.ContainsKey(kickId))
            nm.DisconnectClient(kickId, "서버가 내보냈다.");
        kickId = ulong.MaxValue;
    }

    void OnGUI()
    {
        UiKit.Apply();
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        GUILayout.BeginArea(new Rect(440, Screen.height - 150, 420, 140), GUI.skin.box);
        GUILayout.Label($"접속자 {nm.ConnectedClientsIds.Count}명 (정원 {ApprovalGate.MaxPlayers}명)");
        foreach (ulong id in nm.ConnectedClientsIds)
        {
            ApprovalGate.Names.TryGetValue(id, out string playerName);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{id}: {playerName}");
            if (id != NetworkManager.ServerClientId && GUILayout.Button("내보내기", GUILayout.Width(100)))
                kickId = id;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndArea();
    }
}
```

2. `ServerPanel.cs`를 `NetStarter` 오브젝트에 붙인다.
3. 빌드를 다시 하고 호스트와 클라이언트 둘을 접속시킨다.

**확인할 것.**

1. 호스트 화면 아래에 `0: 교수`, `1: 철수` 같은 목록이 나온다. 클라이언트 화면에는 이 패널이 나오지 않는다(서버가 아니므로).
2. 클라이언트가 들어오고 나갈 때마다 목록이 갱신된다.
3. `철수` 옆의 **내보내기**를 누른다. 철수의 화면에 `서버와의 연결이 끊어졌다`와 `사유: 서버가 내보냈다.`가 나오고, 철수의 캡슐이 모든 화면에서 사라진다.
4. 철수가 같은 이름으로 다시 접속한다. 이전 연결이 끊어지면서 `Names`에서 이름이 지워졌으므로 승인된다. 새 ClientId를 받는다.

`DisconnectClient(clientId)` 한 개 인자짜리도 있다. 이 경우 사유는 `Client-N disconnected by server.`로 자동으로 채워진다. 사유를 직접 정하려면 두 번째 인자로 넘긴다.

### 2.10.6 접속과 종료에서 알아 둘 점

- **재접속하면 새 ClientId를 받는다.** 같은 사용자라도 이전 ClientId가 유지되지 않는다. 사용자를 구분하려면 ClientId가 아니라 이름이나 계정 번호 같은 서버가 정한 키를 쓴다.
- **클라이언트가 나가면** 서버는 그 클라이언트가 소유한 오브젝트를 제거한다(NetworkObject의 **Don't Destroy With Owner**가 꺼져 있을 때). 플레이어 오브젝트가 사라지는 이유이다.
- **서버(호스트)가 종료하면** 클라이언트에게 종료 사유를 보내고 클라이언트의 연결을 끊는다. 클라이언트의 `NetworkManager.DisconnectReason`에 `Disconnected due to host shutting down.`(전용 서버라면 `…server shutting down.`)이 들어 있다. 클라이언트는 `OnClientDisconnectCallback`을 부른 뒤 스스로 `Shutdown`을 진행하므로, 화면 코드는 `ShutdownInProgress`를 확인한다(2.5절).
- **네트워크가 끊어지면** 상대가 사라진 것을 알아채기까지 시간이 걸린다. 프로그램을 강제 종료하거나 랜선을 뽑으면 서버가 연결 종료를 알아채는 데 수십 초가 걸린다(2.5.6절). 프로그램이 정상 종료하면 `Shutdown()`이 서버에게 알리므로 즉시 처리된다.
- **서버 시작 전에 설정한 값은 시작할 때 고정된다.** 접속 중에 `ConnectionApproval`을 바꾸지 않는다.

### 2.10.7 흔한 오류

- **`No ConnectionApprovalCallback defined. Connection approval will timeout` 경고가 나고 접속이 멈춘다.** `ConnectionApproval`은 켜져 있는데 콜백이 없다. `ApprovalGate`가 씬에 붙어 있고 `Start()`가 실행되었는지 확인한다.
- **`ConnectionApprovalCallback is defined but ConnectionApproval is disabled` 경고가 난다.** 콜백은 있는데 `ConnectionApproval`이 꺼져 있다. `nm.NetworkConfig.ConnectionApproval = true;` 줄이 있는지 확인한다.
- **`NetworkConfig mismatch` 오류로 접속이 거절된다.** 서버와 클라이언트의 `ConnectionApproval` 설정이 다르다. 클라이언트 빌드가 `ApprovalGate`가 없던 예전 것이 아닌지 확인하고 다시 빌드한다(2.5.3절).
- **`Only one ConnectionApprovalCallback can be registered at a time.`** 콜백을 두 번 이상 `+=`로 연결했다. `nm.ConnectionApprovalCallback = Approve;`처럼 `=`로 대입한다.
- **올바른 코드를 입력했는데도 `접속 코드가 틀렸다.`로 거절된다.** `JoinPanel`이 `NetStarter` 오브젝트에 붙어 있지 않으면 `ConnectionData`가 비어 있어 거절된다. 접속 정보 패널이 화면에 보이는지 확인한다.
- **클라이언트가 접속했는데 거절 사유가 `[Disconnect Event]…`로 시작하는 긴 문장으로 나온다.** 서버가 사유(`response.Reason`)를 채우지 않았다. 사유를 채우면 그 문장이 그대로 전달된다.
- **호스트가 접속 코드가 맞지 않다고 거절당하지 않는다.** 정상이다. 호스트는 거절할 수 없다(`You cannot decline the host connection…`).
- **재접속하면 이름 중복으로 거절된다.** 이전 연결이 아직 끊어졌다고 서버가 알아채지 못했다. 잠시 뒤에 다시 시도한다(2.10.6절).

**이 절의 정리.**

- 접속 승인 = 클라이언트가 `ConnectionData`(페이로드)를 실어 요청하고, 서버의 `ConnectionApprovalCallback`이 `Approved`, `Reason`, `CreatePlayerObject`, `Position`을 정한다.
- 서버와 클라이언트 모두 `ConnectionApproval`을 켠다. 콜백은 서버에서만 호출되고, 하나만 대입한다. 호스트는 거절할 수 없다.
- 거절 사유는 `response.Reason` → 클라이언트의 `NetworkManager.DisconnectReason`으로 전달된다.
- 접속자를 내보낼 때는 `DisconnectClient(clientId, 사유)`를 쓴다.
- 페이로드는 암호화되지 않는다. 비밀번호를 그대로 보내는 방식은 연습용이다.

## 2.11 채팅 프로그램 완성

앞 절까지 접속, 스폰, 이동 동기화, NetworkVariable, RPC, 접속 승인을 하나씩 익혔다. 이 절에서는 이것들을 조합하여 **채팅 프로그램**을 완성한다. 이 교재에서는 Unity, 언리얼, 6장의 C++ 서버까지 모두 채팅 프로그램을 가장 먼저 만들고, 그것으로 서버의 기능을 검증한다. 접속자를 관리하고 받은 메시지를 알맞은 사람에게 전달하는 것이 서버의 기본 일이기 때문이다. 채팅을 만들 수 있으면 같은 구조로 게임의 다른 기능도 만들 수 있다.

### 2.11.1 채팅 씬으로 서버를 검증한다

**[검증 방식]** 실시간 게임의 네트워크는 게임 화면에서 검증하기가 매우 어렵다. 입력, 렌더링, 물리가 함께 동작하면 문제가 네트워크에서 생겼는지 게임 코드에서 생겼는지 구분하기 어렵기 때문이다. 그래서 실무에서는 먼저 채팅 씬 하나에 GUI 창(버튼과 Text)을 만들어 서버의 기능을 하나씩 호출하며 검증하고, 검증된 네트워크 코드를 게임 클라이언트에 옮겨 적용한다. 이 절의 실습은 모두 이 방식을 따른다.

**[작업 순서]** 1. ChatScene 하나에 서버 기능마다 버튼(요청)과 Text(응답·상태)를 만든다. 2. 서버(호스트)와 이 GUI 클라이언트를 여러 개 실행하여 기능별로 검증한다. 3. 검증된 네트워크 코드를 게임 클라이언트에 그대로 옮기고, GUI 부분만 게임 UI로 교체한다.

**[GUI로 검증할 기능]**

- 초기화와 접속: 주소·포트 입력, Host / Client / Server / 종료 버튼, 상태 Text (실습 1)
- 인증: 이름과 인증 코드 전송, 승인·거부 사유 표시 (실습 6)
- 접속자 목록: 입장·퇴장 알림, 접속자 목록 Text (실습 8)
- 채널(방) 생성·입장·퇴장: 채널 이름 입력, 생성·입장·퇴장 버튼, 채널별 접속자 Text (실습 10)
- 위치·상태 전달: 자동 이동(가상 입력)과 다른 화면에 보이는 캡슐의 위치 (실습 3), 체력 증감·준비 버튼과 상태 목록 Text (실습 11)
- 요청과 응답: 보너스 받기 같은 요청 버튼과 결과 Text (실습 12)
- 오브젝트 생성·제거: 생성·제거 버튼과 목록 Text (실습 13)

이 씬은 게임이 아니라 서버 검증 도구이다. 이 절의 예제는 처음부터 네트워크 코드(`ChatHub`)와 화면 코드(`…Panel`)를 따로 작성한다. 게임 클라이언트로 옮길 때는 화면 코드만 게임 UI로 바꾸면 된다(2.11.8절). 화면은 버튼과 글자를 코드 한 줄로 그리는 IMGUI로 만들고, Canvas 기반의 uGUI로 바꾸는 방법은 2.11.9절에서 다룬다. 이 교재의 프로젝트는 씬이 하나뿐이므로 `SampleScene`이 위 설명의 ChatScene이다.

### 2.11.2 이 절에서 만들 것

![그림 2-12 채팅 검증 화면의 배치 (1280×720 기준 개념도)](images/ch02_fig12_screen_layout.png)

그림 2-12는 이 절을 마쳤을 때 Game 창의 모습이다. 왼쪽 위의 접속 화면(`NetStarter`)과 아래쪽의 닉네임·서버 패널은 앞 절에서 이미 만들었다. 이 절에서 새로 만드는 것은 가운데의 채팅 로그, 입력 창, 접속자 목록, 채널·상태·보너스 패널이다. 화면 배치는 해상도 1280×720을 기준으로 잡았으므로 먼저 Game 창의 해상도를 맞춘다.

1. Game 창 위쪽의 화면 비율 드롭다운(기본값 `Free Aspect`)을 열어 `1280x720`(또는 `HD`) 같은 고정 해상도를 고른다. 목록에 없으면 목록 맨 아래의 **+** 를 눌러 Fixed Resolution으로 너비 1280, 높이 720을 추가한다. 창이 작아서 화면이 줄어들어 보여도 해상도는 그대로이므로 배치는 같다.
2. 빌드한 프로그램은 2.2절에서 정한 대로 **Windowed 1280×720**으로 실행된다.

패널의 위치와 크기는 코드의 `Rect(x, y, 너비, 높이)`로 정했다. 글꼴과 환경에 따라 패널 안의 줄 높이가 조금 다를 수 있으므로, 패널 아래쪽의 내용이 잘려 보이면 해당 스크립트의 `Rect` 높이를 조금 키운다. 이 배치는 채팅을 검증하기 위한 것이어서 Game 화면의 가운데를 거의 다 덮고, 뒤에 있는 3D 화면(바닥과 캡슐)은 가려진다. 3D 화면을 볼 때 패널을 치우는 버튼은 2.12절에서 만든다.

이 절의 코드는 네트워크 쪽 `ChatHub`와 화면 쪽 `…Panel`로 나뉜다.

- **`ChatHub`** (네트워크 코드): 씬에 하나만 두는 서버 소유 오브젝트이다. 접속자 목록(NetworkList)을 관리하고, 클라이언트의 요청(RPC)을 받아 검사하고, 결과를 대상에게 전달한다. 화면을 전혀 모른다. 대신 화면이 구독할 수 있는 이벤트(`OnLog`, `OnBonusResult`)를 제공한다.
- **`…Panel`** (화면 코드): IMGUI로 버튼과 글자를 그린다. 네트워크를 직접 다루지 않고 `ChatHub`의 함수를 부르고, `ChatHub`의 이벤트와 목록을 읽어서 보여 주기만 한다.

`ChatHub`는 기능이 많아서 **`partial class`** 로 여러 파일에 나누어 만든다. `partial`은 하나의 클래스 정의를 여러 파일에 나누어 쓰는 C#의 기능이다. 컴파일하면 하나의 클래스가 된다. C++에서는 클래스 선언을 한 곳에 모아야 하지만 C#은 이렇게 나눌 수 있다. 이 절에서는 기능 하나를 추가할 때마다 파일 하나를 새로 만들기만 하고, 앞에서 만든 파일은 고치지 않는다. 파일 이름은 `ChatHub.Chat.cs`처럼 클래스 이름 뒤에 기능 이름을 붙인다. 첫 번째 파일(`ChatHub.cs`)에만 `: NetworkBehaviour`가 적혀 있고 나머지 파일에는 적지 않는다. `[Rpc]` 함수도 파일을 나누어 선언할 수 있다. NGO가 RPC 코드를 만드는 일은 컴파일이 끝난 뒤에 하므로 클래스가 몇 개의 파일에 나뉘어 있든 상관없다.

만드는 파일은 다음과 같다. 실습 번호는 만드는 순서이다.

- 실습 8: `UserState.cs`(접속자 한 명의 정보), `ChatHub.cs`(접속자 목록과 입장·퇴장), `ChatLogPanel.cs`(채팅 로그), `UserListPanel.cs`(접속자 목록 화면)
- 실습 9: `ChatHub.Chat.cs`(채팅·귓속말·공지), `ChatInputPanel.cs`(입력 창)
- 실습 10: `ChatHub.Channel.cs`(채널 만들기·입장·나가기), `ChannelPanel.cs`
- 실습 11: `ChatHub.State.cs`(체력·준비 상태), `StatePanel.cs`
- 실습 12: `ChatHub.Bonus.cs`(보너스 요청과 응답), `BonusPanel.cs`

모든 파일은 `Assets/Scripts`에 만든다. `ChatHub`는 파일이 여러 개여도 클래스는 하나이므로 새 게임 오브젝트 `ChatHub`에 한 번만 붙인다. `…Panel` 계열은 지금까지처럼 `NetStarter` 오브젝트에 붙인다.

### 2.11.3 실습 8: 접속자 목록과 입장·퇴장 알림

**목표.** 서버가 접속자 목록을 `NetworkList`로 관리하고 모든 접속자에게 복제한다. 접속자가 들어오고 나갈 때마다 모든 화면에 알림이 한 줄 나타난다. 채팅 로그와 접속자 목록 화면의 첫 버전이다.

**먼저 알아 둘 것.**

이 실습에는 새로운 도구가 둘 있다. 하나는 **`NetworkList<T>`** , 다른 하나는 **씬에 놓는 서버 소유 오브젝트**이다.

`NetworkList<T>`는 NetworkVariable의 목록 버전이다. 값이 하나가 아니라 여러 개일 때(접속자 목록, 방 목록, 인벤토리) 쓴다. 규칙은 NetworkVariable과 거의 같다.

- 요소 타입 `T`는 `unmanaged`(참조 타입이나 `string`을 멤버로 갖지 않는 값 타입)이고 `IEquatable<T>`를 구현해야 한다. 구조체를 요소로 쓰려면 `INetworkSerializable`도 구현하여 NGO에게 직렬화 방법을 알려 준다(2.8.5절).
- 기본 설정에서 **서버만 쓴다.** `Add`, `Insert`, `Remove`, `RemoveAt`, `Clear`와 인덱서로 값을 바꾸는 일(`list[i] = x`)은 서버에서만 효과가 있다. 클라이언트가 하면 값은 바뀌지 않고 Console에 오류가 나온다(`RemoveAt`은 예외를 던진다). 읽기(`Count`, `list[i]`, `Contains`, `IndexOf`)는 모두가 할 수 있다.
- 구조체 요소의 필드 하나를 바꾸려면 꺼내서(`UserState u = list[i];`) 고친 뒤 다시 대입한다(`list[i] = u;`). 대입해야 변화가 전달된다.
- `Awake()`에서 만든다(`list = new NetworkList<UserState>();`).
- 목록이 바뀌면 `OnListChanged` 이벤트가 발생한다. 이 교재의 화면은 매 프레임 목록을 읽어서 그리므로 이벤트가 필요 없다.
- 늦게 접속한 클라이언트는 `OnNetworkSpawn()`이 불리기 전에 현재 목록 전체를 받는다.

**씬에 놓는 서버 소유 오브젝트.** 지금까지의 NetworkObject는 접속한 사람마다 서버가 코드로 스폰한 플레이어 캡슐이었다. `ChatHub`는 다르다. 접속자 전체를 위한 하나의 서비스이므로 **씬에 미리 배치**한다(2.6.1절). 서버가 시작되면 자동으로 스폰되고, 나중에 접속하는 클라이언트는 접속할 때 받는다. 이 오브젝트의 소유자는 서버(ClientId 0)이다. 클라이언트는 소유자가 아니므로 `ChatHub`의 RPC에는 `InvokePermission`을 `Owner`로 쓸 수 없고 기본값인 `Everyone`을 쓴다(2.9.5절).

**단계.**

1. `Assets/Scripts`에 `UserState.cs`를 만든다. 접속자 한 명의 정보이다. `Channel`, `Hp`, `Ready`, `Gold`는 뒤의 실습에서 쓰는 필드이지만 구조체를 나중에 바꾸지 않도록 미리 넣어 둔다.

```csharp
using System;
using Unity.Collections;
using Unity.Netcode;

// 접속자 한 명의 정보. 서버가 NetworkList에 담아 모든 접속자에게 복제한다.
public struct UserState : INetworkSerializable, IEquatable<UserState>
{
    public ulong Id;                      // ClientId
    public FixedString64Bytes Name;       // 접속 승인 때 확인한 이름
    public FixedString64Bytes Channel;    // 들어가 있는 채널 이름. 비어 있으면 로비(어느 채널에도 없음)
    public int Hp;                        // 체력 (실습 11)
    public bool Ready;                    // 준비 여부 (실습 11)
    public int Gold;                      // 받은 보너스 (실습 12)

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref Channel);
        serializer.SerializeValue(ref Hp);
        serializer.SerializeValue(ref Ready);
        serializer.SerializeValue(ref Gold);
    }

    public bool Equals(UserState other)
    {
        return Id == other.Id && Name.Equals(other.Name) && Channel.Equals(other.Channel)
            && Hp == other.Hp && Ready == other.Ready && Gold == other.Gold;
    }
}
```

2. 같은 폴더에 `ChatHub.cs`를 만든다. 이 파일이 `ChatHub` 클래스의 첫 부분이다.

```csharp
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// 채팅 서버 기능을 맡는 오브젝트. 씬에 하나만 두고 서버가 소유한다.
// 기능이 많아서 여러 파일로 나눈다(partial). 이 파일은 접속자 목록과 입장·퇴장을 맡는다.
public partial class ChatHub : NetworkBehaviour
{
    public static ChatHub Instance { get; private set; }

    // 화면이 구독하는 이벤트. 서버가 보낸 한 줄이 도착하면 알려 준다. ChatHub는 화면이 무엇인지 모른다.
    public static event Action<string> OnLog;

    // 접속자 목록. 서버만 고치고 모든 접속자가 읽는다.
    NetworkList<UserState> users;
    public NetworkList<UserState> Users => users;

    void Awake()
    {
        users = new NetworkList<UserState>();       // NetworkList는 Awake에서 만든다
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        if (!IsServer) return;

        users.Clear();                              // 서버를 다시 시작했을 때 지난 접속자를 비운다
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            AddUser(clientId, false);               // 호스트 자신처럼 이미 접속해 있는 사람

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
        if (NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // ---------- 서버에서만 실행되는 부분: 입장과 퇴장 ----------

    void OnClientConnected(ulong clientId) => AddUser(clientId, true);

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsSpawned || NetworkManager.ShutdownInProgress) return;    // 서버를 끄는 중이면 정리할 필요가 없다

        int i = IndexOf(clientId);
        if (i < 0) return;

        string leftName = users[i].Name.ToString();
        users.RemoveAt(i);
        ToAll($"* {leftName} 퇴장");
    }

    void AddUser(ulong clientId, bool announce)
    {
        if (IndexOf(clientId) >= 0) return;         // 이미 등록되어 있으면 건너뛴다

        ApprovalGate.Names.TryGetValue(clientId, out string userName);    // 접속 승인 때 확인한 이름
        if (string.IsNullOrEmpty(userName)) userName = $"Player {clientId}";

        users.Add(new UserState { Id = clientId, Name = ToFixed(userName), Hp = 100 });
        if (announce) ToAll($"* {userName} 입장");
    }

    // ---------- 목록을 찾는 도우미 ----------

    int IndexOf(ulong clientId)
    {
        for (int i = 0; i < users.Count; i++)
            if (users[i].Id == clientId) return i;
        return -1;
    }

    // 클라이언트도 부를 수 있다. 목록에서 ClientId로 접속자를 찾는다.
    public bool TryGetUser(ulong clientId, out UserState user)
    {
        int i = IndexOf(clientId);
        user = i >= 0 ? users[i] : default;
        return i >= 0;
    }

    // 문자열을 NetworkList에 담을 수 있는 고정 길이 문자열로 바꾼다. 16자까지만 쓴다.
    static FixedString64Bytes ToFixed(string text)
    {
        if (text == null) text = "";
        if (text.Length > 16) text = text.Substring(0, 16);
        return new FixedString64Bytes(text);
    }

    // 같은 채널에 들어가 있는 접속자들의 ClientId 목록 (채널은 실습 9, 10에서 쓴다)
    List<ulong> MembersOf(FixedString64Bytes channel)
    {
        var ids = new List<ulong>();
        for (int i = 0; i < users.Count; i++)
            if (users[i].Channel.Equals(channel)) ids.Add(users[i].Id);
        return ids;
    }

    // ---------- 서버가 한 줄을 내보내는 세 가지 방법 ----------

    void ToAll(string line)                             // 모든 접속자에게
    {
        ServerLog(line);
        LineToAllRpc(line);
    }

    void ToGroup(List<ulong> clientIds, string line)    // 지정한 접속자들에게만
    {
        ServerLog(line);
        LineToSomeRpc(line, RpcTarget.Group(clientIds, RpcTargetUse.Temp));
    }

    void ToOne(ulong clientId, string line)             // 한 사람에게만 (안내, 거절 사유)
    {
        LineToSomeRpc(line, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    void ServerLog(string line)
    {
        Debug.Log($"[서버] {line}");
        if (!IsClient) Emit(line);      // 화면만 있는 서버(Server 모드)는 클라이언트용 RPC를 받지 않으므로 직접 보여 준다
    }

    // 서버가 모든 클라이언트(호스트 포함)에게 한 줄을 보낸다. 서버만 부를 수 있다.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    void LineToAllRpc(string line)
    {
        Emit(line);
    }

    // 서버가 호출할 때 정한 접속자에게만 한 줄을 보낸다. 서버만 부를 수 있다.
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void LineToSomeRpc(string line, RpcParams rpcParams = default)
    {
        Emit(line);
    }

    void Emit(string line)
    {
        OnLog?.Invoke(line);
    }
}
```

3. Hierarchy의 빈 곳에서 마우스 오른쪽 버튼 > **Create Empty**로 새 오브젝트를 만들고 이름을 `ChatHub`로 바꾼다. Inspector에서 **Add Component**를 눌러 `Network Object`(Netcode/Network Object)를 추가하고, 이어서 `ChatHub` 스크립트를 추가한다. 오류 메시지에 이 오브젝트의 이름이 나오므로 이름은 `ChatHub` 그대로 둔다.
4. 같은 폴더에 `ChatLogPanel.cs`를 만든다. `ChatHub`가 알려 주는 줄을 모아서 보여 주는 로그 창이다.

```csharp
using System.Collections.Generic;
using UnityEngine;

// 채팅 로그 창. ChatHub가 알려 주는 줄을 모아서 보여 준다.
public class ChatLogPanel : MonoBehaviour
{
    const int MaxLines = 200;                       // 이보다 많아지면 오래된 줄부터 버린다

    readonly List<string> lines = new List<string>();
    Vector2 scroll;
    bool follow;                                    // 새 줄이 오면 맨 아래로 내린다
    bool wasConnected;

    void OnEnable()  { ChatHub.OnLog += AddLine; }
    void OnDisable() { ChatHub.OnLog -= AddLine; }

    void AddLine(string line)
    {
        if (lines.Count >= MaxLines) lines.RemoveAt(0);
        lines.Add(line);
        follow = true;
    }

    void Update()
    {
        bool connected = ChatHub.Instance != null;
        if (wasConnected && !connected) lines.Clear();      // 접속이 끝나면 그 접속의 대화를 지운다
        wasConnected = connected;
    }

    void OnGUI()
    {
        UiKit.Apply();
        if (ChatHub.Instance == null) return;

        GUILayout.BeginArea(new Rect(10, 175, 550, Screen.height - 486), GUI.skin.box);
        if (follow) scroll.y = Mathf.Infinity;
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (string line in lines) GUILayout.Label(line);
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (Event.current.type == EventType.Repaint) follow = false;
    }
}
```

5. `UserListPanel.cs`를 만든다. 접속자 목록을 표처럼 그려 주는 화면이다.

```csharp
using Unity.Netcode;
using UnityEngine;

// 접속자 목록 화면. NetworkList를 매 프레임 읽어서 그린다. 서버와 클라이언트가 같은 코드를 쓴다.
public class UserListPanel : MonoBehaviour
{
    void OnGUI()
    {
        UiKit.Apply();
        ChatHub hub = ChatHub.Instance;
        if (hub == null) return;

        GUILayout.BeginArea(new Rect(440, 10, 480, 160), GUI.skin.box);
        GUILayout.Label($"접속자 {hub.Users.Count}명");
        DrawRow("ID", "이름", "채널", "HP", "상태", "골드");

        ulong myId = NetworkManager.Singleton.LocalClientId;
        for (int i = 0; i < hub.Users.Count; i++)
        {
            UserState u = hub.Users[i];
            DrawRow(u.Id.ToString(),
                    u.Name.ToString() + (u.Id == myId ? " (나)" : ""),
                    u.Channel.Length == 0 ? "로비" : u.Channel.ToString(),
                    u.Hp.ToString(),
                    u.Ready ? "준비" : "대기",
                    u.Gold.ToString());
        }
        GUILayout.EndArea();
    }

    static void DrawRow(string id, string userName, string channel, string hp, string state, string gold)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(id, GUILayout.Width(30));
        GUILayout.Label(userName, GUILayout.Width(140));
        GUILayout.Label(channel, GUILayout.Width(100));
        GUILayout.Label(hp, GUILayout.Width(50));
        GUILayout.Label(state, GUILayout.Width(50));
        GUILayout.Label(gold, GUILayout.Width(50));
        GUILayout.EndHorizontal();
    }
}
```

6. 두 화면 스크립트를 `NetStarter` 오브젝트에 붙인다. 씬을 저장하고 새 오브젝트(`ChatHub`)가 생겼으므로 **빌드를 다시 한다.**

**코드를 읽는 법.**

- `ChatHub`는 `NetworkBehaviour`이다. 접속자 목록 `users`는 NetworkVariable의 한 종류이므로 이 클래스가 스폰된 뒤에 동기화가 시작된다. 목록을 읽는 쪽(화면)은 `ChatHub.Instance`가 null이 아닐 때만 읽는다. 서버를 시작하거나 서버에 접속을 마치면 `OnNetworkSpawn()`에서 `Instance`가 채워지고, 연결이 끝나면 `OnNetworkDespawn()`에서 null이 된다.
- `OnNetworkSpawn()`의 서버 부분이 하는 일은 세 가지이다. 목록을 비우고(`Clear`), 이미 접속해 있는 사람을 등록하고(호스트 자신), 이후의 접속·종료 이벤트를 받을 준비를 한다. 목록을 비우는 이유는 호스트를 껐다 켰을 때 이전 접속자가 남아 있지 않게 하기 위해서이다. 씬에 놓은 오브젝트는 서버를 껐다 켜도 같은 오브젝트가 다시 쓰이기 때문이다.
- 호스트는 시작할 때 자기 자신의 `OnClientConnectedCallback`도 받는다. 호스트의 스폰 때 이미 등록했으므로 `AddUser()`는 "이미 등록되어 있으면 건너뛴다". 이렇게 해 두면 콜백과 스폰의 순서가 어떻든 한 사람이 두 번 등록되지 않는다.
- 이름은 클라이언트가 보내 주는 것이 아니라 2.10절의 `ApprovalGate.Names`에서 가져온다. 서버가 접속 승인 때 확인한 이름이므로 믿을 수 있다. 클라이언트가 채팅 요청에 이름을 실어 보내게 하면 남의 이름으로 말하는 것이 가능해진다.
- `ToAll`, `ToGroup`, `ToOne`은 서버가 한 줄을 내보내는 세 가지 방법이다. 이 절의 모든 알림과 대화가 이 셋을 지난다. 전체는 `SendTo.ClientsAndHost`, 일부는 `SendTo.SpecifiedInParams`와 `RpcTarget.Group`/`RpcTarget.Single`이다(2.9.3절). 두 RPC에는 `InvokePermission = RpcInvokePermission.Server`가 붙어 있어서 클라이언트가 직접 부를 수 없다. 클라이언트가 "서버인 척" 모든 사람에게 글을 쓰는 일을 막기 위해서이다(2.9.4절 실습에서 확인했다).
- `ServerLog()`는 서버의 Console에 기록을 남기고, 클라이언트가 없는 서버(Server 모드)에서는 화면에도 보여 준다. 호스트는 `ClientsAndHost` RPC를 자기도 받으므로 직접 보여 줄 필요가 없다. 서버가 모든 대화를 기록하는 것은 실제 서비스에서도 하는 일이다. 서버 운영자는 귓속말을 포함한 모든 대화를 볼 수 있다는 점을 기억해 둔다.
- 화면 쪽은 `ChatHub.OnLog`를 구독하고(`OnEnable`/`OnDisable`) 받은 줄을 자기 목록에 쌓는다. `ChatHub`는 화면의 존재를 모른다. 화면이 하나 더 늘어나도(2.11.8절) `ChatHub`는 바뀌지 않는다.
- `ChatLogPanel.Update()`가 연결이 끝나는 순간에 로그를 비운다. 연결 중에는 로그를 그리지 않으므로(`Instance`가 null) 그 사이에 비워도 보이지 않는다.
- `Mathf.Infinity`로 스크롤 위치를 맨 아래로 보낸다. 새 줄이 올 때마다 맨 아래가 보이게 하는 IMGUI의 흔한 방법이다.

**확인할 것.**

1. 에디터에서 이름 `교수`, 접속 코드 `1234`로 **Host**를 시작한다. 화면 가운데 위에 접속자 목록이 나타난다.

```text
접속자 1명
ID  이름          채널  HP   상태  골드
0   교수 (나)     로비  100  대기  0
```

   채팅 로그 창(가운데 왼쪽)은 비어 있다. 호스트 자신의 입장은 알리지 않는다. Console에는 빨간 오류가 없어야 한다.
2. 빌드한 프로그램에서 이름 `철수`, 접속 코드 `1234`로 **Client**를 시작한다. 클라이언트 화면의 목록에 두 사람이 나오고 로그에 `* 철수 입장`이 한 줄 나타난다. 호스트 화면에도 같은 줄이 나타나고 목록이 두 사람이 된다. 호스트의 Console에는 `[서버] * 철수 입장`이 기록된다.
3. 세 번째 클라이언트 `영희`를 접속시킨다. 모든 화면에 `* 영희 입장`이 나타난다. 영희 화면의 목록에는 세 사람이 **바로** 나오지만, 로그에는 영희 자신의 `* 영희 입장` 한 줄뿐이다. 앞서 있었던 `* 철수 입장`은 보이지 않는다. **목록은 상태이므로 늦게 온 사람도 현재 모습을 받고, 알림은 사건이므로 지나간 것을 받지 못한다.** 2.9절 외치기에서 본 차이가 그대로 나타난 것이다.
4. 철수 화면에서 **종료**를 누른다. 나머지 화면의 로그에 `* 철수 퇴장`이 나타나고 목록에서 철수가 사라진다.
5. 호스트의 접속자 패널(2.10.5절)에서 영희의 **내보내기**를 누른다. 영희는 연결이 끊어지고, 나머지 화면에는 `* 영희 퇴장`이 나타난다.
6. 철수를 같은 이름으로 다시 접속시킨다. 새 ClientId를 받고 `* 철수 입장`이 다시 나타난다.
7. 호스트를 종료한다. 클라이언트 화면은 시작 화면으로 돌아가면서 로그 창과 목록 창이 사라진다. 호스트를 다시 시작하면 목록이 `교수` 한 명으로 새로 시작한다.
8. 이번에는 에디터에서 **Server**(서버만)로 시작한다. 목록은 `접속자 0명`이다. 클라이언트가 접속하면 목록에 나타나고, 서버 화면의 로그에도 입장 알림이 나타난다(`ServerLog`가 직접 보여 준 것이다).

**그 뒤에서 일어난 일.**

철수가 접속을 마치면 서버에서 `OnClientConnectedCallback`이 불리고, `AddUser()`가 `users.Add()`를 호출한다. 서버의 목록이 즉시 바뀌고, NetworkList는 "끝에 한 명 추가"라는 변경 정보를 모아 두었다가 **다음 네트워크 틱**(기본 초당 30번, 2.4절)에 접속해 있는 모든 클라이언트에게 보낸다. 클라이언트는 받은 변경을 자기 목록에 똑같이 적용한다. `UserListPanel`은 목록의 주인이 아니라 복사본을 읽을 뿐이고, 진짜 목록은 서버에만 있다.

같은 순간에 `ToAll()`이 부른 `LineToAllRpc`는 목록과 별개의 경로로 간다. 이 호출은 보낼 메시지 대기열에 쌓였다가 그 프레임의 끝에 클라이언트들에게 전송되고, 클라이언트는 다음 프레임의 맨 앞에서 받아 본문을 실행한다. 본문이 `OnLog` 이벤트를 일으키고 `ChatLogPanel`이 한 줄을 추가한다. 이 두 경로가 서로 독립이므로 목록이 먼저 갱신될 수도 있고 알림이 먼저 나타날 수도 있다. 이 절의 코드는 둘의 순서에 의존하지 않는다.

늦게 접속한 클라이언트가 목록을 바로 받는 것은 2.8.4절에서 본 NetworkVariable의 초기값 동기화와 같은 원리이다. 서버가 새 클라이언트에게 오브젝트를 스폰시킬 때 NetworkVariable과 NetworkList의 현재 값을 함께 보내므로, 그 클라이언트의 `OnNetworkSpawn()`이 불리는 시점에는 목록이 이미 채워져 있다. RPC는 호출된 순간에 접속해 있던 사람에게만 가므로 이 방법으로는 이전의 알림을 전달할 수 없다.

### 2.11.4 실습 9: 채팅, 귓속말, 공지

**목표.** 전체 채팅, 같은 채널의 채팅, 귓속말을 보낸다. 서버는 보낸 사람을 확인하고 내용을 검사한 뒤 알맞은 사람에게만 전달한다. 서버(호스트)는 공지를 보낼 수 있다.

**먼저 알아 둘 것.**

채팅 한 줄이 지나가는 길은 그림 2-13과 같다.

![그림 2-13 채팅 한 줄이 전달되는 길](images/ch02_fig13_chat_path.png)

이 길을 설계하는 원칙이 세 가지 있다. 2.9.5절의 RPC 설계 지침을 채팅에 적용한 것이다.

- **클라이언트는 "무엇을 보낼지"만 요청한다.** 요청에는 글자만 담고 보낸 사람의 이름은 담지 않는다. 보낸 사람은 서버가 `rpcParams.Receive.SenderClientId`로 알아낸다. 이 값은 서버가 채우므로 클라이언트가 속일 수 없다. 이름까지 클라이언트가 보내게 하면 남의 이름으로 말하는 것이 가능해진다.
- **서버가 검사한다.** 목록에 있는 접속자인가, 내용이 비어 있지 않은가, 너무 길지 않은가, 줄바꿈 문자로 가짜 줄을 만들려 하지 않는가를 서버가 확인한다. 클라이언트의 화면에서 이미 막았더라도 서버는 다시 검사한다. 고친 클라이언트는 화면의 제한을 지키지 않기 때문이다.
- **서버가 누구에게 보낼지 정한다.** 전체는 모든 클라이언트에게, 채널은 같은 채널의 접속자에게, 귓속말은 보낸 사람과 받는 사람에게만 간다. 이 결정을 클라이언트에게 맡기면 안 된다. `SendTo.Everyone` 같은 대상을 클라이언트가 직접 쓰면 서버는 아무 검사 없이 전달만 하게 된다.

그래서 각 채팅 종류마다 RPC가 두 개씩 필요하다. 클라이언트가 서버에게 요청하는 RPC(`SendTo.Server`)와, 서버가 대상에게 전달하는 RPC(2.11.3절의 `LineToAllRpc`, `LineToSomeRpc`)이다. 전달하는 쪽은 앞 실습에서 이미 만들었다.

**단계.**

1. `Assets/Scripts`에 `ChatHub.Chat.cs`를 만든다. `ChatHub`의 두 번째 부분이다. 클래스 선언에 `: NetworkBehaviour`를 적지 않는 점에 주의한다.

```csharp
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// ChatHub의 일부: 채팅 보내기(전체, 채널, 귓속말)와 공지
public partial class ChatHub
{
    const int MaxChatLength = 200;      // 한 줄의 최대 글자 수

    // ---------- 화면이 부르는 함수 ----------

    public void SendChat(string text)                       { if (IsSpawned) SendChatRpc(text); }
    public void SendChannelChat(string text)                { if (IsSpawned) SendChannelChatRpc(text); }
    public void SendWhisper(ulong targetId, string text)    { if (IsSpawned) SendWhisperRpc(targetId, text); }

    // 서버(호스트)가 모든 접속자에게 공지를 보낸다.
    public void Announce(string text)
    {
        if (!IsServer) return;
        text = text.Trim();
        if (text.Length == 0) return;
        ToAll("[공지] " + text);
    }

    // ---------- 서버에서 실행되는 RPC 본문 ----------

    // 전체 채팅
    [Rpc(SendTo.Server)]
    void SendChatRpc(string text, RpcParams rpcParams = default)
    {
        if (!CheckChat(rpcParams.Receive.SenderClientId, ref text, out UserState me)) return;
        ToAll($"{me.Name}: {text}");
    }

    // 채널 채팅: 보낸 사람이 들어가 있는 채널의 접속자에게만 전달한다
    [Rpc(SendTo.Server)]
    void SendChannelChatRpc(string text, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CheckChat(sender, ref text, out UserState me)) return;
        if (me.Channel.Length == 0)
        {
            ToOne(sender, "채널에 들어가 있지 않다. 먼저 채널에 입장한다.");
            return;
        }

        ToGroup(MembersOf(me.Channel), $"[{me.Channel}] {me.Name}: {text}");
    }

    // 귓속말: 받는 사람과 보낸 사람에게만 전달한다
    [Rpc(SendTo.Server)]
    void SendWhisperRpc(ulong targetId, string text, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CheckChat(sender, ref text, out UserState me)) return;
        if (targetId == sender || !TryGetUser(targetId, out UserState target))
        {
            ToOne(sender, "귓속말을 받을 사람이 올바르지 않다.");
            return;
        }

        var members = new List<ulong> { sender, targetId };
        ToGroup(members, $"[귓속말] {me.Name} → {target.Name}: {text}");
    }

    // 채팅 요청을 검사한다. 통과하면 true를 돌려주고, text는 다듬어진 문장이 된다.
    bool CheckChat(ulong sender, ref string text, out UserState me)
    {
        if (!TryGetUser(sender, out me)) return false;      // 목록에 없는 사람의 요청은 버린다

        text = (text ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();   // 줄바꿈으로 가짜 줄을 만들지 못하게 한다
        if (text.Length == 0) return false;
        if (text.Length > MaxChatLength) text = text.Substring(0, MaxChatLength);
        return true;
    }
}
```

2. 같은 폴더에 `ChatInputPanel.cs`를 만든다. 보낼 곳을 고르고 한 줄을 보내는 입력 창이다.

```csharp
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 채팅 입력 창: 보낼 곳(전체, 채널, 귓속말)을 고르고 한 줄을 보낸다. 서버(호스트)에는 공지 입력칸이 더 나온다.
public class ChatInputPanel : MonoBehaviour
{
    const int MaxLength = 200;
    static readonly string[] Modes = { "전체", "채널", "귓속말" };

    int mode;                               // 0 전체, 1 채널, 2 귓속말
    ulong whisperTo = ulong.MaxValue;       // 귓속말을 받을 사람의 ClientId
    string input = "";
    string announce = "";
    Action pending;                         // 눌린 버튼이 시킨 일. OnGUI는 기록만 하고 Update에서 실행한다

    void Update()
    {
        if (pending == null) return;
        Action job = pending;
        pending = null;
        job();
    }

    void OnGUI()
    {
        UiKit.Apply();
        ChatHub hub = ChatHub.Instance;
        NetworkManager nm = NetworkManager.Singleton;
        if (hub == null || nm == null) return;

        GUILayout.BeginArea(new Rect(10, Screen.height - 305, 550, 150), GUI.skin.box);

        if (hub.TryGetUser(nm.LocalClientId, out _))
            DrawChatInput(hub, nm.LocalClientId);
        else
            GUILayout.Label("이 실행은 접속자가 아니다(Server 모드). 공지만 보낼 수 있다.");

        if (nm.IsServer) DrawAnnounce(hub);
        GUILayout.EndArea();
    }

    void DrawChatInput(ChatHub hub, ulong myId)
    {
        int picked = GUILayout.Toolbar(mode, Modes);
        if (picked != mode) pending = () => mode = picked;      // 화면 구성이 달라지는 값이므로 Update에서 바꾼다
        if (mode == 2) DrawWhisperTarget(hub, myId);

        // Enter 키는 입력 칸을 그리기 전에 확인한다.
        Event e = Event.current;
        bool enter = e.type == EventType.KeyDown
                     && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                     && GUI.GetNameOfFocusedControl() == "ChatInput";

        GUILayout.BeginHorizontal();
        GUI.SetNextControlName("ChatInput");
        input = GUILayout.TextField(input, MaxLength);
        bool clicked = GUILayout.Button("보내기", GUILayout.Width(80));
        GUILayout.EndHorizontal();

        if (clicked || enter)
        {
            string text = input;
            int sendMode = mode;
            ulong target = whisperTo;
            input = "";
            pending = () => Send(hub, sendMode, target, text);
        }
    }

    // 나를 뺀 접속자의 이름을 버튼 줄로 보여 주고 한 명을 고르게 한다.
    void DrawWhisperTarget(ChatHub hub, ulong myId)
    {
        var ids = new List<ulong>();
        var names = new List<string>();
        for (int i = 0; i < hub.Users.Count; i++)
        {
            UserState u = hub.Users[i];
            if (u.Id == myId) continue;
            ids.Add(u.Id);
            names.Add(u.Name.ToString());
        }

        if (ids.Count == 0)
        {
            GUILayout.Label("귓속말을 보낼 다른 접속자가 없다");
            return;
        }

        int selected = Mathf.Max(0, ids.IndexOf(whisperTo));    // 고른 사람이 나갔으면 첫 번째 사람
        selected = GUILayout.SelectionGrid(selected, names.ToArray(), Mathf.Min(names.Count, 4));
        whisperTo = ids[selected];
    }

    void DrawAnnounce(ChatHub hub)
    {
        GUILayout.BeginHorizontal();
        announce = GUILayout.TextField(announce, MaxLength);
        if (GUILayout.Button("공지", GUILayout.Width(80)))
        {
            string text = announce;
            announce = "";
            pending = () => hub.Announce(text);
        }
        GUILayout.EndHorizontal();
    }

    static void Send(ChatHub hub, int sendMode, ulong target, string text)
    {
        if (text.Trim().Length == 0) return;
        if (sendMode == 0) hub.SendChat(text);
        else if (sendMode == 1) hub.SendChannelChat(text);
        else hub.SendWhisper(target, text);
    }
}
```

3. `ChatInputPanel.cs`를 `NetStarter` 오브젝트에 붙인다. 씬을 저장하고 코드가 바뀌었으므로 빌드를 다시 한다.

**코드를 읽는 법.**

- `SendChat()`, `SendChannelChat()`, `SendWhisper()`는 화면이 부르는 일반 함수이다. 안에서 RPC를 부른다. RPC를 함수로 한 겹 감싸 두면 화면 코드가 RPC나 NGO를 몰라도 되고, 스폰되기 전에 호출되는 실수(2.9.7절의 `RpcException`)를 `IsSpawned` 검사 한 줄로 막을 수 있다.
- RPC 본문 세 개는 모두 `[Rpc(SendTo.Server)]`이다. `InvokePermission`을 쓰지 않았으므로 기본값 `Everyone`이다. `ChatHub`는 서버가 소유한 오브젝트이므로 클라이언트는 소유자가 아니고, 따라서 `Owner`로 막으면 아무도 부를 수 없다.
- `CheckChat()`이 검사를 한곳에 모았다. `ref string text`는 호출한 쪽의 변수를 직접 고치겠다는 뜻이고, `out UserState me`는 값을 돌려받겠다는 뜻이다(C++의 참조 인자와 비슷하다). 검사를 통과하면 `text`는 줄바꿈이 공백으로 바뀌고 앞뒤 공백이 지워지고 200자로 잘린 문장이 되어 있다.
- `ToGroup(members, …)`가 채널과 귓속말 둘 다 처리한다. 둘의 차이는 대상 목록을 어떻게 만드느냐뿐이다. 채널은 같은 채널의 모든 접속자, 귓속말은 보낸 사람과 받는 사람이다. 보낸 사람을 목록에 넣는 이유는 자기가 보낸 글도 자기 화면에 보이게 하려는 것이다.
- `ChatInputPanel`의 `pending`은 2.5.4절에서 설명한 규칙이다. 눌린 일은 적어 두기만 하고 `Update()`에서 실행한다. 여기서는 화면 구성이 달라지는 값(`mode`)을 바꾸는 일도 같은 방법으로 미룬다. 전환 버튼을 누른 같은 프레임에 귓속말 받는 사람 줄이 갑자기 생기면 그 프레임의 그리기 순서가 어긋나기 때문이다.
- `GUILayout.Toolbar`는 버튼이 가로로 늘어선 선택 줄을 그리고 고른 번호를 돌려준다. `GUILayout.SelectionGrid`는 같은 일을 격자로 한다. 둘 다 "지금 선택된 번호"를 받아서 "새로 선택된 번호"를 돌려주는 방식이다.
- `GUI.SetNextControlName("ChatInput")`은 바로 다음에 그리는 컨트롤에 이름을 붙인다. `GUI.GetNameOfFocusedControl()`로 키보드 입력을 받고 있는 컨트롤의 이름을 알 수 있어서, "입력 칸에 포커스가 있는 상태에서 Enter를 눌렀다"를 알아낸다. 보내기 버튼을 마우스로 눌러서 보내면 포커스가 입력 칸을 떠나므로 다시 입력 칸을 눌러야 한다.
- `hub.TryGetUser(nm.LocalClientId, out _)`가 false이면 이 실행은 접속자 목록에 없는 것이다. Server 모드가 그렇다. 이 경우 채팅 입력 대신 안내문만 보이고 공지 입력칸만 나온다.
- `Announce()`는 서버(호스트)만 부른다. 서버는 요청을 기다리는 쪽이기만 한 것이 아니라 스스로 메시지를 보낼 수도 있다는 것을 보여 준다.

**확인할 것.**

호스트 하나(`교수`)와 클라이언트 둘(`철수`, `영희`)을 접속시켜 놓고 시작한다.

1. 철수의 입력 칸에 `안녕하세요`를 입력하고 **보내기**를 누른다(또는 Enter). 철수, 영희, 교수 화면의 로그에 모두 `철수: 안녕하세요`가 한 줄씩 나타난다. 보낸 철수의 화면에도 나타난다. 호스트의 Console에 `[서버] 철수: 안녕하세요`가 기록된다.
2. 호스트(교수)에서도 한 줄 보낸다. 모든 화면에 `교수: …`가 나타난다. 호스트도 같은 `SendChatRpc`를 부르며, 호스트는 서버이므로 본문이 그 자리에서 실행된다.
3. 철수가 **귓속말** 탭을 누른다. 받는 사람 줄에 `교수`, `영희`가 나타난다. `영희`를 고르고 `비밀이야`를 보낸다. 철수와 영희의 로그에 `[귓속말] 철수 → 영희: 비밀이야`가 나타난다. **교수 화면에는 나타나지 않는다.** 단, 호스트의 Console에는 `[서버] [귓속말] 철수 → 영희: 비밀이야`가 기록된다. 서버는 모든 대화를 볼 수 있다.
4. 철수가 **채널** 탭에서 한 줄 보낸다. 아직 채널에 들어가 있지 않으므로 철수의 화면에만 `채널에 들어가 있지 않다. 먼저 채널에 입장한다.`가 나타난다. 채널 채팅은 다음 실습에서 시험한다.
5. 호스트 화면에서 아래쪽 공지 입력칸에 `5분 뒤 점검합니다`를 쓰고 **공지**를 누른다. 모든 화면에 `[공지] 5분 뒤 점검합니다`가 나타난다. 클라이언트 화면에는 공지 입력칸이 없다.
6. 빈 칸이나 공백만 입력하고 보낸다. 아무 일도 일어나지 않는다. 서버가 빈 문장을 버린 것이다.
7. 영희를 종료했다가 같은 이름으로 다시 접속시킨다. 접속한 영희의 로그는 비어 있다(이전의 대화는 오지 않는다). 철수가 새로 한 줄 보내면 영희에게도 보인다.
8. **Server**(서버만)로 시작해 보면, 서버 화면에서는 채팅 입력 대신 `이 실행은 접속자가 아니다(Server 모드). 공지만 보낼 수 있다.`와 공지 입력칸이 나온다. 클라이언트들의 대화는 서버 화면의 로그에도 모두 나타난다.

> 입력 칸에서 `ㅁ`, `ㅏ` 같은 한글 조합이 늦게 보이는 경우가 있다. IMGUI의 한글 입력은 에디터와 환경에 따라 불안정할 수 있으므로(2.5.8절), 입력이 이상하면 영문으로 시험한다.

**그 뒤에서 일어난 일.**

철수가 `안녕하세요`를 보내는 순간을 그림 2-13과 함께 따라가 본다.

1. 철수의 `ChatInputPanel`이 입력 내용을 `pending`에 적어 둔다. 다음 `Update()`에서 `hub.SendChat()`이 실행되고 `SendChatRpc`가 호출된다. 메시지는 철수 컴퓨터의 보낼 대기열에 쌓인다.
2. 그 프레임의 끝에 대기열이 비워지면서 메시지가 서버로 전송된다.
3. 서버는 다음 프레임의 맨 앞에서 메시지를 받는다. 이때 NGO가 "이 메시지는 ClientId 1의 연결에서 왔다"는 사실을 `rpcParams.Receive.SenderClientId`에 채워 준다. 이 값은 메시지 안에 들어 있는 것이 아니라 서버가 연결에서 직접 알아낸 것이다.
4. `SendChatRpc` 본문이 `CheckChat()`으로 검사하고, 목록에서 ClientId 1의 이름 `철수`를 찾아 `철수: 안녕하세요`를 만든다. `ToAll()`이 `LineToAllRpc`를 호출한다.
5. `ClientsAndHost`로 정해진 이 RPC는 접속해 있는 모든 클라이언트(철수 포함)에게 전송되고, 서버가 호스트이면 호스트 자신에게는 메시지 없이 그 자리에서 실행된다.
6. 각 컴퓨터는 다음 프레임에 메시지를 받아 `Emit()`을 실행하고, `ChatLogPanel`이 줄을 추가한다.

그래서 철수가 보낸 글이 철수 자신의 화면에 나타나기까지도 서버를 한 번 다녀온다. 두 번의 전송과 프레임 경계 몇 개가 들어 있으므로 지연이 큰 환경에서는 자기가 보낸 글이 늦게 나타난다. 이것이 서버가 모든 것을 정하는 구조의 대가이다. 채팅에서는 문제가 되지 않지만, 캐릭터 이동처럼 반응이 즉각적이어야 하는 기능에서는 이 지연을 숨기는 기법(2.16절)이 필요하다.

귓속말과 채널은 4번 이후만 다르다. `ToGroup()`이 `LineToSomeRpc`를 `RpcTarget.Group(…)`으로 호출하면, 서버가 그 목록에 있는 클라이언트에게만 개별 메시지를 보낸다. 목록에 없는 클라이언트는 메시지를 아예 받지 않는다. 서버가 보내지 않은 것이므로 클라이언트 프로그램을 고쳐도 볼 방법이 없다. 이것이 "누구에게 보낼지를 서버가 정해야 하는" 이유이다. 만약 귓속말을 `SendTo.Everyone`으로 보내고 받는 쪽 화면에서 "내 것이 아니면 숨기기"로 구현했다면, 모든 클라이언트가 귓속말을 받아 놓고 화면에서만 숨기는 것이므로 고친 클라이언트는 남의 귓속말을 읽을 수 있다.

### 2.11.5 실습 10: 채널 만들기, 들어가기, 나가기

**목표.** 접속자가 채널(방)을 만들고, 들어가고, 나온다. 채널의 알림과 채팅은 같은 채널에 있는 사람에게만 전달되고, 채널 목록과 인원 수는 모든 화면에 나타난다. 실습 9에서 미뤄 둔 채널 채팅도 이 실습에서 확인한다.

**먼저 알아 둘 것.**

이 실습의 핵심은 채널을 어떻게 저장하느냐이다. 흔한 방법은 서버가 `채널 이름 → 접속자 목록` 표(`Dictionary<string, List<ulong>>`)를 따로 두는 것이다. 이 방법은 두 가지가 번거롭다.

- 이 표는 서버의 메모리에만 있다. 클라이언트 화면이 채널 목록을 보여 주려면 표가 바뀔 때마다 내용을 따로 전달해야 한다. 서버의 자료구조를 클라이언트에서 읽으려고 하면 값이 없다.
- 접속자가 나가면 표에서도 지워야 한다. 접속 종료 이벤트를 받는 코드를 빠뜨리면 이미 나간 접속자가 채널에 남는다.

이 교재는 다른 방법을 쓴다. **채널을 따로 저장하지 않는다.** 실습 8에서 `UserState`에 넣어 둔 `Channel` 칸이 곧 채널이다. 철수의 `Channel`이 `대기실`이면 철수는 대기실에 있는 것이다. `Channel`이 `대기실`인 접속자가 한 명이라도 있으면 대기실이라는 채널이 있는 것이고, 한 명도 없으면 그 채널은 없는 것이다.

이렇게 하면 앞의 두 문제가 사라진다. 접속자 목록은 이미 모든 클라이언트에 복제되므로 채널 목록을 따로 보낼 필요가 없고, 접속자가 연결을 끊으면 실습 8의 `OnClientDisconnected()`가 목록에서 지우면서 채널의 구성원에서도 함께 빠진다. 마지막 사람이 나가면 채널은 저절로 사라진다. 단점은 채널에 비밀번호나 방장 같은 속성을 붙일 곳이 없다는 것이다. 그런 속성이 필요하면 채널 정보를 담는 별도의 목록이 필요하다(연습 문제).

채널의 규칙은 다음과 같다.

- **만들기**: 같은 이름의 채널이 이미 있으면 거절한다. 만들면 그 채널에 바로 들어간다.
- **입장**: 그 이름의 채널이 없으면 거절한다.
- **나가기**: 로비(어느 채널에도 없는 상태)로 돌아간다.
- 이미 다른 채널에 있는 사람이 만들기나 입장을 하면 먼저 있던 채널에서 자동으로 나온다. 한 사람은 한 번에 한 채널에만 있다.
- 채널 이름은 1~16자이다. `로비`는 화면에서 "어느 채널에도 없음"을 가리키는 이름이므로 채널 이름으로 쓸 수 없다.
- 요청이 거절되면 서버는 요청한 사람에게만 이유를 알려 준다.

**단계.**

1. `Assets/Scripts`에 `ChatHub.Channel.cs`를 만든다. `ChatHub`의 세 번째 부분이다.

```csharp
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

// ChatHub의 일부: 채널 만들기, 들어가기, 나가기
// 채널을 따로 저장하지 않는다. 접속자 목록(users)의 Channel 칸이 곧 채널이다.
public partial class ChatHub
{
    const int MaxChannelNameLength = 16;
    const string LobbyName = "로비";      // 화면에서 "어느 채널에도 없음"을 가리키는 이름. 채널 이름으로는 쓸 수 없다.

    // ---------- 화면이 부르는 함수 ----------

    public void CreateChannel(string channelName) { if (IsSpawned) CreateChannelRpc(channelName); }
    public void JoinChannel(string channelName)   { if (IsSpawned) JoinChannelRpc(channelName); }
    public void LeaveChannel()                    { if (IsSpawned) LeaveChannelRpc(); }

    // 만들어진 채널의 이름과 인원 수. 접속자 목록에서 계산하므로 클라이언트도 부를 수 있다.
    public Dictionary<string, int> GetChannels()
    {
        var result = new Dictionary<string, int>();
        for (int i = 0; i < users.Count; i++)
        {
            if (users[i].Channel.Length == 0) continue;         // 로비에 있는 사람은 채널이 아니다
            string channelName = users[i].Channel.ToString();
            result.TryGetValue(channelName, out int count);
            result[channelName] = count + 1;
        }
        return result;
    }

    // ---------- 서버에서 실행되는 RPC 본문 ----------

    [Rpc(SendTo.Server)]
    void CreateChannelRpc(string channelName, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CheckChannelName(sender, ref channelName)) return;

        if (ChannelExists(ToFixed(channelName)))
        {
            ToOne(sender, $"이미 있는 채널이다: {channelName}");
            return;
        }
        MoveToChannel(sender, channelName);
    }

    [Rpc(SendTo.Server)]
    void JoinChannelRpc(string channelName, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CheckChannelName(sender, ref channelName)) return;

        if (!ChannelExists(ToFixed(channelName)))
        {
            ToOne(sender, $"없는 채널이다: {channelName}");
            return;
        }
        MoveToChannel(sender, channelName);
    }

    [Rpc(SendTo.Server)]
    void LeaveChannelRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!TryGetUser(sender, out UserState me)) return;

        if (me.Channel.Length == 0)
        {
            ToOne(sender, "채널에 들어가 있지 않다.");
            return;
        }
        MoveToChannel(sender, "");                              // 빈 문자열은 로비
    }

    // ---------- 서버 안에서 쓰는 도우미 ----------

    // 채널 이름을 다듬고 검사한다. 통과하면 true를 돌려주고, channelName은 다듬어진 이름이 된다.
    bool CheckChannelName(ulong sender, ref string channelName)
    {
        if (!TryGetUser(sender, out _)) return false;           // 목록에 없는 사람의 요청은 버린다

        channelName = (channelName ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (channelName.Length == 0 || channelName.Length > MaxChannelNameLength || channelName == LobbyName)
        {
            ToOne(sender, $"채널 이름은 1~{MaxChannelNameLength}자이고 '{LobbyName}'는 쓸 수 없다.");
            return false;
        }
        return true;
    }

    // 그 이름을 가진 접속자가 한 명이라도 있으면 채널이 있는 것이다.
    bool ChannelExists(FixedString64Bytes channel)
    {
        for (int i = 0; i < users.Count; i++)
            if (users[i].Channel.Equals(channel)) return true;
        return false;
    }

    // 접속자를 다른 채널로 옮기고 관련된 사람들에게 알린다. 로비로 보낼 때는 빈 문자열을 넘긴다.
    void MoveToChannel(ulong clientId, string newChannel)
    {
        int i = IndexOf(clientId);
        if (i < 0) return;

        UserState u = users[i];
        FixedString64Bytes before = u.Channel;
        FixedString64Bytes after = ToFixed(newChannel);

        if (before.Equals(after))
        {
            ToOne(clientId, "이미 그 채널에 있다.");
            return;
        }

        u.Channel = after;
        users[i] = u;                                           // 꺼내서 고친 뒤 다시 대입해야 변화가 전달된다

        if (before.Length > 0)                                  // 나온 채널에 남은 사람들에게
            ToGroup(MembersOf(before), $"* {u.Name} 님이 [{before}] 채널에서 나갔다");

        if (after.Length > 0)                                   // 들어간 채널의 사람들에게(들어온 본인 포함)
            ToGroup(MembersOf(after), $"* {u.Name} 님이 [{after}] 채널에 들어왔다");
        else
            ToOne(clientId, $"[{before}] 채널에서 나왔다. 지금은 로비이다.");
    }
}
```

2. 같은 폴더에 `ChannelPanel.cs`를 만든다. 채널 이름을 쓰고 세 버튼을 누르는 화면이다. Game 창 가운데 오른쪽에 그려진다.

```csharp
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 채널 화면: 채널 이름을 쓰고 만들기, 입장, 나가기를 누른다. 만들어진 채널과 인원 수를 보여 준다.
public class ChannelPanel : MonoBehaviour
{
    string channelName = "";
    Action pending;                         // 눌린 버튼이 시킨 일. OnGUI는 기록만 하고 Update에서 실행한다

    void Update()
    {
        if (pending == null) return;
        Action job = pending;
        pending = null;
        job();
    }

    void OnGUI()
    {
        UiKit.Apply();
        ChatHub hub = ChatHub.Instance;
        NetworkManager nm = NetworkManager.Singleton;
        if (hub == null || nm == null) return;
        if (!hub.TryGetUser(nm.LocalClientId, out UserState me)) return;    // 접속자가 아니면(Server 모드) 그리지 않는다

        GUILayout.BeginArea(new Rect(570, 175, 350, 185), GUI.skin.box);

        GUILayout.Label("내 채널: " + (me.Channel.Length == 0 ? "로비" : me.Channel.ToString()));
        channelName = GUILayout.TextField(channelName, 16);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("만들기")) { string n = channelName; pending = () => hub.CreateChannel(n); }
        if (GUILayout.Button("입장"))   { string n = channelName; pending = () => hub.JoinChannel(n); }
        if (GUILayout.Button("나가기")) pending = hub.LeaveChannel;
        GUILayout.EndHorizontal();

        var parts = new List<string>();
        foreach (KeyValuePair<string, int> channel in hub.GetChannels())
            parts.Add($"{channel.Key}({channel.Value}명)");
        GUILayout.Label(parts.Count == 0 ? "만들어진 채널이 없다" : "만들어진 채널: " + string.Join(", ", parts));

        GUILayout.EndArea();
    }
}
```

3. `ChannelPanel.cs`를 `NetStarter` 오브젝트에 붙인다. 씬을 저장하고 코드가 바뀌었으므로 빌드를 다시 한다.

**코드를 읽는 법.**

- 클라이언트의 요청은 세 가지이고 RPC도 세 개이다(`CreateChannelRpc`, `JoinChannelRpc`, `LeaveChannelRpc`). 요청마다 RPC를 따로 두면 서버가 요청의 뜻을 따로 확인할 필요가 없고, 클라이언트가 보내는 값은 채널 이름 하나뿐이다. "이 사람을 이 채널로 옮겨라"처럼 대상까지 클라이언트가 정하게 하는 RPC는 만들지 않았다. 옮겨지는 사람은 언제나 요청을 보낸 사람(`SenderClientId`)이다.
- `CheckChannelName()`은 실습 9의 `CheckChat()`과 같은 역할이다. 이름의 줄바꿈을 공백으로 바꾸고, 앞뒤 공백을 지우고, 길이와 `로비`를 검사한다. 이름이 16자를 넘지 않는다는 것은 `ToFixed()`가 안전하게 `FixedString64Bytes`로 바꿀 수 있다는 뜻이기도 하다. 한글은 한 글자가 3바이트이고, 16자는 48바이트이므로 61바이트 안에 들어간다.
- `MoveToChannel()`이 이 실습의 중심이다. 하는 일은 두 가지이다. 첫째, `users[i]`를 꺼내서 `Channel`을 바꾸고 다시 대입한다. 이 한 줄이 모든 화면의 채널 정보를 바꾼다. 둘째, 나온 채널의 남은 사람들과 들어간 채널의 사람들에게 알림을 보낸다. 알림은 `ToGroup()`이므로 그 채널의 구성원에게만 간다.
- `MembersOf()`는 바꾼 **뒤의** 목록으로 구성원을 찾는다. 그래서 나온 채널의 구성원에는 방금 나간 사람이 빠져 있고, 들어간 채널의 구성원에는 들어온 사람이 들어 있다. 나온 채널에 남은 사람이 없으면 목록이 비어 있고, 빈 목록으로 보낸 RPC는 아무에게도 전송되지 않는다.
- `ChannelExists()`와 `GetChannels()`는 `users`를 읽기만 한다. `GetChannels()`는 `public`이고 서버가 아니라 화면이 쓰는 함수이다. 채널 목록을 따로 전달하지 않아도 클라이언트가 접속자 목록에서 채널을 계산할 수 있기 때문에 가능하다.
- `ChannelPanel`이 보여 주는 `내 채널`은 내가 누른 결과가 아니라 **서버가 목록에 쓴 값**이다. 만들기를 누르고 서버에 다녀오는 동안(보통 수십 ms 이하) 화면은 이전 값 그대로이다. 서버가 거절하면 계속 이전 값이다. 화면은 서버가 정한 결과만 보여 준다.
- 이름 입력칸은 `GUILayout.TextField(channelName, 16)`의 두 번째 인자로 16자까지만 입력되게 했다. 화면에서 막는 것은 편의일 뿐이고, 진짜 검사는 서버의 `CheckChannelName()`이 한다.

**확인할 것.**

호스트 `교수`와 클라이언트 `철수`, `영희`를 접속시켜 놓고 시작한다.

1. 철수 화면의 채널 이름 칸에 `대기실`을 쓰고 **만들기**를 누른다. 철수 화면의 로그에 `* 철수 님이 [대기실] 채널에 들어왔다`가 나타나고, 채널 패널은 `내 채널: 대기실`, `만들어진 채널: 대기실(1명)`이 된다. **세 화면 모두**의 접속자 목록에서 철수의 채널 칸이 `대기실`로 바뀌고, 채널 패널의 `만들어진 채널`에도 `대기실(1명)`이 나타난다. 그러나 영희와 교수의 로그에는 아무 줄도 나타나지 않는다. 대기실에 있는 사람이 철수뿐이기 때문이다. 교수의 Console에는 `[서버] * 철수 님이 [대기실] 채널에 들어왔다`가 기록된다.
2. 영희가 `대기실`을 쓰고 **입장**을 누른다. 철수와 영희의 로그에 `* 영희 님이 [대기실] 채널에 들어왔다`가 나타나고, 교수의 로그에는 나타나지 않는다. 채널 목록은 `대기실(2명)`이 된다.
3. 철수가 채팅 입력 창의 **채널** 탭에서 `안녕`을 보낸다. 철수와 영희의 로그에 `[대기실] 철수: 안녕`이 나타난다. 교수 화면에는 나타나지 않는다. 이어서 영희가 **전체** 탭에서 한 줄 보내면 교수에게도 보인다.
4. 교수가 `던전`을 **만들기**한다. 영희가 `던전`을 **입장**한다. 영희는 대기실에서 자동으로 나와 던전으로 옮겨진다. 철수의 로그에 `* 영희 님이 [대기실] 채널에서 나갔다`, 교수와 영희의 로그에 `* 영희 님이 [던전] 채널에 들어왔다`가 나타난다. 채널 목록은 `대기실(1명), 던전(2명)`이다.
5. 철수가 이미 있는 `던전`을 **만들기**한다. 철수의 로그에만 `이미 있는 채널이다: 던전`이 나타난다. 이어서 없는 `광장`을 **입장**한다. `없는 채널이다: 광장`이 철수의 로그에만 나타난다. 이름 칸을 비우고 눌러도 철수의 로그에만 이름 규칙 안내가 나타난다. 서버의 Console에는 오류가 없다. 존재하지 않는 채널에 입장하려는 요청은 서버가 아무것도 바꾸지 않고 요청한 사람에게만 알린다.
6. 철수가 **나가기**를 누른다. `[대기실] 채널에서 나왔다. 지금은 로비이다.`가 나타나고 접속자 목록의 철수는 `로비`가 된다. 대기실에는 이제 아무도 없으므로 채널 목록에서 `대기실`이 사라진다. 채널을 지우는 코드는 어디에도 없다. 이름을 가진 사람이 없으면 채널이 없는 것이다.
7. 영희를 **종료**한다. 교수의 로그에 `* 영희 퇴장`이 나타나고 던전의 인원이 줄어든다. 접속이 끊긴 사람이 채널에 남지 않는다.
8. 새 클라이언트 `민수`를 접속시킨다. 접속하자마자 채널 패널에 현재의 채널 목록이 나타난다. 접속자 목록과 같은 원리로 늦게 온 사람도 현재의 상태를 받기 때문이다. 로그에는 이전에 있었던 입장·퇴장 알림이 오지 않는다.

**그 뒤에서 일어난 일.**

영희가 `대기실`에 입장을 눌렀을 때 네트워크에 오간 것을 순서대로 따라가 본다.

1. 영희의 `ChannelPanel`이 `pending`에 일을 적고, 다음 `Update()`에서 `JoinChannel("대기실")`이 실행된다. `JoinChannelRpc`가 서버로 전송된다. 이 메시지에 담긴 것은 `"대기실"` 한 줄이다.
2. 서버는 보낸 사람이 영희임을 `SenderClientId`로 확인하고, 이름을 검사하고, 대기실이 있는지 확인한 다음 `MoveToChannel()`을 실행한다. 서버의 `users[i] = u`는 서버 쪽 목록을 그 자리에서 바꾸고 "i번 칸이 이 값으로 바뀌었다"는 변경을 기록해 둔다.
3. 다음 네트워크 틱에 그 변경이 접속해 있는 **모든** 클라이언트에게 간다. NetworkList는 목록 전체가 아니라 바뀐 칸 하나(번호와 새 값)만 보낸다. 교수, 철수, 영희의 화면이 모두 갱신된다.
4. 같은 순간 `ToGroup()`이 철수와 영희에게만 알림을 보낸다. 이 알림은 목록과 별개의 메시지이고, 교수에게는 전송되지 않는다.

세 가지가 서로 다른 길을 갔다. 요청(영희 → 서버, 한 번), 상태 변경(서버 → 모든 클라이언트, 바뀐 칸 하나), 알림(서버 → 관련된 사람들). 이 구분이 중요하다. **상태는 모두가 알아야 하므로 모두에게 보내고, 알림은 관련된 사람에게만 보낸다.** 접속자가 수천 명인 서비스에서는 모두에게 보내는 상태를 최소로 줄이고 관련된 사람에게만 보내는 알림을 늘리는 방향으로 설계한다. 2.17절의 관심 영역이 이 생각을 게임 월드로 넓힌 것이다.

또 하나. 서버가 보내는 메시지의 수는 구성원 수에 비례한다. 100명이 있는 채널에서 한 줄을 말하면 서버는 100개의 메시지를 보낸다. 채팅 한 줄이 작아도 서버의 일은 "받은 한 개 × 구성원 수"만큼 늘어난다. 6장에서 서버를 직접 만들면 이 일을 코드로 직접 하게 된다.

### 2.11.6 실습 11: 체력과 준비 상태

**목표.** 접속자가 자기 체력과 준비 상태를 바꾸는 버튼을 누르면 서버가 검사하고 접속자 목록에 쓰고, 모든 화면의 목록이 갱신된다. 늦게 접속한 사람도 현재 값을 바로 받는 것과, 클라이언트가 목록에 직접 쓰려고 하면 거부되는 것을 확인한다.

**먼저 알아 둘 것.**

- **상태를 바꾸는 길은 하나이다.** 접속자 목록 `users`의 쓰기 권한은 서버(NetworkList의 기본값)이다. 클라이언트가 자기 체력을 바꾸고 싶으면 서버에게 요청하는 RPC를 보내고, 서버가 검사한 뒤 목록에 쓴다. 채팅, 채널과 같은 흐름이다. 요청 → 서버의 검사 → 서버가 상태를 바꿈 → 모든 클라이언트에 복제.
- **이 실습의 버튼은 시험 도구이다.** 실제 게임에서 클라이언트가 "체력을 10 올려 달라"고 요청하지는 않는다. 체력은 피해와 회복 같은 게임 규칙을 서버가 계산한 결과이다. 여기서는 상태가 모든 화면에 동기화되는 모습을 눈으로 보려고 버튼으로 값을 바꾼다. 그래도 서버는 요청값을 믿지 않고 범위를 검사한다.
- **NetworkVariable과 NetworkList의 선택.** 이 교재의 2.8절에서는 플레이어마다 닉네임과 색을 NetworkVariable로 두었다. 이번에는 모든 접속자의 체력과 준비 상태를 NetworkList 하나에 담는다. 기준은 다음과 같다.
  - 값 **하나**는 NetworkVariable이다(라운드 번호, 남은 시간, 게임 단계). 값이 바뀌면 값 전체를 보내고, 같은 틱에 여러 번 바뀌어도 마지막 값 하나만 전달된다.
  - 접속자마다 있는 값의 **모음**은 NetworkList이다(접속자 목록, 방 목록, 인벤토리). 추가, 삭제, 한 칸의 변경이 각각 이벤트가 되어 순서대로 전달되고, 같은 틱에 여러 번 바뀌면 모두 전달된다. 늦게 접속한 사람은 목록 전체를 받는다.
  - 접속자별 값을 플레이어 오브젝트의 NetworkVariable에 두는 설계도 가능하다. 그러면 데이터가 오브젝트와 함께 있어서 그 플레이어의 다른 컴포넌트가 읽기 쉽다. 반대로 NetworkList에 모으면 한곳에서 표로 보여 주기 쉽고 플레이어 오브젝트가 없어도 존재한다. 이 교재의 채팅 검증 도구는 후자를 쓴다. 6장의 C++ 서버가 접속자 정보를 서버의 표 하나에 모아 두는 것과 같은 모양이다.
- 구조체 요소의 필드 하나를 고칠 때는 꺼내서 고친 뒤 다시 대입한다(2.11.3절). 이번 실습에서 `Hp`와 `Ready`를 고칠 때 쓴다.

**단계.**

1. `Assets/Scripts`에 `ChatHub.State.cs`를 만든다. `ChatHub`의 네 번째 부분이다.

```csharp
using Unity.Netcode;
using UnityEngine;

// ChatHub의 일부: 접속자의 체력과 준비 상태
public partial class ChatHub
{
    const int MaxHp = 100;

    // ---------- 화면이 부르는 함수 ----------

    public void ChangeHp(int delta)  { if (IsSpawned) ChangeHpRpc(delta); }
    public void SetReady(bool ready) { if (IsSpawned) SetReadyRpc(ready); }

    // 시험용: 이 컴퓨터에서 접속자 목록에 직접 쓰면 어떻게 되는지 본다.
    public void TryWriteDirectly()
    {
        int i = IndexOf(NetworkManager.LocalClientId);
        if (i < 0) return;

        UserState u = users[i];
        u.Hp = 1;
        users[i] = u;           // 서버(호스트)에서는 바뀌고, 클라이언트에서는 거부된다
    }

    // ---------- 서버에서 실행되는 RPC 본문 ----------

    [Rpc(SendTo.Server)]
    void ChangeHpRpc(int delta, RpcParams rpcParams = default)
    {
        int i = IndexOf(rpcParams.Receive.SenderClientId);
        if (i < 0) return;

        delta = Mathf.Clamp(delta, -MaxHp, MaxHp);          // 요청값의 범위를 먼저 제한한다
        UserState u = users[i];
        u.Hp = Mathf.Clamp(u.Hp + delta, 0, MaxHp);         // 더한 결과도 0~MaxHp 안에 둔다
        users[i] = u;

        Debug.Log($"[서버] {u.Name} 체력 {u.Hp}");
    }

    [Rpc(SendTo.Server)]
    void SetReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        int i = IndexOf(rpcParams.Receive.SenderClientId);
        if (i < 0) return;

        UserState u = users[i];
        u.Ready = ready;
        users[i] = u;

        Debug.Log($"[서버] {u.Name} {(ready ? "준비" : "대기")}");
    }
}
```

2. 같은 폴더에 `StatePanel.cs`를 만든다. 체력과 준비 상태를 바꾸는 화면이다. 채널 패널 아래에 그려진다.

```csharp
using System;
using Unity.Netcode;
using UnityEngine;

// 상태 화면: 내 체력과 준비 상태를 바꾸는 버튼. 화면에 보이는 값은 서버가 목록에 쓴 값이다.
public class StatePanel : MonoBehaviour
{
    Action pending;                         // 눌린 버튼이 시킨 일. OnGUI는 기록만 하고 Update에서 실행한다

    void Update()
    {
        if (pending == null) return;
        Action job = pending;
        pending = null;
        job();
    }

    void OnGUI()
    {
        UiKit.Apply();
        ChatHub hub = ChatHub.Instance;
        NetworkManager nm = NetworkManager.Singleton;
        if (hub == null || nm == null) return;
        if (!hub.TryGetUser(nm.LocalClientId, out UserState me)) return;    // 접속자가 아니면(Server 모드) 그리지 않는다

        GUILayout.BeginArea(new Rect(570, 365, 350, 115), GUI.skin.box);

        GUILayout.Label($"내 상태: 체력 {me.Hp}, {(me.Ready ? "준비" : "대기")}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("체력 -10")) pending = () => hub.ChangeHp(-10);
        if (GUILayout.Button("체력 +10")) pending = () => hub.ChangeHp(10);
        bool ready = GUILayout.Toggle(me.Ready, "준비", GUILayout.Width(70));
        if (ready != me.Ready) pending = () => hub.SetReady(ready);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("목록에 직접 쓰기 (시험)")) pending = hub.TryWriteDirectly;

        GUILayout.EndArea();
    }
}
```

3. `StatePanel.cs`를 `NetStarter` 오브젝트에 붙인다. 씬을 저장하고 코드가 바뀌었으므로 빌드를 다시 한다.

**코드를 읽는 법.**

- 서버의 두 RPC는 같은 순서로 일한다. 보낸 사람을 목록에서 찾고(없으면 버린다), 요청값을 검사하고, 구조체를 꺼내 고친 뒤 다시 대입한다. 마지막 대입이 NetworkList에 변경을 기록하는 부분이다. 필드를 고치기만 하고 대입하지 않으면 서버의 목록도 클라이언트의 목록도 바뀌지 않는다.
- `ChangeHpRpc`에서 `delta`를 먼저 `Clamp`하는 데에는 이유가 있다. 고친 클라이언트가 `int.MaxValue`를 보내면 `u.Hp + delta`는 C#의 `int` 덧셈에서 범위를 넘어 음수로 돌아 버린다. 그러면 결과가 `0`으로 바뀌는 등 의도하지 않은 값이 된다. 더하기 전에 요청값의 범위를 제한하면 이런 일이 없다. 클라이언트가 보낸 숫자는 크기를 가리지 않고 검사한다.
- `StatePanel`의 체크 상자 `준비`는 `me.Ready`, 즉 **서버가 쓴 값**을 그린다. 상자를 누르면 `Toggle`이 새 값을 돌려주지만 그 값을 화면에 그대로 저장하지 않고 서버에 요청만 보낸다. 서버가 목록을 바꿔서 그 변경이 도착해야 체크가 바뀐다. 그래서 체크 상자는 서버를 다녀온 뒤에 바뀐다. 지연이 큰 환경(2.13절의 Network Simulator)에서는 이 시간이 눈에 보인다. 상태는 서버가 정하고, 화면은 그 결과를 그릴 뿐이라는 것을 이 지연이 보여 준다.
- `Debug.Log`로 서버가 바꾼 결과를 서버의 Console에 남긴다. 클라이언트 화면과 서버의 기록을 나란히 놓고 비교할 수 있다.
- `TryWriteDirectly()`는 일부러 서버가 아닌 쪽에서도 목록에 쓴다. 이 함수는 서버인지 확인하지 않는다. 클라이언트에서 실행되면 `users[i] = u`가 NGO에 의해 거부된다.

**확인할 것.**

호스트 `교수`와 클라이언트 `철수`, `영희`를 접속시켜 놓고 시작한다.

1. 철수 화면에서 **체력 -10**을 누른다. 세 화면의 접속자 목록에서 철수의 HP가 `90`이 된다. 교수의 Console에 `[서버] 철수 체력 90`이 기록된다.
2. 버튼을 계속 눌러 본다. 체력은 `0`에서 멈추고, **체력 +10**을 계속 눌러도 `100`에서 멈춘다. 범위를 지키는 곳은 서버이다.
3. 철수가 **준비** 체크 상자를 누른다. 잠시 뒤 체크가 되고, 모든 화면에서 철수의 상태 칸이 `대기`에서 `준비`로 바뀐다. 다시 누르면 `대기`로 돌아온다.
4. 변경을 마친 뒤 새 클라이언트 `민수`를 접속시킨다. 민수의 접속자 목록에 철수의 현재 HP와 `준비`가 **바로** 나타난다. 변경이 일어난 순간에 없었던 사람도 현재의 값을 받는다. 사건을 전달하는 RPC로는 불가능하고, 상태를 복제하는 NetworkList여서 가능한 일이다.
5. 클라이언트(철수)에서 **목록에 직접 쓰기 (시험)** 를 누른다. 철수의 HP는 바뀌지 않는다. 철수 쪽 Console에 빨간 오류가 나타난다.

```text
|Client-1|ChatHub|users| Write permissions (Server) for this client instance is not allowed!
```

   `Client-1`은 이 컴퓨터의 ClientId, `ChatHub`는 게임 오브젝트의 이름, `users`는 필드의 이름이다. 클라이언트가 빌드한 프로그램이면 이 오류는 `Player.log`에 기록된다(2.13절). 에디터의 Console에서 바로 보고 싶으면 이 시험만은 에디터를 **Client**로, 빌드를 **Host**로 바꿔서 한다.
6. 같은 버튼을 **호스트**(교수)에서 누른다. 오류 없이 교수의 HP가 `1`이 된다. 서버는 목록에 쓸 수 있기 때문이다. 같은 코드가 어디에서 실행되느냐에 따라 결과가 다르다. NGO가 쓰기 권한을 검사해 주므로 클라이언트가 실수로, 혹은 고의로 목록을 고치려 해도 서버의 값은 지켜진다.
7. 철수와 영희가 **동시에** 버튼을 연타한다. 두 사람의 요청이 서버에서 하나씩 차례로 처리되므로 값이 서로 어긋나지 않고 각자의 HP가 정확히 오르내린다.

**그 뒤에서 일어난 일.**

철수가 **체력 -10**을 눌렀을 때를 따라가 본다.

1. 철수의 요청 `ChangeHpRpc(-10)`이 서버로 전송된다. 담긴 것은 정수 `-10` 하나이다.
2. 서버가 `users[i] = u`를 실행하면 서버의 목록이 그 자리에서 바뀌고, NetworkList는 "i번 칸의 값이 이렇게 바뀌었다"는 변경을 기록한다.
3. 다음 네트워크 틱에 그 변경이 접속한 모든 클라이언트에게 전송된다. NetworkList가 보내는 것은 목록 전체가 아니라 바뀐 칸 하나(번호와 새 값)이다. 다만 그 칸의 값은 `UserState` **전체**이다. 필드 하나(`Hp`)만 따로 보낼 수는 없다. 구조체가 커질수록 작은 변화 하나의 전송 비용도 커지므로, 자주 바뀌는 값과 거의 바뀌지 않는 큰 값을 같은 구조체에 넣을 때는 이 비용을 생각한다.
4. 같은 틱 안에 철수가 세 번 눌러서 서버가 세 번 고쳤다면 변경 세 개가 순서대로 전송되고 클라이언트는 차례로 적용한다. 같은 틱의 변경을 마지막 값 하나로 합치는 NetworkVariable과 다른 점이다.

서버가 두 사람의 요청을 하나씩 차례로 처리한다는 점도 짚어 둔다. NGO의 RPC 본문은 모두 Unity의 메인 스레드에서 한 번에 하나씩 실행된다. 그래서 이 절의 서버 코드에는 잠금(lock)이 없다. 6장의 C++ 서버에서는 스레드를 어떻게 구성하느냐에 따라 여러 요청이 같은 상태에 동시에 접근할 수 있고, 그것을 막는 것은 직접 해야 하는 일이다.

### 2.11.7 실습 12: 보너스 요청과 응답

**목표.** 접속자가 "보너스 받기"를 요청하면 서버가 접속 중 한 번만 지급하고, 결과를 **요청한 사람에게만** 알려 준다. 게임 서버의 가장 흔한 형태인 요청과 응답을 만들고, 상태(골드)와 응답(결과 메시지)이 서로 다른 길로 가는 것을 확인한다.

**먼저 알아 둘 것.**

- **요청과 응답.** 로그인, 상점 구매, 퀘스트 보상 받기처럼 서비스의 대부분 기능은 "클라이언트가 요청하고, 서버가 판단하고, 결과를 알려 주는" 모양이다. 서버가 판단하므로 규칙(몇 번까지 받을 수 있는가, 얼마를 주는가)은 서버 코드에만 있고, 클라이언트는 화면만 그린다.
- **RPC는 값을 돌려주지 않는다.** RPC 함수의 반환형은 항상 `void`이다(2.9.2절). 함수를 호출하고 결과를 받는 방식이 아니라, 서버가 별도의 RPC로 클라이언트를 호출하는 방식으로 응답한다. 요청 RPC가 하나, 응답 RPC가 하나이고 둘은 방향이 반대이다. 요청과 응답이 짝이라는 것은 NGO가 보장하는 것이 아니라 우리가 코드로 만든 약속이다.
- **보너스를 받았다는 기록은 `UserState.Gold`에 둔다.** 골드가 0이면 아직 받지 않은 것이고, 받으면 100이 된다. 접속자가 연결을 끊으면 `UserState`가 목록에서 지워지므로 이 기록도 함께 사라진다. 그래서 "접속 중 한 번"이 된다. 같은 이름으로 다시 접속하면 새 ClientId와 새 `UserState`를 받으므로 다시 받을 수 있다. 서버가 "같은 이름이면 같은 사람"으로 보지 않았기 때문이다. 접속이 달라져도 같은 사람임을 알아보려면 계정이 필요하고, 그것은 5장의 데이터베이스가 하는 일이다. 이 단순한 방법의 한계도 있다. 골드를 쓰는 기능이 생기면 0으로 돌아가 다시 받을 수 있게 되므로, 그런 기능이 생기면 "받았음"을 따로 기록해야 한다(연습 문제).
- 결과는 두 가지 길로 나간다. **골드**는 모두에게 보이는 상태이므로 NetworkList에 쓴다. **결과 메시지**("보너스 100 지급", "이미 받았다")는 요청한 사람 한 명에게만 가는 사건이므로 RPC로 보낸다.

**단계.**

1. `Assets/Scripts`에 `ChatHub.Bonus.cs`를 만든다. `ChatHub`의 마지막 부분이다.

```csharp
using System;
using Unity.Netcode;

// ChatHub의 일부: 보너스 요청과 응답
public partial class ChatHub
{
    const int BonusAmount = 100;

    // 화면이 구독하는 이벤트. 서버의 응답이 도착하면 알려 준다. (지급 여부, 받은 양)
    public static event Action<bool, int> OnBonusResult;

    // 화면이 부르는 함수
    public void RequestBonus() { if (IsSpawned) ClaimBonusRpc(); }

    // 요청: 클라이언트 → 서버. 담긴 값이 없다. 누가 보냈는지는 서버가 알고, 얼마를 줄지는 서버가 정한다.
    [Rpc(SendTo.Server)]
    void ClaimBonusRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        int i = IndexOf(sender);
        if (i < 0) return;

        UserState u = users[i];
        bool ok = u.Gold == 0;                  // 골드가 0이면 아직 받지 않은 것이다
        if (ok)
        {
            u.Gold = BonusAmount;
            users[i] = u;                       // 골드는 모두에게 보이는 상태이다
        }

        // 응답: 서버 → 요청한 사람에게만
        BonusResultRpc(ok, ok ? BonusAmount : 0, RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    // 응답: 서버만 부를 수 있고, 호출할 때 정한 한 사람에게만 전달된다.
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void BonusResultRpc(bool ok, int amount, RpcParams rpcParams = default)
    {
        OnBonusResult?.Invoke(ok, amount);
    }
}
```

2. 같은 폴더에 `BonusPanel.cs`를 만든다. 버튼과 결과 글자뿐인 화면이다.

```csharp
using System;
using Unity.Netcode;
using UnityEngine;

// 보너스 화면: 보너스 받기 버튼과 서버가 보내 준 결과
public class BonusPanel : MonoBehaviour
{
    string result = "";
    Action pending;                         // 눌린 버튼이 시킨 일. OnGUI는 기록만 하고 Update에서 실행한다

    void OnEnable()  { ChatHub.OnBonusResult += OnResult; }
    void OnDisable() { ChatHub.OnBonusResult -= OnResult; }

    void OnResult(bool ok, int amount)
    {
        result = ok ? $"보너스 {amount} 지급" : "이미 받았다";
    }

    void Update()
    {
        if (ChatHub.Instance == null) result = "";      // 연결이 끝나면 결과를 지운다

        if (pending == null) return;
        Action job = pending;
        pending = null;
        job();
    }

    void OnGUI()
    {
        UiKit.Apply();
        ChatHub hub = ChatHub.Instance;
        NetworkManager nm = NetworkManager.Singleton;
        if (hub == null || nm == null) return;
        if (!hub.TryGetUser(nm.LocalClientId, out _)) return;       // 접속자가 아니면(Server 모드) 그리지 않는다

        GUILayout.BeginArea(new Rect(570, 485, 350, 80), GUI.skin.box);
        if (GUILayout.Button("보너스 받기")) pending = hub.RequestBonus;
        GUILayout.Label("결과: " + result);
        GUILayout.EndArea();
    }
}
```

3. `BonusPanel.cs`를 `NetStarter` 오브젝트에 붙인다. 씬을 저장하고 코드가 바뀌었으므로 빌드를 다시 한다.

**코드를 읽는 법.**

- 요청 `ClaimBonusRpc`에는 매개변수가 없다. 누가 보냈는지는 서버가 `SenderClientId`로 알고, 얼마를 줄지는 서버가 정한다. 클라이언트가 금액을 실어 보내게 만들지 않는 것이 요청을 설계하는 기본이다. 클라이언트에게 맡긴 값은 고쳐서 보낼 수 있기 때문이다.
- 판정과 기록은 모두 서버가 한다. `u.Gold == 0`이면 지급하고 `u.Gold`를 바꿔 다시 대입한다. 판정하는 줄이 서버 코드에 한 줄뿐이므로 지급 조건을 바꾸는 일도 이 한 줄을 고치는 것으로 끝난다(확인할 것 5번).
- 응답 `BonusResultRpc`는 `SendTo.SpecifiedInParams`이고 호출할 때 `RpcTarget.Single(sender, …)`로 대상을 정했다. 요청을 보낸 한 사람에게만 메시지가 간다. 요청을 보낸 사람이 호스트 자신이면 `Single`이 자기 자신을 가리키므로 메시지 없이 그 자리에서 실행된다.
- `InvokePermission = RpcInvokePermission.Server`가 붙어 있어서 클라이언트가 이 RPC를 직접 부를 수 없다. 서버만 "지급되었다"고 알려 줄 수 있어야 한다.
- 화면(`BonusPanel`)은 `ChatHub.OnBonusResult` 이벤트를 구독하고 오는 결과를 글자로 바꿔 보여 줄 뿐이다. 서버가 보낸 결과를 `ChatHub`가 이벤트로 바꿔 알려 주므로 화면은 RPC를 모른다. 게임 UI로 옮길 때도 같은 이벤트를 구독하면 된다(2.11.8절).
- `RequestBonus`를 `pending`으로 미루는 것은 앞의 화면들과 같은 규칙이다(2.5.4절). 호스트가 버튼을 누르면 요청도 응답도 그 자리에서 실행되므로, 미루지 않으면 `OnGUI`가 그려지는 도중에 화면의 상태가 바뀐다. `Update()`에서 실행하면 그런 일이 없다.

**확인할 것.**

호스트 `교수`와 클라이언트 `철수`, `영희`를 접속시켜 놓고 시작한다.

1. 철수가 **보너스 받기**를 누른다. 철수 화면에 `결과: 보너스 100 지급`이 나타나고, **세 화면 모두**의 접속자 목록에서 철수의 골드가 `100`이 된다. 영희와 교수의 `결과` 글자에는 아무 변화가 없다. 응답은 철수에게만 갔고, 골드는 모두가 보는 상태이기 때문이다.
2. 철수가 한 번 더 누른다. `결과: 이미 받았다`가 나타나고 골드는 `100` 그대로이다.
3. 영희가 따로 **보너스 받기**를 누르면 영희도 100을 받는다. 철수가 받았는지와 무관하다. 호스트(교수)가 눌러도 같다. 이때 결과는 메시지를 거치지 않고 바로 나타난다.
4. 영희가 **종료**했다가 같은 이름으로 다시 접속하여 누른다. 다시 받을 수 있다. 접속이 달라지면 새 ClientId와 새 `UserState`가 만들어지기 때문이다. "같은 사람인가"를 서버가 어떻게 판단하는가는 서버가 정해야 하는 문제이다.
5. **서버 규칙을 바꿔서 시험한다.** `ChatHub.Bonus.cs`의 판정을 다음처럼 바꾼다.

```csharp
        bool ok = u.Gold < 300;                 // 300골드가 될 때까지 받을 수 있다
        if (ok)
        {
            u.Gold += BonusAmount;
            users[i] = u;
        }
```

   **호스트만** 다시 실행한다(에디터에서 ▶). 클라이언트 빌드는 RPC의 이름과 매개변수를 바꾸지 않았으므로 대개 다시 만들지 않아도 된다(이상하게 동작하면 다시 빌드한다). 이제 철수가 세 번까지 받는다. 서버의 규칙을 고쳤는데 화면 코드는 그대로 쓰고도 바뀐 규칙이 바로 검증되었다. 이것이 서버의 기능을 GUI로 먼저 검증하는 이유이다. 확인한 뒤에는 원래대로 되돌린다.

**그 뒤에서 일어난 일.**

철수가 버튼을 누르면 하나의 요청이 두 갈래의 결과가 되어 돌아온다.

1. 요청 RPC가 서버로 간다.
2. 서버가 판정하고 `users[i] = u`를 실행한다. 이 변경은 다음 틱에 **모든 클라이언트**에게 간다(상태).
3. 서버가 `BonusResultRpc`를 철수에게만 보낸다(사건).

두 메시지는 서로 독립이다. 어느 쪽이 먼저 도착하는지 보장되지 않으므로 화면 코드는 순서에 의존하지 않는다. 늦게 접속한 `민수`는 철수의 골드 `100`을 목록에서 바로 보지만, 철수가 받은 `보너스 100 지급` 메시지는 받지 못한다. 상태는 현재 모습을 전하고, 사건은 일어난 순간에 있던 사람에게만 전달된다.

**[선택 기준]** 중간에 접속한 사람에게도 보여야 하는 지속 상태는 NetworkVariable 또는 NetworkList, 한 번 일어나고 지나가는 사건과 개별 응답은 RPC로 전달한다. RPC는 SendTo.Server, Everyone, NotOwner, SpecifiedInParams 등 대상 지정에 따라 실행되는 위치가 달라진다. 이전 자료의 [ServerRpc], [ClientRpc]는 각각 서버로 보내는 RPC, 클라이언트들에게 보내는 RPC에 대응하는 이전 방식이므로 사용하는 NGO 버전에서 지원 여부를 확인한다.

이 기준을 지금까지 만든 기능에 적용하면 다음과 같다. 이 교재의 NGO 2.x에서는 `[ServerRpc]`와 `[ClientRpc]` 대신 `[Rpc(SendTo.…)]`를 쓴다(2.9.6절).

- 접속자 목록, 채널, 체력, 준비 상태, 골드는 늦게 접속한 사람도 지금의 값을 봐야 하는 **지속 상태**이다. → NetworkList(`users`)
- 입장·퇴장 알림, 채팅, 귓속말, 공지는 **한 번 일어나고 지나가는 사건**이다. → RPC(`LineToAllRpc`, `LineToSomeRpc`)
- 보너스 결과와 요청 거절 안내는 요청한 사람 한 명에게 가는 **개별 응답**이다. → RPC(`BonusResultRpc`, `ToOne`)
- 클라이언트의 요청은 모두 `SendTo.Server`이고, 서버의 전달은 전체(`ClientsAndHost`) 또는 일부(`SpecifiedInParams`)이다.

새 기능을 만들 때 "이 정보를 나중에 접속한 사람도 봐야 하는가?"를 먼저 묻는다. 그렇다면 상태이고, 아니라면 사건이다.

### 2.11.8 검증한 코드를 게임 클라이언트로 옮기기

채팅 씬에서 검증한 네트워크 코드를 게임 클라이언트로 옮긴다. 옮길 때는 네트워크 코드와 GUI를 분리하고, GUI 대신 게임 UI와 게임 오브젝트가 같은 이벤트를 받도록 바꾼다.

**[구현 순서]** 1. 네트워크 코드에서 Text와 Button 참조를 제거하고 이벤트로 결과를 알리도록 바꾼다. 2. 채팅 씬의 GUI 스크립트는 이 이벤트를 구독하는 검증용 UI로 남긴다. 3. 게임 씬에서는 게임 UI가 같은 이벤트를 구독한다. 4. 가상 좌표를 실제 오브젝트로 바꾼다.

이 절의 코드는 처음부터 이 순서대로 만들어져 있다.

1. `ChatHub`는 화면을 모른다. 결과는 `ChatHub.OnLog`, `ChatHub.OnBonusResult` 이벤트로 알리고, 목록은 `ChatHub.Users`로 읽게 한다. 버튼이나 글자 상자에 대한 참조가 `ChatHub` 안에는 하나도 없다.
2. `ChatLogPanel`, `UserListPanel`, `ChatInputPanel`, `ChannelPanel`, `StatePanel`, `BonusPanel`이 검증용 UI이다. 이벤트를 구독하고 `ChatHub`의 함수를 부르는 일만 한다.
3. 게임 씬의 UI는 같은 이벤트를 구독하면 된다. 아래에서 직접 해 본다.
4. 가상 좌표를 실제 오브젝트로 바꾸는 일은 이미 했다. 2.7절의 자동 이동(가상 입력)은 `NetPlayer` 캡슐을 움직였고, 2.8절의 닉네임은 그 캡슐 위에 떠 있다.

**화면만 바꿔 끼워 보기.** 같은 `ChatHub`에 다른 화면을 붙여서, 네트워크 코드를 건드리지 않고 화면만 바뀌는 것을 확인한다. 게임 화면처럼 최근 대화 몇 줄만 보였다가 사라지는 표시를 만든다.

1. `Assets/Scripts`에 `ChatHud.cs`를 만든다.

```csharp
using System.Collections.Generic;
using UnityEngine;

// 게임 화면용 채팅 표시: 최근 대화를 몇 줄만 보여 주고 시간이 지나면 지운다. ChatHub는 고치지 않았다.
public class ChatHud : MonoBehaviour
{
    const int MaxLines = 6;
    const float ShowSeconds = 10f;

    struct Line
    {
        public string Text;
        public float At;
    }

    readonly List<Line> lines = new List<Line>();

    void OnEnable()  { ChatHub.OnLog += AddLine; }
    void OnDisable() { ChatHub.OnLog -= AddLine; }

    void AddLine(string text)
    {
        if (lines.Count >= MaxLines) lines.RemoveAt(0);
        lines.Add(new Line { Text = text, At = Time.unscaledTime });
    }

    void Update()
    {
        lines.RemoveAll(line => Time.unscaledTime - line.At > ShowSeconds);   // 오래된 줄을 지운다
    }

    void OnGUI()
    {
        UiKit.Apply();
        if (ChatHub.Instance == null) return;

        GUILayout.BeginArea(new Rect(10, 175, 550, 234));
        foreach (Line line in lines) GUILayout.Label(line.Text);
        GUILayout.EndArea();
    }
}
```

2. `ChatHud.cs`를 `NetStarter` 오브젝트에 붙이고, 같은 오브젝트의 `Chat Log Panel` 컴포넌트는 이름 왼쪽의 체크를 꺼서 끈다. 둘이 같은 이벤트를 받아 같은 자리에 그리기 때문이다.
3. 에디터에서 Host를 시작하고, **전에 만들어 둔 빌드**를 Client로 접속시킨다. 빌드를 다시 하지 않아도 된다. 화면 스크립트는 네트워크 설정이 아니므로 서버와 클라이언트 사이의 접속 조건에 영향을 주지 않는다. 이상하게 동작하면 다시 빌드한다.
4. 철수(빌드)가 채팅을 보낸다. 호스트 화면에는 `ChatHud`가 최근 대화를 보여 주고 10초 뒤에 한 줄씩 지운다. 철수 화면에는 전에 만든 `ChatLogPanel`이 그대로 대화를 쌓아 보여 준다. **같은 서버와 같은 `ChatHub`에서 서로 다른 두 화면이 동시에 동작한다.**
5. 확인한 뒤 `Chat Log Panel`의 체크를 다시 켜고 `ChatHud`는 끈다.

이것이 "게임 UI가 같은 이벤트를 구독한다"의 전부이다. 게임 UI가 3D 말풍선이든, 화면 구석의 글자든, 이벤트를 구독하고 `ChatHub`의 함수를 부르는 방식은 바뀌지 않는다.

**[핵심 개념]** 같은 Player 오브젝트가 접속자 수만큼의 인스턴스로 존재하므로 코드가 어느 인스턴스에서 실행되는지 구분해야 한다. IsServer는 서버 로직을 실행하는 인스턴스(Host 포함), IsClient는 클라이언트 역할의 인스턴스, IsHost는 둘 다인 경우, IsOwner는 이 오브젝트의 소유자가 이 인스턴스의 클라이언트인 경우, IsLocalPlayer는 이 인스턴스의 로컬 플레이어 오브젝트인 경우이다. 소유권이나 NetworkVariable 값에 의존하는 초기화는 Awake와 Start가 아니라 OnNetworkSpawn에서 수행한다.

2.4.4절에서 정리한 내용이다. 이 절의 코드에서는 다음 곳에서 쓰였다.

- `IsServer`: `ChatHub.OnNetworkSpawn()`의 서버 부분(목록 초기화, 접속·종료 이벤트 등록), 서버만 부를 수 있는 `Announce()`.
- `IsClient`: `ServerLog()`. 클라이언트가 없는 서버(Server 모드)일 때만 대화를 화면에 직접 보여 준다.
- `IsHost`: 서버이자 클라이언트인 호스트에서는 RPC가 메시지 전송 없이 자기 자신에게 실행된다. 실습 9, 12에서 확인했다.
- `IsOwner`, `IsLocalPlayer`: `PlayerMove`처럼 플레이어 오브젝트에서 "내 것"만 입력을 읽는 코드에서 쓴다. `ChatHub`는 서버가 소유한 오브젝트이므로 `IsOwner`를 쓰지 않는다.
- `OnNetworkSpawn()`: `ChatHub.Instance`를 채우고, 서버가 접속자 목록을 처음 만드는 곳이다. `Awake()`나 `Start()`에서는 아직 스폰되지 않았다.

**[시험 시나리오]** 아래 상황에서 먼저 채팅 씬의 GUI로 서버가 의도한 대로 동작하는지 확인하고, 게임 클라이언트에서도 같은 결과가 나오는지 비교한다.

- 정원을 넘은 접속 시도, 인증 코드가 틀린 접속 시도
- 채널 대화 중 접속자가 강제 종료되는 경우
- 상태 변경 후 새 클라이언트가 접속하는 경우
- 플레이 중 Host가 종료되는 경우
- RTT 300ms 환경에서 이동과 채널 대화를 진행하는 경우

이 교재에서 각 시나리오를 시험하는 곳은 다음과 같다.

- 정원 초과와 접속 코드 오류: 실습 6(2.10.4절)의 확인할 것.
- 채널 대화 중 강제 종료: 실습 10의 확인할 것 7번에서 **종료** 버튼으로 나갔다. 이번에는 작업 관리자로 클라이언트를 강제 종료해 본다. 서버가 알아채는 데 수십 초가 걸리고(2.10.6절), 그동안 목록에 그 사람이 남아 있다가 알아챈 뒤에 사라지는 것을 본다.
- 상태 변경 후 새 클라이언트 접속: 실습 11의 확인할 것 4번, 실습 8의 확인할 것 3번.
- 플레이 중 Host 종료: 실습 8의 확인할 것 7번. 클라이언트는 시작 화면으로 돌아간다.
- RTT 300ms: 2.13절에서 Network Simulator로 지연을 주고, 자동 이동(실습 3), 채널 채팅(실습 10), 체크 상자(실습 11), 보너스 응답(실습 12)이 어떻게 달라지는지 기록한다.

**C++ 서버로 이어지는 연결.** 6장에서는 이 절에서 만든 채팅 서버를 C++로 다시 만든다. 이때 Unity 쪽에서는 `ChatHub`가 있던 자리에 `TcpChatService`가 놓인다. 화면 코드가 서비스의 함수를 부르고 이벤트를 구독하는 같은 모양이므로, 로그 창과 입력 창은 함수와 이벤트의 이름만 맞추면 거의 그대로 쓸 수 있다. 접속자 목록처럼 NGO의 `NetworkList`에 기대는 화면은 C++ 서버가 보내는 패킷으로 다시 만든다. 이 절의 각 기능이 6장에서는 어떤 패킷이 되는지 미리 보면 다음과 같다.

- 접속 승인(`ApprovalGate`의 이름·접속 코드·정원·중복 검사): 클라이언트가 `HELLO`(이름|인증 코드)를 보내고 서버가 `WELCOME` 또는 `DENY`(사유)로 답한다.
- 채팅 요청 `SendChatRpc`와 전달 `LineToAllRpc`: `SAY`(대화)와 `CHAT`(이름|대화). 서버가 보낸 사람의 이름을 붙여 주는 점이 같다. NGO에서는 `SenderClientId`로, C++ 서버에서는 연결(세션)에 저장해 둔 이름으로 안다.
- 입장·퇴장 알림, 공지, 거절 안내(`ToAll`, `ToOne`): `NOTICE`.
- 접속자 목록(`NetworkList`): `USERS` 패킷. NGO는 목록이 바뀔 때마다 변경을 알아서 보내 주지만, C++ 서버는 접속자가 바뀔 때마다 서버 코드가 `USERS` 패킷을 만들어 보낸다.
- 채널 만들기·입장·나가기: `ROOM_CREATE`, `ROOM_JOIN`, `ROOM_LEAVE`. 이 절에서는 채널이 비면 저절로 사라졌지만, C++ 서버는 방 표에서 직접 지운다.
- 핑(2.9.4절): `PING`과 `PONG`.
- 보너스 요청과 응답(실습 12): `BONUS`와 `BONUS_RES`.

이렇게 놓고 보면 NGO가 해 주던 일이 분명해진다. 직렬화, 받을 사람 지정, 상태 복제, 보낸 사람 확인, 늦게 온 사람에게 현재 상태 전달이 모두 6장에서는 우리가 쓰는 코드의 몫이다.

### 2.11.9 uGUI로 화면 만들기 (선택)

이 절의 화면은 IMGUI로 만들었다. 코드 몇 줄로 버튼과 글자를 그릴 수 있어서 서버 기능을 시험하기에 좋지만, 실제 게임의 화면에는 쓰지 않는다. 게임에서는 Canvas 위에 Button, Text, InputField를 놓는 **uGUI**(또는 UI Toolkit)를 쓴다. 이 절은 선택이다. 이 교재의 다른 실습에는 필요하지 않으므로 건너뛰어도 된다.

uGUI로 바꿔도 달라지는 것은 화면 쪽뿐이다. 보너스 화면(`BonusPanel`)을 uGUI로 다시 만들어 본다.

1. Hierarchy의 빈 곳에서 오른쪽 버튼 > **UI > Canvas**로 Canvas를 만들고 이름을 `GameUI`로 바꾼다. Canvas와 함께 `EventSystem` 오브젝트가 자동으로 만들어진다. 프로젝트가 새 Input System을 쓰는 경우(2.2.4절), `EventSystem`의 Inspector에 `Replace with InputSystemUIInputModule` 같은 버튼이 보이면 눌러 바꾼다. 이 모듈이 맞지 않으면 클릭이 전달되지 않거나 Input 예외가 난다.
2. `GameUI`의 **Canvas Scaler**에서 **UI Scale Mode**를 **Scale With Screen Size**로, **Reference Resolution**을 1280 × 720으로 바꾼다. 해상도가 달라져도 UI가 같은 비율로 배치된다.
3. `GameUI` 아래에 Button과 Text를 하나씩 만든다. 메뉴는 **UI > Legacy > Button**, **UI > Legacy > Text**이다(Unity 6에서 이 항목은 `Legacy` 아래에 있고, 버전에 따라 이름이 조금 다를 수 있다). Button의 이름은 `BonusButton`, Text의 이름은 `BonusResultText`로 바꾸고, Button 아래의 Text(글자)를 `보너스 받기`로 바꾼다. Scene 뷰에서 두 개를 화면 오른쪽 아래의 적당한 자리로 옮긴다.
4. `Assets/Scripts`에 `BonusView.cs`를 만든다.

```csharp
using UnityEngine;
using UnityEngine.UI;

// BonusPanel의 uGUI 버전. ChatHub는 그대로이고 화면만 다르다.
public class BonusView : MonoBehaviour
{
    [SerializeField] Button bonusButton;        // Inspector에서 연결한다
    [SerializeField] Text resultText;           // Inspector에서 연결한다

    void Awake()
    {
        // 기본 글꼴에는 한글이 없으므로 PC에 설치된 한글 글꼴을 쓴다. (UiKit과 같은 방법)
        Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 16);
        resultText.font = font;
        bonusButton.GetComponentInChildren<Text>().font = font;
    }

    void OnEnable()
    {
        ChatHub.OnBonusResult += OnResult;
        bonusButton.onClick.AddListener(OnClick);
    }

    void OnDisable()
    {
        ChatHub.OnBonusResult -= OnResult;
        bonusButton.onClick.RemoveListener(OnClick);
    }

    void Update()
    {
        bool connected = ChatHub.Instance != null;
        bonusButton.interactable = connected;           // 접속하기 전에는 누를 수 없다
        if (!connected) resultText.text = "";
    }

    void OnClick()
    {
        ChatHub.Instance.RequestBonus();                // uGUI의 클릭은 Update 단계에서 처리되므로 바로 불러도 된다
    }

    void OnResult(bool ok, int amount)
    {
        resultText.text = ok ? $"보너스 {amount} 지급" : "이미 받았다";
    }
}
```

5. `BonusView.cs`를 `GameUI` 오브젝트에 붙이고, Inspector의 `Bonus Button`과 `Result Text` 칸에 방금 만든 두 오브젝트를 Hierarchy에서 끌어다 놓는다. **칸을 비워 두면 `NullReferenceException`이 난다.**
6. `NetStarter`의 `Bonus Panel` 컴포넌트는 체크를 끈다. 두 화면이 겹치지 않게 하려는 것이다.
7. 씬을 저장하고 실습 12와 같은 방법으로 확인한다. 접속하기 전에는 버튼이 회색이고, 접속하면 눌러진다. 결과 글자는 IMGUI 때와 같이 나타난다.

IMGUI와 달라진 곳을 정리한다.

- 버튼의 동작을 `pending`에 적어 두지 않고 `onClick`에서 바로 `RequestBonus()`를 부른다. IMGUI는 한 프레임에 `OnGUI`를 여러 번 부르므로 도중에 상태를 바꾸면 안 되었지만, uGUI의 클릭 처리는 `Update` 단계에서 한 번 일어난다.
- 화면을 매 프레임 다시 그리는 코드가 없다. 글자는 `resultText.text`에 한 번 넣으면 바뀔 때까지 그대로 있다. 그래서 값이 바뀔 때(이벤트가 왔을 때)만 고친다.
- 이벤트는 `OnEnable`에서 구독하고 `OnDisable`에서 해제한다. IMGUI 패널과 같다.
- `ChatHub`는 한 줄도 고치지 않았다.

새 프로젝트에서는 글자 컴포넌트로 TextMeshPro(`UI > Text - TextMeshPro`)를 흔히 쓴다. TextMeshPro로 한글을 쓰려면 한글이 들어 있는 글꼴로 Font Asset을 따로 만들어야 해서 준비할 것이 더 많으므로 이 교재에서는 다루지 않는다. 구조는 같다. 이벤트를 구독해서 컴포넌트의 `text`에 문자열을 넣는다.

### 2.11.10 흔한 오류

- **접속했는데 접속자 목록, 채팅 창, 패널이 하나도 안 보인다.** `ChatHub.Instance`가 null이다. 서버나 클라이언트를 시작했는지, 씬에 `ChatHub` 오브젝트가 있고 `Network Object`와 `ChatHub` 스크립트가 같은 오브젝트에 붙어 있는지 확인한다. `ChatHub`를 `NetworkObject` 없이 붙였거나 런타임에 코드로 만든 경우에도 스폰되지 않아 `Instance`가 채워지지 않는다. 서버를 시작할 때 자동으로 스폰되는 것은 씬에 미리 놓은 `NetworkObject`뿐이다.
- **패널을 만들었는데 화면에 나타나지 않는다.** 패널 스크립트를 `NetStarter` 오브젝트에 붙였는지, 컴포넌트의 체크가 켜져 있는지 확인한다. `ChatInputPanel`, `ChannelPanel`, `StatePanel`, `BonusPanel`은 이 실행이 접속자 목록에 있을 때만 그려진다. Server 모드(서버만)에서는 나타나지 않는 것이 정상이다.
- **`ChatHub.users cannot be null. All NetworkVariableBase instances must be initialized.`** `NetworkList`를 `OnNetworkSpawn()`에서 만들었다. NGO는 스폰하기 전에 NetworkVariable과 NetworkList 필드를 모으므로 `Awake()`에서 만들어야 한다. 필드를 선언하면서 바로 만들면(`NetworkList<UserState> users = new NetworkList<UserState>();`) 에디터가 스크립트 인스턴스를 만들 때마다 목록이 함께 만들어져서 `A Native Collection has not been disposed` 같은 경고가 날 수 있다.
- **컴파일 오류 `The type 'UserState' cannot be used as type parameter 'T' in the generic type or method 'NetworkList<T>'`.** 구조체가 `IEquatable<UserState>`를 구현하지 않았다. `Equals`를 구현하고 필드를 하나 추가했을 때는 `Equals`와 `NetworkSerialize`에도 같은 필드를 넣었는지 확인한다.
- **`RpcException: The NetworkBehaviour must be spawned before calling this method.`** 접속하기 전에, 즉 스폰되기 전에 RPC를 불렀다. 화면은 `ChatHub.Instance`가 있을 때만 그리고, `ChatHub`의 함수는 `IsSpawned`를 확인한 뒤에 RPC를 부른다. 같은 스크립트를 `NetworkObject`가 없는 오브젝트에 붙여도 같은 오류가 난다.
- **`RpcException: This RPC can only be sent by the server.`** 클라이언트가 `InvokePermission = Server`인 RPC(`LineToAllRpc`, `LineToSomeRpc`, `BonusResultRpc`)를 직접 불렀다. 클라이언트의 코드는 `SendChat()` 같은 요청 함수만 부른다.
- **`Write permissions (Server) for this client instance is not allowed!`** 클라이언트가 접속자 목록에 직접 썼다. 상태를 바꾸려면 서버에게 요청하는 RPC를 보내고 서버가 쓰게 한다(2.11.6절).
- **클라이언트에서 접속자의 이름이 모두 `Player 1`, `Player 2`처럼 나온다.** 서버가 승인 때 확인한 이름을 찾지 못해 기본 이름을 붙인 것이다. NetworkManager의 **Connection Approval**이 꺼져 있거나 `ApprovalGate`가 씬에 없다(2.10절). 이름은 서버(호스트)의 `ApprovalGate.Names`에만 있으며, 서버가 `UserState`에 담아 복제한 것을 클라이언트가 읽는다. 서버에서만 값이 있는 자료구조를 클라이언트에서 직접 읽으려고 하면 비어 있다.
- **접속이 끊긴 사람이 목록이나 채널에 남아 있다.** 접속 종료 콜백(`OnClientDisconnectCallback`)을 등록하지 않았거나 `OnNetworkSpawn()`의 서버 부분이 실행되지 않았다. 채널이나 방을 접속자 목록과 **따로 저장하는** 설계에서 가장 흔한 실수이다. 이 교재의 채널은 접속자 목록의 한 칸이므로 사람이 지워지면 채널에서도 저절로 빠진다.
- **호스트 화면에서만 같은 줄이 두 번 나타난다.** 호스트는 서버이자 클라이언트이므로 `ClientsAndHost` RPC가 자기 자신에게 한 번 실행된다. 여기에 `ServerLog()`가 직접 `Emit()`까지 하면 두 번 나온다. `ServerLog()`의 `if (!IsClient)`를 지우지 않았는지 확인한다.
- **Server 모드(서버만)에서 대화가 화면에 나타나지 않는다.** 클라이언트가 없는 서버는 `ClientsAndHost` RPC를 실행할 곳이 없다. 서버 화면에 보이게 하는 것은 `ServerLog()`의 몫이다.
- **보너스 결과가 모든 화면에 나타난다.** 응답을 `SendTo.Everyone`이나 `ClientsAndHost`로 보냈다. 요청한 사람에게만 가도록 `SpecifiedInParams`와 `RpcTarget.Single`을 쓴다.
- **접속을 끊었다가 다시 접속하면 보너스를 또 받는다.** 재접속한 사용자를 같은 사용자로 볼지 정하지 않아서 생기는 일이다. ClientId는 재접속할 때 바뀐다. 이 교재의 서버는 접속마다 새 사용자로 본다. 같은 사용자로 보려면 계정이 필요하고, 5장에서 다룬다.
- **체크 상자가 눌러도 바로 안 바뀌고 한참 뒤에 바뀐다.** 오류가 아니다. 화면은 서버가 목록에 쓴 값을 그리므로 서버를 다녀온 뒤에 바뀐다(2.11.6절).
- **Console에 `Getting control N's position in a group with only N controls when doing Repaint`가 나온다.** `OnGUI` 안에서 화면의 구성이 달라지는 일을 했다. 버튼은 할 일을 `pending`에 적어 두기만 하고 `Update()`에서 실행한다(2.5.4절).
- **uGUI에서 `NullReferenceException`이 난다.** UI 참조를 연결하지 않았다. `BonusView` 같은 스크립트의 `[SerializeField]` 칸이 Inspector에서 `None`이 아닌지 본다.
- **오브젝트를 없앴는데 이벤트 함수가 계속 불리고 `MissingReferenceException`이 난다.** `OnLog`나 `OnBonusResult`, `OnListChanged` 같은 이벤트 구독을 `OnDisable()`이나 `OnNetworkDespawn()`에서 해제하지 않았다.
- **코드를 고쳤는데 클라이언트에는 반영되지 않는다.** 빌드한 프로그램은 빌드한 시점의 코드를 가지고 있다. 서버 쪽 본문만 고친 경우는 다시 빌드하지 않아도 되는 경우가 많지만, RPC의 이름이나 매개변수, `UserState`의 필드를 바꿨다면 반드시 다시 빌드한다.

**이 절의 정리.**

- 서버가 하는 일의 기본형은 이렇다. 클라이언트가 **요청**(RPC, `SendTo.Server`)하고, 서버가 **검사**하고, **상태**(NetworkList)를 바꾸고, 대상을 정해 **전달**(RPC)한다.
- 요청에는 필요한 값만 싣는다. 보낸 사람은 `SenderClientId`로 서버가 알아내고, 이름·금액·받을 사람 같은 것은 클라이언트가 정하게 하지 않는다. 길이와 범위는 서버가 검사한다.
- 늦게 접속한 사람도 알아야 하는 것은 상태(NetworkVariable, NetworkList)이고, 한 번 일어나고 지나가는 것은 사건(RPC)이다. 개별 응답은 요청한 사람에게만 보낸다.
- 값 하나는 NetworkVariable, 접속자마다 있는 값의 모음은 NetworkList이다. 구조체 요소는 꺼내서 고친 뒤 다시 대입한다. 클라이언트는 쓸 수 없다.
- 채널은 따로 저장하지 않고 접속자 목록의 한 칸으로 두었다. 클라이언트가 읽을 수 있고, 접속이 끊기면 저절로 빠지고, 빈 채널은 저절로 사라진다.
- 네트워크 코드(`ChatHub`)와 화면 코드(`…Panel`)를 나눈다. 둘은 함수 호출과 이벤트로만 만난다. 그래서 화면을 IMGUI에서 uGUI나 게임 UI로 바꿔도 네트워크 코드는 바뀌지 않는다.
- IMGUI의 화면은 `OnGUI`에서 상태를 바꾸지 않고 `pending`에 적어 둔 일을 `Update()`에서 실행한다.
- 서버의 기능은 GUI로 먼저 검증한다. 이 방법은 Unity의 NGO뿐 아니라 언리얼, C++ 서버에서도 똑같이 쓴다.

## 2.12 서버가 만드는 오브젝트: 생성, 제거, 소유권 이전

지금까지 만난 네트워크 오브젝트는 두 종류였다. 접속할 때 서버가 자동으로 만들어 주는 플레이어 캡슐, 그리고 씬에 미리 놓아 둔 `ChatHub`이다. 게임에는 세 번째 종류가 필요하다. 게임이 진행되는 도중에 서버가 코드로 만들고 없애는 오브젝트이다. 아이템, 투사체, 몬스터, 방 하나가 모두 여기에 해당한다. 이 절에서는 서버가 오브젝트를 만들고(스폰), 없애고(디스폰), 주인을 바꾸는(소유권 이전) 방법을 익힌다.

### 2.12.1 스폰의 규칙

**서버만 만들고 서버만 없앤다.** 클라이언트가 `Instantiate`로 만든 오브젝트는 그 클라이언트의 컴퓨터에만 존재하고 다른 컴퓨터에는 나타나지 않는다(2.4.5절). 모든 접속자에게 보이는 오브젝트를 만드는 길은 하나이다. 서버가 프리팹을 `Instantiate`하고 `NetworkObject`의 `Spawn()`을 부른다. 그러면 NGO가 접속해 있는 모든 클라이언트에게 "이 프리팹을 이 위치에 만들어라"는 메시지를 보내고, 각 클라이언트가 자기 컴퓨터에서 같은 프리팹을 `Instantiate`한다(2.6.1절). 늦게 접속하는 클라이언트는 접속할 때 이미 스폰되어 있는 오브젝트들을 한꺼번에 받는다.

스폰하는 함수는 용도에 따라 고른다.

- `Spawn()`: 소유자가 없는 오브젝트로 만든다. 서버가 주인이다.
- `SpawnWithOwnership(clientId)`: 소유자를 지정해서 만든다. 플레이어가 만든 아이템이나 투사체가 이 방식이다.
- `NetworkObject.InstantiateAndSpawn(NetworkManager, 소유자, …)`: `Instantiate`와 스폰을 한 번에 한다. 프리팹이 Network Prefabs 목록에 등록되어 있는지 검사해 주는 장점이 있다. 이 절은 단계가 눈에 보이도록 `Instantiate`와 `SpawnWithOwnership`을 따로 쓴다.

**프리팹은 Network Prefabs 목록에 있어야 한다.** 클라이언트는 "이 번호의 프리팹을 만들어라"는 메시지를 받으면 NetworkManager의 Network Prefabs 목록에서 그 프리팹을 찾아 `Instantiate`한다. 목록에 없으면 만들 수 없다. `NetworkObject`를 붙인 프리팹은 기본 목록 `DefaultNetworkPrefabs`에 자동으로 등록된다(2.6.2절). 그래도 새로 만든 프리팹이 목록에 들어갔는지 눈으로 확인하는 습관을 들인다.

**없앨 때는 `Despawn()`을 쓴다.** `Despawn()`은 모든 접속자의 복사본을 없애고, 기본으로 서버의 오브젝트도 `Destroy`한다. 서버에서 `Destroy(gameObject)`를 바로 불러도 NGO가 디스폰 처리를 해 주지만, 의도를 분명히 하려고 `Despawn()`을 쓴다. 클라이언트에서 `Destroy`를 부르면 그 컴퓨터의 복사본만 사라지고 NGO가 `[Invalid Destroy]` 오류를 남긴다. 서버와 다른 컴퓨터에는 그대로 남아서 상태가 어긋난다.

**소유권은 서버가 바꾼다.** 소유자는 `OwnerClientId`로 읽는다. `ChangeOwnership(clientId)`로 다른 접속자에게 넘기고, `RemoveOwnership()`으로 서버에게 돌려준다. 소유권이 바뀌면 NGO가 모든 컴퓨터에 알려 주며, 모든 컴퓨터에서 `OnOwnershipChanged(이전 소유자, 새 소유자)`가 불린다. 소유자가 되면 서버로부터 권한을 위임받는다. NetworkTransform의 Authority Mode가 Owner이면 소유자가 위치를 정하고, `InvokePermission`이 `Owner`인 RPC는 소유자만 부를 수 있다. 서버가 코드에서 "이 요청을 한 사람이 소유자인가"를 확인하는 방식으로 권한을 판단할 수도 있다. 이 절의 실습이 그렇게 한다.

**접속이 끊기면 그 사람이 소유한 오브젝트도 사라진다.** 기본 동작이다. 소유자가 연결을 끊으면 서버가 그 사람이 소유한 오브젝트를 모두 디스폰하고 없앤다. 남기고 싶은 오브젝트는 프리팹의 Network Object 컴포넌트에서 **Dont Destroy With Owner**를 켠다. 그러면 소유자가 나갈 때 오브젝트는 남고 소유권만 서버로 돌아온다.

### 2.12.2 실습 13: 아이템 만들기, 없애기, 넘기기

**목표.** 접속자가 버튼으로 서버에게 아이템(큐브)을 만들어 달라고 요청한다. 서버가 아이템을 스폰하면 모든 화면에 같은 큐브가 나타난다. 아이템은 만든 사람이 소유하고, 소유자만 없앨 수 있다. 소유권을 다른 사람에게 넘기면 색과 이름표가 바뀌고 권한도 함께 넘어간다.

**먼저 알아 둘 것.**

- 이 실습의 서버 쪽 코드는 `ChatHub`와 같은 모양이다. 씬에 미리 놓은 서버 소유 오브젝트 `SpawnHub`가 요청(RPC)을 받아 검사하고 실행한다. 화면(`SpawnPanel`)은 `SpawnHub`의 함수를 부르기만 한다.
- 아이템에는 `Item` 스크립트가 붙는다. 스폰된 아이템을 `Item.All`이라는 목록에 모아 둔다. 서버도 클라이언트도 자기 컴퓨터에 있는 아이템을 이 목록으로 알 수 있다. 서버가 아이템 목록을 따로 관리하지 않아도 되고, 화면은 이 목록으로 개수를 센다.
- 아이템의 색은 소유자 번호로 계산한다. 2.8절에서 캡슐이 처음 칠해지는 색과 같은 공식이므로, 색 바꾸기 버튼을 누르지 않았다면 소유자의 캡슐과 아이템은 같은 색이다. 이름표에는 소유자의 이름이 붙는다. 소유자가 바뀌면 `OnOwnershipChanged()`에서 색과 이름표를 갱신한다.
- 2.11절의 패널은 Game 화면의 대부분을 덮고 있어서 아이템이 패널 뒤에 가려진다. 그래서 패널을 숨기고 보이게 하는 버튼(`ViewToggle`)을 먼저 만든다. 이 버튼은 이후의 절에서도 3D 화면을 볼 때 쓴다.
- 서버는 한도(8개)를 넘는 요청과 남의 아이템을 없애려는 요청을 거절한다.

**먼저, 3D 화면이 보이게 한다.** `Assets/Scripts`에 `ViewToggle.cs`를 만들고 `NetStarter` 오브젝트에 붙인다. 화면 오른쪽 아래에 **패널 숨기기** 버튼이 생긴다.

```csharp
using UnityEngine;

// 패널을 숨기고 다시 보이게 하는 버튼. 패널이 가리고 있는 3D 화면(캡슐, 아이템)을 볼 때 쓴다.
public class ViewToggle : MonoBehaviour
{
    // 숨기지 않을 패널. 아이템 버튼은 3D 화면을 보면서 눌러야 하므로 남겨 둔다.
    const string KeepName = "SpawnPanel";

    bool shown = true;
    bool pendingToggle;                     // OnGUI는 눌렸다는 사실만 기록하고, 바꾸는 일은 Update에서 한다

    void Update()
    {
        if (!pendingToggle) return;
        pendingToggle = false;
        shown = !shown;

        // 이 게임 오브젝트(NetStarter)에 붙은 스크립트 중 이름이 Panel로 끝나는 것을 켜고 끈다.
        // enabled가 false인 스크립트는 OnGUI()와 Update()가 불리지 않으므로 화면에서 사라진다.
        foreach (MonoBehaviour b in GetComponents<MonoBehaviour>())
        {
            string typeName = b.GetType().Name;
            if (typeName.EndsWith("Panel") && typeName != KeepName) b.enabled = shown;
        }
    }

    void OnGUI()
    {
        UiKit.Apply();
        string label = shown ? "패널 숨기기" : "패널 보이기";
        if (GUI.Button(new Rect(Screen.width - 130, Screen.height - 35, 120, 30), label)) pendingToggle = true;
    }
}
```

- 버튼을 누르면 패널이 사라지고 바닥과 캡슐이 보인다. 다시 누르면 돌아온다. `SpawnPanel`은 이 실습에서 만들 아이템 버튼이어서, 3D 화면을 보면서 누를 수 있도록 남겨 둔다. 접속 화면(`NetStarter`)은 이름이 `Panel`로 끝나지 않으므로 그대로 남는다.
- 스크립트를 끄면 `OnDisable()`이 불리고, 다시 켜면 `OnEnable()`이 불린다. 그래서 `ChatLogPanel`은 숨겨진 동안 `ChatHub.OnLog` 구독을 풀고, 그동안 도착한 대화는 로그에 쌓이지 않는다. 이벤트를 `OnEnable()`에서 구독하고 `OnDisable()`에서 해제하는 코드의 성질이다. 대화 내용을 확인하려면 패널을 다시 보이게 한 뒤 본다.

**단계.**

1. `Assets/Scripts`에 `Item.cs`를 만든다.

```csharp
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 아이템 한 개. 서버가 스폰한 NetworkObject이다. 소유자에 따라 색이 달라지고 이름표가 붙는다.
public class Item : NetworkBehaviour
{
    // 이 컴퓨터에 있는 스폰된 아이템 전부. 서버는 전체를, 클라이언트는 자기에게 복제된 것을 가진다.
    public static readonly List<Item> All = new List<Item>();

    public override void OnNetworkSpawn()
    {
        All.Add(this);
        ApplyColor(OwnerClientId);
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
    }

    public override void OnDestroy()
    {
        All.Remove(this);
        base.OnDestroy();               // NetworkBehaviour의 OnDestroy를 재정의하면 반드시 base를 부른다
    }

    // 소유자가 바뀌면 서버와 모든 클라이언트에서 불린다.
    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        ApplyColor(current);
        Debug.Log($"[{(IsServer ? "서버" : "클라이언트")}] Item {NetworkObjectId} 소유자 {previous} → {current}");
    }

    // 2.8절의 PlayerProfile이 캡슐을 처음 칠할 때 쓰는 공식과 같다.
    void ApplyColor(ulong owner)
    {
        GetComponent<Renderer>().material.color = Color.HSVToRGB(((float)owner * 0.17f) % 1f, 0.65f, 0.95f);
    }

    string OwnerName
    {
        get
        {
            ChatHub hub = ChatHub.Instance;
            if (hub != null && hub.TryGetUser(OwnerClientId, out UserState owner)) return owner.Name.ToString();
            return $"Client {OwnerClientId}";
        }
    }

    // 아이템 위에 이름표를 그린다. 스폰되지 않은 복사본에는 그렇다고 적는다.
    void OnGUI()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 p = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.2f);
        if (p.z <= 0f) return;                          // 카메라 뒤에 있으면 그리지 않는다

        UiKit.Apply();
        string text = IsSpawned ? $"Item {NetworkObjectId} / {OwnerName}" : "(스폰되지 않음)";
        Vector2 size = GUI.skin.label.CalcSize(new GUIContent(text));
        var rect = new Rect(p.x - size.x / 2f, Screen.height - p.y - size.y, size.x, size.y);

        Color old = GUI.color;
        GUI.color = Color.black;                        // 글자 그림자
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = old;
        GUI.Label(rect, text);
    }
}
```

2. 아이템 프리팹을 만든다. Hierarchy에서 마우스 오른쪽 버튼 > **3D Object** > **Cube**를 만들고 이름을 `Item`으로 바꾼다. **Add Component**로 `Network Object`를 추가하고, 이어서 `Item` 스크립트를 추가한다. Project 창의 `Assets/Prefabs` 폴더로 끌어다 프리팹으로 만든 뒤, Hierarchy에 남은 `Item`은 지운다.
3. Project 창에서 `Assets/DefaultNetworkPrefabs.asset`을 선택해 Inspector의 목록에 `Item`이 들어 있는지 확인한다. 2.6.2절의 `NetPlayer`와 같은 방식으로 자동 등록된다. 없으면 목록 아래의 **+** 로 추가하고 `Item` 프리팹을 끌어다 놓는다.
4. `Assets/Scripts`에 `SpawnHub.cs`를 만든다.

```csharp
using Unity.Netcode;
using UnityEngine;

// 서버가 아이템을 만들고, 없애고, 소유자를 바꾸는 서비스. ChatHub처럼 씬에 미리 놓는 서버 소유 오브젝트이다.
public class SpawnHub : NetworkBehaviour
{
    public const int MaxItems = 8;                  // 한 번에 존재할 수 있는 아이템 수

    public static SpawnHub Instance { get; private set; }

    [SerializeField] NetworkObject itemPrefab;      // Inspector에서 Item 프리팹을 연결한다

    public override void OnNetworkSpawn()   { Instance = this; }
    public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

    // ---------- 화면이 부르는 함수 ----------

    public void RequestSpawn()   { if (IsSpawned) SpawnItemRpc(); }
    public void RequestDespawn() { if (IsSpawned) DespawnItemRpc(); }
    public void RequestPass()    { if (IsSpawned) PassItemRpc(); }

    // 시험용: 서버에 알리지 않고 이 컴퓨터에서만 Instantiate한다. 스폰하지 않았으므로 다른 컴퓨터에는 나타나지 않는다.
    public void SpawnLocalOnly()
    {
        NetworkObject local = Instantiate(itemPrefab, RandomPosition(), Quaternion.identity);
        Destroy(local.gameObject, 5f);              // 5초 뒤에 저절로 지운다
    }

    // ---------- 서버에서 실행되는 RPC 본문 ----------

    [Rpc(SendTo.Server)]
    void SpawnItemRpc(RpcParams rpcParams = default)
    {
        if (Item.All.Count >= MaxItems) return;     // 요청이 아무리 와도 서버가 정한 한도를 넘지 않는다

        ulong sender = rpcParams.Receive.SenderClientId;
        NetworkObject item = Instantiate(itemPrefab, RandomPosition(), Quaternion.identity);
        item.SpawnWithOwnership(sender);            // 서버가 스폰하면 모든 클라이언트에 복제된다. 소유자는 요청한 사람이다.
    }

    [Rpc(SendTo.Server)]
    void DespawnItemRpc(RpcParams rpcParams = default)
    {
        Item item = LastOwnedBy(rpcParams.Receive.SenderClientId);   // 요청한 사람이 소유한 아이템만 없앨 수 있다
        if (item != null) item.NetworkObject.Despawn();              // 제거도 서버만 한다
    }

    [Rpc(SendTo.Server)]
    void PassItemRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        Item item = LastOwnedBy(sender);
        if (item == null) return;

        ulong next = NextClientAfter(sender);
        if (next != sender) item.NetworkObject.ChangeOwnership(next);
    }

    // ---------- 서버 안에서 쓰는 도우미 ----------

    // 플레이어 캡슐이 서 있는 줄(z = 0)을 피해서 그 앞쪽 바닥에 놓는다.
    static Vector3 RandomPosition()
    {
        return new Vector3(Random.Range(-8f, 8f), 0.5f, Random.Range(2f, 6f));
    }

    // clientId가 소유한 아이템 중 가장 나중에 만들어진 것
    static Item LastOwnedBy(ulong clientId)
    {
        for (int i = Item.All.Count - 1; i >= 0; i--)
            if (Item.All[i].OwnerClientId == clientId) return Item.All[i];
        return null;
    }

    // 접속해 있는 사람들을 접속 순서대로 놓았을 때 clientId 다음 사람. 마지막 사람의 다음은 처음 사람이다.
    ulong NextClientAfter(ulong clientId)
    {
        var ids = NetworkManager.ConnectedClientsIds;
        for (int i = 0; i < ids.Count; i++)
            if (ids[i] == clientId) return ids[(i + 1) % ids.Count];
        return clientId;
    }
}
```

5. Hierarchy에서 **Create Empty**로 새 오브젝트를 만들고 이름을 `SpawnHub`로 바꾼다. **Add Component**로 `Network Object`와 `SpawnHub` 스크립트를 추가한다. `SpawnHub`의 Inspector에 있는 **Item Prefab** 칸에, Project 창의 `Assets/Prefabs/Item` 프리팹을 끌어다 놓는다. Hierarchy의 오브젝트가 아니라 **Project 창의 프리팹 파일**이어야 한다.
6. `Assets/Scripts`에 `SpawnPanel.cs`를 만든다. 화면 오른쪽 아래에 그려지는 작은 패널이다.

```csharp
using System;
using Unity.Netcode;
using UnityEngine;

// 아이템 화면: 만들기, 없애기, 넘기기 버튼과 아이템 개수
public class SpawnPanel : MonoBehaviour
{
    Action pending;                         // 눌린 버튼이 시킨 일. OnGUI는 기록만 하고 Update에서 실행한다

    void Update()
    {
        if (pending == null) return;
        Action job = pending;
        pending = null;
        job();
    }

    void OnGUI()
    {
        UiKit.Apply();
        SpawnHub hub = SpawnHub.Instance;
        NetworkManager nm = NetworkManager.Singleton;
        if (hub == null || nm == null) return;

        int mine = 0;
        foreach (Item item in Item.All)
            if (item.OwnerClientId == nm.LocalClientId) mine++;

        GUILayout.BeginArea(new Rect(Screen.width - 350, 420, 340, 145), GUI.skin.box);
        GUILayout.Label($"아이템 {Item.All.Count}/{SpawnHub.MaxItems}개 (내 것 {mine}개)");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("만들기")) pending = hub.RequestSpawn;
        if (GUILayout.Button("없애기")) pending = hub.RequestDespawn;
        if (GUILayout.Button("넘기기")) pending = hub.RequestPass;
        GUILayout.EndHorizontal();

        if (GUILayout.Button("내 컴퓨터에만 만들기 (시험)")) pending = hub.SpawnLocalOnly;
        GUILayout.EndArea();
    }
}
```

7. `SpawnPanel.cs`를 `NetStarter` 오브젝트에 붙인다. 씬을 저장한다. 프리팹과 씬 오브젝트(`SpawnHub`)와 코드가 바뀌었으므로 빌드를 다시 한다.

**코드를 읽는 법.**

- `SpawnItemRpc`는 서버에서 실행된다. `Instantiate`로 서버에 오브젝트를 만들고, `SpawnWithOwnership(sender)`가 그 순간 모든 클라이언트에게 "만들어라"는 메시지를 보낸다. 이 두 줄 사이에 모든 것이 들어 있다. `Spawn` 전의 오브젝트는 서버만 아는 평범한 Unity 오브젝트이다.
- 소유자는 요청을 보낸 사람(`SenderClientId`)이다. 요청에 "누구 것으로 만들까"를 실어 보내게 하지 않는다. 클라이언트가 남의 이름으로 아이템을 만드는 일을 막기 위해서이다.
- `DespawnItemRpc`와 `PassItemRpc`는 `LastOwnedBy(sender)`로 **요청한 사람이 소유한 아이템**만 찾는다. 그래서 남의 아이템을 없애거나 넘길 방법이 없다. 소유권을 서버의 권한 판단에 쓰는 가장 단순한 형태이다. 소유권을 넘기면 판단 결과가 바뀐다. 넘겨받은 사람이 없애기를 누르면 그 아이템을 없앨 수 있게 된다.
- `Item.All`은 각 컴퓨터가 자기 복사본들을 모아 둔 목록이다. 서버에서는 서버가 스폰한 아이템 전부가, 클라이언트에서는 서버가 복제해 준 아이템 전부가 들어 있다. `OnNetworkSpawn`에서 넣고 `OnNetworkDespawn`에서 뺀다. `OnDestroy`에서도 빼 두는 것은, 클라이언트가 실수로 `Destroy`한 경우에도 목록에 사라진 오브젝트가 남지 않게 하기 위해서이다. `NetworkBehaviour`의 `OnDestroy`를 재정의할 때는 `base.OnDestroy()`를 반드시 부른다.
- `NextClientAfter`는 `NetworkManager.ConnectedClientsIds`(서버가 알고 있는 접속자 번호 목록)에서 보낸 사람의 다음 사람을 고른다. 마지막 사람이면 처음 사람으로 돌아간다. 접속자가 한 명뿐이면 자기 자신이 나오므로 `next != sender` 검사로 넘기지 않는다.
- `OnOwnershipChanged`는 소유권이 바뀔 때 서버와 모든 클라이언트에서 불린다. 소유자 본인에게만 불리는 `OnGainedOwnership()`, 이전 소유자에게만 불리는 `OnLostOwnership()`과 다르다. 모든 컴퓨터의 색을 맞추려고 이 함수를 썼다.
- `SpawnLocalOnly`는 일부러 잘못된 일을 해 보는 시험이다. 서버에게 요청하지 않고 `Instantiate`만 한다. 이 오브젝트는 `Spawn`하지 않았으므로 그 컴퓨터에만 있는 평범한 큐브이다. `Item.All`에도 들어가지 않는다.
- `Item.OnGUI`의 이름표는 2.8절의 `PlayerProfile`과 같은 방식이다. 월드 좌표를 화면 좌표로 바꿔서 글자를 그린다.

**확인할 것.**

호스트 `교수`와 클라이언트 `철수`, `영희`를 접속시켜 놓고 시작한다. 접속 순서는 교수(0), 철수(1), 영희(2)이다. 세 화면 모두에서 오른쪽 아래의 **패널 숨기기**를 눌러 3D 화면을 보이게 한다. 패널 숨기기 상태에서도 `SpawnPanel`의 **만들기**, **없애기**, **넘기기** 버튼은 쓸 수 있다.

1. 철수가 **만들기**를 누른다. 세 화면 모두에 큐브가 같은 자리에 나타나고, 색은 철수 캡슐이 처음 칠해진 색과 같으며, 이름표는 `Item 숫자 / 철수`이다. 철수의 패널은 `아이템 1/8개 (내 것 1개)`, 영희와 교수의 패널은 `아이템 1/8개 (내 것 0개)`가 된다. 아이템을 만든 것은 철수의 컴퓨터가 아니라 서버이다. 에디터(호스트)의 Hierarchy에서 `Item(Clone)`을 선택해 Network Object의 `OwnerClientId`가 `1`인지 확인한다.
2. 영희가 **없애기**를 누른다. 영희는 소유한 아이템이 없으므로 아무 일도 일어나지 않는다. 남의 아이템은 서버가 건드리게 해 주지 않는다. 철수가 **없애기**를 누르면 큐브가 세 화면에서 모두 사라진다.
3. 철수가 **만들기**를 두 번 누른다. 이어서 **넘기기**를 누른다. 철수가 마지막으로 만든 아이템이 영희에게 넘어간다. 그 큐브의 색이 영희 캡슐의 색으로, 이름표가 `영희`로 **세 화면 모두에서** 바뀐다. 철수는 `내 것 1개`, 영희는 `내 것 1개`이다. 각 실행 파일의 Console(빌드는 `Player.log`, 2.13절)에 `Item 숫자 소유자 1 → 2`가 한 줄씩 기록된다.
4. 영희가 **없애기**를 누른다. 영희가 받은 아이템이 사라진다. 소유권을 넘겨받은 사람이 그 아이템에 대한 권한을 갖게 된 것이다. 철수는 자기에게 남은 아이템을 없앨 수 있다.
5. `민수`를 새로 접속시킨다. 접속하자마자 이미 있는 아이템들이 같은 자리에, 같은 소유자로 보인다. 변경이 일어났을 때 없었던 클라이언트도 현재 스폰된 오브젝트를 받는다.
6. 철수가 아이템을 하나 가진 상태에서 **종료**한다. 철수의 아이템이 모든 화면에서 사라진다. 소유자가 나가면 그 사람의 오브젝트도 함께 없어지는 것이 기본 동작이다. 같은 실험을 철수가 아이템을 영희에게 넘긴 뒤에 해 보면, 영희가 소유한 아이템은 남는다.
7. **내 컴퓨터에만 만들기 (시험)** 를 철수에서 누른다. 철수 화면에만 큐브가 나타나고 이름표는 `(스폰되지 않음)`이다. 5초 뒤에 저절로 사라진다. 영희와 교수의 화면에는 아무것도 나타나지 않는다. 패널의 개수도 늘지 않는다. 클라이언트가 `Instantiate`만 한 오브젝트는 다른 컴퓨터에 복제되지 않는다.
8. **만들기**를 여덟 번 넘게 누른다. 개수는 `8/8`에서 멈춘다. 서버가 한도를 정하고 있다.

**그 뒤에서 일어난 일.**

철수가 **만들기**를 눌렀을 때를 따라간다.

1. `SpawnItemRpc`가 서버에서 실행된다. `Instantiate`는 서버의 메모리에 큐브를 하나 만들 뿐이고, 다른 컴퓨터는 아직 아무것도 모른다.
2. `SpawnWithOwnership(1)`이 실행되면 서버가 이 오브젝트에 `NetworkObjectId`를 부여하고 스폰된 오브젝트의 표(SpawnManager)에 올린다. 그리고 접속해 있는 모든 클라이언트에게 "프리팹 `Item`을 이 위치에, 이 번호, 이 소유자로 만들어라"는 메시지를 보낸다. 서버의 `Item.OnNetworkSpawn()`도 이때 호출된다.
3. 각 클라이언트는 메시지를 받고 Network Prefabs 목록에서 `Item` 프리팹을 찾아 `Instantiate`한다. 번호와 소유자를 메시지의 값으로 채우고 `OnNetworkSpawn()`을 부른다. 프리팹이 목록에 없으면 이 단계에서 실패하고 `Failed to create object locally. NetworkPrefab could not be found. Is the prefab registered with this NetworkManager` 같은 오류가 그 클라이언트에 남는다.
4. `Despawn()`은 반대로 진행한다. 서버가 "이 번호의 오브젝트를 없애라"는 메시지를 보내고, 각 컴퓨터가 `OnNetworkDespawn()`을 부른 뒤 오브젝트를 `Destroy`한다.
5. `ChangeOwnership(2)`는 서버의 표에서 소유자를 바꾸고 모든 클라이언트에게 알린다. 모든 컴퓨터에서 `OnOwnershipChanged`가 불리고, 이전 소유자에게는 `OnLostOwnership()`, 새 소유자에게는 `OnGainedOwnership()`가 불린다.

스폰은 NetworkVariable, RPC와 다른 세 번째 동기화 수단이라고 볼 수 있다. 오브젝트가 **존재하는지 여부** 자체를 모든 컴퓨터에 맞춰 주는 수단이고, 늦게 접속한 사람에게도 현재 존재하는 오브젝트를 전달한다는 점에서 상태에 가깝다.

### 2.12.3 흔한 오류

- **서버(호스트)에서는 아이템이 보이는데 클라이언트에는 나타나지 않는다.** 프리팹을 Network Prefabs 목록에 등록하지 않았거나, 서버에서 `Spawn` 없이 `Instantiate`만 했다. `DefaultNetworkPrefabs.asset`에 `Item`이 있는지, `SpawnWithOwnership`이 호출되는지 확인한다. 클라이언트 쪽 Console에 `Failed to create object locally. NetworkPrefab could not be found. Is the prefab registered with this NetworkManager`가 있으면 목록에 없는 것이다. 클라이언트 빌드가 오래되어서 프리팹이 아직 없는 경우도 같은 증상이다.
- **클라이언트에서 만든 아이템이 다른 컴퓨터에 안 보인다.** 클라이언트가 `Instantiate`만 했기 때문이다. 스폰은 서버만 한다. 클라이언트는 서버에게 요청하는 RPC를 보낸다.
- **클라이언트의 Console에 `[Item(Clone)] Only server can spawn NetworkObjects.`가 남고 아이템이 생기지 않는다.** 클라이언트가 `Spawn()`을 직접 불렀다. 스폰은 서버(호스트)에서 실행되는 RPC 본문 안에서 한다. 접속하기 전에 불렀다면 `… is not listening, start a server or host before spawning objects.`가 남는다.
- **오브젝트를 없앴는데 다른 컴퓨터에는 남아 있다.** `Despawn()` 대신 `Destroy`를 클라이언트에서 불렀다. 그 클라이언트의 Console에 `[Invalid Destroy][Item(Clone)][NetworkObjectId:3] Destroy a spawned NetworkObject on a non-host client is not valid. Call Destroy or Despawn on the server/host instead.` 같은 오류가 남는다. 없애는 일은 서버가 `Despawn()`으로 한다.
- **`NullReferenceException`이 `SpawnItemRpc`에서 난다.** `SpawnHub`의 `Item Prefab` 칸이 비어 있다. Project 창의 프리팹 파일을 끌어다 연결했는지 확인한다.
- **소유자를 바꿨는데 색이나 이름표가 일부 컴퓨터에서만 바뀐다.** 소유자 본인에게만 불리는 `OnGainedOwnership()`에 갱신 코드를 썼다. 모든 컴퓨터에서 반응해야 한다면 `OnOwnershipChanged()`를 쓴다.
- **소유자가 나갔더니 아이템이 사라진다.** 기본 동작이다. 남기고 싶으면 프리팹의 Network Object에서 **Dont Destroy With Owner**를 켠다.

**이 절의 정리.**

- 네트워크 오브젝트를 만들고 없애는 일은 서버만 한다. 서버가 프리팹을 `Instantiate`하고 `Spawn()`(또는 `SpawnWithOwnership`)하면 모든 접속자에게 만들어지고, `Despawn()`하면 모두에게서 사라진다.
- 프리팹에는 Network Object가 있어야 하고 Network Prefabs 목록에 등록되어 있어야 한다.
- 소유자는 `OwnerClientId`이고 서버가 `ChangeOwnership`으로 바꾼다. 소유자는 서버로부터 위임받은 권한을 갖는다. 서버 코드는 요청한 사람이 소유자인지 확인하는 방식으로 권한을 판단할 수도 있다.
- 소유자가 접속을 끊으면 그 사람이 소유한 오브젝트도 사라진다. 남기려면 Dont Destroy With Owner를 켠다.
- 클라이언트가 요청에 실을 수 있는 것은 "무엇을 하고 싶은지"까지이다. 누구 것으로 만들지, 몇 개까지 만들 수 있는지는 서버가 정한다.

## 2.13 테스트와 디버깅

네트워크 프로그램은 컴퓨터 여러 대에서 동시에 실행되기 때문에, 문제가 생기면 어느 컴퓨터에서 무슨 일이 일어났는지부터 알아내야 한다. 또 같은 PC 안에서 시험하면 지연이 거의 없어서, 인터넷에서 나타날 문제를 개발 중에 볼 수 없다. 이 절에서는 시험 환경을 꾸리는 습관, 로그를 읽는 법, 통계를 보는 법, 지연과 패킷 손실을 일부러 거는 법, 그리고 지금까지 만난 오류 메시지를 한곳에 모은 오류 사전을 다룬다.

### 2.13.1 시험 환경과 시험 습관

한 PC에서 여러 인스턴스를 띄우는 방법은 2.5.5절에서 다루었다. 이 장의 실습은 에디터를 호스트로, 빌드한 프로그램을 클라이언트로 쓰는 방법(방법 B)을 기준으로 썼다. Multiplayer Play Mode(방법 A)를 쓸 때는 다음 제약을 알아 두어야 한다. Unity의 패키지 문서(2.0 버전)에 있는 내용이다.

- 최대 4개의 에디터 플레이어(메인 에디터 하나와 추가 세 개)를 한꺼번에 실행할 수 있다. 빌드한 인스턴스도 따로 추가할 수 있다.
- 추가 에디터는 가벼운 인스턴스여서 그 안에서 오브젝트를 만들거나 속성을 고칠 수 없다. 그런 변경은 메인 에디터에서 한다. Package Manager도 추가 에디터에서는 동작하지 않는다.
- 메인 에디터에서 Play 중에 바꾼 값은 Play를 멈추면 원래대로 돌아온다. 일반 에디터와 같다.
- 추가 에디터마다 Console이 따로 있다. 로그를 볼 때는 어느 인스턴스의 Console인지 먼저 확인한다.

NGO 문서는 이 밖에 ParrelSync라는 외부 도구도 소개한다. 프로젝트의 복제본을 만들어 여러 에디터로 실행하는 도구인데, Unity가 지원하는 도구는 아니다. 이 교재에서는 쓰지 않는다.

시험할 때 지킬 습관을 정리한다.

- **한 번에 하나만 바꾼다.** 코드와 설정을 여러 개 고치고 한꺼번에 실행하면 무엇이 원인인지 알 수 없다.
- **빌드는 항상 최신으로 맞춘다.** 코드, 프리팹, NetworkManager 설정을 고쳤다면 에디터와 모든 빌드가 같은 상태인지 확인한다. 서로 다르면 `NetworkConfig mismatch`로 접속이 거절되거나(2.5.8절), 고친 기능이 한쪽에서만 동작한다.
- **로그에 "누가 찍었는지" 적는다.** 같은 코드가 서버와 클라이언트에서 모두 실행되므로 로그 한 줄만 봐서는 어느 컴퓨터의 로그인지 알 수 없다. 2.8절의 `PlayerProfile`이 한 것처럼 역할과 ClientId를 앞에 붙인다. 예: `[Host] …`, `[Client 1] …`.
- **서버 쪽 사실은 서버 로그로 확인한다.** 클라이언트 화면에 보이는 것은 서버가 알려 준 결과일 뿐이다. 값이 이상하면 서버의 Console에서 서버가 실제로 받은 값을 본다.
- **여러 인스턴스를 늘어놓고 한 번에 본다.** 창을 나란히 놓고, 한 인스턴스에서 한 일이 다른 인스턴스에서 어떻게 보이는지 확인한다. 2.12절의 `ViewToggle`로 패널을 숨기면 3D 화면도 볼 수 있다.

### 2.13.2 로그 읽기

에디터에서는 Console 창을 본다(2.2.6절). 빌드한 프로그램의 로그는 파일에 남는다. Windows에서는 다음 위치이다.

- `C:\Users\사용자이름\AppData\LocalLow\<Company Name>\<Product Name>\Player.log`
- 직전에 실행한 로그는 같은 폴더의 `Player-prev.log`에 남는다.

한 가지 문제가 있다. 같은 빌드를 여러 개 실행하면 모두 같은 `Player.log`에 쓰려고 하므로 로그가 섞이거나 한 인스턴스의 로그만 남을 수 있다. 인스턴스마다 `-logFile` 인수로 다른 파일을 지정하면 확실하다. 프로젝트 폴더에 다음 배치 파일을 만들어 두면 한 번에 여러 클라이언트를 띄우고 로그를 따로 저장할 수 있다. 빌드 위치와 파일 이름은 2.5.5절에서 정한 `Builds\Client\NgoChat.exe`를 기준으로 했다.

`run_clients.bat`:

```bat
@echo off
rem 빌드한 클라이언트를 3개 띄우고 로그를 따로 저장한다. 이 파일은 프로젝트 폴더(Builds 폴더가 있는 곳)에 둔다.
if not exist "%~dp0logs" mkdir "%~dp0logs"
start "" "%~dp0Builds\Client\NgoChat.exe" -logFile "%~dp0logs\client1.log"
start "" "%~dp0Builds\Client\NgoChat.exe" -logFile "%~dp0logs\client2.log"
start "" "%~dp0Builds\Client\NgoChat.exe" -logFile "%~dp0logs\client3.log"
```

창이 모두 같은 자리에 겹쳐 뜨므로 마우스로 옮겨 놓는다. 로그는 `logs` 폴더의 파일을 텍스트 편집기로 열어 본다.

NGO의 로그와 오류를 읽을 때는 다음을 지킨다.

- **첫 번째 오류를 본다.** 오류 하나가 다른 오류를 줄줄이 일으킨다. Console을 위에서부터 읽고 가장 먼저 나온 빨간 줄의 원인을 해결한다. 나머지는 사라지는 경우가 많다.
- **NGO 로그의 상세 정도는 NetworkManager의 Log Level로 바꾼다.** 평소에는 Normal로 두고, 연결이나 스폰이 왜 안 되는지 추적할 때 **Developer**로 바꾸면 NGO 내부가 남기는 로그가 늘어난다. 확인이 끝나면 되돌린다.
- **클라이언트의 일을 서버 Console에도 남기고 싶으면 `NetworkLog`를 쓴다.** `NetworkLog.LogInfoServer("…")`는 메시지를 그 컴퓨터에 남기고, 가능하면 서버에도 남긴다. 클라이언트가 서버로 보낸 요청이 어디까지 갔는지 서버 Console 하나로 보고 싶을 때 유용하다. `LogWarningServer`, `LogErrorServer`도 있다. 오류 로그에서 만났던 `[Invalid Destroy]` 메시지도 이 함수로 남긴다.
- **오류 메시지는 한 글자도 바꾸지 말고 그대로 검색한다.** 메시지에는 클래스와 변수 이름이 들어 있어서 어디서 났는지 알려 준다. 이 절 끝의 오류 사전에 지금까지 만난 메시지를 모아 두었다.

### 2.13.3 통계 보기

지연과 전송량은 눈으로 보이지 않으므로 숫자로 확인한다. 세 가지 방법이 있다.

**방법 1: 우리가 만든 핑.** 2.9.4절의 **Ping** 버튼은 RPC 한 번이 왕복하는 시간을 잰다. 프레임 간격이 포함되고 서버(호스트)에서는 0이 나온다. 가장 단순하지만 어떤 조건에서든 같은 방식으로 비교할 수 있다.

**방법 2: 전송 계층이 재는 RTT.** Unity Transport가 신뢰성 있는 패킷의 응답 시간을 재서 보관하고 있고, 코드에서 읽을 수 있다. 서버는 접속한 클라이언트마다, 클라이언트는 서버까지의 값을 읽는다. 다음 도구를 만들어 `NetStarter` 오브젝트에 붙인다. 화면 오른쪽 아래에 작은 상자가 나타난다.

`Assets/Scripts/NetStatHud.cs`:

```csharp
using Unity.Netcode;
using UnityEngine;

// 전송 계층(Unity Transport)이 재는 왕복 시간(RTT)을 화면 오른쪽 아래에 보여 주는 도구.
// 이름이 Panel로 끝나지 않으므로 ViewToggle의 '패널 숨기기'를 눌러도 남는다.
public class NetStatHud : MonoBehaviour
{
    void OnGUI()
    {
        UiKit.Apply();
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        NetworkTransport transport = nm.NetworkConfig.NetworkTransport;
        string text;
        if (nm.IsServer)
        {
            // 서버(호스트)는 접속한 클라이언트마다 읽는다. 호스트 자신은 서버와 같은 프로그램이라 건너뛴다.
            text = "";
            foreach (ulong id in nm.ConnectedClientsIds)
                if (!(nm.IsHost && id == nm.LocalClientId))
                    text += $"클라이언트 {id}: {transport.GetCurrentRtt(id)} ms\n";
            if (text.Length == 0) text = "접속한 클라이언트 없음";
        }
        else
        {
            text = $"서버까지: {transport.GetCurrentRtt(NetworkManager.ServerClientId)} ms";
        }

        GUILayout.BeginArea(new Rect(Screen.width - 350, Screen.height - 150, 340, 115), GUI.skin.box);
        GUILayout.Label("전송 계층 RTT");
        GUILayout.Label(text.TrimEnd());
        GUILayout.EndArea();
    }
}
```

- `GetCurrentRtt(clientId)`는 밀리초 단위의 값을 돌려준다. 클라이언트에서는 서버의 번호(`NetworkManager.ServerClientId`, 0)를 넘긴다.
- Unity Transport는 신뢰성 있는 패킷에 대한 응답이 올 때마다 이 값을 갱신한다. 오가는 메시지가 없으면 값이 갱신되지 않아 마지막 값이 그대로 보이거나 0이 나올 수 있다. 값이 멈춰 있으면 2.9.4절의 **1초마다**를 켜서 메시지가 계속 오가게 한다.
- 2.9.4절의 핑과 값이 다를 수 있다. 측정하는 지점이 다르기 때문이다. 어느 쪽이든 조건을 바꿔 가며 같은 방법으로 비교한다.

**방법 3: Runtime Net Stats Monitor.** Multiplayer Tools 패키지가 제공하는 화면 위 통계판이다. 패키지는 NGO가 자동으로 설치하지 않는 선택 설치 항목이다.

1. `Window > Package Manager`에서 왼쪽 위 드롭다운을 **Unity Registry**로 두고 **Multiplayer Tools**(패키지 이름 `com.unity.multiplayer.tools`)를 설치한다. 컴파일이 끝날 때까지 기다린다.
2. Hierarchy에서 빈 오브젝트를 하나 만들고 선택한 상태에서 메뉴 **Component > Netcode > RuntimeNetStatsMonitor**를 고른다. Add Component에서 `RuntimeNetStatsMonitor`를 검색해도 된다.
3. Play를 하면 화면 위에 통계판이 나타난다. 문서에 따르면 NGO가 수집하는 통계와 사용자가 정의한 통계를 보여 준다. 무엇을 보여 줄지는 컴포넌트의 **Display Elements**에 연결된 구성 에셋(기본값 `DefaultNetStatsMonitorConfiguration`)이 정한다. 기본 구성은 고칠 수 없으므로, 고치려면 `Create > Multiplayer > NetStatsMonitorConfiguration`으로 새 에셋을 만들어 연결한다.

이 절의 실습은 방법 1과 방법 2로 충분하다. 방법 3은 전송량이나 메시지 개수가 궁금할 때 따로 써 본다.

### 2.13.4 지연과 패킷 손실을 일부러 걸기

같은 PC에서 시험하면 지연이 없고 패킷도 잃어버리지 않는다. 그런 환경에서만 시험한 프로그램은 인터넷에서 처음 만나는 문제를 개발 중에 놓치기 쉽다. 그래서 개발 중에 네트워크를 일부러 나쁘게 만들어 시험한다. 용어를 먼저 정리한다.

- **지연(delay, latency)**: 패킷이 한 방향으로 가는 데 걸리는 시간이다. 왕복 시간이 RTT이다.
- **지터(jitter)**: 지연이 일정하지 않고 흔들리는 정도이다. 패킷마다 걸리는 시간이 다르면 도착 순서가 바뀔 수도 있다.
- **패킷 손실(packet loss)**: 보낸 패킷 중 도착하지 못하는 비율이다.

도구는 두 가지를 소개한다.

**Network Simulator(Multiplayer Tools).** 에디터에서 쓰는 도구이고, 인스턴스마다 다른 조건을 줄 수 있다.

1. 위 2.13.3절의 방법 3에서처럼 Multiplayer Tools를 설치한다.
2. `NetworkManager`가 붙은 오브젝트(씬이 바뀌어도 남는 오브젝트)에 **Add Component**로 `NetworkSimulator` 컴포넌트를 붙인다.
3. 컴포넌트의 **Connection Preset**에서 조건을 고른다. 문서에 따르면 가정용 인터넷과 모바일 2G~5G 프리셋이 들어 있다. 숫자를 직접 정하려면 Project 창에서 마우스 오른쪽 버튼 > **Create > Multiplayer > NetworkSimulatorPresetAsset**으로 프리셋 에셋을 만들고, 에셋의 지연(Packet Delay, ms), 지터(Packet Jitter, ms), 패킷 손실(Packet Loss Interval, Packet Loss %) 값을 정한다. 그런 다음 **Connection Preset**을 **Custom**으로 바꾸고 **Custom Preset** 칸에 그 에셋을 연결한다. 버전에 따라 칸의 배치나 이름이 조금 다를 수 있다.
4. 에디터에서는 **Connection**을 **Disconnected**로 바꾸어 네트워크가 끊긴 상황을, **Duration of the lag spike (ms)** 를 정하고 **Trigger**를 눌러 잠깐 멈추는 상황(렉 스파이크)을 만들 수도 있다. **Scenario** 칸으로 조건이 시간에 따라 바뀌는 시나리오도 고를 수 있다.

알아 둘 점은 다음과 같다.

- 이 시뮬레이터는 NGO의 전송 계층 소스에서 에디터와 개발 빌드(Development Build)에서만 켜지도록 되어 있다(조건 `UNITY_EDITOR` 또는 `DEBUG`). 이 교재는 **에디터에서** 쓴다.
- 조건은 그 시뮬레이터가 붙은 인스턴스의 통신에만 적용된다. 에디터에만 붙이면 에디터는 느린 사용자이고 빌드한 프로그램들은 정상 사용자이다.
- 지연, 지터, 손실은 양방향에 같은 조건으로 적용된다고 NGO 문서가 설명한다. 따라서 Packet Delay를 50ms로 두었을 때 RTT가 50ms 늘어나는지 100ms 늘어나는지는 측정해서 확인한다(아래 실습 14).
- 값은 연결을 시작하기 전에 정한다. NGO 문서는 이 값이 Unity Transport의 드라이버가 만들어질 때 적용된다고 설명한다. 값을 바꾸었으면 연결을 종료하고 다시 시작해서 시험한다.
- 예전 자료에 나오는 `UnityTransport.SetDebugSimulatorParameters()`는 NGO 2.x에서 더 이상 지원되지 않고 효과가 없다(소스에 `[Obsolete]`로 표시되어 있다). 이 도구로 바뀌었다.

**Clumsy(Windows 전용).** 운영체제 수준에서 패킷을 늦추거나 버리는 외부 프로그램이다. Network Simulator를 쓸 수 없는 릴리스 빌드를 시험할 때 쓴다. NGO 문서가 소개하는 방법을 요약한다. 이 교재에서는 직접 실행해 보지 못했으므로 사용법은 Clumsy의 공식 안내를 따른다.

- Clumsy를 실행하고, 이어서 시험할 프로그램들을 실행한다. 시험 중에도 Clumsy의 값을 바꿔 가며 결과를 볼 수 있다.
- **Filter**에 시험할 통신만 잡히도록 조건을 적는다. 우리 프로그램은 UDP 포트 7777을 쓴다.
- **Lag**가 지연, **Drop**이 패킷 손실이다. Throttle, Out of order, Duplicate, Tamper는 모두 추가 지연처럼 나타난다.
- 같은 컴퓨터 안(localhost)의 통신에도 동작한다고 Clumsy 사이트가 설명한다.
- 시스템 전체에 적용되므로 같은 PC의 모든 인스턴스가 같은 조건을 받는다. **시험이 끝나면 반드시 끈다.** 켜 둔 채로 두면 PC의 다른 네트워크 통신도 느려진다.

**얼마나 나쁘게 걸어야 하는가.** NGO 문서는 샘플 게임을 시험할 때 PC에서는 100~150ms, 모바일에서는 200~300ms의 지연과 5~10%의 패킷 손실을 썼다고 소개한다. 100, 200, 500, 1000ms처럼 폭넓게 시험해 보는 것도 유익하다고 한다. 지연만 걸고 손실과 지터를 0으로 두면 현실적이지 않으므로, 기본 시험에도 작은 값을 넣어 두는 것이 좋다.

### 2.13.5 실습 14: 지연과 손실을 걸고 관찰하기

**목표.** 에디터의 클라이언트에만 지연과 손실을 걸고, 그 클라이언트와 다른 접속자의 화면에서 이동, 상태, 채팅이 어떻게 달라지는지 조건별로 기록한다. 지연에 따른 RTT를 직접 측정하고, 패킷 손실이 신뢰성 있는 전송과 신뢰성 없는 전송에서 어떻게 다르게 보이는지 확인한다.

**먼저 알아 둘 것.**

- 이번 실습에서는 역할이 지금까지와 반대이다. **빌드한 프로그램이 호스트**이고, **에디터가 지연이 걸리는 클라이언트**이다. 시뮬레이터가 에디터에서만 동작하기 때문이다. 빌드한 프로그램을 하나 더 띄워 정상 클라이언트로 쓴다.
- 새로 만드는 것은 `NetStatHud`(2.13.3절)뿐이다. 이동 관찰에는 2.7절의 **자동 이동**과 2.12절의 `ViewToggle`을 쓰고, RTT는 2.9.4절의 **Ping**과 `NetStatHud`로 잰다.
- 시험 조건은 지연 RTT 0ms, 100ms, 300ms와 손실률 0%, 5%, 10%를 조합한다. 조건 하나마다 아래 관찰 항목을 기록한다.

**단계.**

1. 2.13.3절의 `NetStatHud.cs`를 만들어 `NetStarter`에 붙인다. 2.13.3절의 방법 3처럼 Multiplayer Tools를 설치하고, `NetworkManager` 오브젝트에 `NetworkSimulator`를 붙인다. 프리셋 에셋 `Slow`를 하나 만들어 **Custom Preset**에 연결하고, **Connection Preset**을 **Custom**으로 둔다. 코드가 바뀌었으므로 빌드를 다시 한다.
2. 빌드한 프로그램 하나를 **Host**로 시작한다. 에디터와 빌드한 프로그램 하나를 각각 **Client**로 접속시킨다. 세 화면의 이름은 `교수`(호스트), `철수`(에디터), `영희`(빌드)로 한다. 세 화면에서 **패널 숨기기**로 3D 화면을 보이게 하고, 에디터 화면의 **자동 이동**을 켠다. 프리셋의 지연·지터·손실이 모두 0인 상태에서 이 기준선을 확인한다. 영희 화면에서 철수의 캡슐이 부드럽게 움직이고, 철수가 핑을 보내면 RTT가 몇 ms로 나오는지 적어 둔다.
3. **지연의 단위를 확인한다.** 에디터의 클라이언트를 종료하고, `Slow` 프리셋의 Packet Delay를 `50`으로 바꾼 뒤 다시 접속한다. 철수 화면에서 **Ping**을 눌러 RTT를 읽는다. 2단계의 기준선보다 약 100ms 늘었다면 설정값이 한 방향의 지연이라는 뜻이고(왕복에 두 번 적용), 약 50ms 늘었다면 설정값이 왕복 지연이라는 뜻이다. 이 결과에 맞추어 이후 시험에서 RTT가 약 100ms와 300ms가 되도록 Packet Delay를 정한다.
4. **지연만 걸고 관찰한다.** RTT 100ms와 300ms가 되도록 설정하고, 손실률은 0%로 둔 채 각각 연결을 새로 시작해서 다음 네 가지를 기록한다.
   - **입력 후 내 캐릭터가 반응하는 시간.** 철수가 방향키를 눌렀을 때 철수 자신의 화면에서 캡슐이 움직이기 시작하는 시간이다. 이동 권한이 Owner이므로 지연과 상관없이 거의 즉시 움직인다. 2.7.2절의 확인할 것 5번에서 본 것처럼 **Authority Mode**를 Server로 바꾸면(프리팹을 고치고 다시 빌드) 반응이 RTT만큼 늦어진다. 이것이 서버 권한 방식의 대가이다.
   - **다른 플레이어의 움직임이 끊기거나 튀는 정도.** 영희 화면에서 본 철수 캡슐의 움직임이다. 틱 간격으로 도착한 위치를 이어 붙이는 보간(Interpolate)이 켜져 있으므로 지연이 크더라도 부드럽게 보이는 대신 철수의 현재 모습이 늦게 보인다.
   - **NetworkVariable 갱신 지연.** 철수가 `StatePanel`의 **체력 -10**을 눌렀을 때 교수와 영희의 접속자 목록에 반영되는 데 걸리는 시간이다. 반대로 교수가 누르면 철수의 목록에 반영되는 시간도 본다.
   - **RPC 도착 순서.** 철수가 `1`, `2`, `3`, `4`, `5`를 빠르게 연달아 채팅으로 보낸다. 교수와 영희의 채팅 창에 어떤 순서로 나타나는지 본다. 기본 설정의 RPC는 신뢰성 있는 전송이어서 호출한 순서대로 도착한다(2.9.5절).
   - **채널 대화와 보너스 응답.** 같은 조건에서 철수가 채널을 만들고 영희가 입장하는 데 걸리는 시간, 철수가 **보너스 받기**를 누르고 결과가 나올 때까지의 시간도 적는다. 요청이 서버를 다녀와야 결과가 보이므로 RTT만큼 늦어진다.
5. **패킷 손실을 걸고 관찰한다.** 지연을 RTT 100ms로 두고 손실률을 5%, 10%로 바꾸어 같은 네 가지를 기록한다. 손실률 0%일 때와 달리 철수 캡슐의 움직임이 가끔 멈췄다가 따라잡는지, RTT 숫자가 가끔 크게 튀는지 본다. 신뢰성 있는 전송은 잃어버린 패킷을 다시 보내므로 데이터는 잃지 않는 대신 그만큼 늦게 도착한다. NetworkTransform의 기본 설정이 이 경우이다(NGO 소스에서 Use Unreliable Deltas가 꺼져 있으면 모든 위치 갱신을 신뢰성 있는 방식으로 보낸다).
6. **신뢰성 없는 전송과 비교한다.** 프로그램을 모두 종료한다. `NetPlayer` 프리팹의 NetworkTransform에서 **Use Unreliable Deltas**를 켜고 빌드를 다시 한다. 같은 조건(RTT 100ms, 손실률 10%)으로 다시 접속해서 영희 화면의 철수 캡슐을 본다. 변화분을 신뢰성 없는 전송으로 보내므로 일부는 잃어버리지만 다시 보내느라 기다리지 않는다. 잃어버린 위치는 다음 갱신이 메운다. NGO 문서와 툴팁은 이 옵션을 켜면 패킷 손실에 더 잘 견디지만 다른 신뢰성 메시지와의 순서가 달라진다고 설명한다. 끊김이 줄어드는지 직접 비교한다.
7. **끊김을 시험한다.** 지연과 손실을 0으로 되돌리고, 에디터에서 철수가 접속한 상태로 시뮬레이터의 **Connection**을 **Disconnected**로 바꾸어 본다. 네트워크가 끊긴 것과 같은 상황이다. 서버가 철수를 접속 종료로 처리하기까지 얼마나 걸리는지, 그동안 교수와 영희의 화면에서 철수의 접속자 목록과 캡슐이 어떻게 보이는지 본다. 2.10.6절에서 작업 관리자로 강제 종료했을 때와 비교한다.
8. **접속자를 늘려 본다.** 빌드한 클라이언트를 하나씩 늘리며(`run_clients.bat`을 쓰면 편하다) 각 클라이언트의 자동 이동을 켠다. 정원이 4명이므로 `ApprovalGate.cs`의 `MaxPlayers`(기본 4)를 8로 고치고 다시 빌드한다. 교수의 `NetStatHud`에 나오는 클라이언트별 RTT가 늘어나기 시작하는 접속자 수를 기록한다.

**확인할 것.**

지연을 0, 100, 300ms로 바꾸며 RTT가 이에 맞게 표시되는지 확인한다. 지연 상황에서 실습 3의 좌표 갱신, 실습 10의 채널 대화, 실습 12의 보너스 응답이 어떻게 달라지는지 기록한다. 자동 이동 클라이언트를 늘리며 RTT가 증가하기 시작하는 접속자 수를 기록한다.

기록은 조건 하나마다 한 덩어리로 적는다. 예를 들면 이렇게 적는다.

- 조건: RTT 100ms, 손실 5%
- 내 캐릭터의 반응: 즉시 / 영희 화면에서 철수 캡슐의 움직임: 부드러움, 가끔 멈춤 / 체력 변경이 다른 화면에 반영되는 시간: 약 0.2초 / 채팅 `1`~`5`의 도착 순서: 그대로
- 메모: Ping 숫자가 가끔 300ms까지 튐

**흔한 오류.** Editor와 빌드본 사이의 로컬 접속에서 지연을 설정하지 않고 결과를 일반화하는 경우, 패킷 손실 설정을 켠 채로 Reliable 전송 결과를 비교하는 경우.

앞의 오류는 지연이 0인 환경에서 본 결과를 "이 코드는 문제없다"로 받아들이는 것이다. 같은 PC의 시험은 문제가 없다는 증거가 아니라 문제를 아직 못 봤다는 뜻이다. 뒤의 오류는 손실을 걸고도 신뢰성 있는 전송에서는 데이터가 모두 도착하므로 "손실이 영향을 주지 않는다"고 결론짓는 것이다. 신뢰성 있는 전송에서는 손실이 데이터의 유실이 아니라 지연으로 나타난다. 손실이 데이터에 주는 영향을 보려면 신뢰성 없는 전송과 비교해야 한다.

그 밖에 자주 생기는 실수는 다음과 같다.

- **시뮬레이터를 켰는데 RTT가 그대로이다.** 값을 바꾼 뒤 연결을 새로 시작하지 않았거나, `NetworkSimulator`가 붙은 오브젝트가 `NetworkManager`와 다른 곳에 있거나, 호스트(서버)에서 Ping을 눌렀다. 호스트의 Ping은 항상 0이다. 시뮬레이터를 붙인 인스턴스가 에디터의 클라이언트인지도 확인한다.
- **세 인스턴스 모두에 지연이 걸린 것 같다.** 빌드한 프로그램에는 시뮬레이터가 없다. 에디터가 보내고 받는 통신이 느려진 것이므로 서버와 다른 클라이언트도 에디터와 주고받는 부분이 늦어진다. 정상이다.
- **접속자를 늘렸더니 RTT가 늘었다.** 서버가 느려진 것이 아니라 이 PC가 바빠진 것일 수 있다. 인스턴스가 늘면 프레임이 느려지고, 프레임 간격이 RTT에 더해진다(2.9.4절). 실제 서버의 한계를 보려면 별도의 부하 시험이 필요하며, 이 시험은 PC 한 대의 한계를 보여 줄 뿐이다.

**그 뒤에서 일어난 일.**

Network Simulator는 Unity Transport가 패킷을 보내고 받는 길목에 시뮬레이터 단계를 끼워 넣는다. NGO의 전송 계층 소스를 보면, 파이프라인 단계 목록에 `SimulatorPipelineStage`가 들어 있고 이 단계가 지연, 지터, 손실을 만든다. 패킷은 이 단계를 지나는 동안 정해진 시간만큼 붙들려 있거나 버려진다. 그래서 NGO나 게임 코드는 아무것도 모른 채로 느린 네트워크 위에서 동작한다.

신뢰성 있는 전송(Reliable)은 보낸 패킷의 응답이 오지 않으면 같은 패킷을 다시 보낸다. 그래서 손실은 데이터 유실이 아니라 도착 지연으로 나타난다. 순서도 지키므로 앞 패킷이 다시 올 때까지 뒤 패킷이 기다린다. RTT 숫자가 가끔 튀고 캡슐이 멈췄다가 한꺼번에 따라잡는 모습이 이 때문이다. 신뢰성 없는 전송(Unreliable)은 다시 보내지 않으므로 잃은 패킷은 그냥 사라진다. 대신 기다리지 않는다. 위치처럼 곧 새 값이 오는 데이터에 어울리고, 채팅이나 체력처럼 하나도 잃으면 안 되는 데이터에는 어울리지 않는다.

NGO에서 NetworkVariable의 변경과 RPC는 기본적으로 신뢰성 있는 전송이다. NetworkTransform의 위치 갱신도 기본 설정에서는 그렇다. 그래서 이 실습에서 NetworkVariable과 채팅은 손실률이 높아도 값을 잃지 않고 늦게 도착하기만 한다. 지연과 손실이 있어도 게임이 어느 정도 버티게 하는 방법은 2.16절에서 다룬다.

### 2.13.6 오류 사전

지금까지 만난 오류와 경고를 단계별로 모았다. 각 항목은 메시지, 원인, 자세한 설명이 있는 절 순서이다. 메시지가 길어서 앞부분만 적은 것도 있다. 먼저 Console의 첫 번째 오류를 찾고, 이 목록에서 비슷한 줄을 찾는다.

**컴파일할 때**

- `The type 'UserState' cannot be used as type parameter 'T' in the generic type or method 'NetworkList<T>'`: 구조체가 `IEquatable<T>`를 구현하지 않았다(2.11.10절).
- `Rpc method must end with 'Rpc' suffix!`, `RPC method must not be static!` 등: RPC 선언 규칙 위반이다(2.9.7절).
- `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class…`: Input System 설정과 예전 `Input` 클래스를 함께 쓴 것이다(2.3.7절).

**접속할 때**

- `Server failed to bind. This is usually caused by another process being bound to the same port.`: 같은 포트를 다른 프로세스가 쓰고 있다(2.5.8절).
- `NetworkConfig mismatch. The configuration between the server and client does not match`: 서버와 클라이언트의 설정이나 프리팹 목록이 다르다. 빌드가 오래된 경우가 많다(2.5.8절, 2.6.4절).
- `Failed to connect to server.`: 서버가 없거나 주소, 포트, 방화벽이 맞지 않다(2.5.6절, 2.14절).
- `No ConnectionApprovalCallback defined. Connection approval will timeout`: Connection Approval은 켜져 있는데 콜백이 없다(2.10.7절).
- `ConnectionApprovalCallback is defined but ConnectionApproval is disabled`: 콜백은 있는데 Connection Approval이 꺼져 있다(2.10.7절).
- `Only one ConnectionApprovalCallback can be registered at a time.`: 콜백을 `+=`로 여러 번 연결했다(2.10.7절).

**스폰과 오브젝트**

- `Failed to create object locally. NetworkPrefab could not be found. Is the prefab registered with this NetworkManager`: 프리팹이 Network Prefabs 목록에 없다(2.12.3절).
- `[Item(Clone)] Only server can spawn NetworkObjects.`: 클라이언트가 `Spawn()`을 불렀다(2.12.3절).
- `[Invalid Destroy][…] Destroy a spawned NetworkObject on a non-host client is not valid.`: 클라이언트가 스폰된 오브젝트를 `Destroy`했다. 서버가 `Despawn()`한다(2.12.3절).

**NetworkVariable, NetworkList**

- `Write permissions (Server) for this client instance is not allowed!`: 쓰기 권한이 없는 쪽이 값을 썼다(2.8.6절, 2.11.10절).
- `NetworkVariable is written to, but doesn't know its NetworkBehaviour yet.`: 스폰되기 전에 값을 썼다(2.8.6절).
- `ChatHub.users cannot be null. All NetworkVariableBase instances must be initialized.`: `NetworkList`를 `Awake()`가 아니라 `OnNetworkSpawn()`에서 만들었다(2.11.10절).

**RPC**

- `RpcException: The NetworkBehaviour must be spawned before calling this method.`: 스폰되기 전이나 디스폰된 뒤에 RPC를 불렀다(2.9.7절).
- `RpcException: This RPC can only be sent by the server.` / `… by its owner.`: 호출 권한이 없는 쪽에서 불렀다(2.9.7절).
- `RpcException: This method requires a runtime-specified send target.`: `SpecifiedInParams` RPC에 대상을 넘기지 않았다(2.9.7절).

**화면(IMGUI)**

- `Getting control N's position in a group with only N controls when doing Repaint`: `OnGUI` 안에서 화면의 구성이 바뀌었다. 버튼은 할 일을 `pending`에 적어 두고 `Update()`에서 실행한다(2.5.4절, 2.11.10절).
- 한글이 네모로 보인다: `UiKit.Apply()`를 `OnGUI`의 첫 줄에서 불렀는지 확인한다(2.5.8절).

메시지가 없는데 동작이 이상할 때는 증상으로 찾는다.

- **접속은 되는데 다른 사람의 캡슐이 안 보인다.** 프리팹이 목록에 있는지, 빌드가 최신인지 확인한다(2.6.4절).
- **키를 누르면 모든 캡슐이 함께 움직인다.** `IsOwner` 검사가 없다(2.7.3절).
- **한쪽 컴퓨터에서만 값이 바뀐다.** 쓰기 권한, 읽기 권한, RPC의 대상을 확인한다(2.8.6절, 2.9.7절).
- **늦게 접속한 클라이언트의 화면이 비어 있다.** 늦게 접속한 사람도 알아야 하는 정보를 RPC로만 보냈다. 상태는 NetworkVariable이나 NetworkList에 둔다(2.8.6절, 2.9.4절).
- **RPC를 불렀는데 아무 일도 일어나지 않는다.** 본문이 실행되는 컴퓨터가 어디인지(서버인가 클라이언트인가) 확인하고, 그 컴퓨터의 Console을 본다(2.9.7절).
- **고친 코드가 한쪽에서만 동작한다.** 빌드한 프로그램은 빌드한 시점의 코드를 가지고 있다. 다시 빌드한다(2.7.3절).

원인을 좁힐 때는 다음 순서로 묻는다. 이 코드는 **어느 컴퓨터**에서 실행되고 있는가(`IsServer`, `IsClient`, `IsOwner`). 이 오브젝트는 **스폰**되어 있는가(`IsSpawned`). 이 일을 **할 수 있는 쪽**(서버, 소유자)에서 하고 있는가. 값이나 메시지는 **누구에게** 가는가. 빌드는 **최신**인가.

**이 절의 정리.**

- 한 번에 하나만 바꾸고, 빌드를 최신으로 맞추고, 로그에는 역할과 ClientId를 적는다.
- 빌드의 로그는 `Player.log`에 남는다. 여러 개를 띄울 때는 `-logFile`로 인스턴스마다 파일을 나눈다.
- 오류는 첫 번째 것부터 본다. 메시지는 그대로 검색하거나 오류 사전에서 찾는다.
- 지연과 손실은 개발 중에 일부러 건다. 에디터에서는 Network Simulator를 쓰고, 릴리스 빌드는 Clumsy 같은 외부 도구를 쓴다.
- 신뢰성 있는 전송에서 손실은 지연으로, 신뢰성 없는 전송에서는 유실로 나타난다.

## 2.14 다른 PC와 연결하기: LAN, 서버 전용 실행, 인터넷

지금까지는 모든 프로그램이 한 PC에서 돌았다. 실제 서비스에서는 서버와 클라이언트가 서로 다른 컴퓨터에서 실행되고, 서버는 화면 없이 계속 켜져 있다. 이 절에서는 같은 공유기 안의 다른 PC와 연결하는 방법(2.14.1절), 화면 없는 서버를 실행하는 방법(2.14.2절, 실습 15), 인터넷을 통해 연결하는 방법(2.14.3절)을 다룬다. PC가 한 대뿐이라면 2.14.2절의 서버 전용 실행까지만 해 본다. 2.14.3절은 개념 설명이다.

### 2.14.1 같은 네트워크의 다른 PC와 연결하기

같은 공유기에 연결된 PC들은 `192.168.0.12`처럼 공유기가 나누어 준 사설 주소를 가진다. 클라이언트가 이 주소로 접속하면 된다. 필요한 것은 세 가지이다. 서버 PC의 주소, 서버가 그 주소로 오는 접속을 받고 있는 것, 방화벽이 그 접속을 막지 않는 것이다.

**서버 PC의 주소 확인.** 서버 PC에서 명령 프롬프트를 열고 `ipconfig`를 입력한다. 이더넷이나 Wi-Fi 어댑터의 **IPv4 주소**를 읽는다. VPN, Hyper-V, WSL 같은 가상 어댑터가 함께 보이면 공유기에 연결된 어댑터의 주소를 쓴다. 클라이언트 PC에서 `ping 서버주소`로 PC끼리 통신이 되는지도 본다. 다만 `ping`은 ICMP이고 우리 프로그램은 UDP를 쓰므로, `ping`이 성공해도 UDP 접속이 된다는 보장은 없고 `ping`이 막혀 있어도 UDP는 될 수 있다. 같은 네트워크에 있는지 정도만 알려 주는 확인이다.

**서버가 받는 주소.** 2.5.4절의 `NetStarter`는 호스트와 서버를 `SetConnectionData("127.0.0.1", 포트, "0.0.0.0")`로 시작한다. 세 번째 인자 `0.0.0.0`이 "이 PC의 모든 주소에서 접속을 받는다"는 뜻이어서 별도로 고칠 것이 없다. 이 인자를 빼면 서버는 같은 PC 안에서만 접속을 받는다.

**방화벽.** Windows 방화벽은 모르는 프로그램이 외부의 접속을 받으려 하면 막는다. 서버(호스트) 프로그램을 처음 실행할 때 나오는 대화 상자에서 **개인 네트워크**를 허용한다. 에디터에서 호스트를 시작하는 경우에는 Unity 에디터에 대한 같은 대화 상자가 나온다. 대화 상자를 놓쳤거나 막아 버렸다면 관리자 권한의 명령 프롬프트에서 UDP 7777을 허용하는 규칙을 직접 추가한다.

```bat
netsh advfirewall firewall add rule name="NgoChat UDP 7777" dir=in action=allow protocol=UDP localport=7777
```

시험이 끝나면 규칙을 지운다.

```bat
netsh advfirewall firewall delete rule name="NgoChat UDP 7777"
```

Unity Transport는 UDP를 쓰므로 규칙의 protocol이 UDP이다(2.5.7절). 네트워크가 **공용 네트워크**로 분류되어 있으면 방화벽이 더 엄격하게 적용되므로 PC의 네트워크 속성을 확인한다. 공유기에 따라 게스트 Wi-Fi나 AP 격리 기능이 같은 공유기에 연결된 기기끼리의 통신을 막는 경우가 있다. 이런 경우에는 PC를 같은 유선 또는 같은 일반 Wi-Fi에 연결한다.

### 2.14.2 실습 15: 서버 전용 실행과 다른 PC에서 접속하기

**목표.** 화면이 없는 서버 프로세스를 실행하고, 같은 PC와 다른 PC의 클라이언트가 그 서버에 접속해 채팅이 오가는 것을 확인한다.

**먼저 알아 둘 것.**

- 지금까지 서버를 켠 방법은 화면의 **Server** 버튼이었다. 화면이 없는 서버는 버튼을 누를 수 없으므로 **프로그램을 시작할 때 명령줄 인수로** 서버로 시작하게 한다.
- Unity 플레이어는 `-batchmode`(창과 입력 없이 실행)와 `-nographics`(그래픽 장치를 만들지 않음) 인수를 지원한다. 이 둘을 붙이면 화면 없이 서버만 실행할 수 있다. Unity에는 서버 전용 빌드(Dedicated Server) 설정도 있다. 서버 전용 빌드는 그래픽 관련 내용을 빼서 빌드 크기를 줄이는 방법이다. 빌드 설정의 이름과 위치는 버전마다 달라서 이 교재에서는 지금까지의 일반 Windows 빌드에 `-batchmode`를 붙여 서버를 실행한다.
- `-server`는 우리가 정하는 인수이다. 이 인수가 있으면 `NetStarter`가 서버를 자동으로 시작한다. 포트는 Unity Transport가 이미 이해하는 `-port` 인수로 바꾼다. `SetConnectionData()`를 부를 때 명령줄에 `-port`와 `-ip`가 있으면 코드에서 넘긴 값 대신 그 값을 쓴다(NGO 소스와 문서에 있는 동작이다).
- 서버는 30fps면 충분하다. 프레임 수를 제한하지 않으면 서버 빌드가 CPU를 한 코어 가득 쓴다고 NGO 문서가 경고한다.

**단계.**

1. `NetStarter.cs`의 `Start()`를 고친다. 기존의 이벤트 등록 아래에 명령줄을 읽는 부분을 추가한다.

```csharp
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
```

2. `File > Build Profiles`에서 Windows 빌드를 다시 한다(2.5.5절, `Builds\Client` 폴더). 코드가 바뀌었으므로 에디터와 모든 빌드가 같은 상태여야 한다. 같은 빌드를 서버로도 클라이언트로도 쓴다.
3. 프로젝트 폴더에 `run_server.bat`을 만든다(2.13.2절의 `run_clients.bat`과 같은 폴더).

```bat
@echo off
rem 화면 없는 서버를 실행한다. 로그는 logs\server.log에 남는다. 포트를 바꾸려면 -port 값을 바꾼다.
if not exist "%~dp0logs" mkdir "%~dp0logs"
start "" "%~dp0Builds\Client\NgoChat.exe" -batchmode -nographics -logFile "%~dp0logs\server.log" -server -port 7777
```

4. `run_server.bat`을 실행한다. 창이 뜨지 않는 것이 정상이다. 잠시 뒤 `logs\server.log`를 텍스트 편집기로 열어 서버가 시작되었는지 본다(아래 확인할 것). 작업 관리자의 **세부 정보** 탭에 `NgoChat.exe`가 있는 것도 확인한다.
5. **같은 PC에서 접속한다.** 에디터나 빌드에서 서버 IP를 `127.0.0.1`, 포트를 `7777`로 두고 **Client**를 누른다. 접속 승인을 쓰므로 이름과 접속 코드를 정확히 입력한다(2.10.4절). 채팅을 보내 본다.
6. **다른 PC에서 접속한다.** `Builds\Client` 폴더를 통째로 다른 PC에 복사한다(USB 메모리나 공유 폴더). 서버 PC의 방화벽이 UDP 7777을 허용하는지 확인하고(2.14.1절), 다른 PC에서 `NgoChat.exe`를 실행하여 서버 IP에 `ipconfig`로 확인한 서버 PC의 주소를 입력하고 **Client**를 누른다.
7. 서버를 끝낼 때는 작업 관리자의 **세부 정보** 탭에서 `NgoChat.exe`(창이 없는 것)를 선택하고 **작업 끝내기**를 누른다. 클라이언트도 같은 이름의 프로그램이므로 서버만 골라서 끝내야 한다. 이것은 정상 종료가 아니라 강제 종료이다. 클라이언트들은 2.5.6절의 9번처럼 한참 뒤에야 서버가 사라진 것을 알아챈다.

**확인할 것.**

1. `logs\server.log`에 `[Server] 접속 완료: ClientId 1` 같은 줄이 클라이언트가 접속할 때마다 늘어난다. `[서버]`로 시작하는 입장 알림과 채팅 기록도 남는다(2.11.3절의 `ServerLog`). 서버가 화면이 없어도 일을 하고 있다는 증거이다.
2. 같은 PC의 클라이언트와 다른 PC의 클라이언트가 서로의 채팅을 본다. 접속자 목록에 두 사람이 모두 나온다.
3. `-port 8000`으로 서버를 시작해 본다. `run_server.bat`의 포트를 바꾸고 서버를 다시 시작한 뒤, 클라이언트의 포트 칸에 `8000`을 입력해야 접속된다. 코드의 기본 포트는 7777인데도 명령줄의 값이 이겼다. (방화벽 규칙은 7777만 열었으므로 다른 PC에서는 새 포트 규칙을 추가해야 한다.)
4. 작업 관리자에서 서버 프로세스의 CPU 사용률을 본다. 거의 0%에 가깝게 나와야 한다. 궁금하면 `Application.targetFrameRate = 30;` 줄을 지우고 다시 빌드해 비교한다. 지우면 CPU를 한 코어 가득 쓰는 모습이 보인다.
5. 서버를 두 번 실행해 본다. 두 번째 서버는 같은 포트를 열지 못한다. 창이 없으므로 화면에는 아무 일도 없고, 두 번째 서버의 로그에만 `Server failed to bind. This is usually caused by another process being bound to the same port.`가 남는다(2.5.8절).

**그 뒤에서 일어난 일.**

`NgoChat.exe -batchmode -nographics -server -port 7777`로 실행하면 Unity 플레이어는 창을 만들지 않고 게임 루프만 돈다. 모든 스크립트의 `Start()`가 끝난 첫 `Update()`에서 `NetStarter`가 `pending`에 적힌 `"Server"`를 실행한다. 화면의 **Server** 버튼을 누른 것과 같은 경로이므로 `SetConnectionData("127.0.0.1", 포트, "0.0.0.0")`를 부르고 `StartServer()`를 한다. 이때 Unity Transport는 명령줄에서 `-port` 값을 찾아 코드가 넘긴 포트 대신 쓴다. 클라이언트에서 `-ip`와 `-port`로 접속 주소를 정하는 것도 같은 원리이다. 씬에 미리 놓아 둔 `ChatHub`는 서버가 시작될 때 스폰되므로 채팅 서버 기능이 모두 동작한다. 서버에는 화면이 없으므로 확인은 로그 파일로 한다. 로그에 서버의 기록(`ServerLog`)을 남겨 두었기 때문에 가능하다. 실제 서버 프로그램도 접속자 수와 오류, 주요 이벤트를 로그로 남기는 것이 기본이다.

**흔한 오류.**

- **다른 PC에서 접속이 안 된다.** 가장 흔한 원인은 방화벽이다. 서버 PC에서 UDP 7777 규칙이 있는지, 네트워크가 개인 네트워크인지 본다. 서버 주소가 맞는지(`ipconfig`), 서버가 실제로 실행 중인지(작업 관리자, `server.log`), 두 PC가 같은 공유기 아래에 있는지도 확인한다. 클라이언트의 `서버에 접속하는 중...`이 한참 이어지다 실패하는 것이 이 경우의 증상이다(2.5.6절의 7번).
- **서버를 켰는데 로그 파일이 없다.** `-logFile` 경로의 폴더가 없으면 파일이 만들어지지 않는다. `run_server.bat`의 `mkdir` 줄이 있는지 확인한다.
- **`-server`를 붙였는데 서버가 시작하지 않는다.** 코드를 고친 뒤 다시 빌드하지 않았다. 빌드한 프로그램은 빌드한 시점의 코드를 가지고 있다(2.7.3절).
- **서버 창이 없어서 이미 켜 둔 줄 모르고 또 켠다.** 포트가 겹쳐 두 번째 서버는 시작하지 못한다. 작업 관리자의 `NgoChat.exe` 목록을 확인한다.
- **서버가 CPU를 한 코어 가득 쓴다.** `Application.targetFrameRate`를 30으로 제한하지 않았거나, 수직 동기화가 켜져 있어서 무시되었다.

### 2.14.3 인터넷으로 연결하기: 포트 포워딩, 호스팅, Relay

![그림 2-14 인터넷 연결: 직접 연결과 Relay](images/ch02_fig14_direct_relay.png)

집이나 학교의 공유기 안에 있는 PC는 사설 주소를 가지고 있어서 인터넷의 다른 곳에서 직접 접속할 수 없다. 공유기(NAT)가 밖에서 오는 접속을 막기 때문이다. 인터넷에 서버를 공개하는 방법은 크게 세 가지이다.

**방법 1: 포트 포워딩(직접 연결).** 공유기 설정에서 "바깥에서 UDP 7777으로 오는 접속을 서버 PC의 사설 주소로 보내라"고 지정한다(그림 2-14의 왼쪽). 클라이언트는 서버 집의 **공인 IP**와 포트로 접속한다. 추가 서비스가 없어서 가장 단순하고 빠르다. 대신 다음 조건이 따른다.

- 공유기를 직접 설정할 수 있어야 한다. 서버 PC가 같은 사설 주소를 계속 받도록 주소를 고정하거나 공유기에서 예약해야 한다.
- 인터넷 연결이 공인 IP를 주어야 한다. 통신사가 여러 가정이 하나의 공인 주소를 나눠 쓰게 하는 경우(CGNAT)에는 포트 포워딩이 되지 않는다.
- 서버 PC의 포트가 인터넷에 열린다. 시험이 끝나면 규칙을 지운다. 이 교재의 서버는 승인 때 접속 코드를 평문으로 받는 연습용이므로(2.10절) 인터넷에 오래 열어 두지 않는다.

**방법 2: 인터넷에 있는 서버에서 전용 서버를 실행한다.** 공인 IP를 가진 클라우드 서버나 호스팅 서버에 서버 프로그램을 올려 실행한다. 클라우드의 방화벽(보안 그룹)에서 서버 포트를 열어 준다. 실제 서비스가 취하는 방식이다. 서버 프로그램이 항상 켜져 있고 누구의 PC에도 의존하지 않는다. 이 교재의 6장에서 C++ 서버를 이 방식으로 배포한다.

**방법 3: 릴레이(Relay).** 호스트와 클라이언트가 모두 중간의 Relay 서버에 접속하고, Relay 서버가 패킷을 전달한다(그림 2-14의 오른쪽). 호스트 PC는 밖으로 먼저 접속하므로 공인 IP도 포트 포워딩도 필요 없다. 클라이언트는 호스트가 알려 주는 짧은 **Join Code**로 접속한다. Unity에서는 Unity Gaming Services의 Relay를 쓴다. 대신 서비스 설정이 필요하고, 중계 서버를 한 번 더 거치므로 지연이 늘 수 있다. 사용량에 따라 요금이 붙을 수 있으므로 조건은 Unity의 서비스 안내를 확인한다.

Unity 문서의 Relay와 NGO 튜토리얼(Multiplayer Services 문서의 `Relay와 NGO` 튜토리얼)을 요약하면 순서는 다음과 같다.

1. Package Manager(Unity Registry)에서 `com.unity.services.multiplayer`를 설치하고, 프로젝트를 Unity Gaming Services에 연결한다. 이전에 따로 있던 Relay 패키지는 Unity 6에서 이 패키지로 대체되었다고 튜토리얼이 설명한다.
2. `NetworkManager`의 Unity Transport 컴포넌트에서 **Protocol Type**을 **Relay Unity Transport**로 바꾼다.
3. 호스트는 서비스를 초기화하고(`UnityServices.InitializeAsync()`), 익명으로 로그인하고(`AuthenticationService.Instance.SignInAnonymouslyAsync()`), Relay에 할당을 요청한다(`RelayService.Instance.CreateAllocationAsync(최대 접속자 수)`). 받은 할당 정보를 `UnityTransport.SetRelayServerData(...)`에 넣고, Join Code를 받아(`RelayService.Instance.GetJoinCodeAsync(...)`) 친구에게 알려 준 뒤 `StartHost()`를 한다. 전용 서버라면 `StartServer()`를 부른다.
4. 클라이언트는 같은 방식으로 서비스를 초기화하고 로그인한 뒤, 받은 Join Code로 `RelayService.Instance.JoinAllocationAsync(joinCode)`를 부르고, 같은 방식으로 `SetRelayServerData(...)`를 하고 `StartClient()`를 한다.

보다시피 NGO 코드(`StartHost`, `StartClient`, RPC, NetworkVariable 같은 모든 것)는 바뀌지 않는다. 바뀌는 것은 Unity Transport에 넣는 접속 정보(`SetConnectionData` 대신 `SetRelayServerData`)뿐이다. 비동기(`async`/`await`) 코드와 Unity 계정이 필요하므로 이 교재에서는 실습하지 않았고, 이 요약은 위 튜토리얼의 내용을 정리한 것이다. 실제로 쓸 때는 사용하는 패키지 버전의 튜토리얼 코드를 그대로 따라 한다. Multiplayer Services에는 이 과정을 감싸 주는 세션(Session) 기능도 있다.

세 방법 중 무엇을 고를지는 서비스의 성격이 정한다. 친구끼리 가볍게 하는 게임이면 Relay, 항상 켜져 있어야 하는 서비스면 인터넷에 있는 전용 서버가 알맞다. 4장에서는 Steam이 제공하는 연결 방식으로 같은 문제를 푼다.

**이 절의 정리.**

- 다른 PC와 연결하려면 서버 PC의 주소, 서버가 받는 주소(`0.0.0.0`), 방화벽(UDP 7777)을 확인한다.
- 화면 없는 서버는 `-batchmode -nographics`로 실행하고, 시작 방식과 포트는 명령줄 인수(`-server`, `-port`)로 정한다. 서버는 `Application.targetFrameRate`를 30으로 제한한다.
- 서버의 상태는 로그 파일로 확인한다. 로그 파일의 위치는 `-logFile`로 정한다.
- 인터넷으로 공개하는 방법은 포트 포워딩, 인터넷의 전용 서버, Relay 셋이다. 어느 방법이든 NGO 코드는 같고 접속 정보를 넣는 부분만 달라진다.

## 2.15 NGO의 내부 구조와 동기화 원리

앞의 실습은 NGO가 제공하는 기능을 사용하는 방법이었다. 이 절은 그 기능이 내부에서 어떻게 동작하는지 설명한다. 원리를 알면 "왜 값이 안 바뀌는가", "왜 RPC가 호출되지 않는가" 같은 문제를 추측이 아니라 근거로 해결할 수 있다.

이 절은 2.4절에서 그림으로 본 큰 구조를 실습을 거친 뒤에 다시 정리한 것이다. 새로 만드는 코드는 없다. 앞 실습에서 보았던 현상, 예를 들어 RPC로 보낸 채팅 한 줄이 한 프레임 늦게 나타난 것이나 같은 틱의 변경이 하나로 합쳐지던 것이 왜 그랬는지를 확인하는 절이다. 6장에서는 이 구조를 C++로 직접 만든다. NGO가 대신해 준 일이 무엇인지는 이 절의 마지막(2.15.6절)에 정리했다.

### 2.15.1 구성 요소와 메시지 흐름

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

이 흐름에서 실습 중에 부딪히는 규칙 몇 가지는 다음과 같다.

- Network Prefabs 목록: 서버가 프리팹을 스폰하면 클라이언트는 메시지에 담긴 프리팹 식별값으로 자기 목록에서 같은 프리팹을 찾아 복사본을 만든다. 서버와 클라이언트의 프리팹 목록이 다르면 클라이언트에 `Failed to create object locally. NetworkPrefab could not be found. Is the prefab registered with this NetworkManager` 오류가 난다(2.12.3절). 프리팹에 Network Object를 붙이면 프로젝트의 `Assets/DefaultNetworkPrefabs.asset`에 자동으로 올라가므로, 이 교재의 실습에서는 따로 등록할 일이 거의 없다(2.6.2절).
- 스폰 시점의 상태: 스폰 메시지에는 그 순간 NetworkVariable과 NetworkList가 가진 값이 함께 실려 간다. 중도 참여자가 `OnNetworkSpawn()`이 불리는 시점에 이미 현재 값을 읽을 수 있는 이유이다(2.8.4절).
- 씬에 미리 배치한 오브젝트: 씬에 처음부터 놓아 둔 NetworkObject(2.11절의 `ChatHub`)는 프리팹처럼 만들어지는 것이 아니라, 서버가 접속자에게 그 오브젝트의 상태를 알려 주어 각자의 씬에 있는 같은 오브젝트와 연결된다. 이 교재의 프로젝트는 씬이 하나뿐이므로 씬 전환 동기화는 다루지 않는다.

### 2.15.2 한 프레임 안에서 일어나는 일

NGO는 별도의 스레드에서 돌지 않고 Unity의 프레임 루프 안에서 동작한다. Unity는 매 프레임 정해진 단계를 순서대로 부르는데, NGO는 그 단계 사이에 자기 일을 끼워 넣는다. 우리가 쓴 `Update()`는 그 단계 중 하나일 뿐이다.

```text
한 프레임 (Unity가 이 순서로 단계를 부른다)

EarlyUpdate      전송 계층이 받은 데이터를 읽고, 받은 메시지를 처리한다
                 (RPC 본문 실행, NetworkVariable 값 반영, 스폰·디스폰, 접속 승인)
FixedUpdate      물리 계산
PreUpdate        네트워크 시간을 갱신한다. 틱이 지났으면 틱마다 NetworkVariable과
                 NetworkList의 변경 사항을 "보낼 메시지"로 만든다
Update           우리가 작성한 Update()
LateUpdate       우리가 작성한 LateUpdate()
PostLateUpdate   이번 프레임에 쌓인 보낼 메시지를 전송 계층으로 내보낸다
```

이 순서에서 알 수 있는 사실이 여럿 있다.

1. **받은 메시지는 프레임의 맨 앞에서 한 번에 하나씩 처리된다.** RPC 본문과 `OnValueChanged` 콜백은 Unity의 메인 스레드에서 차례로 실행된다. 같은 시각에 두 개가 겹쳐 실행되어 같은 변수를 동시에 건드리는 일이 없다. 2.11절의 `ChatHub`에 잠금(lock)이 하나도 없는 이유이다. 6장의 C++ 서버는 입출력을 처리하는 스레드가 여러 개이므로 이 보장이 사라지고, 같은 Room의 상태는 한 실행 문맥에서만 바꾸도록 직접 설계해야 한다(1.7.2절, 6.3.1절).
2. **보낼 메시지는 바로 전송되지 않고 대기열에 쌓였다가 프레임 끝에 한꺼번에 나간다.** `Rpc`를 호출하는 줄이 실행되는 순간에는 네트워크로 아무것도 나가지 않는다. 같은 프레임에서 호출한 RPC 열 개는 그 프레임의 끝에 함께 전송 계층으로 넘어간다.
3. **RPC는 틱을 기다리지 않고, NetworkVariable은 틱을 기다린다.** RPC는 호출한 프레임의 끝에 나간다. NetworkVariable과 NetworkList의 변경은 다음 틱이 되어야 메시지가 되므로, 기본 설정(초당 30틱)에서 최대 약 33ms가 더 걸린다. 같은 프레임에서 NetworkVariable을 바꾸고 RPC도 불렀다면 RPC가 먼저 도착할 수 있다. 2.11절에서 목록의 갱신과 채팅 알림 사이의 순서에 의존하지 않도록 코드를 쓴 이유이다.
4. **프레임이 느리면 네트워크도 느려진다.** 받는 일도 보내는 일도 프레임 루프 안에서 하므로, 프레임 속도가 낮으면 응답이 그만큼 늦는다. 에디터 창이 가려져서 프레임이 느려지면 핑이 커지는 것도 같은 이유이다(2.9.4절). 화면이 없는 서버도 이 루프를 그대로 돈다. 프레임 속도를 제한하지 않으면 CPU를 한 코어 가득 쓰므로 2.14.2절에서 30으로 제한했다.
5. **틱이 한 프레임에 여러 번 지날 수도 있다.** 프레임이 틱 간격(약 33ms)보다 길면 한 프레임에 틱이 둘 이상 지나가고, 그 틱마다 변경 사항이 모인다. 프레임이 짧으면 여러 프레임이 한 틱을 공유한다. 틱과 프레임은 별개의 주기이다(1.4.3절, 1.7.1절).

### 2.15.3 NetworkVariable의 동작

NetworkVariable은 값이 바뀔 때만 전송되는 상태 동기화 수단이다. 쓰기 권한(Write Permission)을 가진 쪽(기본은 서버)이 값을 바꾸면 틱 경계에서 변경 사항이 모아져 전송되고, 읽는 쪽은 OnValueChanged 콜백으로 변화를 받는다.

- 쓰기 권한이 없는 쪽이 값을 바꾸면 예외가 발생한다. 소유자가 직접 값을 바꾸게 하려면 Write Permission을 Owner로 지정한다.
- 같은 틱에 여러 번 바꾸면 마지막 값만 전달된다. 중간 값이 모두 필요하면 NetworkVariable이 아니라 RPC나 NetworkList를 쓴다.
- 새 클라이언트가 접속하면 현재 값을 한 번에 받는다. 이 점이 RPC와 가장 큰 차이이다. RPC는 호출 시점에 있던 클라이언트에게만 전달되고, 나중에 들어온 클라이언트는 과거 호출을 받지 못한다.
- 값 타입은 직렬화 가능한 구조여야 한다. 사용자 구조체는 INetworkSerializable을 구현하고, 문자열은 FixedString처럼 크기가 고정된 형식을 쓰는 것이 일반적이다.

**[NGO 2.x에서 확인한 점]** 위 설명을 NGO 2.13.3의 소스와 문서로 확인했다. 한 곳은 동작이 다르다.

- 쓰기 권한이 없는 쪽이 `Value`에 값을 쓰면 예외를 던지지 않는다. 값은 바뀌지 않고 Console에 `|Client-1|NetPlayer(Clone)|tint| Write permissions (Server) for this client instance is not allowed!` 같은 오류가 남을 뿐이다(2.8.6절). 예외가 아니라 오류 로그이므로 프로그램이 멈추지 않고 지나가 버린다. 그래서 값이 안 바뀌는 증상으로 나타나며, Console을 읽는 습관이 필요하다.
- NetworkList는 NetworkVariable과 달리 같은 틱의 변경을 합치지 않는다. 추가, 삭제, 한 칸의 변경이 하나씩 순서대로 전달되고 클라이언트가 같은 순서로 적용한다(2.11.6절).
- 늦게 접속한 클라이언트의 초기값은 스폰 메시지에 실려 오므로 `OnValueChanged`가 아니라 `OnNetworkSpawn()`에서 읽는다(2.8.4절).
- 변경 감지는 `Value`에 값을 대입하는 순간 이전 값과 비교해서 한다. 같은 값을 다시 대입하면 변경으로 치지 않아 전송되지 않는다. 구조체의 필드 하나만 고치면 대입이 일어나지 않아 변경이 감지되지 않으므로, 꺼내서 고친 뒤 다시 대입해야 한다(2.8.6절).

### 2.15.4 RPC의 종류와 신뢰성

RPC(Remote Procedure Call)는 한 번 일어난 일을 원격에서 실행하게 하는 호출이다. 서버로 보내는 ServerRpc, 클라이언트로 보내는 ClientRpc로 나뉘고, 사용하는 NGO 버전에 따라 속성(attribute) 이름과 지정 방식이 다르므로 설치한 버전의 문서를 확인한다.

- 기본은 신뢰성 있는 전송으로, 순서와 도착이 보장된다. 위치처럼 낡으면 의미가 없는 데이터에 쓰면 HOL Blocking을 겪을 수 있으므로 비신뢰 전송(Unreliable)을 선택할 수 있다.
- ServerRpc는 기본적으로 오브젝트의 소유자만 호출할 수 있다. 소유권이 없는 오브젝트에서 호출하려면 소유권 요구를 끄는 옵션을 쓰되, 그때는 서버에서 호출자를 검증해야 한다.
- RPC 안에서는 호출한 클라이언트의 ID(ServerRpcParams)를 얻을 수 있다. 채팅 서버에서 보낸 사람의 이름을 서버가 붙여 전달하는 이유는, 클라이언트가 보내는 이름을 믿지 않고 서버가 아는 접속자 정보를 쓰기 위해서이다.
- 호스트는 서버이면서 클라이언트이므로 자신이 호출한 ServerRpc가 같은 프로세스에서 실행된다. 호스트에서만 시험하면 이 차이가 드러나지 않으므로 반드시 별도 클라이언트로도 시험한다.

**[NGO 2.x에서 확인한 점]** 위 문장에 나오는 이름을 이 교재의 NGO 2.x 표기로 옮기면 다음과 같다(자세한 대응은 2.9.6절).

- 속성: `[ServerRpc]`는 `[Rpc(SendTo.Server)]`, `[ClientRpc]`는 `[Rpc(SendTo.ClientsAndHost)]`처럼 보낼 대상을 `SendTo`로 적는다. 대상은 서버, 소유자, 소유자가 아닌 쪽, 모든 클라이언트, 지정한 대상 등으로 더 다양하다(2.9.3절). 함수 이름은 `Rpc`로 끝내야 한다.
- 호출 권한: 1.x의 "소유자만 호출"은 2.x에서 `InvokePermission = RpcInvokePermission.Owner`로 적는다. 아무것도 적지 않은 기본값은 `Everyone`이다. 1.x의 "소유권 요구를 끄는 옵션"은 새 방식에 따로 없고 기본값이 곧 그 동작이므로, `Everyone`인 RPC는 서버가 반드시 `SenderClientId`로 호출자를 검증한다.
- 호출자 확인: `RpcParams`의 `rpcParams.Receive.SenderClientId`이다. 호스트가 자기 자신의 서버 RPC를 부르면 이 값은 호스트의 `LocalClientId`(0)이다.
- 전송 방식: `Delivery = RpcDelivery.Reliable`이 기본이고 `RpcDelivery.Unreliable`로 바꿀 수 있다. 신뢰성 있는 전송은 앞 메시지가 유실되면 뒤 메시지가 이미 도착해 있어도 재전송이 끝날 때까지 기다린다. 이것이 HOL Blocking이다(1.5.2절). 비신뢰 전송은 기다리지 않는 대신 순서와 도착을 보장하지 않는다.
- 권한 검사는 두 번 일어난다. 보내는 쪽이 보내기 전에 한 번 확인해 막고(`RpcException`), 받는 쪽도 한 번 더 확인해 막는다. 변조된 클라이언트가 보내는 쪽 검사를 우회해도 서버가 막는다(2.9.5절).
- 응답을 받을 수 없다: RPC는 값을 돌려주지 않는다. 요청 RPC와 응답 RPC를 한 쌍으로 만든다. 2.9.4절의 Ping과 Pong, 2.11.7절의 보너스 요청과 응답이 이 구조이다.

### 2.15.5 NetworkTransform과 틱

NetworkTransform은 위치·회전·크기를 NetworkVariable과 비슷한 방식으로 동기화한다. 기본은 서버 권한이므로 소유 클라이언트가 직접 움직이려면 소유자 권한을 쓰는 설정(버전에 따라 방법이 다르다)이 필요하다.

- 변화량이 임계값(threshold)보다 작으면 전송하지 않아 대역폭을 줄인다. 임계값이 크면 움직임이 계단처럼 보이고, 작으면 전송량이 늘어난다.
- Interpolate를 켜면 받은 값 사이를 부드럽게 이어서 보여 준다. 대신 화면에 보이는 위치가 실제 값보다 약간 과거의 값이 된다.
- 네트워크 틱은 NetworkManager 설정의 Tick Rate로 정한다. 초당 30틱이 기본값으로 알려져 있으며, 위의 변경 모으기와 전송이 이 틱에 맞춰 일어난다. 게임 프레임(Update)과 네트워크 틱은 서로 다른 주기이다.

**[NGO 2.x에서 확인한 점]**

- 소유자 권한 이동은 NetworkTransform의 **Authority Mode**를 `Owner`로 바꾸면 된다(2.7.2절). 1.x 자료의 `ClientNetworkTransform` 클래스는 필요하지 않다.
- 임계값의 기본값은 위치 0.001, 회전 0.01도, 크기 0.01이다. 1단위를 1m로 보면 위치는 1mm쯤 움직여야 전송 대상이 된다. 이 값 아래의 움직임은 보내지 않으므로, 아주 천천히 움직이는 물체가 조금씩 끊겨 보이면 임계값을 의심한다.
- **Interpolate**의 기본값은 켜짐이다. 보간 방식(Interpolation Type)은 기존의 Legacy Lerp(기본), 개선된 Lerp, Smooth Dampening 중에서 고를 수 있다. 어떤 값이 어떻게 보이는지는 2.16.5절에서 직접 비교한다.
- **Use Unreliable Deltas**의 기본값은 꺼짐이다. 그래서 이 교재의 실습에서 위치 갱신은 기본 설정으로 신뢰성 있는 전송(재전송과 순서 보장)을 쓴다. 켜면 중간 갱신은 비신뢰 전송으로 보내고 일정 주기로 전체 상태를 신뢰성 있게 다시 보내 어긋남을 바로잡는다. 손실이 있는 환경에서 어떻게 달라지는지는 실습 14(2.13.5절)에서 시험한다.
- **Tick Rate**는 NetworkManager의 `Tick Rate`이고 기본값은 30이다. 값을 올리면 초당 전송 횟수와 대역폭이 같이 늘어난다. 올린다고 해서 받는 쪽의 움직임이 저절로 매끄러워지지는 않는다(NGO 문서의 설명). 물리(FixedUpdate)를 쓰는 게임에서는 틱 속도를 Fixed Timestep과 같은 주기로 맞추라고 NGO 문서가 안내한다. 이 교재의 프로젝트는 물리를 쓰지 않아 기본값 30을 그대로 쓴다.

틱을 코드에서 쓰고 싶으면 `NetworkManager.NetworkTickSystem.Tick` 이벤트에 함수를 등록한다. 틱마다 한 번씩 호출된다. `OnNetworkSpawn()`에서 등록하고 `OnNetworkDespawn()`에서 해제한다(2.8.4절의 규칙과 같다). 2.17절의 관심 영역 검사처럼 매 프레임이 아니라 틱마다 해도 되는 일에 쓴다.

**네트워크 시간.** 같은 순간이라도 컴퓨터마다 시각이 다르다. NGO는 이를 맞추기 위해 시간을 두 가지로 제공한다.

- `NetworkManager.LocalTime`: 클라이언트에서는 서버보다 **앞선** 시각이다. 이 시각에 서버로 RPC를 보내면 서버에 도착할 때쯤 서버의 시각과 비슷해진다. 소유자 권한 오브젝트와 일반적인 시간 값에 쓴다.
- `NetworkManager.ServerTime`: 클라이언트에서는 서버보다 **뒤처진** 시각이다. 서버가 이 시각에 보낸 메시지가 클라이언트에 도착할 때쯤 클라이언트의 `ServerTime`과 비슷해진다. 서버 권한 오브젝트와 NetworkTransform의 위치 갱신과 맞출 때 쓴다.
- 값은 `double`이다. 서버를 몇 시간 이상 켜 두면 `float`로는 정밀도가 모자라므로 시간 계산은 `double`로 한다.

서버와 호스트에서는 두 시각이 같다. 2.16절의 보간과 지연 보상은 이 두 시각의 차이 위에서 설명한다.

### 2.15.6 NGO가 대신해 준 일과 6장의 구조

이 장의 서버는 몇 줄의 코드로 접속자를 받고, 오브젝트를 복제하고, 값을 동기화했다. 그 아래에서 NGO가 한 일을 6장의 C++ 서버가 직접 만들 때와 나란히 놓아 본다.

- 접속과 승인: NGO는 `ConnectionApprovalCallback`과 `ConnectedClients`를 제공했다. 6장에서는 접속 요청 패킷을 직접 받아 검사하고 세션 목록을 관리한다.
- 메시지 형식과 직렬화: NGO는 RPC 매개변수와 NetworkVariable 값을 알아서 바이트로 바꾸었다. 6장에서는 패킷 헤더와 본문을 정의하고 직렬화 코드를 직접 쓴다(1.6절).
- 오브젝트 번호와 복제: NGO는 `NetworkObjectId`를 붙이고 스폰 메시지를 모든 접속자에게 보냈다. 6장에서는 엔티티 번호를 직접 부여하고 생성·제거 패킷을 보낸다.
- 신뢰성과 순서: NGO는 Unity Transport 위에서 Reliable과 Unreliable을 골라 쓰게 했다. 6장에서는 TCP의 보장을 쓰고 그 대가(HOL Blocking)를 이해한다(1.5.2절).
- 틱과 변경 모으기: NGO는 틱마다 바뀐 값만 모아 보냈다. 6장에서는 서버 루프와 틱 예산을 직접 설계한다(6.12.1절).
- 실행 문맥: NGO는 모든 콜백을 메인 스레드에서 차례로 실행해 주었다. 6장에서는 I/O 스레드와 게임 로직의 경계를 직접 정한다(6.3.1절).
- 보낸 사람 확인: NGO는 `SenderClientId`를 채워 주었다. 6장에서는 세션이 보낸 사람이라는 사실을 서버가 직접 기록하고 모든 요청을 검증한다(1.8절).

이 목록의 오른쪽 열을 직접 만들어 본 사람은 NGO가 편리하게 감춘 것을 안다. 그래서 NGO의 동작이 이상할 때 어디를 의심해야 할지도 안다.

**이 절의 정리.**

- NGO는 Unity의 프레임 루프 안에서 일한다. 받은 메시지는 프레임의 맨 앞에서, 보낼 메시지는 프레임의 끝에서 처리하며, 모든 콜백은 메인 스레드에서 차례로 실행된다.
- NetworkVariable의 변경은 틱을 기다려 나가고, RPC는 호출한 프레임의 끝에 나간다. 둘 사이의 도착 순서는 보장되지 않는다.
- 쓰기 권한이 없는 NetworkVariable 쓰기와 권한이 없는 RPC는 조용히 거부되거나 오류 로그만 남긴다. 값이 안 바뀌면 Console부터 본다.
- 소유자 권한 이동은 Authority Mode, 위치 보간은 Interpolate, 전송 빈도는 Tick Rate와 임계값으로 정한다.

## 2.16 네트워크 지연과 지연 보정 기법

RTT·지터·패킷 손실이 게임 상태 표현에 미치는 영향을 테스트한다. 보간(Interpolation), 외삽(Extrapolation), 추측항법(Dead Reckoning), 클라이언트 예측과 서버 재조정의 개념을 비교하고, Unity Netcode에서 제공하는 동기화·전송 기능과 직접 구현해야 할 기능을 구분한다.

학습자가 소규모 게임의 지연 상황을 재현하고 캐릭터 움직임 및 상태 갱신의 차이를 관찰하는 것을 필수 실습으로 삼는다. 이 필수 실습은 2.13.5절의 실습 14이다. 이 절은 그 실습에서 본 현상이 왜 생기는지 설명하고, 그 현상을 줄이는 기법을 소개한다.

### 2.16.1 지연 시뮬레이션 실습

**[구현 순서]** Multiplayer Tools가 제공하는 Network Simulator 등 사용하는 버전에서 지원하는 도구로 지연, 지터, 패킷 손실을 설정한다. 조건은 RTT 0ms, 100ms, 300ms와 손실률 0%, 5%, 10%를 조합한다.

**[관찰 항목]** 입력 후 내 캐릭터가 반응하는 시간, 다른 플레이어의 움직임이 끊기거나 튀는 정도, NetworkVariable 갱신 지연, RPC 도착 순서를 조건별로 기록한다.

위 두 항목은 2.13.5절의 실습 14에서 구현해 두었다. 아직 하지 않았다면 이 절을 읽기 전에 먼저 한다. 실습 14의 기록을 옆에 두고, 관찰한 현상을 아래와 같이 원인과 대응 기법에 연결한다.

- **내 캐릭터가 입력 후 RTT만큼 늦게 반응했다.** 이동 권한을 서버(Authority Mode = Server)로 바꾸었을 때 나타나는 현상이다. 서버가 위치를 정하면 키를 누른 뒤 서버를 한 번 다녀와야 화면이 바뀐다. 이 시간을 숨기는 기법이 클라이언트 예측이다(2.16.7절).
- **다른 플레이어의 움직임이 끊기거나 튀었다.** 위치는 틱 간격으로 도착하고, 지터와 손실이 있으면 도착 간격이 불규칙해진다. 받은 값 사이를 이어서 그리는 기법이 보간이다(2.16.5절). 다음 값이 늦을 때 앞을 추정하는 기법이 외삽이다(2.16.6절).
- **NetworkVariable의 반영이 늦었다.** 변경은 다음 틱에 모여서 나가고(2.15.2절) 전송 지연이 더해진다. 값은 늦게 도착하지만 잃지는 않는다. 즉각적인 화면 반응이 필요한 값은 클라이언트가 먼저 보여 주고 서버가 정한 값이 오면 맞추는 방식(2.16.7절)을 쓴다.
- **RPC가 순서대로 도착했다.** 기본 전송이 신뢰성 있고 순서를 보장하기 때문이다. 손실이 있으면 순서는 지켜지고 대신 도착이 늦어진다(2.15.4절).

### 2.16.2 개념 정리

- 보간(Interpolation): 과거에 받은 두 상태 사이를 부드럽게 채운다. 약간 과거를 보여 주는 대신 끊김을 줄인다.
- 외삽(Extrapolation): 마지막으로 받은 상태와 속도를 이용해 미래 위치를 추정한다. 방향이 바뀌면 오차가 커진다.
- 추측항법(Dead Reckoning): 속도와 방향으로 위치를 예측하고, 예측과 실제의 차이가 클 때만 갱신을 보낸다.
- 클라이언트 예측: 내 입력을 서버 응답 전에 로컬에서 먼저 적용한다.
- 서버 재조정(Reconciliation): 서버 결과가 예측과 다르면 서버 상태로 되돌리고 그 이후의 입력을 다시 적용한다.

### 2.16.3 NGO가 제공하는 것과 직접 구현할 것

- 제공: NetworkTransform의 보간과 전송 임계값, 틱 기반 상태 전송, 신뢰성 있는 RPC와 NetworkVariable
- 직접 구현: 입력 순번 관리, 클라이언트 예측과 서버 재조정, 지연 보상(Lag Compensation)

사용하는 NGO 버전에 따라 예측을 돕는 기능이 추가되어 있을 수 있으므로 버전 문서에서 제공 범위를 확인하고, 제공되지 않는 부분만 직접 구현한다고 정리한다.

**[NGO 2.x에서 확인한 점]** NGO 2.13.3의 공식 문서와 소스로 제공 범위를 확인했다.

- **보간**: NetworkTransform의 `Interpolate`가 제공한다(2.16.5절).
- **클라이언트 예측의 일부**: NGO는 완전한 클라이언트 예측과 재조정(입력을 되감아 다시 적용하는 순환)을 제공하지 않는다. 대신 그 일부인 **클라이언트 예상(Client Anticipation)** 을 제공한다. 서버 값과 별도로 화면에 보여 줄 값을 둘 수 있는 `AnticipatedNetworkVariable<T>`와 `AnticipatedNetworkTransform`이 그것이다(2.16.7절). 입력 순번 관리와 되감아 다시 적용하는 부분은 직접 구현해야 한다.
- **외삽**: NGO 문서는 NetworkTransform에 기본적인 외삽이 있다고 적지만, NGO 2.13.3의 NetworkTransform 소스에서는 외삽을 켜고 끄는 설정을 찾지 못했다(물리 오브젝트용 Rigidbody의 보간 모드에 `Extrapolate` 항목이 있을 뿐이다). 이 교재는 외삽을 NGO가 제공하는 기능으로 쓰지 않는다.
- **지연 보상**: NGO에는 서버가 과거로 되감아 판정하는 기능이 없다. 문서도 직접 구현하라고 안내한다(2.16.8절).

### 2.16.4 지연을 숨기는 기법

서버가 값을 정하고 클라이언트가 그것을 받아 보여 주는 구조에서는 모든 동작에 왕복 지연이 끼어든다. RTT가 100ms이면 키를 누르고 화면이 반응하기까지 최소 100ms가 걸리므로, 상용 게임은 아래 기법으로 이를 숨긴다. 이 교재의 엔진 파트는 소유자 권한 이동을 쓰므로 이런 보정이 필요 없지만, 서버 권한 게임(6장 이후)에서는 필수이다.

지연의 크기에 대한 감각도 잡아 둔다. NGO 문서는 일반적으로 200ms 안팎부터 플레이어가 게임이 나빠졌다고 느끼기 시작하며, 슈팅 게임은 100ms 아래에서 가장 잘 동작하고 실시간 전략 게임은 500ms 정도까지 견딘다고 설명한다. 같은 지연이라도 장르에 따라 허용 범위가 다르다. 지연의 일부는 네트워크가 아니라 입력 장치, 렌더링, 수직 동기화, 모니터의 처리 시간에서 생긴다. 이 교재가 재는 RTT에는 이런 것들이 들어 있지 않다.

### 2.16.5 보간(Interpolation)

다른 플레이어의 움직임은 전송 간격(예: 33ms)마다 도착한다. 도착한 값으로 바로 이동시키면 순간이동하듯 끊겨 보이므로, 보여 주는 시각을 일부러 조금 과거로(예: 100ms 전) 두고 받은 두 스냅샷 사이를 선형 보간한다. 패킷 하나가 늦거나 사라져도 다음 값이 있으므로 부드럽게 보인다.

```text
서버 시각:    t0     t1     t2     t3
스냅샷:       S0     S1     S2     S3  ...
화면 시각:                 ^ (현재 - 보간 지연)
                      S1 <----- 보간 ----> S2
```

![그림 2-15 틱과 보간: 화면은 약간 과거를 보여 준다](images/ch02_fig15_tick_interp.png)

그림 2-15는 같은 내용을 그린 것이다. 파란 점이 틱마다 받은 스냅샷이다. 화면에 그리는 시각(갈색)은 가장 최근에 받은 시각(빨강)보다 보간 지연만큼 뒤에 있고, 그 시각의 위치는 두 스냅샷을 이은 선 위에서 읽는다.

보간 지연은 전송 간격의 2~3배가 흔히 쓰이며, 지연이 클수록 부드럽지만 상대의 모습이 더 과거가 된다. 이 값이 클수록 상대 화면의 반응이 늦어지는 것이 보간의 대가이다.

**[NGO 2.x에서 확인한 점]**

- NetworkTransform의 **Interpolate**(기본 켜짐)가 이 기능이다. 받은 상태를 버퍼에 쌓아 두었다가 약간 늦게 꺼내어 적용한다. 끄면 받은 값이 바로 적용되므로 지연이 크면 끊기거나 튄다.
- Interpolate를 켜면 보간 방식을 고를 수 있다. 처음의 **Legacy Lerp**(기본값, 예전 결과를 그대로 유지한다), 개선된 **Lerp**, 변화 속도를 고려하는 **Smooth Dampening**이 있고 위치, 회전, 크기에 각각 정한다.
- **Max Interpolation Time**(위치 기본 0.1초)은 부드러움과 정확도 사이를 조절한다. 값이 클수록 부드럽지만 급한 방향 전환의 중간 점을 놓칠 수 있고, 작을수록 정확하지만 지터가 있으면 조금 끊길 수 있다고 NGO가 설명한다. 이 값은 보간으로 구한 목표 위치를 부드럽게 따라가는 데 쓰는 시간(스무딩 시간)이고, 위에서 말한 보간 지연(받은 상태를 쌓아 두는 시간)과는 다른 값이다. 이 값은 Lerp smoothing을 켰을 때 쓰인다.
- 틱 속도를 올려서 표본을 늘려도 받는 쪽의 움직임이 저절로 매끄러워지지는 않고 대역폭만 늘어난다고 NGO 문서가 설명한다.
- NGO 문서는 NetworkTransform이 **클라이언트 쪽에서만** 보간한다고 설명한다. 호스트나 서버 화면의 다른 플레이어를 부드럽게 보이려면 직접 보간을 구현해야 한다.
- NGO 2.13.3의 변경 기록에는 보간이 서버 시계가 아니라 로컬 시계를 기준으로 하여 클라이언트에서 보간이 사실상 스냅처럼 보이던 문제를 고쳤다는 항목이 있다. 보간 결과가 기대와 다르면 설치한 NGO 버전의 `CHANGELOG`를 확인하고, 실습 14의 기록에 NGO 버전을 함께 적는다.

**[관찰]** 실습 14의 환경(빌드가 호스트, 에디터가 지연 클라이언트)에서 영희 화면의 철수 캡슐을 비교한다. 지연은 RTT 300ms, 손실 5%로 둔다.

1. 철수(에디터) 화면에서 플레이 중에 Hierarchy의 철수 캡슐을 선택하고, Inspector의 NetworkTransform에서 **Interpolate**를 끈다. NGO 문서는 소유자(이동 권한을 가진 쪽)가 플레이 중에 바꾼 값이 다른 접속자에게 동기화된다고 설명한다. 영희 화면에서 캡슐의 움직임이 계단처럼 변하는지 본다. 반영되지 않는 것 같으면 `NetPlayer` 프리팹의 설정을 바꾸고 다시 빌드한다.
2. **Interpolate**를 다시 켠다. 같은 움직임이 어떻게 달라지는지 본다.
3. **Interpolation Type**을 Lerp, Smooth Dampening으로 바꾸어 가며 비교한다. 방향을 급하게 바꿀 때 어느 쪽이 부드러운지, 캡슐이 실제 위치보다 얼마나 뒤처져 보이는지 기록한다. 결과는 설치한 NGO 버전과 프레임 속도에 따라 다를 수 있다.

### 2.16.6 외삽(Extrapolation)과 추측 항법

다음 스냅샷이 아직 도착하지 않았을 때 마지막 속도로 위치를 계속 예측하는 방식이 외삽(추측 항법, dead reckoning)이다. 직선으로 달리는 자동차에는 잘 맞지만 방향을 급하게 바꾸는 캐릭터에는 틀리기 쉽다. 틀리면 새 스냅샷이 도착했을 때 위치가 튀므로 시간 제한을 두거나 부드럽게 보정한다.

보간과 외삽은 서로 반대 방향이다. 보간은 두 값이 모두 있는 구간만 그리므로 항상 과거를 보여 주고 틀리지 않는다. 외삽은 가장 최근 값을 지나 앞을 추정하므로 현재에 가깝게 보여 주지만 틀릴 수 있다. 상용 게임은 보통 보간을 기본으로 쓰고, 보간에 쓸 다음 값이 없을 때만 짧은 시간 동안 외삽으로 버틴다.

추측항법은 같은 계산을 **보내는 쪽**에서도 한다. 보내는 쪽이 받는 쪽의 외삽과 똑같은 계산으로 상대가 추정한 위치를 알고 있다가, 실제 위치와 차이가 임계값을 넘을 때만 새 상태를 보낸다. 직선으로 움직이는 동안에는 아무것도 보내지 않으므로 전송량이 크게 줄어든다. NetworkTransform의 임계값(2.15.5절)과 목적이 같지만, 추측항법은 단순히 "변하지 않았을 때 보내지 않는" 것이 아니라 "예측대로 움직였을 때 보내지 않는" 것이다.

### 2.16.7 클라이언트 예측과 서버 보정

서버 권한 방식에서 내 캐릭터가 입력 후 100ms 뒤에야 움직이면 조작감이 나쁘다. 그래서 내 클라이언트는 입력을 받자마자 결과를 예측해 먼저 움직이고(client-side prediction), 서버에는 입력을 보낸다. 서버가 계산한 결과가 돌아오면 예측과 비교해 다르면 서버 값으로 되돌린다(reconciliation).

1. 클라이언트가 입력에 순번(예: 입력 #57)을 붙여 서버로 보내고, 같은 입력을 자신에게도 바로 적용한다. 보낸 입력은 목록에 보관한다.
2. 서버는 입력을 순서대로 처리하고, 결과 상태와 "마지막으로 처리한 입력 번호(#57)"를 클라이언트에게 보낸다.
3. 클라이언트는 서버가 처리한 번호까지의 입력을 목록에서 지우고, 서버가 보낸 상태에서 시작해 아직 확인되지 않은 입력(#58, #59...)을 다시 적용한다.
4. 예측이 맞았으면 화면에 변화가 없고, 틀렸으면 위치가 보정된다.

![그림 2-16 클라이언트 예측과 서버 보정](images/ch02_fig16_prediction.png)

그림 2-16은 이 순서를 그린 것이다. 클라이언트는 서버의 답을 기다리는 동안에도 새 입력(#58, #59)을 받아 화면에 바로 적용하므로 조작감이 유지된다. 답이 오면 서버가 처리한 번호(#57)까지의 입력을 목록에서 지우고, 서버가 보낸 상태에서 시작해 남은 입력만 다시 적용한다.

이 방식에서는 서버와 클라이언트의 시뮬레이션 코드가 같은 입력에 같은 결과를 내야 한다. 그래서 이동 계산을 공통 함수로 만들어 양쪽이 같이 쓴다. Unreal의 CharacterMovementComponent가 이 구조를 이미 구현해 두었다(3.5.6절).

**[NGO 2.x에서 확인한 점: 클라이언트 예상]** NGO는 위의 전체 구조를 제공하지는 않지만, 그 일부를 쉽게 만드는 기능을 제공한다.

- `AnticipatedNetworkVariable<T>`와 `AnticipatedNetworkTransform`은 값을 두 가지로 나누어 가진다. 화면에 보여 주는 **예상 값**(`Value`)과 서버가 정한 **권위 값**(`AuthoritativeValue`)이다. 클라이언트에서 `Anticipate(새 값)`를 부르면 예상 값만 바뀌어 화면이 즉시 반응하고, 서버가 보낸 값이 도착하면 권위 값이 갱신된다. 서버에서 `Anticipate`를 부르면 두 값이 모두 바뀌므로 같은 코드를 서버와 클라이언트가 함께 쓸 수 있다.
- 서버 값이 예상과 다르면 기본은 새 서버 값으로 바뀐다(스냅). 이것이 거슬리면 `Smooth()`로 이전 값에서 새 값까지 일정 시간 동안 부드럽게 이어 준다.
- 클라이언트가 예상한 직후에 도착한 서버 갱신 중에는 "내 요청이 반영되기 전의 옛 값"이 섞일 수 있다. NGO는 이를 **오래된 데이터(stale data)** 로 보고 `StaleDataHandling`으로 처리 방식을 정한다. `Ignore`(`AnticipatedNetworkVariable`의 기본값)는 옛 값을 무시해 깜빡임을 없애고, `Reanticipate`는 서버 값으로 되돌린 뒤 `OnReanticipate`에서 입력을 다시 적용할 기회를 준다.
- 이 기능은 "클라이언트가 요청 RPC를 서버에 보내는 동시에 같은 변경을 예상한다"는 가정 위에서 동작한다. 예를 들어 버튼을 누르면 `Anticipate`로 화면을 먼저 바꾸고, 같은 프레임에 서버로 요청 RPC를 보낸다.
- 완전한 예측(입력 순번 보관, 되감기와 다시 적용)을 만들려면 `OnReanticipate`와 `NetworkManager`의 전역 `OnReanticipate` 콜백에 직접 구현해야 한다. NGO 문서도 이는 복잡하여 숙련자에게 권한다고 안내한다. 이 교재는 개념만 다루고 구현하지 않는다.

이 기능을 쓰지 않고도 쓸 수 있는 가벼운 방법이 있다. **행동 예상(Action Anticipation)** 은 게임 규칙에 영향이 없는 효과(애니메이션, 효과음, 버튼의 "요청 중" 표시)만 입력 즉시 보여 주고, 실제 결과는 서버가 알려 줄 때까지 기다리는 방법이다. 서버가 요청을 거절해도 헛된 효과 하나가 재생되었을 뿐이므로 어긋날 것이 없다. 2.11.7절의 보너스 받기 버튼을 누른 즉시 결과 칸에 "요청 중..."을 보여 주고 서버의 응답이 오면 결과로 바꾸는 것이 이 방식이다. 2.11.7절의 `BonusPanel`에는 이 표시가 없으므로 연습 문제로 남겨 둔다(2.20절).

### 2.16.8 지연 보상(Lag Compensation)

사격 게임에서 내 화면의 적을 맞췄는데 서버에서는 적이 이미 이동한 뒤일 수 있다. 서버가 발사 요청을 받으면 그 클라이언트가 본 시점(요청 시각 - 그 클라이언트의 지연 - 보간 지연)으로 적의 위치를 되돌려 놓고 명중을 판정하는 것이 지연 보상이다. 맞은 쪽에서는 벽 뒤로 숨은 뒤 맞는 것처럼 느껴질 수 있으므로, 보상 범위를 제한(예: 최대 200ms)한다.

NGO 문서는 이 방식을 서버 쪽 되감기(Server Side Rewind)라고 부르고, 공격자에게 유리하게 판정하는 것이 목적이라고 설명한다. 공격자의 화면에서 조준한 적은 이미 RTT의 절반만큼 과거의 모습이므로, 그 판정을 서버의 현재 모습으로 하면 공격자는 항상 빗맞는다. 반대로 되감아 판정하면 맞은 쪽이 가끔 벽 뒤에서 맞는 일이 생긴다. 둘 중에서는 공격자가 항상 빗맞는 쪽이 훨씬 불만이 크므로 공격자 편을 든다. NGO에는 이 기능이 구현되어 있지 않아 직접 만들어야 한다.

이 기법들의 핵심은 지연을 없앨 수는 없으니 보이는 방식으로 감춘다는 점과, 그만큼 화면에 보이는 상태와 서버의 실제 상태가 달라질 수 있다는 점이다.

### 2.16.9 어떤 기법을 고를까

NGO 문서는 서버 권한 게임에서 지연에 대응하는 방법을 네 가지로 비교한다. 이 교재의 장별 위치와 함께 정리한다.

- **클라이언트 권한**(2장~4장의 이동): 가장 반응이 빠르고 구현이 쉽다. 대신 클라이언트가 거짓 값을 보내도 서버가 믿고, 클라이언트끼리 보는 세계가 어긋날 수 있다.
- **행동 예상**: 규칙에 영향이 없는 효과만 먼저 보여 준다. 안전하고 반응도 어느 정도 좋지만 화면의 연출이 서버 결과와 어긋날 수 있다. 쉽게 쓸 수 있다.
- **클라이언트 예측과 서버 보정**: 서버가 최종 판정을 하면서도 반응이 빠르다. 대신 어긋났을 때 보정해야 하고 구현이 복잡하다. 6장 이후의 서버 권한 이동에서 필요하다.
- **지연 보상**: 서버가 과거로 되감아 판정한다. 안전하고 공격자에게 공정하지만 벽 뒤에서 맞는 문제가 생긴다. 사격류 게임에서 쓴다.

기능 하나를 정할 때 NGO 문서가 권하는 질문은 이것이다. **"이 값을 서버가 고쳐 줄 수 있는가?"** 서버가 고쳐 줄 수 있다면 서버가 정하게 한다. 입력(키를 눌렀는가)이나 선택 표시처럼 클라이언트가 주인인 값, 체력이나 위치처럼 서버가 바로잡아야 하는 값을 나누고, 반응이 필요한 곳에만 위 기법을 더한다.

**이 절의 정리.**

- 지연은 없앨 수 없고 숨길 뿐이다. 숨길수록 화면의 상태와 서버의 실제 상태가 달라진다.
- 다른 플레이어의 움직임은 보간으로 부드럽게 만든다. 대가는 상대가 약간 과거로 보이는 것이다.
- 내 입력의 반응은 클라이언트 예측이나 행동 예상으로 숨긴다. NGO는 클라이언트 예상(`AnticipatedNetworkVariable`, `AnticipatedNetworkTransform`)까지 제공하고 나머지는 직접 만든다.
- 사격 판정의 지연 보상은 NGO에 없다. 서버 쪽 되감기를 직접 구현한다.
- 이 장의 소유자 권한 이동은 이런 보정이 필요 없는 대신 클라이언트를 믿는다. 이 선택이 가진 위험을 아는 것이 2장의 목표 중 하나이다.

## 2.17 관심 영역과 대역폭 관리

접속자가 늘면 서버가 모든 클라이언트에게 모든 오브젝트의 상태를 보내는 방식은 곧 한계에 닿는다. 100명이 서로의 상태를 모두 받으면 한 틱에 100×100 = 10,000건이 오간다. 그래서 각 플레이어에게 의미 있는 오브젝트만 보낸다. 이를 관심 영역 관리(Interest Management)라고 한다.

### 2.17.1 관심 영역을 정하는 세 가지 방식

- 거리 기반: 플레이어 주변 일정 반경 안의 오브젝트만 전송한다.
- 격자(grid) 기반: 월드를 칸으로 나누고 같은 칸과 인접 칸의 오브젝트만 전송한다. 거리 계산 비용을 줄인다.
- 방 단위: 같은 방에 있는 플레이어끼리만 전송한다. 6장의 구조이다.

![그림 2-17 관심 영역을 정하는 세 가지 방식](images/ch02_fig17_interest.png)

그림 2-17은 세 방식을 한 장에 그린 것이다. 거리 기반은 플레이어마다 원을 그려 그 안의 것만 보낸다. 플레이어가 움직일 때마다 모든 상대와의 거리를 구해야 하므로 인원이 늘면 계산이 급격히 늘어난다. 격자 기반은 칸 번호만 비교하면 되므로 계산이 가볍다. 대신 칸의 경계 근처에서는 실제로는 가까운 상대가 다른 칸이라는 이유로 빠질 수 있으므로 인접한 칸까지 포함한다. 방 단위는 방 번호가 같은지만 보면 된다. 규칙이 가장 단순하고 가장 많은 일을 덜어 주지만, 방 안에서는 모두가 모두를 본다.

2.11.5절의 채널도 같은 생각이다. 채널 알림을 모든 접속자가 아니라 같은 채널에 있는 사람에게만 보낸 것은 방 단위 관심 영역을 아주 작게 만든 것이다.

### 2.17.2 NGO에서 구현하는 방법

NGO에서는 NetworkObject의 가시성(visibility)을 서버가 지정하는 기능(NetworkShow, NetworkHide, CheckObjectVisibility)으로 구현할 수 있다. 보이지 않는 오브젝트는 해당 클라이언트에 생성되지 않으므로 대역폭뿐 아니라 월핵 방지에도 도움이 된다.

NGO 2.13.3의 문서와 소스로 확인한 사용법은 다음과 같다. 모두 **서버(호스트)에서** 호출한다.

- **`NetworkObject.NetworkHide(clientId)`**: 그 클라이언트에게서 이 오브젝트를 숨긴다. 그 클라이언트에서는 오브젝트가 디스폰되고(동적으로 스폰된 오브젝트는 파괴되고), 이후 이 오브젝트가 만드는 메시지(NetworkVariable의 변경, NetworkTransform의 위치, RPC)를 받지 않는다.
- **`NetworkObject.NetworkShow(clientId)`**: 숨겼던 오브젝트를 그 클라이언트에게 다시 보인다. 그 클라이언트에 오브젝트가 새로 만들어지고 현재의 NetworkVariable 값이 함께 전달된다.
- **`NetworkObject.IsNetworkVisibleTo(clientId)`**: 이 오브젝트가 그 클라이언트에게 보이는 상태인지 묻는다. 서버는 이 오브젝트를 아는 접속자의 목록(`Observers`)을 가지고 있고, 호스트의 Inspector에서도 볼 수 있다(2.6.2절).
- **`NetworkObject.CheckObjectVisibility`**: `bool 함수(ulong clientId)` 모양의 콜백을 등록한다. 서버는 오브젝트를 스폰할 때, 새 클라이언트가 접속해서 이미 있는 오브젝트를 보여 줄지 정할 때, `NetworkShow`를 부를 때 이 콜백에 물어본다. 등록하지 않으면 모든 클라이언트에게 보인다.
- **`NetworkObject.SpawnWithObservers = false`** 를 `Spawn()` 전에 설정하면 아무에게도 보이지 않는 상태로 스폰한다. 필요한 접속자에게 `NetworkShow`를 부른다.
- 여러 오브젝트를 한꺼번에 처리하는 `NetworkObject.NetworkShow(List<NetworkObject>, clientId)`와 `NetworkObject.NetworkHide(List<NetworkObject>, clientId)`도 있다.

이 기능을 쓸 때 알아 둘 점이 있다.

- **숨기면 파괴된다.** 렌더러를 끄는 것이 아니라 그 컴퓨터에서 오브젝트를 없애는 것이다. 다시 보이면 새로 만들어지고 `OnNetworkSpawn()`이 다시 불린다. 그 컴퓨터에서만 있던 값(NetworkVariable이 아닌 변수)은 사라진다.
- **RPC도 보이는 접속자에게만 간다.** NGO 문서에 따르면 `SendTo.Everyone`, `SendTo.ClientsAndHost`, `SendTo.NotOwner` 같은 대상은 모두 "현재의 관찰자 목록으로 걸러서" 보낸다. 그 오브젝트가 안 보이는 클라이언트는 그 오브젝트의 RPC를 받지 못한다.
- **가시성은 오브젝트마다 따로 정한다.** 어떤 플레이어의 캡슐을 숨겨도 접속자 목록(`ChatHub`의 NetworkList)은 그대로이다. `ChatHub`는 모두에게 보이는 별개의 오브젝트이기 때문이다.
- **주인과 서버에게는 숨기지 않는다.** 플레이어 오브젝트를 그 주인에게서 숨기면 자기 캡슐이 사라진다. 호스트의 서버 부분에서 숨기는 것도 의미가 없다. 호출하는 코드에서 건너뛴다.
- **서버만 부를 수 있다.** 클라이언트가 호출하면 `[이름] Only the server can change visibility!` 오류가 난다. 스폰되지 않은 오브젝트에 부르면 `Attempted NetworkShow while NetworkObject not spawned.`(NetworkHide는 `... is not spawned.`) 오류가 난다. 이미 보이는 오브젝트에 `NetworkShow`를 다시 부르거나 이미 숨긴 오브젝트에 `NetworkHide`를 다시 부르면 아무 일도 없이 무시된다(Log Level이 Developer일 때만 경고가 나온다).
- **검사는 틱마다 한 번이면 충분하다.** 매 프레임 모든 쌍의 거리를 구하면 서버가 느려진다. 틱 이벤트(2.15.5절)에서 검사하고, 인원이 많으면 거리 대신 격자 칸 번호로 비교한다.

### 2.17.3 실습 16: 가까운 캡슐만 보이게 하기 (선택)

**목표.** 플레이어의 캡슐이 가까이 있는 접속자의 컴퓨터에만 존재하게 한다. 멀어지면 그 컴퓨터에서 사라지고(디스폰), 가까워지면 다시 만들어지는(스폰) 것을 Console 로그와 화면으로 확인한다.

**단계.**

1. `ProximityVisibility.cs`를 `Assets/Scripts`에 만든다.

```csharp
using Unity.Netcode;
using UnityEngine;

// NetPlayer 프리팹에 붙인다. 서버(호스트)에서만 일한다.
// 이 캡슐을 가까이 있는 접속자의 컴퓨터에만 존재하게 한다.
public class ProximityVisibility : NetworkBehaviour
{
    [SerializeField] float viewDistance = 3f;       // 이 거리(미터) 안에 있는 접속자에게만 보인다

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkObject.CheckObjectVisibility += IsNear;                 // 새 접속자나 NetworkShow가 물을 때 서버가 부른다
        NetworkManager.NetworkTickSystem.Tick += OnTick;               // 틱마다 한 번씩 검사한다
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        NetworkObject.CheckObjectVisibility -= IsNear;
        NetworkManager.NetworkTickSystem.Tick -= OnTick;
    }

    // clientId의 플레이어가 이 캡슐 가까이 있는가
    bool IsNear(ulong clientId)
    {
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject == null)
            return true;                                               // 플레이어가 아직 없으면 일단 보이게 한다
        return Vector3.Distance(client.PlayerObject.transform.position, transform.position) <= viewDistance;
    }

    void OnTick()
    {
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            // 주인과 서버(호스트)에게는 항상 보인다.
            if (clientId == OwnerClientId || clientId == NetworkManager.ServerClientId) continue;

            bool near = IsNear(clientId);
            bool visible = NetworkObject.IsNetworkVisibleTo(clientId);
            if (near && !visible) NetworkObject.NetworkShow(clientId);
            else if (!near && visible) NetworkObject.NetworkHide(clientId);
        }
    }
}
```

2. `NetPlayer` 프리팹을 더블클릭하여 연 뒤 `ProximityVisibility`를 붙이고 닫는다. **View Distance**의 기본값은 3이다. 프리팹이 바뀌었으므로 빌드를 다시 한다(2.5.5절).
3. 시험은 호스트 하나와 클라이언트 셋으로 한다. 에디터를 Host로 시작하고(`교수`), 빌드한 프로그램 셋을 Client로 접속시킨다. 접속한 순서대로 `철수`, `영희`, `민수`이다. 2.13.2절의 `run_clients.bat`을 쓰면 편하다. 정원이 4명이므로 이 넷이 한도이다. 네 화면 모두에서 **패널 숨기기**(2.12.2절)로 3D 화면을 본다.
4. 캡슐은 접속 순서(ClientId)대로 x = −7, −5, −3, −1에 놓인다(2.6.2절). 서로 2미터씩 떨어져 있고 View Distance는 3이므로, 바로 옆 사람만 보이고 그 밖의 사람은 보이지 않는다.

**확인할 것.**

1. **호스트(교수) 화면에는 캡슐이 넷 다 보인다.** 서버에게 숨기지 않기 때문이다.
2. **철수 화면**에는 교수와 영희의 캡슐이 보이고 민수의 캡슐은 보이지 않는다. **영희 화면**에는 철수와 민수가 보이고 교수는 보이지 않는다. 민수 화면에는 영희만 보인다. 각 화면의 Console에서 어떤 `스폰:`과 `디스폰:` 줄이 나왔는지 본다. 접속 직후에는 새 플레이어가 모두의 컴퓨터에 한 번 만들어졌다가 다음 틱에 먼 쪽에서 사라질 수 있다.
3. 호스트(에디터)에서 철수의 `NetPlayer(Clone)`을 Hierarchy에서 선택하고 **Network Object** 컴포넌트의 **Observers**를 펼친다(처음에는 접혀 있다). `ClientId: 0`, `1`, `2`가 있고 민수의 `3`은 없다. 같은 방법으로 민수의 캡슐을 보면 `0`, `2`, `3`이 있고 철수의 `1`은 없다. 목록의 번호는 그 오브젝트를 아는 접속자이다.
4. 민수 화면에서 ←(왼쪽)를 약 1초 누른다. 민수가 철수 가까이 가는 순간 철수 화면에 민수의 캡슐이 나타나고, 철수 Console에 `스폰:` 줄이 찍힌다. 영희에게서 멀어지는 방향이 아니므로 영희 화면에는 계속 보인다. 다시 →로 멀어지면 철수 화면에서 사라지고 `디스폰:`이 찍힌다.
5. **접속자 목록은 그대로이다.** 철수 화면의 목록(`UserListPanel`)에는 민수가 계속 올라 있다. 가시성은 캡슐이라는 오브젝트에만 적용되고 접속자 목록은 `ChatHub`가 가진 것이기 때문이다.
6. **외치기가 달라진다.** 민수의 캡슐이 철수에게 숨겨진 상태에서 민수가 **외치기**(2.9.4절)를 누르면 철수 화면의 외침 목록에는 나타나지 않아야 하고 영희의 화면에는 나타난다. 외치기 RPC가 민수의 캡슐을 통해 나가고, 그 캡슐을 모르는 컴퓨터에는 RPC도 가지 않는다고 NGO 문서가 설명하기 때문이다. 채팅은 `ChatHub`로 가므로 철수에게도 보인다. 철수 화면에도 외침이 나타난다면 2.17.2절의 설명과 다른 것이므로 NGO 버전과 `SendTo` 대상을 확인한다.
7. 호스트의 **View Distance**를 키우려면 프리팹의 값을 바꾸고 다시 빌드한다. 값을 20으로 두면 모두가 모두를 본다.

**그 뒤에서 일어난 일.**

1. 서버는 플레이어 오브젝트를 스폰할 때 모든 접속자를 관찰자로 넣는다. `OnNetworkSpawn()`에서 `CheckObjectVisibility`를 등록한 것은 그 뒤이므로, 처음에는 모두에게 만들어진다.
2. 틱마다 `OnTick()`이 접속자 모두에 대해 거리를 계산한다. 가까운데 보이지 않으면 `NetworkShow`, 멀리 있는데 보이면 `NetworkHide`를 부른다. 서버가 그 클라이언트에게 오브젝트 파괴 메시지(`NetworkHide`)나 스폰 메시지(`NetworkShow`)를 보낸다.
3. 클라이언트는 파괴 메시지를 받으면 그 오브젝트의 `OnNetworkDespawn()`을 부르고 게임 오브젝트를 지운다. 스폰 메시지를 받으면 프리팹으로 새로 만들고 현재의 NetworkVariable 값(닉네임과 색)을 채운 뒤 `OnNetworkSpawn()`을 부른다. 철수 화면에서 민수가 다시 나타났을 때 이전과 같은 닉네임과 색인 이유이다.
4. 서버는 숨겨진 접속자에게는 그 오브젝트의 위치 갱신(NetworkTransform)도 RPC도 보내지 않는다. 먼 플레이어가 많을수록 줄어드는 전송량이 관심 영역 관리가 노린 것이다.
5. 새 접속자가 있으면 서버가 이미 있는 오브젝트마다 `CheckObjectVisibility`에 물어 보여 줄지 정한다. 그 접속자의 플레이어가 아직 만들어지지 않았으므로 `IsNear`는 `true`를 돌려주고, 다음 틱의 `OnTick()`이 실제 거리로 바로잡는다.

**흔한 오류.**

- **자기 캡슐이 사라진다.** `OnTick()`에서 `clientId == OwnerClientId`를 건너뛰지 않아 주인에게서 오브젝트를 숨겼다. 캡슐이 사라지면 조종도 할 수 없다.
- **호스트 화면에서 캡슐이 사라진다.** `clientId == NetworkManager.ServerClientId`를 건너뛰지 않았다.
- **`Only the server can change visibility!` 오류가 난다.** 클라이언트에서 `NetworkShow`나 `NetworkHide`를 불렀다. `OnNetworkSpawn()`의 `if (!IsServer) return;`을 확인한다.
- **아무 일도 일어나지 않는다.** 프리팹에 `ProximityVisibility`를 붙이지 않았거나, 프리팹을 바꾼 뒤 빌드를 다시 하지 않았다. View Distance가 너무 큰 값은 아닌지도 확인한다.
- **멀리 있는 캡슐이 보이다 말다 한다.** 거리가 View Distance 근처에서 왔다 갔다 하는 것이다. 보일 때의 거리와 숨길 때의 거리를 다르게(예: 3과 4) 두면 깜빡임이 줄어든다. 이 간격을 이력 현상(히스테리시스)이라고 한다.

### 2.17.4 그 밖에 전송량을 줄이는 방법

그 밖에 전송량을 줄이는 방법은 1.6절의 양자화와 델타 압축, 갱신 빈도를 거리나 중요도에 따라 달리하는 방법(가까운 상대는 30Hz, 먼 상대는 5Hz)이 있다.

**[NGO 2.x에서 확인한 점]** 이 방법들이 NGO의 어디에 해당하는지 정리한다.

- **델타 전송**: NetworkVariable과 NetworkTransform은 바뀐 값만 보낸다(2.15.3절, 2.15.5절). 임계값을 키우면 더 적게 보낸다.
- **양자화**: NetworkTransform의 **Use Half Float Precision**은 값을 반정밀도(16비트) 실수로 줄여 보내고, **Use Quaternion Compression**은 회전을 압축해서 보낸다. 대신 정밀도가 줄어든다.
- **쓰지 않는 축 끄기**: NetworkTransform에서 위치, 회전, 크기의 각 축을 동기화할지 따로 고른다. 바닥 위를 걷는 캐릭터의 회전은 Y축만 동기화하면 된다.
- **갱신 빈도**: NGO는 NetworkManager의 Tick Rate 하나로 전체 빈도를 정한다. 거리에 따라 빈도를 달리하는 기능은 NGO에 따로 없다. 필요하면 먼 상대에게는 덜 자주 보내는 RPC를 직접 만들거나, 아주 먼 오브젝트는 위의 `NetworkHide`로 관심 영역에서 뺀다.
- **측정**: 줄이기 전에 먼저 잰다. 2.13.3절의 Runtime Net Stats Monitor로 전송량과 메시지 개수를 보고, 관심 영역을 적용하기 전과 후를 비교한다.

**이 절의 정리.**

- 모든 접속자에게 모든 것을 보내는 방식은 인원이 늘면 N×N으로 늘어난다. 각 접속자에게 의미 있는 것만 보내는 것이 관심 영역 관리이다.
- 방식은 거리, 격자, 방 단위가 있고 뒤로 갈수록 계산이 가볍고 규칙이 단순하다.
- NGO에서는 `NetworkShow`, `NetworkHide`, `CheckObjectVisibility`로 오브젝트마다 누구에게 보일지 서버가 정한다. 숨긴 오브젝트는 그 컴퓨터에서 파괴되고 RPC도 가지 않는다.
- 주인과 서버에게는 숨기지 않는다. 검사는 틱마다 한 번이면 충분하다.

## 2.18 실습 프로젝트

2.11절의 채팅 GUI 씬을 하나의 프로젝트로 묶고, 검증된 코드를 2~4인 규모의 게임 클라이언트(캐릭터 이동)에 적용한다. 플레이어 접속, 이동·상호작용 동기화, 게임 진행을 접속자 관리와 메시지 중계 기준으로 검증한다.

지연·패킷 손실 시험에는 Multiplayer Tools의 통계 및 네트워크 시뮬레이션 기능을 사용 가능한 범위에서 활용한다(2.13절). 단순 위치 보간과 클라이언트 예측/서버 재조정은 서로 다르며, NGO가 모든 지연 보정 알고리즘을 자동 제공한다고 설명하지 않는다(2.16절).

이 장의 프로젝트는 이동·공격 같은 게임 규칙을 소유 클라이언트가 처리하고, 호스트(서버)는 접속자 관리와 중계를 맡는다. NetworkTransform의 Owner Authority나 NetworkVariable의 Owner 쓰기 권한은 편의 기능이지만, 경쟁형 게임의 최종 판정을 클라이언트에 맡겨도 안전하다는 의미는 아니다.

이 절은 지금까지 만든 NgoChat 프로젝트(2.5절~2.12절)를 그대로 이어서 쓴다. 새 프로젝트를 만들지 않는다. 채팅 패널, 접속 승인, 접속자 목록, 아이템 생성은 이미 있고, 이 절에서 더하는 것은 게임 진행(준비, 시작, 종료, 결과)과 아이템 획득 규칙이다.

### 2.18.1 프로젝트 요구사항

- 접속: Host와 Client 접속, 최대 인원 제한, 접속 종료와 재접속
- 상태: 플레이어 체력과 준비 상태, 게임 단계(Lobby, Playing, Result)
- 상호작용: 아이템 획득 같은 상호작용 한 가지 이상을 소유 클라이언트가 처리하고 결과를 중계
- 오브젝트: 투사체 또는 아이템을 서버가 Spawn과 Despawn
- 진행: 모든 플레이어가 준비하면 시작하고 종료 조건을 만족하면 결과 표시

### 2.18.2 역할 분담표

프로젝트의 각 행동에 대해 처리하는 쪽, 서버의 역할, 전파 방식을 표로 작성하고 구현이 표와 일치하는지 점검한다.

- 이동: 소유 클라이언트가 처리하고 서버는 위치를 중계한다. 전파는 NetworkTransform이다.
- 공격: 소유 클라이언트가 결과를 계산하여 RPC로 서버에 알리고, 서버가 다른 클라이언트에 중계한다. 전파는 체력 NetworkVariable과 이펙트 RPC이다.
- 아이템 획득: 소유 클라이언트가 충돌을 판단하여 서버에 알리고, 서버가 Despawn한다.
- 접속과 종료: 서버가 접속을 승인하고 접속자 목록을 관리하며 입장·퇴장을 모든 클라이언트에 알린다.
- 게임 시작과 종료: 서버가 접속자의 준비 상태를 확인하고 게임 단계를 전환한다.

표의 한 줄은 이런 모양이다. "행동 / 처리하는 쪽 / 서버의 역할 / 전파 방식 / 구현한 곳"의 다섯 칸으로 적고, 마지막 칸에 해당 스크립트와 함수 이름을 적는다. 이 칸이 비어 있거나 표와 코드가 다르면 그 행동은 설계를 정하지 않고 구현한 것이다. 다음은 아이템 획득을 적은 예이다.

- 행동: 아이템 획득
- 처리하는 쪽: 소유 클라이언트(아이템에 닿았는지 판단)
- 서버의 역할: 요청을 받아 아이템이 아직 있는지 확인하고 Despawn, 획득자의 점수를 올림
- 전파 방식: 요청은 RPC, 점수는 NetworkList(접속자 목록)의 값, 아이템 제거는 Despawn
- 구현한 곳: `PickupRpc`(서버 RPC), `ChatHub`의 목록

### 2.18.3 구현 순서와 점검

1. 채팅 GUI 씬에서 접속·채널·상태·보너스 검증  2. 게임 씬에 접속과 플레이어 생성  3. 이동 동기화  4. 준비 상태와 게임 단계  5. 상호작용  6. 오브젝트 생성·제거  7. 종료와 결과 화면  8. 접속 예외 상황 시험

접속자 관리 점검을 위해 정원 초과 접속, 게임 시작 후 중간 접속, 플레이 중 클라이언트 강제 종료, Host 종료, 재접속을 시험한다. 각 상황에서 서버(호스트)의 접속자 목록과 클라이언트 화면이 일치하는지 기록하여 역할 분담표와 함께 제출한다.

### 2.18.4 앞 절의 어느 부분을 쓰는가

위 요구사항은 새로 배우는 것이 없도록 이미 만든 것을 조합하면 된다. 각 항목을 어디서 만들었는지 정리한다.

- **접속, 최대 인원, 재접속**: 2.10절의 `ApprovalGate`(정원, 접속 코드, 이름 중복), 2.10.5절의 접속자 목록과 내보내기. 재접속하면 새 ClientId를 받는다(2.4.3절).
- **체력과 준비 상태**: 2.11.6절의 `UserState`의 `Hp`, `Ready`와 `ChatHub.State.cs`. 이미 서버가 검사하고 모든 화면의 목록에 반영한다.
- **게임 단계**: 이 절에서 새로 만든다(2.18.5절). 모든 접속자가 보아야 하고 중도 참여자도 알아야 하는 값이므로 NetworkVariable이다(2.4.6절).
- **이동**: 2.7절의 `PlayerMove`와 NetworkTransform(Authority Mode = Owner). 게임 단계가 `Playing`일 때만 움직이게 하려면 `PlayerMove.Update()` 맨 앞에서 단계를 검사한다.
- **아이템 생성·제거**: 2.12절의 `SpawnHub`, `Item`. 2.12절에서는 버튼을 누른 클라이언트의 요청으로 아이템을 만들었지만, 이 프로젝트에서는 게임이 시작될 때 서버가 직접 만든다. 만들고 제거하는 규칙(서버만 `Spawn`, `Despawn`)은 같다.
- **획득 알림과 결과**: 2.11.7절의 요청과 응답 구조. 소유 클라이언트가 획득을 요청하고 서버가 결과를 돌려준다.
- **결과 화면**: 2.11절의 `…Panel`과 같은 방식으로 `ResultPanel`을 하나 더 만든다(uGUI로 만들고 싶으면 2.11.9절). 단계가 `Result`일 때만 그린다. 패널은 네트워크를 직접 다루지 않고 `GameHub`의 값만 읽는다(2.11.2절). 화면 코드의 규칙(`UiKit.Apply()`를 먼저 부르고, 버튼은 `pending`에 적어 두었다가 `Update()`에서 실행한다)은 2.5.4절과 같다.
- **지연 시험**: 2.13.5절의 실습 14와 같은 조건(RTT 100ms/300ms, 손실 5%/10%)으로 게임 진행을 한 번 이상 해 본다.

### 2.18.5 게임 단계를 만드는 방법

게임 단계(`Lobby`, `Playing`, `Result`)는 접속자 모두가 같은 값을 보아야 하고, 중간에 접속한 사람도 지금 단계를 알아야 한다. 그래서 `NetworkVariable`로 만들고 쓰기 권한은 서버로 둔다. 채팅 허브와 같은 방식으로 씬에 서버가 소유하는 오브젝트를 하나 두고 담는다. 아래는 뼈대이다. 규칙은 직접 채운다.

```csharp
using Unity.Netcode;
using UnityEngine;

public enum GamePhase : byte { Lobby, Playing, Result }

// 게임 진행을 맡는 서버 소유 오브젝트. 씬에 하나만 두고 Network Object를 붙인다(ChatHub와 같은 방식).
public class GameHub : NetworkBehaviour
{
    public static GameHub Instance { get; private set; }

    // 모든 접속자가 읽고, 서버만 쓴다(기본 권한).
    public NetworkVariable<GamePhase> Phase = new NetworkVariable<GamePhase>(GamePhase.Lobby);

    public override void OnNetworkSpawn() { Instance = this; }
    public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

    // 준비 상태가 바뀔 때 서버가 부른다. 조건을 만족하면 게임을 시작한다.
    public void CheckStart()
    {
        if (!IsServer || Phase.Value != GamePhase.Lobby) return;
        // TODO: 접속자 목록(ChatHub.Users)을 보고 "두 명 이상이고 모두 Ready"이면 시작한다.
        //       시작할 때 Phase.Value = GamePhase.Playing; 아이템 생성 등 시작 준비도 여기서 한다.
    }

    // 종료 조건을 만족하면 서버가 부른다.
    public void Finish()
    {
        if (!IsServer || Phase.Value != GamePhase.Playing) return;
        Phase.Value = GamePhase.Result;
    }
}
```

몇 가지 정할 것이 있다.

- **누가 `CheckStart()`를 부르는가.** 접속자가 준비를 바꾸는 서버의 코드(`ChatHub`의 `SetReadyRpc`) 끝에서 `if (GameHub.Instance != null) GameHub.Instance.CheckStart();`를 부르면 서버가 한 곳에서만 시작을 판단한다(`?.`는 Unity 오브젝트에서 쓰지 않는다, 2.3.6절). 클라이언트가 "시작하자"는 RPC를 보내게 만들면 안 된다. 시작 조건을 판단하는 것은 서버의 일이다.
- **종료 조건.** 아이템을 모두 모으거나 정해진 시간이 지나는 것처럼 서버가 알 수 있는 조건으로 정한다. 시간은 서버의 `Time.time`이나 `NetworkManager.ServerTime`으로 센다. 클라이언트의 시계를 믿지 않는다.
- **게임 시작 후 중간 접속.** 단계가 `Playing`이면 접속을 막을지 받을지를 정한다. 막으려면 `ApprovalGate`의 접속 승인 콜백에서 `GameHub.Instance.Phase.Value != GamePhase.Lobby`일 때 `response.Approved = false`와 이유를 쓴다(2.10.4절). 받는다면 중도 참여자가 현재 단계와 아이템을 보는지 확인한다. 이 선택과 근거를 기록에 적는다.
- **결과 이후.** `Result`에서 다시 `Lobby`로 돌아갈 수 있게 할지 정한다. 돌아가려면 모든 접속자의 `Ready`와 `Hp`를 초기화하는 서버 함수가 필요하다.
- **화면은 값을 읽을 뿐이다.** `ResultPanel`처럼 단계에 따라 보이는 패널은 `GameHub.Instance.Phase.Value`를 읽어서 그린다. 값이 바뀌었을 때 알림이 필요하면 `Phase.OnValueChanged`에 등록하고 `OnNetworkSpawn()`에서 현재 값을 먼저 읽는다(2.8.4절).

### 2.18.6 완성 기준

아래 항목에 모두 "예"라고 답할 수 있으면 프로젝트가 끝난 것이다.

- 호스트 하나와 클라이언트 셋이 접속해서 서로의 이름, 체력, 준비 상태를 목록에서 본다.
- 정원을 넘는 접속, 접속 코드 오류, 이름 중복이 모두 이유와 함께 거부된다.
- 모두 준비를 누르면 게임이 시작되고, 시작 전에는 캡슐이 움직이지 않는다.
- 아이템을 서버가 만들고 소유 클라이언트가 획득을 요청하면 서버가 제거한다. 두 사람이 동시에 같은 아이템을 요청해도 한 사람만 얻는다.
- 종료 조건이 되면 모든 화면에 결과가 뜬다. 결과를 본 뒤 접속자 목록이 호스트와 같다.
- 클라이언트를 강제 종료하거나 호스트를 종료해도 남은 화면이 멈추지 않고 접속자 목록이 맞는다.
- 역할 분담표의 모든 행이 구현과 일치한다.
- RTT 100ms/300ms, 손실 5%/10%에서 진행해 본 기록이 있다.

위 네 번째 항목의 "두 사람이 동시에 같은 아이템을 요청해도 한 사람만 얻는다"는 서버가 RPC를 한 번에 하나씩 처리하는 덕분에 쉽게 풀린다(2.15.2절). 서버 RPC 안에서 아이템이 아직 스폰되어 있는지 확인하고, 있으면 Despawn한 뒤 획득자를 정하면 두 번째 요청은 이미 없는 아이템이어서 거절된다.

## 2.19 Steam 연동과 참고자료

본 장에서는 Unity Transport를 사용하여 NGO의 네트워크 객체·게임 규칙을 먼저 완성한다. 4장 Steam Listen Server에서는 동일한 Unity 프로젝트에 Steam 로비·친구 초대 기능을 추가하고, Unity Netcode와 호환되는 Steam 기반 Transport를 적용한다. Unity Transport만 설정했다고 Steam P2P/Relay 연결이 자동으로 되는 것은 아니다.

실습 참고자료: 고라니 유니티 강좌, 「멀티 서버 게임오브젝트 넷코드 마스터하기」(2024-12-27), https://goranitv.tistory.com/30 — 설치, 접속, 위치·변수·RPC·오브젝트 동기화의 단계별 학습 순서를 참고한다. 예제 코드와 설명을 그대로 복제하지 않고 교재용 프로젝트에 맞추어 재구성한다.

후속 참고자료: Unity Gaming Services Lobby — https://goranitv.tistory.com/31 / Unity Gaming Services Relay — https://goranitv.tistory.com/32. 두 서비스는 4장의 Steamworks Lobby/Steam Datagram Relay와 다른 플랫폼이며, 본 장에서는 선택 확장으로만 소개한다. Relay로 연결하는 순서는 2.14.3절에 요약했다.

공식 기술 기준: Unity Netcode for GameObjects 패키지 문서 https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@latest/ . 교재 원고와 코드에는 Unity Editor, NGO, Unity Transport, Multiplayer Tools의 실제 검증 버전을 함께 기재한다. 2024년 블로그의 패키지 설정과 코드 동작은 최신 프로젝트에서 재확인한다.

실습 진행 원칙: 참고 글의 기능별 학습 흐름을 따르되 예제 코드는 직접 작성한다. 2024년 글에 등장하는 API 명칭·NetworkPrefab 등록 방식·권한 설정은 교재에서 확정한 NGO 패키지 버전을 기준으로 다시 검증한다.

### 2.19.1 이 장이 참고한 자료

이 장은 아래 자료를 읽고 구성했다. 코드와 문장을 가져오지 않고, 단계별 학습 순서와 설명할 내용을 참고한 뒤 NGO 2.13.3의 공식 문서와 소스로 다시 확인했다. 인터넷의 강좌와 블로그는 쓰인 버전이 이 교재와 다르므로, 따라 하기 전에 어느 버전 기준인지부터 확인한다.

- **Netcode for GameObjects 공식 문서** (https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@latest/): 이 장의 API 이름, Inspector 항목, 오류 문구의 기준이다. 문서 주소의 `latest`는 가장 새 버전을 가리키므로, 설치한 패키지와 문서의 버전 표시가 같은지 확인하고 읽는다.
- **Manually configure Relay with Netcode for GameObjects**, Unity Multiplayer Services SDK 튜토리얼 (https://docs.unity.com/ko-kr/mps-sdk/tutorials/relay-and-ngo): `com.unity.services.multiplayer` 패키지를 설치하고, Unity Transport의 Protocol Type을 Relay로 바꾸고, 호스트는 Relay 할당과 Join Code를 만들고 클라이언트는 그 코드로 참가하는 순서를 보여 준다. 이 튜토리얼은 따로 있던 Relay 패키지가 Unity 6에서 이 패키지로 대체되었다고 안내한다. 2.14.3절의 Relay 요약이 이 튜토리얼을 따른다.
- **Netcode for GameObjects**, Unity 2023.1 한국어 매뉴얼 (https://docs.unity3d.com/kr/2023.1/Manual/com.unity.netcode.gameobjects.html): NGO 1.7.0을 소개하는 오래된 페이지이다. NGO가 무엇인지 한 줄로 확인하는 용도로만 본다. Unity 6.3 LTS에는 이 교재가 쓰는 2.x를 설치한다.
- **유니티 멀티플레이의 기반인 Netcode For GameObjects 튜토리얼**, kimyir 블로그, 2024-10-18 (https://kimyir.tistory.com/42): Unity 2021.3.20f1에서 설치, NetworkManager, 플레이어 프리팹, 호스트·서버·클라이언트 버튼, 이동 동기화, NetworkVariable의 권한과 사용자 정의 타입, RPC, 오브젝트 생성과 제거, NetworkAnimator, Runtime Net Stats Monitor를 차례로 다룬다. NGO 패키지 버전은 적혀 있지 않고 NGO 1.x 방식의 글이다. `ServerRpc`·`ClientRpc`와 `ClientNetworkTransform`은 2.9.6절의 대응을 보고 2.x 방식으로 바꾸어 읽는다.
- **[Unity6] Netcode 멀티 게임 구현**, velog pakadopa, 2025-01-28 (https://velog.io/@pakadopa/Unity6-Netcode-%EB%A9%80%ED%8B%B0-%EA%B2%8C%EC%9E%84-%EA%B5%AC%ED%98%84): Unity 6에서 패키지 설정, NetworkManager, 플레이어 프리팹, Multiplayer Play Mode로 시험하기, NetworkBehaviour, NetworkVariable, RPC의 `SendTo`와 `RpcParams`, 서버가 총알을 만들고 없애는 과정을 짧게 보여 준다. 이 글에도 NGO 패키지 버전은 적혀 있지 않으므로 설치한 버전을 확인하고 읽는다.
- **게임 오브젝트용 Netcode로 첫 번째 멀티플레이어 게임 제작**, Unity 블로그, 2022-10-26 (https://unity.com/kr/blog/games/building-a-multiplayer-game-first-time-netcode-for-gameobjects): 샘플 게임 Galactic Kittens를 NGO로 만든 과정이다. 새 접속을 캐릭터 선택 단계까지만 허용하는 연결 관리, 세션 도중 접속이 끊긴 플레이어를 처리하는 방법, 보스 전투의 시작을 RPC와 이벤트로 알리는 방법이 나온다. 앞의 둘은 2.10절과 2.18.5절이 다루는 문제와 같다. 2022년 글이므로 API 이름은 오래되었고, 설계 이야기로 읽는다.

더 공부할 때는 Unity가 NGO 샘플로 공개한 프로젝트를 열어 볼 수 있다. 샘플은 NGO 패키지와 별개의 저장소에 있고 각각 만들어진 Unity 버전이 정해져 있다. 이 교재의 프로젝트에 가져오지 말고, 저장소의 안내에 따라 별도 프로젝트로 연다.

- **Bitesize Samples의 Multiplayer Use Cases** (https://github.com/Unity-Technologies/com.unity.multiplayer.samples.bitesize): 기능 하나씩을 장면 하나로 보여 주고 장면마다 튜토리얼이 들어 있다. NGO 문서는 이 샘플의 장면으로 Anticipation(2.16.7절), NetvarVsRpc·NetworkVariables(2.8절, 2.9절), RPCs(2.9절)를 소개한다.
- **Boss Room** (https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop): NGO로 만든 협동 RPG 샘플이다. 연결 관리와 서버 권한 설계의 실제 구성을 볼 수 있다. NGO 2.13.3의 문서는 Boss Room이 2026년부터 적극적으로 관리되지 않고 최신 NGO·Unity와 맞지 않을 수 있다고 안내한다. 실행해 보는 용도보다 코드 구조를 읽는 용도로 본다.

### 2.19.2 검증 버전 기록

이 장의 설명과 코드는 아래 버전의 문서와 소스를 기준으로 썼다. 학습 시점의 설치 버전이 다르면 이 기록과 비교하고, 달라진 부분은 설치한 버전의 문서와 CHANGELOG에서 확인한다.

- 문서·소스를 대조한 기준
  - Unity Editor: Unity 6.3 LTS (6000.3)
  - Netcode for GameObjects: 2.x. 문서와 소스는 2.13.3으로 확인했고, 주요 API 이름은 2.7.0의 소스와도 대조했다. 설치할 때는 Package Manager에서 2.x 가운데 가장 높은 버전을 고른다.
  - Unity Transport: 2.6.0 (NGO 2.13.3이 요구하는 버전)
  - Multiplayer Tools: 2.2 (선택 설치)  /  Multiplayer Play Mode: 2.0.x 계열 (선택 설치)
- 이 장의 실습을 Unity 6.3 LTS 에디터에서 처음부터 끝까지 실행해 본 버전: Unity Editor [실행 확인 후 기재]  /  NGO [실행 확인 후 기재]  /  Unity Transport [실행 확인 후 기재]
- 4장에서 사용할 Steam 기반 Transport 구현체와 호환 버전: [확인 후 기재]

코드에 쓴 NGO의 클래스, 메서드, 이벤트 이름과 호출 형태, 그리고 본문에 인용한 오류·경고 문구는 2.13.3의 소스와 하나씩 대조했다. 에디터에서 컴파일하고 실행해 본 결과는 바로 위의 '실행해 본 버전'에 기록한다. 에디터에서 실행해야 알 수 있는 부분, 곧 Inspector 항목의 이름과 위치, 로그 문구, 화면 배치는 설치한 버전과 다를 수 있다. 다르면 설치한 버전의 문서와 CHANGELOG부터 확인한다.

### 2.19.3 4장으로 이어지는 부분

4장에서는 이 장의 프로젝트를 그대로 가져가서 연결 방식만 바꾼다. 무엇이 그대로이고 무엇이 바뀌는지 미리 정리한다.

- **그대로인 것**: 채팅 화면(`…Panel`), 네트워크 코드(`ChatHub`), 접속 승인(`ApprovalGate`의 "이름|코드" 검사와 정원), 플레이어 이동과 NetworkVariable, 서버가 소유한 오브젝트의 스폰. 4장의 시험 항목은 이 장의 것과 같은 결과가 나와야 한다.
- **바뀌는 것**: NetworkManager의 전송 계층(Transport). 이 장에서는 Unity Transport가 주소와 포트로 UDP 연결을 만들었다. 4장에서는 Steam이 제공하는 연결 방식을 쓰는 Transport로 바꾼다. 접속 화면(`NetStarter`)의 주소와 포트 입력은 로비에서 받은 호스트 정보로 바뀐다.
- **새로 생기는 것**: Steam 로비 만들기·찾기·참가, 친구 초대. 이 기능은 NGO 밖의 Steamworks가 제공한다. NGO는 연결이 만들어진 뒤에 같은 방식으로 일한다.

이 구조 덕분에 이 장의 코드는 4장에서 다시 쓰이고, 6장에서는 같은 채팅 프로그램을 C++ 서버로 다시 만든다. 한 번 만든 채팅 화면과 시험 항목이 세 장에 걸쳐 같은 기준으로 쓰이는 것이다.

## 2.20 2장 연습 문제

문제 끝의 괄호는 답의 바탕이 되는 절이다. 먼저 책을 보지 않고 답해 보고, 괄호의 절에서 확인한다. 코드를 쓰는 문제(17번~24번)는 반드시 실행해서 확인한다. 기능을 더한 뒤에는 앞의 시험(접속자 목록, 채널, 보너스, 아이템)이 여전히 통과하는지 다시 확인한다.

### 2.20.1 개념과 계산

1. NetworkVariable과 ClientRpc의 차이를 "나중에 접속한 클라이언트가 받는 것"을 기준으로 설명하라. (2.8절, 2.9절, 2.15.3절. `ClientRpc`는 NGO 1.x의 표기이고, 2.x에서는 `[Rpc(SendTo.ClientsAndHost …)]`가 이에 대응한다. 2.9.6절)
2. 보간 지연을 100ms로 두면 상대 플레이어는 화면에서 어떻게 보이는지, 지연을 0으로 하면 어떤 문제가 생기는지 설명하라. (2.16.5절)
3. 서버 틱이 30Hz이고 RTT가 80ms일 때, 클라이언트 예측이 없는 서버 권한 이동은 입력 후 화면 반응까지 최소 몇 ms가 걸리는가? (2.15.5절, 2.16.4절)
4. 클라이언트 예측과 서버 보정에서 입력에 순번을 붙이는 이유를 쓰고, 서버 보정 후 클라이언트가 해야 할 일을 순서대로 나열하라. (2.16.7절)
5. 채팅 프로그램에서 서버가 보낸 사람 이름을 직접 붙여 중계해야 하는 이유를 신뢰 경계 관점에서 설명하라. (2.11.4절, 1.8절)
6. 100명이 같은 방에 있을 때 모두에게 모든 위치를 보내는 방식의 전송량을 구하고, 거리 기반 관심 영역으로 줄이는 방법을 제시하라. (2.17절)
7. 호스트 PC와 클라이언트 PC에서 `NetPlayer`의 `IsServer`, `IsClient`, `IsHost`, `IsOwner` 값이 각각 true인지 false인지 적어라. 칸은 네 가지이다. (가) 호스트가 보는 자기 플레이어, (나) 호스트가 보는 클라이언트의 플레이어, (다) 클라이언트가 보는 자기 플레이어, (라) 클라이언트가 보는 호스트의 플레이어. 적은 뒤 실습 2의 Console 로그와 대조하라. (2.4.3절, 2.4.4절, 2.6.2절)
8. 서버가 점수를 `NetworkVariable<int>`로 올리고 같은 프레임에 "득점!"을 알리는 RPC를 보냈다. 클라이언트의 RPC 처리 함수에서 점수를 읽었더니 올리기 전의 값이 나올 때가 있었다. 프레임 안의 처리 순서와 틱을 써서 이유를 설명하고, 고치는 방법을 두 가지 쓰라. (2.15.2절, 2.15.3절)
9. 서버를 `-batchmode`로 실행하면서 `Application.targetFrameRate`를 10으로 제한하고, 틱 레이트(Tick Rate)는 기본값 30을 그대로 두었다. (가) 프레임 한 번에 틱은 평균 몇 번 일어나는가? (나) 서버가 `Update()`에서 오브젝트를 움직이고 NetworkTransform으로 동기화할 때, 클라이언트가 받는 위치 갱신은 초당 몇 번쯤인가? 이유를 쓰라. (다) 서버의 프레임 제한(30)이 틱 레이트(30)보다 낮으면 안 되는 이유를 (나)의 결과로 설명하라. (2.14.2절, 2.15.2절, 2.15.5절)
10. 이 교재의 `ApprovalGate`는 접속 승인 단계에서 접속 코드, 정원, 이름 중복을 검사한다. (가) 이 검사를 승인 콜백에서 해야 하는 이유를, 접속이 끝난 뒤에 검사하는 경우와 비교하여 설명하라. (나) 접속 코드를 글자 그대로(평문) 보내는 이 방식을 인터넷에 공개한 서버에서 그대로 쓰면 어떤 위험이 있는지, 어떤 보완이 필요한지 쓰라. (2.10.3절, 2.10.4절, 2.14.3절, 1.8절)

### 2.20.2 코드를 읽고 원인 찾기

아래 코드는 교재의 코드를 줄여 쓴 것이다. 읽고 원인을 설명하거나 고쳐 쓴다.

11. 아래 스크립트가 붙은 플레이어에서 화면의 버튼이 `AddScore()`를 부른다. 호스트에서 누르면 점수가 오르는데 클라이언트에서 누르면 오르지 않는다. (가) 클라이언트에서 NGO가 남기는 것은 예외(exception)인가, 로그인가? 값은 어떻게 되는가? (나) 고치는 방법을 두 가지 쓰라. 쓰기 권한을 `Owner`로 바꾸는 방법과 서버에게 요청하는 RPC를 만드는 방법이다. 이 교재의 원칙은 어느 쪽을 권하는가? 점수가 게임 규칙에 영향을 준다면 왜 그쪽인가? (2.8.3절, 2.9.5절, 2.15.3절)

```csharp
public class ScoreBoard : NetworkBehaviour
{
    NetworkVariable<int> score = new NetworkVariable<int>();   // 쓰기 권한을 따로 정하지 않았다

    public void AddScore()                                      // 화면의 버튼이 부른다
    {
        if (IsSpawned) score.Value++;
    }
}
```

12. 아래 채팅 코드에서 서버가 하지 않는 일과 열려 있는 구멍을 세 가지 이상 찾고, 2.11.4절의 방식으로 고쳐 쓰라. 두 번째 RPC의 선언에도 문제가 있다. (2.9.5절, 2.11.4절)

```csharp
[Rpc(SendTo.Server)]
void SayToServerRpc(string who, string text)
{
    ShowToAllRpc(who, text);
}

[Rpc(SendTo.Everyone)]
void ShowToAllRpc(string who, string text)
{
    OnLog?.Invoke(who + ": " + text);
}
```

13. 씬에 미리 배치한 NetworkObject의 스크립트가 `Start()`에서 `if (IsOwner)`로 입력 처리를 켜도록 썼더니 의도대로 동작하지 않았다. 반면 프리팹으로 스폰한 플레이어에서는 같은 코드가 동작하는 것처럼 보였다. 두 경우의 `Awake()`, `Start()`, `OnNetworkSpawn()` 호출 순서를 쓰고, 왜 이런 차이가 생기는지 설명한 뒤 고쳐 쓰라. (2.6.3절)
14. `OnNetworkSpawn()`에서 `NetworkManager.OnClientDisconnectCallback += OnLeft;`로 구독만 하고 해제하지 않았다. 이 오브젝트가 디스폰된 뒤 다른 클라이언트가 나가면 무슨 일이 생기는가? 같은 오브젝트가 다시 스폰되면 어떻게 되는가? 구독과 해제는 각각 어느 함수에 써야 하는가? (2.6.3절, 2.10.2절)
15. 이 교재의 화면 코드는 버튼을 눌렀을 때 할 일을 그 자리에서 하지 않고 `pending`에 적어 두었다가 `Update()`에서 실행한다. (가) 버튼 안에서 곧바로 `NetworkManager.Singleton.StartHost()`를 부르면 Console에 어떤 종류의 오류가 나올 수 있는가? (나) `OnGUI()`가 한 프레임에 여러 번 불리는 것과 이 오류는 어떤 관계인가? (2.5.4절)
16. 2.9.4절의 Ping 버튼이 보여 주는 값과 2.13.3절의 `NetStatHud`가 보여 주는 값이 서로 다를 수 있는 이유를 쓰라. 호스트에서 Ping을 누르면 0이 나오는 이유도 쓰라. (2.9.4절, 2.13.3절)

### 2.20.3 프로젝트에 기능 더하기

앞 절에서 "연습 문제로 남긴다"고 한 세 가지(17번, 18번, 19번)가 들어 있다. 새 기능은 서버가 검사하고, 상태는 NetworkList나 NetworkVariable에, 사건은 RPC에 두는 원칙(2.9.5절, 2.11.7절)을 지킨다. 문제마다 적은 확인 방법으로 직접 확인한다.

17. 채널에 비밀번호와 방장을 붙이려 한다(2.11.5절에서 미룬 문제). (가) 채널 정보를 담는 구조체와 `NetworkList`를 `UserState`와 같은 방식으로 설계하라. (나) 비밀번호를 이 목록에 넣으면 안 되는 이유를 설명하고, 비밀번호를 어디에 두어야 하는지 답하라. 화면에는 자물쇠 표시(`HasPassword`)만 필요하다. (다) 방장이 나가면 어떻게 할지 정하고 구현하라. (라) 확인: 클라이언트 세 개로 틀린 비밀번호로 입장하기, 방장이 나간 뒤 방장 이전하기를 시험하라. (2.11.5절)
18. 보너스를 받았는지 `Gold == 0`으로 판단하는 방식은 골드를 쓰는 기능이 생기면 깨진다(2.11.7절에서 미룬 문제). (가) 골드를 쓰는 기능을 하나 더하고(예: 아이템을 만들 때마다 골드 10을 쓴다) 보너스를 다시 받을 수 있게 되는 것을 확인하라. (나) '받았음'을 따로 기록하도록 고쳐라. 이 기록을 `UserState`의 필드로 두는 방법과 서버만 가진 `HashSet<ulong>`에 두는 방법을 비교하고, 화면이 알 필요가 없는 값은 어느 쪽에 두는 것이 알맞은지 이유와 함께 답하라. (다) 접속자가 나갈 때 이 기록을 어떻게 정리하는가? (2.11.7절)
19. `BonusPanel`에 행동 예상을 더하라(2.16.7절에서 미룬 문제). 보너스 받기 버튼을 누르면 곧바로 결과 칸에 "요청 중..."을 보여 주고, 서버의 응답(`OnBonusResult`)이 오면 결과로 바꾼다. (가) 응답이 오지 않는 경우(연결이 끊김 등)에 "요청 중..."이 영원히 남지 않도록 시간 제한을 두라. (나) 확인: 2.13.5절의 방법으로 에디터의 클라이언트에 RTT 300ms를 걸고 이 표시가 보이는지 확인하라. (다) 같은 방식으로 골드 숫자를 미리 100으로 바꿔 보여 주는 것은 왜 위험한가? 2.16.9절의 질문 "이 값을 서버가 고쳐 줄 수 있는가?"로 설명하라. (2.11.7절, 2.16.7절)
20. 늦게 접속한 클라이언트는 그 전의 대화를 볼 수 없다. 채팅 한 줄은 상태가 아니라 사건이어서 RPC로 한 번 보내고 지나갔기 때문이다(2.11.7절). 서버가 최근 대화 20줄을 기억했다가 새로 들어온 사람에게만 보내게 하라. (가) 서버가 대화를 어디에 어떤 자료구조로 보관하는가? 20줄을 넘으면 어떻게 하는가? (나) 새 접속자 한 명에게만 보내는 RPC 대상은 어떻게 지정하는가? (다) 서버가 접속 콜백에서 바로 보내는 방식은 그 클라이언트가 `ChatHub`를 스폰하고 화면 이벤트를 구독했는지 서버가 확인할 수 없다는 약점이 있다. 클라이언트의 `ChatHub`가 `OnNetworkSpawn()`에서 "지난 대화를 보내 달라"고 서버에 요청하는 방식과 비교하고, 후자로 구현하라. (라) 귓속말과 다른 채널의 대화가 이 기능으로 새 접속자에게 새어 나가지 않으려면 무엇을 보관하고 무엇을 보관하지 않아야 하는가? (2.9.3절, 2.11.4절, 2.11.7절)
21. 서버가 한 접속자의 채팅을 1초에 3번까지만 받고 나머지는 거절하게 하라. (가) 접속자별 마지막 전송 시각(또는 최근 전송 횟수)은 어디에 보관하는가? 접속자가 나가면 어떻게 정리하는가? (나) 거절당한 사람에게만 "너무 빠르다"는 알림이 가게 하라. (다) 클라이언트 화면에서 막기만 하면 부족한 이유를 설명하라. (라) 확인: 화면 코드를 임시로 고쳐 `Update()`에서 매 프레임 채팅 요청을 보내게 하고, 서버 로그와 다른 클라이언트의 화면에서 실제로 전달된 줄 수를 세어 보라. (2.11.4절, 1.8절)
22. 채팅창에 `/nick 새이름`을 입력하면 이름이 바뀌게 하라. 서버가 실습 6의 이름 규칙(길이, 중복)을 다시 적용하고, 접속자 목록의 이름을 바꾸고, 모두에게 "철수님이 철수2로 이름을 바꿨다"를 알린다. (가) 이름이 쓰이는 곳을 모두 찾아라. 접속자 목록(`UserState`) 말고도 이름을 키로 쓰는 곳이 있다. 하나라도 빠뜨리면 어떤 오류가 생기는가? (나) 확인: 이름을 바꾼 뒤 옛 이름으로 귓속말을 보내면 거절되고 새 이름으로는 전달되는지, 다른 접속자가 옛 이름으로는 접속할 수 있고 새 이름으로는 접속할 수 없는지 확인하라. (2.10.4절, 2.11.3절, 2.11.4절)
23. 아이템 한도(`SpawnHub.MaxItems`, 8개)는 서버 전체의 합계이므로 한 사람이 8개를 모두 만들면 다른 사람은 만들 수 없다. 접속자당 3개까지만 만들 수 있게 고쳐라. (가) 개수를 `Item.All`을 돌며 `OwnerClientId`로 세는 방법과 서버가 접속자별 개수를 따로 기록하는 방법 중 하나를 골라 구현하라. (나) 소유권을 넘기거나, 아이템을 없애거나, 소유자가 나갈 때 따로 기록한 개수는 어떻게 맞추는가? 어느 방법이 틀리기 쉬운지 "값의 출처를 하나로 한다"는 말로 설명하라. (다) 확인: 철수가 3개를 만든 뒤 영희에게 1개를 넘기면 철수는 몇 개까지 더 만들 수 있어야 하는가? (2.12.2절)
24. (선택 실습 16을 한 경우) `ProximityVisibility`에서 한 캡슐이 `viewDistance` 근처를 오가면 틱마다 보였다 안 보였다를 반복해 오브젝트가 계속 생성되고 제거된다. 보이기 시작하는 거리(3)와 숨기는 거리(4)를 다르게 하는 이중 임계값으로 고쳐라. 확인: 한 캡슐을 방향키로 경계 근처에서 조금씩 앞뒤로 움직이며 Console의 스폰·디스폰 줄 수를 세어 고치기 전과 후를 비교하라. (2.17.3절)

### 2.20.4 설계와 정리

코드를 쓰지 않고 글이나 그림으로 답한다.

25. 2.11.5절은 채널을 접속자 목록의 `Channel` 칸으로 표현하고, 채널 채팅은 서버가 받을 사람을 골라 RPC로 보낸다. 이것을 채널마다 `ChannelRoom`이라는 NetworkObject를 서버가 스폰하고 구성원에게만 `NetworkShow`로 보이게 하는 방식(2.17.2절)으로 바꾼다면 어떻게 되는지 비교하라. (가) 서버 코드의 양, (나) 늦게 들어온 구성원이 받는 상태, (다) 구성원이 아닌 접속자에게 가는 전송량, (라) 구성원이 나갈 때 해야 하는 정리, 이 네 가지를 비교한 뒤 이 교재의 채팅 프로그램에는 어느 쪽이 알맞은지 결론을 쓰라. (2.11.5절, 2.17.2절)
26. 다음 네 가지 기능에 알맞은 지연 대응 방법을 2.16.9절의 네 가지(클라이언트 권한, 행동 예상, 클라이언트 예측과 서버 보정, 지연 보상)에서 하나씩 고르고, "이 값을 서버가 고쳐 줄 수 있는가?"로 이유를 설명하라. (가) 협동 게임에서 내 캐릭터의 걷기, (나) 인벤토리의 "사용" 버튼을 눌렀을 때의 효과음과 버튼 모양 변화, (다) 서버가 위치를 최종 판정하는 경쟁 게임에서 내 캐릭터의 이동, (라) 총을 쏘았을 때 상대에게 맞았는지의 판정. (2.16.9절)
27. 실습 14(2.13.5절)에서 RTT 100ms와 300ms, 손실률 0%와 5%와 10%로 기록한 결과를 보고, 이 교재의 채팅 프로그램에서 가장 먼저 개선해야 할 한 가지를 고르라. 후보는 (가) 이동의 끊김, (나) 보너스 응답 시간, (다) 채널 입장 시간, (라) 접속자 목록의 갱신 시간이다. 고른 이유를 기록한 숫자로 설명하고, 2.16절의 어느 기법으로 개선할 수 있는지 쓰라. 기록이 없으면 먼저 실습 14를 한다. (2.13.5절, 2.16절)
28. 2.15.6절의 목록은 NGO가 대신해 준 일 일곱 가지를 6장의 구조와 나란히 놓았다. 그중 세 가지를 골라, 이 장의 어느 코드가 그 일을 NGO에게 맡기고 있는지 코드 한 줄씩을 찾아 적어라(예: 보낸 사람 확인은 `rpcParams.Receive.SenderClientId`). 그리고 같은 일을 6장의 C++ 서버가 직접 하려면 무엇이 더 필요한지 쓰라. (2.15.6절)
29. 이 교재의 엔진 파트는 이동과 상호작용을 소유자 클라이언트가 처리하고, 서버는 접속자 관리와 중계만 한다(2.4.5절). 조작된 클라이언트가 하려는 다음 일 가운데 (가) 이 장의 서버 검사로 막을 수 있는 것과 (나) 막을 수 없는 것을 나누어라: 채팅에 남의 이름을 쓰기, 남의 아이템 없애기, 정원이 찬 방에 들어가기, 순간이동, 이동 속도 올리기. (다) (나)를 막으려면 구조를 어떻게 바꾸어야 하는지 쓰고, 그 구조를 이 교재의 어느 장에서 다루는지 답하라. (1.8절, 2.16.9절)
