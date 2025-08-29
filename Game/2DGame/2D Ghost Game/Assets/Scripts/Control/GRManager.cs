using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GRManager : MonoBehaviour
{
    public GameObject reikonobj;//霊魂管理
    public int Good = 5;
    public int Evil = 5;
    public int Total = 2;
    public PlayerController playcnt;
    public GameObject target;
    public int goodper;//善行値の割合
    public int evilper;//悪行値の割合

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(playcnt.rock == true)//ロックオン中対象をのっとり相手に
        {
            target = playcnt.target;
        }
        else if(playcnt.rock == false)//ロックオン解除で対象をいったん空に
        {
            target = null;
        }

        if(Good < 1)//善良値は0以下にならない
        {
            Good = 1;
        }
        if(Evil < 1)//邪悪値は0以下にならない
        {
            Evil = 1;
        }
        
        PercentCal();
    }

    public void PercentCal()//善悪の割合
    {
        goodper = (int)(((float)Good /((float)Good + (float)Evil)) * 100);
        evilper = (int)(((float)Evil / ((float)Good + (float)Evil)) * 100);
    }

    void OnTriggerEnter2D(Collider2D collider)//霊魂と接触したら魂回復
    {
        if (collider.CompareTag("Kaihuku"))
        {
            reikonobj.GetComponent<ReikonManagger>().reikon += collider.GetComponent<BlueFireData>().value;
            reikonobj.GetComponent<ReikonManagger>().plus = collider.GetComponent<BlueFireData>().value;
            Destroy(collider.gameObject);
        }
    }
}
