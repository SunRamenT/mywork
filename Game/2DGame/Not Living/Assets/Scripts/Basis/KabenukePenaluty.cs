using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KabenukePenaluty : MonoBehaviour
{
    public GameObject reikonobj;
    private Animator anim = null;

    // Start is called before the first frame update
    void Start()
    {
        anim = GameObject.FindGameObjectWithTag("Player").transform.GetChild(0).gameObject.GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnTriggerStay2D(Collider2D col)//—H—ì‚ª•Ç‚ğ‚·‚è”²‚¯‚Ä‚¢‚é‚Æ‚«—ì°‚ÌÁ”ï‘¬“x‚ğ2”{‚É‚·‚é
    {
        if (col.transform.gameObject.CompareTag("Player"))
        {
            anim.SetBool("kabenuke", true);
            reikonobj.GetComponent<ReikonManagger>().speed = 2f;
        }
    }
}
