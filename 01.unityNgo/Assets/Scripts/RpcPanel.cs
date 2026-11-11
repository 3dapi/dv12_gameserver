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
