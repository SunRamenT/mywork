using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject targetObject; // プレイヤーオブジェクト
    public float damping = 5.0f; // カメラの移動の減衰係数
    public bool rock = false; // のっとりフラグ
    public Transform RockonTarget; // のっとり対象 (Transform型に変更)
    public EnemyListManager enemyListManager; // マウスのセンサーで感知したMobのリスト
    private Vector3 PrePosition;// 乗っ取り前にいた場所(乗っ取り先に強制移動)
    private GameObject PreObject;//乗っ取り前のオブジェクト(幽霊)
    private int targetIndex = 0;
    private Vector3 targetPosition;//のっとり対象の位置

    // x軸方向の移動範囲の最小値(マップの左端)
    [SerializeField] private float minX = -39.9f;
    // x軸方向の移動範囲の最大値(マップの右端)
    [SerializeField] private float maxX = 43.85f;
    // y軸方向の移動範囲の最小値(マップの下端)
    [SerializeField] private float minY = -23.0f;
    // y軸方向の移動範囲の最大値(マップの上端)
    [SerializeField] private float maxY = 21.0f;

    void Start()
    {
        targetPosition = targetObject.transform.position;
        targetPosition.z = transform.position.z;
    }

    void Update()
    {

        if (targetObject == null)
        {
            LockChange();
        }
        targetPosition = targetObject.transform.position;
        targetPosition.z = transform.position.z;
        
        //カメラの範囲制限
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);
        
        // カメラの位置を滑らかに移動させる
        transform.position = Vector3.Lerp(transform.position, targetPosition, damping * Time.deltaTime);
        if (Input.GetButtonDown("Jump"))
        {
            if (rock == true && PreObject != null)
            {
                rock = false;

                targetObject = PreObject;
                Debug.Log("kaijo");
                return;
            }

            if (enemyListManager.EnemyList.Count == 0)
            {
                return;
            }
            
            LockChange();

        }



        if (enemyListManager.EnemyList.Count <= targetIndex)
        {
            targetIndex = 0;
        }       
    }

    void LockChange()
    {
        if (rock == false && enemyListManager.EnemyList[targetIndex].transform != null)
        {
            // Toggle the rock-on mode
            rock = true;
            Debug.Log("rock!!!");

            // ロックオンモードの場合、ロックオン対象を追跡
            // targetObjectを更新してからPreObjectに保存
            PreObject = targetObject;
            targetIndex = 0;
            // 新しいロックオン対象を取得
            RockonTarget = enemyListManager.EnemyList[targetIndex].transform; // Transformに変更
            targetObject = RockonTarget.gameObject; // GameObjectに変更
            return;
        }
        else if (rock == true)//とりついている対象が消されたとき
        {
            targetObject = PreObject;
            rock = false;
            return;
        }
        return;
    }
}