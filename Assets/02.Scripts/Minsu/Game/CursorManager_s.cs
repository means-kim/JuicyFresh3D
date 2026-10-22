using UnityEngine;

// 마우스 커서를 화면 가운데에 잠그고 숨긴다.
public class CursorManager_s : MonoBehaviour
{
    private void Awake()
    {
        LockCursor();
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked; // 화면 가운데에 고정
        Cursor.visible = false;                   // 커서 숨기기
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}