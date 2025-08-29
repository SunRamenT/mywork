using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunShoot : MonoBehaviour
{
    public float shootspeed = 12.0f;
    public float shootDelay = 60f;
    public GameObject gunPrefab;//銃のオブジェクト
    public GameObject bulPrefab;//弾丸のオブジェクト
    bool inAttack = false; //Unusedfield
    GameObject gunobj;
    public ToritukareManagger torituki;//とりつき判定
    private PlayerController playcnt;
    private GameObject Hand;//マウスの位置
    private StatusManager Status;
    public string colTag;
    public bool hit;//関数全体でインスタンスにアクセス可能

    private Animator anim = null;

    private int zandan = 0;

    public void Attack()
    {
        if (torituki.yurei != null && inAttack == false)
        {
            inAttack = true;//AttackFlagon
            Destroy(gunobj);

            anim.SetBool("gun", true);

            //GhostPlaycont&Hand
            playcnt = torituki.yurei.GetComponent<PlayerController>();//getPlayerComponent
            Hand = torituki.yurei.transform.Find("hand").gameObject;

            float anglez = playcnt.anglez + 90f;//RotateAngle
            //CrateGameObject(RotateMoveDirection)
            Quaternion r = Quaternion.Euler(0, 0, anglez);
            Quaternion q = Quaternion.Euler(0, 0, anglez - 90f);

            gunobj = Instantiate(gunPrefab, (this.transform.position + Hand.transform.position)/2 , q);
            gunobj.transform.SetParent(transform);

            GameObject bulletobj = Instantiate(bulPrefab, Hand.transform.position, r);//弾のオブジェクト(Instance)
            bulletobj.GetComponent<BulletController>().Parent = this.gameObject;
            //ShootVector
            float x = Mathf.Cos(anglez * Mathf.Deg2Rad);
            float y = Mathf.Sin(anglez * Mathf.Deg2Rad);
            Vector3 v = new Vector3(x, y) * shootspeed;
            //AddPowerShoot
            Rigidbody2D body = bulletobj.GetComponent<Rigidbody2D>(); body.AddForce(v, ForceMode2D.Impulse);
            //StopAttack
            Invoke("StopAttack", shootDelay);
            Invoke("GunDestro", 0.5f);
        }
    }

    public void StopAttack()
    {
        inAttack = false;
        anim.SetBool("gun", false);
    }

    public void GunDestro()
    {
        Destroy(gunobj);

    }

    // Start is called before the first frame update
    void Start()
    {
        hit = false;
        inAttack = false;
        anim = transform.GetChild(0).gameObject.GetComponent<Animator>();
    }
    // Update is called once per frame
    void Update()
    {
        if(torituki.yurei!= null)
        {
            PlayerController plmv = torituki.yurei.GetComponent<PlayerController>();
            if(zandan < 6)
            {
                if (Input.GetButtonDown("Fire2"))
                {
                    Attack();
                    zandan++;
                    if (Hand.transform.position.x - transform.position.x > 0f)
                    {
                        transform.GetChild(0).gameObject.transform.localScale = new Vector2(-1, 1);
                    }
                    else if (Hand.transform.position.x - transform.position.x < 0f)
                    {
                        transform.GetChild(0).gameObject.transform.localScale = new Vector2(1, 1);
                    }
                }
            }
            
            if(hit == true)//ヒットフラグがたった場合
            {
                GRManager GRM = torituki.yurei.GetComponent<GRManager>();//GRManagerのインスタンス
                hit = false;//ヒットフラグを消す

                //ステータス変化
                if (colTag == "Hero" || colTag == "Mob")//撃った相手がheroかmobなら
                {
                    GRM.Evil += 1;
                    if (this.tag == "Enemy")//自分がenemy
                    {
                        plmv.target.GetComponent<StatusManager>().reputation -= 2;
                    }
                    else if (this.tag == "Hero")//自分がhero
                    {
                        plmv.target.GetComponent<StatusManager>().reputation -= 3;
                    }
                    else
                    {
                        plmv.target.GetComponent<StatusManager>().reputation -= 1;
                    }
                    Debug.Log("bad");
                }
                else
                {
                    GRM.Good += 1;
                    if (this.tag == "Enemy")
                    {
                        plmv.target.GetComponent<StatusManager>().reputation += 1;
                    }
                    else if (this.tag == "Hero")
                    {
                        plmv.target.GetComponent<StatusManager>().reputation += 2;
                    }
                    else
                    {
                        plmv.target.GetComponent<StatusManager>().reputation += 1;
                    }
                    Debug.Log("good");
                }
            }
        }
    }
}
