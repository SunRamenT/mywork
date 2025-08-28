using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReikonManagger : MonoBehaviour
{
    public Vector3 Scale;
    public GameObject plyobj;
    public float reikon = 30;
    public float minus;//霊魂値減少幅
    public float speed = 1f;
    public float plus = 0;//霊魂値増加幅
    public float firesize;//霊魂の大きさ
    public float defreikon;//変か前の霊魂値
    // Start is called before the first frame update
    void Start()
    {
        Scale = transform.localScale;
        plyobj = transform.root.gameObject;
        firesize = Scale.x / 100f; //ReikonSizePercent
        defreikon = reikon;
    }

    // Update is called once per frame
    void Update()
    {
        if (reikon > 0)
        {
            if (reikon > 100)
            {
                reikon = 100;
            }
            reikon -= Time.deltaTime * speed;
            minus = defreikon - reikon;
            Scale = new Vector3(Scale.x - firesize * Time.deltaTime + firesize * plus, Scale.y - firesize * Time.deltaTime + firesize * plus, 0f);//霊魂の大きさ変更
            transform.localScale = Scale;
            plus = 0;
        }
        else
        {
            PlayerController.gameState = "gameover";
            return;
        }
    }
}
