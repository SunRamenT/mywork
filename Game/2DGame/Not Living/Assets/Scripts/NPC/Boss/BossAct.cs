using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossAct : MonoBehaviour
{
    public GameObject KobunPrefab;

    public int poplim = 3;

    public List<GameObject> poplist = new List<GameObject>();//popしたキャラのList

    private ToritukareManagger torituki;
    // Start is called before the first frame update
    void Start()
    {
        torituki = GetComponent<ToritukareManagger>();
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

    public void Pop()
    {
        if (torituki.yurei != null && Input.GetButtonDown("Fire2"))
        {
            GameObject newBuka = Instantiate(KobunPrefab);
            newBuka.transform.position = new Vector3(this.gameObject.transform.position.x, this.gameObject.transform.position.y, 0f);
            newBuka.transform.SetParent(transform);
            poplist.Add(newBuka);
        }
    }
}
