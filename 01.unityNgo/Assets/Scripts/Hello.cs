using UnityEngine;

public class Hello : MonoBehaviour
{
    [SerializeField] string message = "안녕";   // Inspector에 나타나는 설정값

    void Start()
    {
        Debug.Log(message);                      // Console 창에 출력된다
    }
}
