using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandsController : MonoBehaviour
{

    public PlayerController playcnt;
    float RadLim;//マウスの移動範囲制限(円形)
    public GameObject Lim;//円のコンポーネント
    public GameObject guhand;//手のオブジェクト
    public GameObject obake;//幽霊
    public GRManager GRM;//幽霊の内部データ管理
    public GameObject Player;//捜査対象

    public GameObject punchPrefab;//攻撃判定のある拳(すぐ消える)
    private GameObject punch;//攻撃判定のある拳(持続)

    // Start is called before the first frame update
    void Start()
    {
        transform.position = playcnt.transform.position;
        guhand.SetActive(false);
        obake.SetActive(true);
        Player = GameObject.FindWithTag("Player").gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        var col = Lim.GetComponent<CircleCollider2D>();
        RadLim = col.radius;
        transform.position = playcnt.transform.position;
        var cenPos = transform.position;
        transform.position =new Vector3(playcnt.mousePos.x, playcnt.mousePos.y,0f);
        var nowPos = transform.position;
   
        //追加　現在のポジションを保持する
        var currentPos = cenPos + Vector3.ClampMagnitude(nowPos - cenPos, RadLim);

        //追加　Mathf.ClampでX,Yの値それぞれが最小～最大の範囲内に収める。
        //範囲を超えていたら範囲内の値を代入する

        //追加　positionをcurrentPosにする
        transform.position = currentPos;

        handChange();
    }

    public void handChange()
    {
        if(playcnt.rock == true && playcnt.RockonTarget != null)
        {
            guhand.SetActive(true);
            obake.SetActive(false);
            return;
        }
        else
        {
            guhand.SetActive(false);
            obake.SetActive(true);
            PunchDestroy();
            return;
        }
    }

    void OnTriggerStay2D(Collider2D collider)//攻撃判定
    {        
        if (Input.GetButtonDown("Fire1") && playcnt.rock == true && playcnt.target != collider.gameObject)//
        {
            Destroy(punch);

            float anglez = playcnt.anglez + 90f;//RotateAngle
            //CrateGameObject(RotateMoveDirection)
            Quaternion q = Quaternion.Euler(0, 0, anglez - 90f);

            punch = Instantiate(punchPrefab, transform.position, q);
            punch.transform.SetParent(Player.transform);

            Invoke("PunchDestroy", 1f);
        }
    }

    public void PunchDestroy()
    {
        Destroy(punch);
    }
}
