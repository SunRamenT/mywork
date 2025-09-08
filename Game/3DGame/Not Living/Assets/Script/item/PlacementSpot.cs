using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlacementSpot : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("代わりにドロップさせたい回復アイテムのプレハブ")]
    public GameObject reikonItemPrefab;
    [Tooltip("ドロップする回復アイテムの回復量")]
    public float reikonAmountToDrop = 50f;

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning("PlacementSpotのColliderでIs Triggerがオンになっていません。オンに設定します。", this);
            col.isTrigger = true;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<ItemData>() != null)
        {
            Debug.Log($"アイテム「{other.name}」が正しい場所に置かれました。");

            Destroy(other.gameObject);

            if (reikonItemPrefab != null)
            {
                GameObject droppedItem = Instantiate(reikonItemPrefab, transform.position, Quaternion.identity);
                
                // ▼▼▼ ここを修正 ▼▼▼
                // 生成したアイテムの回復量を設定
                ReikonItem recoveryData = droppedItem.GetComponent<ReikonItem>();
                if (recoveryData != null)
                {
                    recoveryData.recoveryAmount = reikonAmountToDrop;
                }
            }
        }
    }
}