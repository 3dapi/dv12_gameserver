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
