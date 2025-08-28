using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCGenerator : MonoBehaviour
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
        if(poplist.Count < poplim)
        {
            Pop();
        }


        //ポップ対象が消えたらリストから消去する
        for(int i=0; i<poplist.Count; i++)
        {
            if(poplist[i] == null)
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
        var pos = new Vector3(Random.Range(-xlim, xlim), Random.Range(-ylim, ylim), 0f);
        //範囲制限
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        newNPC.transform.position = this.transform.position + new Vector3(pos.x, pos.y, 0f);
    }
}
