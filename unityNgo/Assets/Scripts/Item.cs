using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 아이템 한 개. 서버가 스폰한 NetworkObject이다. 소유자에 따라 색이 달라지고 이름표가 붙는다.
public class Item : NetworkBehaviour
{
    // 이 컴퓨터에 있는 스폰된 아이템 전부. 서버는 전체를, 클라이언트는 자기에게 복제된 것을 가진다.
    public static readonly List<Item> All = new List<Item>();

    public override void OnNetworkSpawn()
    {
        All.Add(this);
        ApplyColor(OwnerClientId);
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
    }

    public override void OnDestroy()
    {
        All.Remove(this);
        base.OnDestroy();               // NetworkBehaviour의 OnDestroy를 재정의하면 반드시 base를 부른다
    }

    // 소유자가 바뀌면 서버와 모든 클라이언트에서 불린다.
    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        ApplyColor(current);
        Debug.Log($"[{(IsServer ? "서버" : "클라이언트")}] Item {NetworkObjectId} 소유자 {previous} → {current}");
    }

    // 2.8절의 PlayerProfile이 캡슐을 처음 칠할 때 쓰는 공식과 같다.
    void ApplyColor(ulong owner)
    {
        GetComponent<Renderer>().material.color = Color.HSVToRGB(((float)owner * 0.17f) % 1f, 0.65f, 0.95f);
    }

    string OwnerName
    {
        get
        {
            ChatHub hub = ChatHub.Instance;
            if (hub != null && hub.TryGetUser(OwnerClientId, out UserState owner)) return owner.Name.ToString();
            return $"Client {OwnerClientId}";
        }
    }

    // 아이템 위에 이름표를 그린다. 스폰되지 않은 복사본에는 그렇다고 적는다.
    void OnGUI()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 p = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.2f);
        if (p.z <= 0f) return;                          // 카메라 뒤에 있으면 그리지 않는다

        UiKit.Apply();
        string text = IsSpawned ? $"Item {NetworkObjectId} / {OwnerName}" : "(스폰되지 않음)";
        Vector2 size = GUI.skin.label.CalcSize(new GUIContent(text));
        var rect = new Rect(p.x - size.x / 2f, Screen.height - p.y - size.y, size.x, size.y);

        Color old = GUI.color;
        GUI.color = Color.black;                        // 글자 그림자
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = old;
        GUI.Label(rect, text);
    }
}
