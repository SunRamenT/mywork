using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyListManager : MonoBehaviour//マウスで選択したcollider内のオブジェクトの数をカウントするためのコード
{
    public List<Transform> EnemyList = new List<Transform>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        for(int i = 0; i < EnemyList.Count; i++)
        {
            for (int k = i + 1; k < EnemyList.Count; k++)
            {
                if(EnemyList[i] == EnemyList[k])
                {
                    EnemyList.RemoveAt(k);
                }
            }

            if(!EnemyList[i])
            {
                EnemyList.RemoveAt(i);
            }
        }

    }
    void OnTriggerEnter2D(Collider2D collider)//リストで管理しないとうまく対象を切り換えできない
    {
        if(collider.CompareTag("Enemy") || collider.CompareTag("Mob") || collider.CompareTag("Hero"))
        {
            EnemyList.Add(collider.gameObject.transform);
        }
    }
    void OnTriggerExit2D(Collider2D collider)
    {
        if(collider.CompareTag("Enemy") || collider.CompareTag("Mob") || collider.CompareTag("Hero"))
        {
            for(int i = 0; i < EnemyList.Count; i++)
            {
                if(EnemyList[i] == collider.gameObject.transform)
                {
                    EnemyList.RemoveAt(i);
                }
            }
        }
    }
}
