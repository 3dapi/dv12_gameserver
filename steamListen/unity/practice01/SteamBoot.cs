using Steamworks;
using UnityEngine;
using UnityEngine.UI;

public class SteamBoot : MonoBehaviour
{
    [SerializeField] Text statusText;

    void Awake()
    {
        bool ok = SteamAPI.Init();                                  // Steam 클라이언트가 켜져 있어야 한다
        statusText.text = ok ? "Steam: " + SteamFriends.GetPersonaName() : "Steam 초기화 실패";
    }
    void Update() { SteamAPI.RunCallbacks(); }                     // 콜백 전달, 매 프레임 호출
    void OnDestroy() { SteamAPI.Shutdown(); }
}
