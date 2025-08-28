using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoalGenerator : MonoBehaviour
{
    //NPC's Prefab
    public GameObject GoalPrefab;

    public List<GameObject> poplist = new List<GameObject>();//popしたキャラのList


    public bool setpop = false;

    // Start is called before the first frame update
    void Start()
    {
        Pop();
    }

    // Update is called once per frame
    void Update()
    {
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
        if(setpop == false)
        {
            float x = -48f;
            float y = -24f;
            for (int i = -24; i <= 22; i+=2)
            {
                
                for (int j = -48; j <= 52; j+=2)
                {                    
                    // 新しいオブジェクトをリストに追加
                    GameObject newGoal = Instantiate(GoalPrefab);
                    newGoal.transform.SetParent(this.transform);
                    poplist.Add(newGoal);
                    // Goal座標決め
                    newGoal.transform.position = new Vector3(x, y, 0f);
                    x += 2f;
                }
                y += 2f;
                x = -48f;
            }
            setpop = true;
        }
    }
}
