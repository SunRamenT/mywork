using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatusManager : MonoBehaviour
{
    public GameObject Main;

    Rigidbody2D rb;

    public int HP;//現在のHP
    public int MaxHp;//最大HP
    public float speed;
    public int power;
    public int reputation = 50;//そのキャラの周囲からの評価
    public string Popularity;//そのキャラの人気度
    public PlayerController playcnt;
    public ToritukareManagger toritukare;//乗っ取り管理
    public float regene = 3f;//回復速度

    public bool ghostHand = false;

    public GameObject firePrefab;//回復アイテム

    float flashInterval = 0.1f;
    int loopCount = 3;

    SpriteRenderer sp;

    enum STATE
    {
        NOMAL,
        DAMAGED,
        MUTEKI
    }
    STATE state;

    public string TagName;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playcnt = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>();
        toritukare = this.gameObject.GetComponent<ToritukareManagger>();

        Main = this.gameObject;
        sp = transform.GetChild(0).gameObject.GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        Reputaion(reputation);

        if (HP > MaxHp)//最大HPを超えたらHPは変わらない
        {
            HP = MaxHp;
        }

        if(HP <= 0)
        {
            HP = 0;

            this.enabled = false;

            if(ghostHand == true)
            {
                GameObject newkaihuku = Instantiate(firePrefab);
                newkaihuku.transform.position = new Vector3(this.gameObject.transform.position.x, this.gameObject.transform.position.y, 0f);
            }

            Destroy(Main);
        }
    }

    public void Reputaion(int x)
    {
        if (x < 10)
        {
            Popularity = "Worst";
        }
        if(10 <= x && x <30)
        {
            Popularity = "So Bad";
        }
        if(30 <= x && x < 40)
        {
            Popularity = "Bad";
        }
        if(40 <=x && x < 60)
        {
            Popularity = "Normal";
        }
        if (60 <= x && x < 70)
        {
            Popularity = "Good";
        }
        if(70 <= x && 90 < x)
        {
            Popularity = "So Good";
        }
        if(90 <= x)
        {
            Popularity = "Saint";
        }
    }

    private void OnTriggerEnter2D(Collider2D other)//ヒット時
    {
        if(other.gameObject.CompareTag("punch"))
        {
            if(state != STATE.NOMAL)//ダメージ中は何もしない
            {
                return;
            }

            gameObject.layer = LayerMask.NameToLayer("Damaged");
            state = STATE.DAMAGED;
            StartCoroutine(_hit());
        }
    }

    IEnumerator _hit()
    {
        Color color = sp.color;
        state = STATE.MUTEKI;
        for (int i = 0; i < loopCount; i++)
        {

            yield return new WaitForSeconds(flashInterval);

            sp.color = new Color(color.r, color.g,color.b, 0.0f);

            yield return new WaitForSeconds(flashInterval);
            sp.color = new Color(color.r, color.g, color.b, 1.0f);
        }
        state = STATE.NOMAL;
        sp.color = color;
        gameObject.layer = LayerMask.NameToLayer("CharaCol");
        sp.color = Color.white;
    }
}
