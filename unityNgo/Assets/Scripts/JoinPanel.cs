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
