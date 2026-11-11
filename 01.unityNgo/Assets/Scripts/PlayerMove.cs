using Unity.Netcode;
using UnityEngine;

// 소유자의 컴퓨터에서만 입력을 읽어 움직인다. 위치를 다른 컴퓨터에 전달하는 일은 NetworkTransform이 한다.
public class PlayerMove : NetworkBehaviour
{
    public static bool AutoWalk;               // true이면 키보드 대신 원을 그리며 걷는다(가상 입력)

    [SerializeField] float speed = 4f;         // 초당 이동 거리(미터)

    void Update()
    {
        if (!IsSpawned || !IsOwner) return;    // 내 것이 아닌 복사본은 입력을 읽지 않는다

        // 글자를 입력하는 중(UiKit.Typing)에는 키보드 이동을 하지 않는다. 입력 칸은 2.8절부터 생긴다.
        Vector2 dir = AutoWalk ? AutoDirection() : (UiKit.Typing ? Vector2.zero : SimpleInput.Move());
        transform.position += new Vector3(dir.x, 0f, dir.y) * (speed * Time.deltaTime);
    }

    // 시간에 따라 방향이 계속 바뀌어 원을 그린다. 접속자마다 시작 방향이 다르다.
    Vector2 AutoDirection()
    {
        float t = Time.time + (float)OwnerClientId;
        return new Vector2(Mathf.Cos(t), Mathf.Sin(t));
    }
}
