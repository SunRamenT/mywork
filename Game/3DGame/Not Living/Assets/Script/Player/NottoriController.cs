using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NottoriController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private GameObject ControleTarget; //今操作しているキャラ
    [SerializeField] private GameObject NottoriTarget; //これからのっとるキャラ

    private GameObject OldChara; //キャラ保持

    private bool Nottori; //のっとり判定


    public List<Transform> NotList = new List<Transform>();

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Interact"))
        {
            if (Nottori == true)
            {

                Nottori = false;
            }

            else if (Nottori == false && NottoriTarget != false)
            {

                Nottori = true;
            }
        }

        for(int i = 0; i < NotList.Count; i++)
        {
            for (int k = i + 1; k < NotList.Count; k++)
            {
                if(NotList[i] == NotList[k])
                {
                    NotList.RemoveAt(k);
                }
            }

            if(!NotList[i])
            {
                NotList.RemoveAt(i);
            }
        }
    }

    private void OnTriggerStay(Collider collider)
    {
        if (collider.gameObject.tag == "NPC")//NPC相手か判定
        {
            NottoriTarget = collider.gameObject;
        }
    }

    private void ChangeTarget()
    {
        OldChara = ControleTarget;
        ControleTarget = NottoriTarget;
    }

}
