using UnityEngine;

// 패널을 숨기고 다시 보이게 하는 버튼. 패널이 가리고 있는 3D 화면(캡슐, 아이템)을 볼 때 쓴다.
public class ViewToggle : MonoBehaviour
{
    // 숨기지 않을 패널. 아이템 버튼은 3D 화면을 보면서 눌러야 하므로 남겨 둔다.
    const string KeepName = "SpawnPanel";

    bool shown = true;
    bool pendingToggle;                     // OnGUI는 눌렸다는 사실만 기록하고, 바꾸는 일은 Update에서 한다

    void Update()
    {
        if (!pendingToggle) return;
        pendingToggle = false;
        shown = !shown;

        // 이 게임 오브젝트(NetStarter)에 붙은 스크립트 중 이름이 Panel로 끝나는 것을 켜고 끈다.
        // enabled가 false인 스크립트는 OnGUI()와 Update()가 불리지 않으므로 화면에서 사라진다.
        foreach (MonoBehaviour b in GetComponents<MonoBehaviour>())
        {
            string typeName = b.GetType().Name;
            if (typeName.EndsWith("Panel") && typeName != KeepName) b.enabled = shown;
        }
    }

    void OnGUI()
    {
        UiKit.Apply();
        string label = shown ? "패널 숨기기" : "패널 보이기";
        if (GUI.Button(new Rect(Screen.width - 130, Screen.height - 35, 120, 30), label)) pendingToggle = true;
    }
}
