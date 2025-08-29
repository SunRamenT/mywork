using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AngerGenerator : MonoBehaviour
{
    //Chaisor's Prefab
    public GameObject NPCPrefab;

    public int popinterval = 10;
    private int time = 0;
    private int poplim = 1;
    private int day = 0;
    public List<GameObject> poplist = new List<GameObject>();//popしたキャラのList

    public int xlim;
    public int ylim;

    // x軸方向の移動範囲の最小値
    private float minX = -51.3f;
    // x軸方向の移動範囲の最大値
    private float maxX = 55.0f;
    // y軸方向の移動範囲の最小値
    private float minY = -27.0f;
    // y軸方向の移動範囲の最大値
    private float maxY = 25.1f;

    GRManager grm;// 幽霊の内部パラメータ

    // Start is called before the first frame update
    void Start()
    {
        InvokeRepeating(nameof(CountMethod), 5f, 1f);

        grm = GameObject.FindGameObjectWithTag("Player").GetComponent<GRManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if (poplist.Count < poplim)
        {
            Pop();
        }


        //ポップ対象が消えたらリストから消去する
        for (int i = 0; i < poplist.Count; i++)
        {
            if (poplist[i] == null)
            {
                poplist.Remove(poplist[i]);
            }
        }
    }


    private void Pop()
    {
        // 新しいオブジェクトをリストに追加
        GameObject newNPC = Instantiate(NPCPrefab);
        newNPC.transform.SetParent(this.transform);
        poplist.Add(newNPC);
        // NPC座標決め
        var pos = new Vector3(Random.Range(-xlim, xlim),Random.Range(-ylim, ylim), 0f);
        //範囲制限
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        newNPC.transform.position = new Vector3(pos.x, pos.y, 0f);
    }

    void CountMethod()
    {
        time++;
        //day
        if(time%60 == 0)
        {
            day++;
        }
        
        if (time % popinterval == 0 && day + grm.goodper/25 > poplim)//善行値の割合と日付で出現率を変える。初日＋何もしていない(善行値50)のばあい2人まで出る
        {
            poplim++;
        }
        else if(time % popinterval == 0)
        {
            while(day + grm.goodper / 25 < poplim)
            {
                Debug.Log("hello");
                poplist.Remove(poplist[poplim - 1]);
                poplim--;
            }
        }
    }
}
