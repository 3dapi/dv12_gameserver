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
