using UnityEngine;
using UnityEngine.SceneManagement;

public class ShinigamiController : MonoBehaviour
{
    GameObject plyobj;
    Transform playerTr; // プレイヤーのTransform
    float speed = 1.5f; // 敵の動くスピード

    public string sceneName;

    public int xlim;
    public int ylim;

    private void Start()
    {
        plyobj = GameObject.FindGameObjectWithTag("Player").gameObject;
        // プレイヤーのTransformを取得（プレイヤーのタグをPlayerに設定必要）
        playerTr = GameObject.FindGameObjectWithTag("Player").transform;
        InvokeRepeating(nameof(CountMethod), 10f, 10f);
    }

    private void Update()
    {

        // プレイヤーとの距離が0.1f未満になったらそれ以上実行しない
        if ((transform.position - playerTr.position).sqrMagnitude < 1.5f)
        {
            PlayerController.gameState = "gameover";
            SceneManager.LoadScene(sceneName);
            return;
        }
            

        // プレイヤーに向けて進む
        transform.position = Vector2.MoveTowards(
            transform.position,
            new Vector2(playerTr.position.x, playerTr.position.y),
            speed * Time.deltaTime);

        //対象の方向を向く
        if (playerTr.position.x - transform.position.x > 0f)//相手が右
        {
            this.transform.localScale = new Vector2(-1, 1);//自分も右
        }
        else if (playerTr.position.x - transform.position.x < 0f)//相手が左
        {
            this.transform.localScale = new Vector2(1, 1);//自分も左
        }
    }

    void CountMethod()
    {
        transform.position = new Vector3(playerTr.position.x + Random.Range(-xlim, xlim), playerTr.position.y + Random.Range(-ylim, ylim));
    }
}
