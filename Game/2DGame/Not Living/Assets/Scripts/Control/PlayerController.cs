using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static string gameState = "playing";
    private Rigidbody2D rb;
    [SerializeField]
    private float speed = 0.03f; 
    public GameObject target;//現在操作できるプレイヤー
    public Transform RockonTarget; // とりついている相手
    public EnemyListManager enemyListManager; // マウスで触っている対象
    private GameObject PreObject;
    public bool rock = false; // とりついているかの判定
    private float prespeed;
    private float onispeedhav;//上位追跡者の領域に入ったときのspeed
    public GameObject reikonobj;//霊魂管理

    public float anglez = -90.0f;

    public float axisH;
    public float axisV;

    public Vector2 lookdir;//プレイヤーの見ている向き

    public Vector2 mousePos;

    bool isMoving = false;
    private int targetIndex = 0;// のっとり対象の保存先

    GameObject ghostobj;

    private Animator anim = null;

    // x軸方向の移動範囲の最小値(マップの左端)
    private float minX = -51.3f;
    // x軸方向の移動範囲の最大値(マップの右端)
    private float maxX = 55.0f;
    // y軸方向の移動範囲の最小値(マップの下端)
    private float minY = -27.0f;
    // y軸方向の移動範囲の最大値(マップの上端)
    private float maxY = 25.1f;

    void Start()
    {
        gameState = "playing";
        rb = target.GetComponent<Rigidbody2D>();
        target.transform.position = this.transform.position;
        ghostobj = this.transform.Find("yuurei").gameObject;

        anim = transform.Find("yuurei").gameObject.GetComponent<Animator>();
    }

    void Update()
    {
        if (target == null)
        {
            LockChange();
        }


        if (isMoving == false)
        {
            axisH = Input.GetAxisRaw("Horizontal");
            axisV = Input.GetAxisRaw("Vertical");
        }
        
        mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        lookdir = new Vector3(mousePos.x, mousePos.y, 0f) - transform.position;
        anglez = Mathf.Atan2(lookdir.y, lookdir.x) * Mathf.Rad2Deg - 90f;
        
        if(axisH < 0f)
        {
            ghostobj.transform.localScale = new Vector2(1, 1);
        }
        else if(axisH > 0f)
        {
            ghostobj.transform.localScale = new Vector2(-1, 1);   
        }

        if (axisH != 0 || axisV != 0)
        {
            anim.SetBool("move", true);
        }
        else
        {
            anim.SetBool("move", false);
        }

        if (Input.GetButtonDown("Jump"))
        {
            if (rock == true && PreObject != null)
            {
                rock = false;
                target = PreObject;
                speed = prespeed;
                Debug.Log("kaijo");
                anim.SetBool("rockon", false);
                return;
            }

            if (enemyListManager.EnemyList.Count == 0)
            {
                return;
            }

            LockChange();
        }
                   

        if (enemyListManager.EnemyList.Count <= targetIndex)
        {
            targetIndex = 0;
        }

        if(rock == true)
        {
            target.transform.localScale = ghostobj.transform.localScale;
        }
    }

    void FixedUpdate()
    {
        if (target == null)
        {
            LockChange();
        }

        
        if(gameState == "gameover")
        {
            GameStop();
        }

        Move();
    }

    void Move()
    {
        Vector3 tmp = target.transform.position;
        this.transform.position = target.transform.position;
        var nextPos = Vector2.zero;
        nextPos.x = speed * axisH * Time.deltaTime; 
        nextPos.y = speed * axisV * Time.deltaTime;
        target.transform.position = tmp + new Vector3(nextPos.x, nextPos.y, 0f);
        transform.Translate(nextPos);

        // 移動範囲制限
        Vector3 clampedPosition = this.transform.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, minX, maxX);
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, minY, maxY);
        this.transform.position = clampedPosition;
    }

    void LockChange()
    {
        if (rock == false && enemyListManager.EnemyList[targetIndex].transform != null)
        {
            // Toggle the rock-on mode
            rock = true;
            Debug.Log("rock!!!");

            anim.SetBool("rockon", true);
            //元のターゲットを保持
            PreObject = target;
            targetIndex = 0;
            //ロックオンターゲットを敵リストから取得
            RockonTarget = enemyListManager.EnemyList[targetIndex].transform; // Transform�ɕύX
            target = RockonTarget.gameObject; // GameObject�ɕύX
            this.transform.position = target.transform.position;//ターゲットの上に重なる
            prespeed = speed;//元のスピード保存
            speed = target.GetComponent<StatusManager>().speed;//ターゲットのスピード保存
            return;
        }
        else if(rock == true)//とりついている対象が消されたとき
        {
            target = PreObject;
            reikonobj.GetComponent<ReikonManagger>().reikon -= 15;
            rock = false;
            anim.SetBool("rockon", false);
            speed = prespeed;
            return;
        }
        return;
    }

    

    public void GameOver()
    {
        gameState = "gameover";
        GameStop();
    }

    void GameStop()
    {
        Rigidbody2D rbody = GetComponent<Rigidbody2D>();
        rbody.velocity = new Vector2(0, 0);
    }

    public void OnTriggerStay2D(Collider2D col)
    {
        if (col.transform.gameObject.CompareTag("Tile"))
        {
            anim.SetBool("kabenuke", true);
            reikonobj.GetComponent<ReikonManagger>().speed = 3f;
        }
    }

    public void OnTriggerEnter2D(Collider2D col)
    {
        if (col.transform.gameObject.CompareTag("Oni") && rock == false)
        {
            anim.SetBool("kabenuke", true);
            onispeedhav = speed;
            speed = 0.7f;
        }
    }

    public void OnTriggerExit2D(Collider2D col)
    {
        if (col.transform.gameObject.CompareTag("Tile"))
        {
            anim.SetBool("kabenuke", false);
            reikonobj.GetComponent<ReikonManagger>().speed = 1f;
        }
        if (col.transform.gameObject.CompareTag("Oni") && rock == false)
        {
            anim.SetBool("kabenuke", false);
            speed = onispeedhav;
        }
    }
}