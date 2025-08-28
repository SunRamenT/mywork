using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


public class BossKobun : MonoBehaviour
{
    private NavMeshAgent _agent;

    public bool goBoss = false;

    //Attack
    public GameObject PunchPrefab;
    private float anglez = -90.0f;
    GameObject punchobj;

    public GameObject AtackedEnemy;

    StatusManager Status;
    float speed = 3;
    Rigidbody2D rb;

    private PlayerController plycnt;

    private Animator anim = null;

    private GameObject Boss;

    // Start is called before the first frame update
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
        _agent.speed = 2f;

        Status = GetComponent<StatusManager>();
        speed = Status.speed;
        rb = GetComponent<Rigidbody2D>();

        anim = transform.GetChild(0).GetComponent<Animator>();

        Boss = transform.parent.gameObject;
        transform.parent = null;

        plycnt = GameObject.FindGameObjectWithTag("Player").gameObject.GetComponent<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (plycnt.rock == false)
        {
            goBoss = true;
            Destroy(gameObject);
        }

        if (goBoss == false)
        { 
            _agent.SetDestination(Boss.transform.position);
            _agent.speed = 3f;
        }
        if (Boss != null)
        {
            if (Boss.transform.position.x - transform.position.x > 0f)
            {
                transform.GetChild(0).transform.localScale = new Vector2(-1, 1);
            }
            else if (Boss.transform.position.x - transform.position.x < 0f)
            {
                transform.GetChild(0).transform.localScale = new Vector2(1, 1);
            }
        } 
    }

    public void Attack(Vector3 enpos)
    {
        anim.SetBool("Punch", true);

        goBoss = true;

        float RadLim;
        RadLim = 1.5f;

        Vector2 lookdir = enpos - transform.position;
        anglez = Mathf.Atan2(lookdir.y, lookdir.x) * Mathf.Rad2Deg - 90f;
        Quaternion q = Quaternion.Euler(0, 0, anglez);

        if (punchobj == null)
        {
            punchobj = Instantiate(PunchPrefab, enpos, q);
            punchobj.transform.SetParent(transform);
        }
        else
        {
            var cenPos = transform.position;
            var nowPos = punchobj.transform.position;

            //追加　現在のポジションを保持する
            var currentPos = cenPos + Vector3.ClampMagnitude(enpos - cenPos, RadLim);
            //追加　Mathf.ClampでX,Yの値それぞれが最小～最大の範囲内に収める。
            //範囲を超えていたら範囲内の値を代入する

            //追加　positionをcurrentPosにする
            punchobj.transform.position = Vector3.MoveTowards(punchobj.transform.position, currentPos, speed);
        }
    }

    public void OnDetectObject(Collider2D col)
    {   
        if (col.CompareTag("Mob") || col.CompareTag("Hero"))
        {
            var enemypos = new Vector3(col.transform.position.x, col.transform.position.y, 0f);

            if (this.GetComponent<StatusManager>().HP < this.GetComponent<StatusManager>().MaxHp)
            {
                if (AtackedEnemy != null)
                {
                    if (AtackedEnemy == col.transform.gameObject)
                    {
                        // ダメージを受けていれば攻撃された相手に向かう
                        _agent.SetDestination(col.transform.position);
                        _agent.speed = Status.speed;
                        Attack(enemypos);

                        //対象の方向を向く
                        if (col.transform.position.x - transform.position.x > 0f)//相手が右
                        {
                            this.transform.localScale = new Vector2(-1, 1);//自分も右
                        }
                        else if (col.transform.position.x - transform.position.x < 0f)//相手が左
                        {
                            this.transform.localScale = new Vector2(1, 1);//自分も左
                        }
                        return;
                    }
                }
            }
            else if(col.gameObject != Boss)
            {
                _agent.SetDestination(col.transform.position);
                _agent.speed = Status.speed;
                Attack(enemypos);

                //対象の方向を向く
                if (col.transform.position.x - transform.position.x > 0f)//相手が右
                {
                    this.transform.localScale = new Vector2(-1, 1);//自分も右
                }
                else if (col.transform.position.x - transform.position.x < 0f)//相手が左
                {
                    this.transform.localScale = new Vector2(1, 1);//自分も左
                }
                return;
            }
        }
    }

    public void OnTriggerExit2D(Collider2D col)
    {
        if (AtackedEnemy == col.transform.gameObject)
        {

            anim.SetBool("Punch", false);

            goBoss = false;
            Destroy(punchobj);
        }
    }
}
