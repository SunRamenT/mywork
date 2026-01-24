using UnityEngine;
using UnityEngine.Events;

public class AthleticSwitch : MonoBehaviour, IInteractable
{
    [Header("設定")]
    [Tooltip("一度しか押せないスイッチか？")]
    public bool isOneTimeOnly = false;
    [Tooltip("スイッチが押された時の色")]
    public Color activeColor = Color.green;
    
    [Header("イベント")]
    [Tooltip("スイッチがONになった時に実行される処理")]
    public UnityEvent onActivate;
    [Tooltip("スイッチがOFFになった時に実行される処理（トグル式の場合）")]
    public UnityEvent onDeactivate;

    private bool isOn = false;
    private Renderer rend;
    private Color originalColor;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) originalColor = rend.material.color;
    }

    // インタラクト時の処理
    public void OnInteract(PlayerController player)
    {
        if (isOneTimeOnly && isOn) return;

        isOn = !isOn; // トグル切り替え

        // 見た目の変更
        if (rend != null)
        {
            rend.material.color = isOn ? activeColor : originalColor;
        }

        // イベント実行
        if (isOn)
        {
            Debug.Log("スイッチON!");
            onActivate.Invoke();
        }
        else
        {
            Debug.Log("スイッチOFF!");
            onDeactivate.Invoke();
        }
    }

    public void OnPlayerEnterRange() { /* UI表示などあれば記述 */ }
    public void OnPlayerExitRange() { /* UI非表示などあれば記述 */ }
}