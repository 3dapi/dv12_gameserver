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
