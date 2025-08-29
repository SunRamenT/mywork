using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCMove : MonoBehaviour
{
    private NavMeshAgent _agent;

    public GameObject goal;//移動目的地
    private GameObject goallist;//目的地リスト
    public bool goGoal = false;//目的地へ向かうか判定
    private int i = 0;//目的地番号

    //Attack
    public GameObject PunchPrefab;//拳オブジェクト
    private float anglez = -90.0f;
    GameObject punchobj;

    public GameObject AtackedEnemy;//被攻撃相手

    StatusManager Status;
    float speed = 3f;
    Rigidbody2D rb;
    public ToritukareManagger toritukare;//乗っ取り管理

    private Animator anim = null;

    private List<GameObject> list;

    // Start is called before the first frame update
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
        _agent.speed = 1f;

        Status = GetComponent<StatusManager>();
        speed = Status.speed;
        rb = GetComponent<Rigidbody2D>();
        toritukare = GetComponent<ToritukareManagger>();

        anim = transform.GetChild(0).gameObject.GetComponent<Animator>();

        goal = GameObject.FindWithTag("Goal");
        list = goal.GetComponent<GoalGenerator>().poplist;
    }

    // Update is called once per frame
    void Update()
    {
        if (toritukare.yurei == null)
        {
            if (goGoal == false)
            {
                anim.SetBool("torituki", false);
                
                i = 0;
                i = i + Random.Range(0, list.Count);
                goallist = list[i];
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
        else if(toritukare.yurei != null)
        {
            anim.SetBool("torituki", true);
            Destroy(punchobj);
            _agent.SetDestination(transform.position);
            goGoal = false;
            Move();
        }
    }

    public void Move()
    {
        Vector3 current = transform.position;
        Vector3 target = new Vector3(current.x, current.y, 0);
        float step = 1f * Time.deltaTime;
        transform.position = Vector3.MoveTowards(current, target, step);
    }

    public void Attack(Vector3 enpos)
    {
        anim.SetBool("punch", true);
        
        
        float RadLim;
        RadLim = 1.5f;
        
        Vector2 lookdir = enpos - transform.position;
        anglez = Mathf.Atan2(lookdir.y, lookdir.x) * Mathf.Rad2Deg - 90f;
        Quaternion q = Quaternion.Euler(0, 0, anglez);
        
        if(punchobj == null)
        {
            punchobj = Instantiate(PunchPrefab, enpos, q);
            punchobj.transform.SetParent(transform);
        }
        else
        {
            var cenPos = transform.position;
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
        if (toritukare.yurei == null)//とりつかれていない時
        {
            if (col.CompareTag("Enemy") || col.CompareTag("Mob") || col.CompareTag("Hero"))
            {
                var enemypos = new Vector3(col.transform.position.x, col.transform.position.y, 0f);
                

                if (this.CompareTag("Mob"))
                {
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
                    if (col.GetComponent<StatusManager>().Popularity == "Worst" || col.GetComponent<StatusManager>().Popularity == "So Bad")
                    {
                        // Worstな相手から逃げる
                        transform.position = Vector2.MoveTowards(transform.position, new Vector2(col.transform.position.x, col.transform.position.y), -Status.speed * Time.deltaTime);

                        anim.SetBool("bad", true);

                        //対象の方向の逆向き
                        if (col.transform.position.x - transform.position.x > 0f)//相手が右
                        {
                            this.transform.localScale = new Vector2(1, 1);//自分は左
                        }
                        else if (col.transform.position.x - transform.position.x < 0f)//相手が左
                        {
                            this.transform.localScale = new Vector2(-1, 1);//自分は右
                        }
                    }
                    if (col.GetComponent<StatusManager>().Popularity == "Saint" || col.GetComponent<StatusManager>().Popularity == "So Good")
                    {
                        anim.SetBool("good", true);
                    }
                    
                }
                else if (this.CompareTag("Enemy"))
                {
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
                    if (col.CompareTag("Mob"))
                    {
                        // Mobに向けて進む
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
                    }
                    else if (col.CompareTag("Hero") && this.GetComponent<StatusManager>().HP >= this.GetComponent<StatusManager>().MaxHp)
                    {
                        // ダメージを受けていなければヒーローから逃げる
                        transform.position = Vector2.MoveTowards(transform.position, new Vector2(col.transform.position.x, col.transform.position.y), -Status.speed * Time.deltaTime);

                        //対象の方向の逆向き
                        if (col.transform.position.x - transform.position.x > 0f)//相手が右
                        {
                            this.transform.localScale = new Vector2(1, 1);//自分は左
                        }
                        else if (col.transform.position.x - transform.position.x < 0f)//相手が左
                        {
                            this.transform.localScale = new Vector2(-1, 1);//自分は右
                        }
                    }
                }
                else if (this.CompareTag("Hero"))
                {
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
                    if (col.CompareTag("Enemy") || col.GetComponent<StatusManager>().Popularity == "Worst")
                    {
                        // EnemyかWorstに向けて進む
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
                    }
                }
            }
        }
        else if(toritukare.yurei != null)
        {
            if (Input.GetButton("Fire3"))
            {
                if (Input.GetButtonDown("Fire1"))
                {
                    if (col.CompareTag("Catch"))
                    {
                        Status.HP += col.GetComponent<ItemData>().value;
                        Destroy(col.gameObject);
                    }
                }
            }
        }
    }

    public void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("Enemy") || col.CompareTag("Mob") || col.CompareTag("Hero"))
        {
            
            anim.SetBool("punch", false);
            anim.SetBool("good", false);
            anim.SetBool("bad", false);
           
            
            goGoal = false;
            Destroy(punchobj);
        }
    }    
}
