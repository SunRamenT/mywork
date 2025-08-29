using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class KaeruMove : MonoBehaviour
{
    private NavMeshAgent _agent;

    private GameObject Item;

    private int data;

    private int ibukuro = 0;

    public GameObject goal;//移動目的地
    private GameObject goallist;//目的地リスト
    public bool goGoal = false;//移動するか判定
    private int i = 0;//目的地の番号

    private Animator anim = null;

    private List<GameObject> list;

    public GameObject firePrefab;//回復アイテム

    private GameObject yurei;//幽霊
    private PlayerController playcnt;
    private GRManager grm;//幽霊の内部データ管理
    private CatchObject catchOb;//プレイヤーが持っているもの


    // Start is called before the first frame update
    void Start()
    {
        yurei = GameObject.FindWithTag("Player");
        playcnt = yurei.GetComponent<PlayerController>();
        grm = yurei.GetComponent<GRManager>();

        _agent = GetComponent<NavMeshAgent>();
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
        _agent.speed = 1f;

        anim = GetComponent<Animator>();

        goal = GameObject.FindWithTag("Goal");
        list = goal.GetComponent<GoalGenerator>().poplist;
        catchOb = GameObject.FindWithTag("Hand").gameObject.GetComponent<CatchObject>();
    }

    // Update is called once per frame
    void Update()
    {
        if (goGoal == false)
        {
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

    public void OnTriggerEnter2D(Collider2D collider)//ヒット時
    {
        if (collider.CompareTag("Catch"))
        {
            anim.SetBool("Paku", true);
            ItemData itemData = collider.GetComponent<ItemData>();

            data = itemData.data;
            if (itemData == null)
            {
                return;
            }

            if (itemData.catched == true)
            {
                if (collider.transform.position.x - transform.position.x > 0f)
                {
                    this.transform.localScale = new Vector2(-1, 1);
                }
                else if (collider.transform.position.x - transform.position.x < 0f)
                {
                    this.transform.localScale = new Vector2(1, 1);
                }
                _agent.updatePosition = false;
                _agent.speed = 0f;
                Invoke("thxEat", 1f);
                Destroy(collider.transform.gameObject);
                return;
            }
            else if (collider.GetComponent<ItemData>().catched == false)
            {
                _agent.updatePosition = false;
                Invoke("Eat", 0.5f);
                Item = collider.transform.gameObject;
                
                return;
            }
        }
        return;
    }

    public void Eat()//普通にアイテムを食う場合
    {
        Invoke("notplayer",0.5f);
        Destroy(Item);
    }

    public void notplayer()//プレイヤーの施し以外でのゴール更新
    {
        anim.SetBool("Paku", false);
        goGoal = false;
        ibukuro++;
        _agent.updatePosition = true;
        if (ibukuro > 3)
        {
            Destroy(this.gameObject);
        }
    }

    public void thxEat()//プレイヤーの施しを得た場合
    {
        anim.SetBool("Love", true);
        Invoke("isplayer", 2f);
        Destroy(Item);

    }

    public void isplayer()//アイテムを出してゴール更新
    {
        GameObject newkaihuku = Instantiate(firePrefab);
        newkaihuku.transform.position = new Vector3(this.gameObject.transform.position.x, this.gameObject.transform.position.y, 0f);
        if(data == 0)
        {
            newkaihuku.GetComponent<BlueFireData>().value = 10;
        }
        else if (data == 1)
        {
            newkaihuku.GetComponent<BlueFireData>().value = 20;
        }

        ibukuro++;
        catchOb.grap = false;  
        
        playcnt.target.GetComponent<StatusManager>().reputation += 1;
        
        grm.Good += 1;
        goGoal = false;
        _agent.updatePosition = true;
        anim.SetBool("Paku", false);
        anim.SetBool("Love", false);
        if(ibukuro > 2)
        {
            Destroy(this.gameObject);
        }
    }
}
