using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoliceGenerator : MonoBehaviour
{
    //NPC's Prefab
    public GameObject NPCPrefab;

    public int poplim;

    public List<GameObject> poplist = new List<GameObject>();//popしたキャラのList

    public int xlim;
    public int ylim;

    // x軸方向の移動範囲の最小値
    private float minX = -39.9f;
    // x軸方向の移動範囲の最大値
    private float maxX = 43.85f;
    // y軸方向の移動範囲の最小値
    private float minY = -23.0f;
    // y軸方向の移動範囲の最大値
    private float maxY = 21.0f;

    // Start is called before the first frame update
    void Start()
    {
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
        int i;
        i = Random.Range(1, 5);
        var pos = Vector3.zero;
        if(i == 1)
        {
            // NPC座標決め
            pos = new Vector3(Random.Range(1, 1), Random.Range(1, 1), 0f);
        }
        else if(i == 2)
        {
            // NPC座標決め
            pos = new Vector3(32f + Random.Range(1, 1), Random.Range(1, 1), 0f);
        }
        else if(i == 3)
        {
            // NPC座標決め
            pos = new Vector3(16f + Random.Range(1, 1), 17f + Random.Range(1, 1), 0f);
        }
        else if(i == 4)
        {
            // NPC座標決め
            pos = new Vector3(Random.Range(1, 1) - 32f, Random.Range(1, 1) - 15f, 0f);
        }
        else if(i == 5)
        {
            // NPC座標決め
            pos = new Vector3(49f + Random.Range(1, 1), Random.Range(1, 1) - 15f, 0f);
        }
        //範囲制限
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        newNPC.transform.position = this.transform.position + new Vector3(pos.x, pos.y, 0f);
    }
}
