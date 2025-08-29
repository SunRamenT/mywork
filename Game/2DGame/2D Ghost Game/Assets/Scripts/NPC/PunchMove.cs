using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PunchMove : MonoBehaviour
{
    private GameObject NPC;
    public float AttackInterval = 0.01f;

    // Start is called before the first frame update
    void Start()
    {
        NPC = transform.parent.gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public IEnumerator DPS(Collider2D collider)
    {
        if (collider.CompareTag("Enemy") || collider.CompareTag("Mob") || collider.CompareTag("Hero"))
        {
            collider.transform.GetComponent<StatusManager>().ghostHand = false;

            int power = NPC.GetComponent<StatusManager>().power;

            collider.GetComponent<StatusManager>().HP -= power;

            if (collider.GetComponent<NPCMove>() != null)
            {
                collider.GetComponent<NPCMove>().AtackedEnemy = NPC.transform.gameObject;//çUåÇÇµÇΩëäéËÇï€ë∂
            }
            else if (collider.GetComponent<BossKobun>())
            {
                collider.GetComponent<BossKobun>().AtackedEnemy = NPC.transform.gameObject;//çUåÇÇµÇΩëäéËÇï€ë∂
            }

            if (collider.GetComponent<StatusManager>().Popularity == "Normal" || collider.GetComponent<StatusManager>().Popularity == "Good")
            {
                if (NPC.CompareTag("Enemy"))
                {
                    NPC.GetComponent<StatusManager>().reputation -= 2;
                }
                else if (NPC.CompareTag("Hero"))
                {
                    NPC.GetComponent<StatusManager>().reputation -= 3;
                }
                else
                {
                    NPC.GetComponent<StatusManager>().reputation -= 1;
                }
                Debug.Log("bad");
            }
            else
            {
                if (NPC.CompareTag("Enemy"))
                {
                    NPC.GetComponent<StatusManager>().reputation += 1;
                }
                else if (NPC.CompareTag("Hero"))
                {
                    NPC.GetComponent<StatusManager>().reputation += 2;
                }
                else
                {
                    NPC.GetComponent<StatusManager>().reputation += 1;
                }
                Debug.Log("good");
            }
        }

        yield return new WaitForSeconds(AttackInterval);
    }

    private void OnTriggerEnter2D(Collider2D collider)//ÉqÉbÉgéû
    {
        StartCoroutine(DPS(collider));
    }
}
