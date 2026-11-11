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
