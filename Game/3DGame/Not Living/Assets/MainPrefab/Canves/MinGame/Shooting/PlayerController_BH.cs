// PlayerController_BH.cs (修正版)
using UnityEngine;

public class PlayerController_BH : MonoBehaviour
{
    // ▼▼▼ 削除: public変数は不要になります ▼▼▼
    // public RectTransform playAreaPanelRectTransform;

    private RectTransform _playerRectTransform;
    private RectTransform _playAreaPanelRectTransform; // ▼▼▼ private変数として保持 ▼▼▼

    void Awake()
    {
        _playerRectTransform = GetComponent<RectTransform>();
        
        // ▼▼▼ 追加: 自分自身の親(つまりPanel)を自動で取得する ▼▼▼
        _playAreaPanelRectTransform = transform.parent.GetComponent<RectTransform>();
        if (_playAreaPanelRectTransform == null)
        {
            Debug.LogError("親オブジェクトにRectTransformを持つPanelが見つかりません！", this.gameObject);
        }
    }

    void Update()
    {
        if (_playAreaPanelRectTransform == null) return;

        // マウスのスクリーン座標を「Panel」のローカル座標に変換
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _playAreaPanelRectTransform, // 基準は自動取得したPanel
            Input.mousePosition,
            null,
            out Vector2 localPoint
        );

        // 座標をPanelの範囲内に制限する
        localPoint.x = Mathf.Clamp(localPoint.x, _playAreaPanelRectTransform.rect.xMin, _playAreaPanelRectTransform.rect.xMax);
        localPoint.y = Mathf.Clamp(localPoint.y, _playAreaPanelRectTransform.rect.yMin, _playAreaPanelRectTransform.rect.yMax);

        // プレイヤーの位置を更新
        _playerRectTransform.anchoredPosition = localPoint;
    }
}