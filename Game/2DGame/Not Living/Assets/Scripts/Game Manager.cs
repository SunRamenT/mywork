using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
       
    public GameObject GameOverpanel;
    public GameObject GameClearpanel;
    public GameObject restart;
    public GameObject minText;
    public GameObject hour1Text;
    public GameObject hour2Text;
    public GameObject day1Text;
    public GameObject day2Text;
    public GameObject reikon;
    TimeControler timeCnt;
    public GameObject targetName;
    public GameObject targetRep;
    
    public GameObject GBSlider;
    Slider GESlider;

    public GameObject ReiSlider;
    Slider ReikonSlider;

    public GameObject HPSlider;
    Slider HPslider;

    GRManager GRM;
    GameObject player;
    PlayerController playcnt;

    // Start is called before the first frame update
    void Start()
    {
        GameOverpanel.SetActive(false);//パネル非表示
        GameClearpanel.SetActive(false);
        timeCnt = GetComponent<TimeControler>();
   
        player = GameObject.FindGameObjectWithTag("Player");
        GRM = player.GetComponent<GRManager>();
        playcnt = player.GetComponent<PlayerController>();


        GESlider = GBSlider.GetComponent<Slider>();
        ReikonSlider = ReiSlider.GetComponent<Slider>();

        float Gmax = 100f;
        GESlider.maxValue = Gmax;
        int goodsper = GRM.goodper;
        GESlider.value = goodsper;

        float Reimax = 100f;
        ReikonSlider.maxValue = Reimax;

        HPslider = HPSlider.GetComponent<Slider>();
        HPslider.maxValue = 0f;
    }

    // Update is called once per frame
    void Update()
    {
        if(PlayerController.gameState == "gameover")
        {
            GameOverpanel.SetActive(true);
            if (timeCnt != null)
            {
                timeCnt.isTimeover = true;//カウント停止
            }

        }
        else if(PlayerController.gameState == "playing")
        {            
            GameObject targetobj = player.GetComponent<PlayerController>().target;
            //タイムをこうしんする 
            if (timeCnt != null)
            {           
                int time = (int)timeCnt.displayTime;
                
                int reitime = (int)timeCnt.reikongage;

                int Goods = GRM.Good;
                int Evils = GRM.Evil;

                int hour1 = timeCnt.hour1;
                int hour2 = timeCnt.hour2;
                int min = timeCnt.min1;
                int day1 = timeCnt.day1;
                int day2 = timeCnt.day2;

                if(day1 > 7)
                {
                    GameClearpanel.SetActive(true);
                }

                int goodsper = GRM.goodper;
                GESlider.value = goodsper;
                ReikonSlider.value = reitime;
                if(playcnt != null && targetobj != null)//何もないときにアクセスしないように
                {
                    if (playcnt.rock == false)//State yurei
                    {
                        string rep = "??";
                        string tarName = player.name;
                        targetRep.GetComponent<Text>().text = rep.ToString();
                        targetName.GetComponent<Text>().text = tarName.ToString();
                        HPslider.maxValue = 0f;
                        HPslider.value = 0f;
                    }
                    else if (playcnt.rock == true)// State RockOn
                    {
                        StatusManager status = targetobj.GetComponent<StatusManager>();
                        string rep = status.Popularity;
                        string tarName = targetobj.tag;
                        targetRep.GetComponent<Text>().text = rep.ToString();
                        targetName.GetComponent<Text>().text = tarName.ToString();
                        HPslider.maxValue = (float)(status.MaxHp);
                        HPslider.value = (float)(status.HP);
                    }
                }

                hour1Text.GetComponent<Text>().text = hour1.ToString();
                hour2Text.GetComponent<Text>().text = hour2.ToString();
                minText.GetComponent<Text>().text = min.ToString();
                day1Text.GetComponent<Text>().text = day1.ToString();
                day2Text.GetComponent<Text>().text = day2.ToString();
                reikon.GetComponent<Text>().text = reitime.ToString();
            }
        }
    }
}
