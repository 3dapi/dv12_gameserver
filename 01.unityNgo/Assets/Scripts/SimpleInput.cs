using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// WASD 또는 방향키 입력을 읽는다. 입력 설정(Active Input Handling)이 무엇이어도 동작한다.
public static class SimpleInput
{
    public static Vector2 Move()
    {
        float x = 0f, y = 0f;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;                       // 새 Input System
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    y += 1f;
        }
#else
        x = Input.GetAxisRaw("Horizontal");              // 예전 Input Manager
        y = Input.GetAxisRaw("Vertical");
#endif
        var v = new Vector2(x, y);
        return v.sqrMagnitude > 1f ? v.normalized : v;   // 대각선 속도가 빨라지지 않게 길이를 1로 제한
    }
}
