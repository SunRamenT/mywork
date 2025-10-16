// UIManager.cs
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("UI設定")]
    [Tooltip("オン/オフを切り替えたい設定画面のCanvas")]
    public GameObject settingsCanvas;

    private bool isSettingsOpen = false;

    private void Start()
    {
        if (settingsCanvas != null)
        {
            settingsCanvas.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire3"))
        {
            SetSettingsScreenActive(!isSettingsOpen);
        }
    }

    public void SetSettingsScreenActive(bool isActive)
    {
        if (settingsCanvas == null || GameStateManager.Instance == null) return;

        if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.End) return;

        isSettingsOpen = isActive;
        settingsCanvas.SetActive(isSettingsOpen);

        if (isSettingsOpen)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // ▼▼▼ 状態を "Paused" に変更 ▼▼▼
            GameStateManager.Instance.SetState(GameStateManager.GameState.Paused);
        }
        else
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            GameStateManager.Instance.SetState(GameStateManager.GameState.Gameplay);
        }
    }
}