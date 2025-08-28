using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaisorCollider : MonoBehaviour
{
    GameObject Chaisor;

    // Start is called before the first frame update
    void Start()
    {
        Chaisor = transform.parent.gameObject;
    }

    public void OnTriggerStay2D(Collider2D collider)
    {
        if (collider.CompareTag("Player"))
        {
            if(Chaisor.GetComponent<ChaisorMove>().chase == true)
            {
                if (collider.transform.position.x - Chaisor.transform.position.x > 0f)
                {
                    Chaisor.transform.localScale = new Vector2(-1, 1);
                }
                else if (collider.transform.position.x - Chaisor.transform.position.x < 0f)
                {
                    Chaisor.transform.localScale = new Vector2(1, 1);
                }
            }
        }
    }
    public void OnTriggerExit2D(Collider2D col)// プレイヤーを感知する範囲
    {
        if (col.CompareTag("Player"))
        {
            Chaisor.GetComponent<ChaisorMove>().chase = false;
            Chaisor.GetComponent<ChaisorMove>().goGoal = false;
        }
    }
}
