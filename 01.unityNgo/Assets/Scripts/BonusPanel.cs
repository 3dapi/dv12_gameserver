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
