using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayPunch : MonoBehaviour
{
    private GameObject yurei;
    private PlayerController playcnt;
    private GRManager GRM;//—H—ì‚Ì“à•”ƒf[ƒ^ŠÇ—
    private Animator anim = null;
    private GameObject tar;//’–Ú‘ÎÛ

    // Start is called before the first frame update
    void Start()
    {
        yurei = GameObject.FindGameObjectWithTag("Player").gameObject;
        playcnt = yurei.GetComponent<PlayerController>();
        GRM = yurei.GetComponent<GRManager>();
        tar = yurei.GetComponent<PlayerController>().target;

        anim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D collider)//ƒqƒbƒg
    {
        if(tar.gameObject != collider.gameObject)
        {
            if (collider.CompareTag("Enemy") || collider.CompareTag("Mob") || collider.CompareTag("Hero"))
            {
                collider.transform.GetComponent<StatusManager>().ghostHand = true;

                if (playcnt.target == null)
                {
                    return;
                }

                StatusManager targetStatus = playcnt.target.GetComponent<StatusManager>();
                if (targetStatus == null)
                {
                    return;
                }

                int power = playcnt.target.GetComponent<StatusManager>().power;
                collider.GetComponent<StatusManager>().HP -= power;
                if (collider.GetComponent<NPCMove>() != null)
                {
                    collider.GetComponent<NPCMove>().AtackedEnemy = playcnt.RockonTarget.transform.gameObject;//UŒ‚‚µ‚½‘Šè‚ğ•Û‘¶
                }
                else if (collider.GetComponent<BossKobun>())
                {
                    collider.GetComponent<BossKobun>().AtackedEnemy = playcnt.RockonTarget.transform.gameObject;//UŒ‚‚µ‚½‘Šè‚ğ•Û‘¶
                }

                if (collider.GetComponent<StatusManager>().Popularity == "Normal" || collider.GetComponent<StatusManager>().Popularity == "Good" || collider.GetComponent<StatusManager>().Popularity == "So Good" || collider.GetComponent<StatusManager>().Popularity == "Saint")
                {
                    GRM.Evil += 1;
                    if (playcnt.target.CompareTag("Enemy"))
                    {
                        playcnt.target.GetComponent<StatusManager>().reputation -= 2;
                    }
                    else if (playcnt.target.CompareTag("Hero"))
                    {
                        playcnt.target.GetComponent<StatusManager>().reputation -= 3;
                    }
                    else
                    {
                        playcnt.target.GetComponent<StatusManager>().reputation -= 1;
                    }
                    Debug.Log("bad");
                }
                else
                {
                    GRM.Good += 1;
                    if (playcnt.target.CompareTag("Enemy"))
                    {
                        playcnt.target.GetComponent<StatusManager>().reputation += 1;
                    }
                    else if (playcnt.target.CompareTag("Hero"))
                    {
                        playcnt.target.GetComponent<StatusManager>().reputation += 2;
                    }
                    else
                    {
                        playcnt.target.GetComponent<StatusManager>().reputation += 1;
                    }
                    Debug.Log("good");
                }
            }
        }
    }
}
