using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CatchObject : MonoBehaviour
{
    public bool grap;//掴み判定
    public GameObject CatchH;//手のオブジェクト
    public GameObject MainOb;//操作キャラ
    private GameObject CatchOb;//掴む対象

    private PlayerController playcnt;

    public Rigidbody2D rb;
    // Start is called before the first frame update
    void Start()
    {
        grap = false;
        playcnt = MainOb.GetComponent<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        CatchH.transform.position = MainOb.transform.position;
        if (CatchOb)
        {
            if ((CatchOb.transform.position - MainOb.transform.position).sqrMagnitude < 3f)
            {
                var posCH = CatchH.transform.position;
                var posCO = CatchOb.transform.position;
                var pos = Vector3.zero;

                pos = new Vector3(CatchH.transform.position.x - CatchOb.transform.position.x - 0.5f, CatchH.transform.position.y - CatchOb.transform.position.y, 0f);
                rb.MovePosition(CatchOb.transform.position + pos);
            }
            
            if (Input.GetButtonUp("Fire3") && grap == true || (CatchOb.transform.position - MainOb.transform.position).sqrMagnitude > 3f)
            {
                CatchOb.GetComponent<ItemData>().catched = false;
                CatchOb = null;
                grap = false;
                Debug.Log("Bye");
                return;
            }
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if(playcnt.rock == true) 
        {
            if (Input.GetButton("Fire3"))
            {
                if (other.CompareTag("Catch") && grap == false)
                {
                    Debug.Log("Catch");
                    grap = true;
                    other.GetComponent<ItemData>().catched = true;

                    CatchOb = other.gameObject;
                    rb = other.GetComponent<Rigidbody2D>();
                }
            }
        }
        else if(playcnt.rock == false)
        {
            if(CatchOb != null)
            {
                CatchOb.GetComponent<ItemData>().catched = false;
                CatchOb = null;
                grap = false;
            }
        }
    }
}
