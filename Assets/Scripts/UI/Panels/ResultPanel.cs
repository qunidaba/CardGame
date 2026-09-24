using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗结算面板（使用TextMeshPro）
/// 显示胜利/失败结果，提供重新开始按钮
/// </summary>
public class ResultPanel : BasePanel
{
    public TextMeshProUGUI resultText;
    public Button restartButton;

    private System.Action onRestart;

    /// <summary>
    /// 设置重新开始回调
    /// </summary>
    public void SetRestartAction(System.Action action)
    {
        onRestart = action;

        restartButton.onClick.RemoveAllListeners();
        restartButton.onClick.AddListener(() =>
        {
            onRestart?.Invoke();
        });
    }

    /// <summary>
    /// 显示结算结果
    /// </summary>
    public void ShowResult(bool isWin)
    {
        // 不使用emoji（TextMeshPro默认字体可能不支持）
        resultText.text = isWin ? "战斗胜利！" : "战斗失败...";
        Show();
    }
}
