using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ChaisorMove : MonoBehaviour
{
    Transform playerTr; // プレイヤーのTransform
    private NavMeshAgent _agent;

    private GameObject goal;// 移動先
    private GameObject goallist;// 移動リスト一覧
    public bool chase = false;// 追跡判定
    public bool goGoal = false;// 目的地へ向かう判定
    private int i = 0;

    public bool angel = true;

    private GRManager grm;// 幽霊の内部パラメータ

    private Animator anim = null;

    public string sceneName;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;

        // プレイヤーのTransformを取得（プレイヤーのタグをPlayerに設定必要）
        playerTr = GameObject.FindGameObjectWithTag("Player").transform;
        grm = GameObject.FindGameObjectWithTag("Player").GetComponent<GRManager>();
        anim = gameObject.GetComponent<Animator>();
    }

    public void Update()
    {
        if (goGoal == false)
        {
            goal = GameObject.FindWithTag("Goal");
            List<GameObject> list = goal.GetComponent<GoalGenerator>().poplist;

            i = 0;
            i = i + Random.Range(0, list.Count);
            goallist = goal.GetComponent<GoalGenerator>().poplist[i];
            _agent.SetDestination(goallist.transform.position);
            _agent.speed = 1.5f;
            anim.SetBool("Run", false);
            goGoal = true;
        }

        if(chase == false)
        {
            if (goallist.transform.position.x - transform.position.x > 0f)
            {
                this.transform.localScale = new Vector2(-1, 1);
            }
            else if (goallist.transform.position.x - transform.position.x < 0f)
            {
                this.transform.localScale = new Vector2(1, 1);
            }
        }

        if ((transform.position - goallist.transform.position).sqrMagnitude < 5f)
        {
            chase = false;
            goGoal = false;
        }
    }
    public void OnDetectObject(Collider2D collider)
    {
        if (collider.CompareTag("Player"))
        {
            // プレイヤーとの距離が0.1f未満になったらそれ以上実行しない
            if ((transform.position - collider.transform.position).sqrMagnitude < 1.5f)
            {
                goGoal = false;
                PlayerController.gameState = "gameover";
                SceneManager.LoadScene(sceneName);
                return;
            }
            chase = true;
            anim.SetBool("Run", true);
            
            if ((grm.goodper > 70 && angel == true) || (grm.evilper > 70 && angel == false))// 
            {
                _agent.speed = 5f;
            }
            else if((grm.goodper > 70 && angel == false) || (grm.evilper > 70 && angel == true))
            {
                _agent.speed = 2f;
            }
            else
            {
                _agent.speed = 4f;
            }

            // プレイヤーに向けて進む
            _agent.SetDestination(collider.transform.position);
        }
    }
}
