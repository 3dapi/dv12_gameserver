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
