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
