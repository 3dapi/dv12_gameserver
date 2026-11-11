using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// 플레이어의 닉네임과 색을 NetworkVariable으로 동기화하고, 캡슐 위에 닉네임을 그린다.
public class PlayerProfile : NetworkBehaviour
{
    // 닉네임: 모두 읽을 수 있고, 이 오브젝트의 소유자만 쓸 수 있다. (값, 읽기 권한, 쓰기 권한)
    NetworkVariable<FixedString64Bytes> nick = new NetworkVariable<FixedString64Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // 색: 권한을 적지 않으면 "모두 읽기, 서버만 쓰기"이다.
    NetworkVariable<Color> tint = new NetworkVariable<Color>(Color.white);

    public static PlayerProfile Local;      // 이 컴퓨터에서 내가 조종하는 플레이어

    string nickText = "";                   // 화면에 그릴 닉네임(NetworkVariable 값의 사본)

    string Where => IsHost ? "Host" : IsServer ? "Server" : "Client";

    public override void OnNetworkSpawn()
    {
        if (IsOwner) Local = this;
        if (IsOwner && JoinInfo.Name.Length > 0) SetNick(JoinInfo.Name);    // 접속할 때 쓴 이름이 첫 닉네임이다

        // 색은 서버가 접속 순서(ClientId)에 따라 정한다.
        if (IsServer) tint.Value = Color.HSVToRGB(((float)OwnerClientId * 0.17f) % 1f, 0.65f, 0.95f);

        // 1) 지금 값을 한 번 반영한다. 늦게 접속했어도 이 시점에는 현재 값이 이미 와 있다.
        nickText = nick.Value.ToString();
        ApplyTint(tint.Value);

        // 2) 이후의 변화는 알림으로 받는다.
        nick.OnValueChanged += OnNickChanged;
        tint.OnValueChanged += OnTintChanged;
    }

    public override void OnNetworkDespawn()
    {
        nick.OnValueChanged -= OnNickChanged;
        tint.OnValueChanged -= OnTintChanged;
        if (Local == this) Local = null;
    }

    void OnNickChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        nickText = current.ToString();
        Debug.Log($"[{Where}] 닉네임 변경: 소유자 {OwnerClientId}, '{previous}' → '{current}'");
    }

    void OnTintChanged(Color previous, Color current) => ApplyTint(current);

    void ApplyTint(Color c) => GetComponent<Renderer>().material.color = c;

    // ---- 화면의 버튼에서 호출한다 ----

    // 소유자가 호출한다. 한글 16자는 48바이트이므로 FixedString64Bytes(61바이트)에 들어간다.
    public void SetNick(string text)
    {
        text = text.Trim();
        if (text.Length > 16) text = text.Substring(0, 16);
        nick.Value = new FixedString64Bytes(text);
    }

    // 일부러 권한이 없는 쪽에서 써 보는 시험용 함수이다. 서버(호스트)에서만 성공한다.
    public void TryChangeTint()
    {
        tint.Value = Random.ColorHSV(0f, 1f, 0.6f, 0.8f, 0.9f, 1f);
    }

    // ---- 캡슐 위에 닉네임 그리기 ----

    // 다른 스크립트도 쓸 수 있는 이름. 닉네임이 비어 있으면 "Player 번호"를 쓴다.
    public string PlayerName => nickText.Length > 0 ? nickText : $"Player {OwnerClientId}";

    string DisplayName => IsOwner ? PlayerName + " (나)" : PlayerName;

    void OnGUI()
    {
        if (!IsSpawned) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 p = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
        if (p.z <= 0f) return;                          // 카메라 뒤에 있으면 그리지 않는다

        UiKit.Apply();
        string text = DisplayName;
        Vector2 size = GUI.skin.label.CalcSize(new GUIContent(text));
        // 월드→화면 변환의 y는 아래가 0이고 IMGUI는 위가 0이다.
        var rect = new Rect(p.x - size.x / 2f, Screen.height - p.y - size.y, size.x, size.y);

        Color old = GUI.color;
        GUI.color = Color.black;                        // 글자 그림자
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = old;
        GUI.Label(rect, text);
    }
}
