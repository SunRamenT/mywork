using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletController : MonoBehaviour
{
    public float deleteTime = 2;//削除時間
    public GameObject HavTarget;//弾丸が当たった相手
    public string ColTag;//弾丸が当たった相手のタグ
    public GameObject Parent;//親オブジェクト(警察)

    // Start is called before the first frame update
    void Start()
    {
        Destroy(gameObject, deleteTime); //一定時間で消す
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Enemy" || collision.gameObject.tag == "Mob" || collision.gameObject.tag == "Hero")
        {
            collision.transform.GetComponent<StatusManager>().ghostHand = true;
            collision.transform.GetComponent<StatusManager>().HP -= 25;
            collision.transform.GetComponent<NPCMove>().AtackedEnemy = Parent;
            Debug.Log(collision.transform.GetComponent<StatusManager>().HP);//対象のHP
            GetComponent<CircleCollider2D>().enabled = false; //当たり判定無効化
            GetComponent<Rigidbody2D>().simulated = false; //物理シミュレーション無効化
            Parent.GetComponent<GunShoot>().hit = true;//銃所持者のヒットフラグをオンにする
            collision.transform.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
            Parent.GetComponent<GunShoot>().colTag = collision.gameObject.tag;//銃所持者に撃った相手のタグを返す
        }
        Destroy(gameObject);//オブジェクトを壊す
    }
}
