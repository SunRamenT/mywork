using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToritukareManagger : MonoBehaviour//乗っ取り状態管理
{
    public GameObject yurei;
    public List<Transform> TarList = new List<Transform>();

    // Start is called before the first frame update
    void Start()
    {
        yurei = null;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // 新しいリストを作成して元のリストをコピーすることで、リストを変更しても問題が発生しないようにします。
        List<Transform> uniqueList = new List<Transform>(TarList);

        for (int i = 0; i < uniqueList.Count; i++)
        {
            for (int k = i + 1; k < uniqueList.Count; k++)
            {
                if (uniqueList[i] == uniqueList[k])
                {
                    uniqueList.RemoveAt(k);
                    k--; // インデックスが変わったのでデクリメントする
                }
            }

            if (!uniqueList[i])
            {
                uniqueList.RemoveAt(i);
                i--; // インデックスが変わったのでデクリメントする
            }
            else if (uniqueList[i].CompareTag("Player"))
            {
                yureiget(uniqueList[i].gameObject);
            }
        }

        // 元のリストをクリアして新しいリストの要素をコピーする
        TarList.Clear();
        TarList.AddRange(uniqueList);
    }

    void yureiget(GameObject tar)
    {
        if (tar.GetComponent<PlayerController>().target == this.gameObject)
        {
            yurei = tar.gameObject;//プレイヤーの情報をyureiに代入
        }
    }

    void OnTriggerEnter2D(Collider2D collider)
    {
        TarList.Add(collider.gameObject.transform);
    }

    void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.tag == "Player" && collider.GetComponent<PlayerController>().rock == false)
        {
            yurei = null;
        }

        for (int i = TarList.Count - 1; i >= 0; i--)
        {
            if (TarList[i] == collider.gameObject.transform)
            {
                TarList.RemoveAt(i);
            }
        }
    }
}