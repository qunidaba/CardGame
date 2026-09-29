using UnityEngine;
using Roguelike;

/// <summary>
/// ESC 打开 / 关闭暂停界面（仅在一局对局中生效）。
/// 挂在 GameBootstrap 所在对象上（由 GameBootstrap 运行时添加）。
/// </summary>
public class PauseController : MonoBehaviour
{
    public KeyCode key = KeyCode.Escape;

    private void Update()
    {
        if (!Input.GetKeyDown(key)) return;

        var ui = UIManager.Instance;
        if (ui == null) return;

        // 从暂停打开的子面板：ESC 先关掉它们（回到暂停）
        if (ui.IsPanelActive(nameof(SettingsPanel))) { ui.Hide<SettingsPanel>(); return; }
        if (ui.IsPanelActive(nameof(HelpPanel))) { ui.Hide<HelpPanel>(); return; }
        if (ui.IsPanelActive(nameof(CodexPanel))) { ui.Hide<CodexPanel>(); return; }

        // 暂停面板开着 → 关闭（继续游戏）
        if (ui.IsPanelActive(nameof(PausePanel))) { ui.Hide<PausePanel>(); return; }

        // 只在对局中可以打开暂停
        if (RunDirector.Instance == null || !RunDirector.Instance.InRun) return;

        ui.ShowPanel<PausePanel>();
    }
}
