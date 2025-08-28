using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BigChaisorMove : MonoBehaviour
{
    Transform playerTr; // プレイヤーのTransform
    private NavMeshAgent _agent;

    private GameObject goal;
    private GameObject goallist;
    public bool goGoal = false;
    private int i = 0;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;

        // プレイヤーのTransformを取得（プレイヤーのタグをPlayerに設定必要）
        playerTr = GameObject.FindGameObjectWithTag("Player").transform;
    }

    public void Update()
    {
        if (goGoal == false)
        {
            goal = GameObject.FindWithTag("Goal");// 移動目標
            List<GameObject> list = goal.GetComponent<GoalGenerator>().poplist;

            i = 0;
            i = i + Random.Range(0, list.Count);
            goallist = goal.GetComponent<GoalGenerator>().poplist[i];
            _agent.SetDestination(goallist.transform.position);
            _agent.speed = 1f;
            goGoal = true;
        }

        if (goallist.transform.position.x - transform.position.x > 0f)
        {
            this.transform.localScale = new Vector2(-1, 1);
        }
        else if (goallist.transform.position.x - transform.position.x < 0f)
        {
            this.transform.localScale = new Vector2(1, 1);
        }

        if ((transform.position - goallist.transform.position).sqrMagnitude < 5f)
        {
            goGoal = false;
        }
    }
}
