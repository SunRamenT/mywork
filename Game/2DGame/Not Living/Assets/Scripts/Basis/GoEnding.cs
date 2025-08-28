using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoEnding : MonoBehaviour
{
    public string sceneNamegood;//善行エンド
    public string sceneNameevil;//悪行エンド
    public string sceneNamenorm;//普通エンド
    public string sceneNameTrueNorm;//普通に長生きエンド
    public string sceneNameSoGood;//とても良い子エンド
    public string sceneNameSoBad;//とても悪い子エンド

    public GameObject Ghost;

    public TimeControler time;
    private PlayerController plycnt;
    private GRManager GRM;//幽霊の内部データ管理
    // Start is called before the first frame update
    void Start()
    {
        plycnt = Ghost.GetComponent<PlayerController>();
        GRM = Ghost.GetComponent<GRManager>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Load()
    {
        if (GRM.goodper > 70 && GRM.Good >= 60)//goodは善行値、perは悪行値とあわせた割合
        {
            SceneManager.LoadScene(sceneNamegood);
        }
        else if(GRM.goodper < 30 && GRM.Evil > 60)//Evilは悪行値
        {
            SceneManager.LoadScene(sceneNameevil);
        }
        else if(30 <= GRM.goodper && GRM.goodper <= 70)
        {
            SceneManager.LoadScene(sceneNamenorm);
        }
        if (GRM.goodper > 70 && GRM.Good >= 60 && time.day1 > 7)
        {
            SceneManager.LoadScene(sceneNameSoGood);
        }
        else if (GRM.goodper < 30 && GRM.Evil > 60 && time.day1 > 7)
        {
            SceneManager.LoadScene(sceneNameSoBad);
        }
        else if (30 <= GRM.goodper && GRM.goodper <= 70 && time.day1 > 7)
        {
            SceneManager.LoadScene(sceneNameTrueNorm);
        }


    }
}
