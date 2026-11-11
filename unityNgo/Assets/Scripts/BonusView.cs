using UnityEngine;
using UnityEngine.UI;

// BonusPanel의 uGUI 버전. ChatHub는 그대로이고 화면만 다르다.
public class BonusView : MonoBehaviour
{
    [SerializeField] Button bonusButton;        // Inspector에서 연결한다
    [SerializeField] Text resultText;           // Inspector에서 연결한다

    void Awake()
    {
        // 기본 글꼴에는 한글이 없으므로 PC에 설치된 한글 글꼴을 쓴다. (UiKit과 같은 방법)
        Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 16);
        resultText.font = font;
        bonusButton.GetComponentInChildren<Text>().font = font;
    }

    void OnEnable()
    {
        ChatHub.OnBonusResult += OnResult;
        bonusButton.onClick.AddListener(OnClick);
    }

    void OnDisable()
    {
        ChatHub.OnBonusResult -= OnResult;
        bonusButton.onClick.RemoveListener(OnClick);
    }

    void Update()
    {
        bool connected = ChatHub.Instance != null;
        bonusButton.interactable = connected;           // 접속하기 전에는 누를 수 없다
        if (!connected) resultText.text = "";
    }

    void OnClick()
    {
        ChatHub.Instance.RequestBonus();                // uGUI의 클릭은 Update 단계에서 처리되므로 바로 불러도 된다
    }

    void OnResult(bool ok, int amount)
    {
        resultText.text = ok ? $"보너스 {amount} 지급" : "이미 받았다";
    }
}
