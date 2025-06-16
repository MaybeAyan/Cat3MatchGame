using UnityEngine;

public class UITest : MonoBehaviour
{
    public void OnTestButtonClick()
    {
        // 使用富文本标签让日志更显眼
        Debug.Log("<color=green>--- 测试按钮被成功点击！事件系统正常！ ---</color>");
    }
}