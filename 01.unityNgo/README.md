# 01.unityNgo

2장(Unity NGO 채팅 서버) 원고의 C# 스크립트 모음.

| 항목 | 내용 |
|---|---|
| 출처 | `docs/ch02_unity.md` 코드 블록 |
| 대상 | Unity 6.3 LTS, Netcode for GameObjects 2.x |
| 사용법 | `Assets/Scripts`를 Unity 프로젝트의 `Assets/Scripts`에 복사, 배치 파일은 프로젝트 폴더에 복사 |
| 반영한 수정 | `ApprovalGate.cs`는 2.10절 완성판, `PlayerProfile.cs`는 승인 이름을 첫 닉네임으로 쓰는 줄 반영, `NetStarter.cs`는 `-server` 명령줄 처리 `Start()` 반영 |
| 제외 | 원고 안의 짧은 예시 조각, 시험용 임시 수정(`ChatHub.Bonus.cs` 판정 변경), 실습 프로젝트 뼈대 |
| 검증 상태 | 미검증. Unity·C# 컴파일러 없이 괄호 짝과 파일·클래스 이름만 확인 |

| 파일 | 원고 위치 |
|---|---|
| Hello.cs, SimpleInput.cs, Mover.cs | 2.3절 기본기, 실습 0 |
| UiKit.cs, NetStarter.cs | 2.5절 실습 1 |
| ApprovalGate.cs, PlayerInfo.cs | 2.6절 실습 2, 2.10절에서 완성 |
| PlayerMove.cs, AutoWalkToggle.cs | 2.7절 실습 3 |
| PlayerProfile.cs, NickPanel.cs | 2.8절 실습 4 |
| PlayerRpc.cs, RpcPanel.cs | 2.9절 실습 5 |
| JoinPanel.cs, ServerPanel.cs | 2.10절 실습 6~7 |
| UserState.cs, ChatHub*.cs, ChatLogPanel.cs, UserListPanel.cs, ChatInputPanel.cs, ChannelPanel.cs, StatePanel.cs, BonusPanel.cs | 2.11절 실습 8~12 |
| ChatHud.cs, BonusView.cs | 2.11절 uGUI 화면 |
| ViewToggle.cs, Item.cs, SpawnHub.cs, SpawnPanel.cs | 2.12절 실습 13 |
| NetStatHud.cs, run_clients.bat | 2.13절 실습 14 |
| run_server.bat | 2.14절 실습 15 |
| ProximityVisibility.cs | 2.17절 실습 16 |
