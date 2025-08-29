using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeControler : MonoBehaviour
{
    public float times;
    public float gameTime;
    public float displayTime;
    public GameObject yurei;
    public float reikongage;
    public bool isTimeover = false;
    public int hour1 = 0;
    public int hour2 = 0;
    public int day = 0;
    public int day1 = 1;
    public int day2 = 0;
    public int min1 = 0;
    public int time = 0;

    // Start is called before the first frame update
    void Start()
    {
        reikongage = yurei.GetComponent<ReikonManagger>().minus;
        displayTime = gameTime;
        InvokeRepeating(nameof(CountMethod),1f,1f);
    }

    // Update is called once per frame
    void Update()
    {
        if(isTimeover == false)
        {
            times += Time.deltaTime;
            displayTime = times;
            reikongage = yurei.GetComponent<ReikonManagger>().reikon;
        }
        else
        {
            displayTime = times;
            isTimeover = true;
        }
        
    }

    void CountMethod()
    {
        time++;
        //day
        if (time % 48 == 0)
        {
            day1++;
            if(day1 > 9)
            {
                day1 = 0;
                day2++;
            }
        }
        //hour
        if (time % 2 == 0)
        {
            hour1++;
            min1 = 0;
            if(hour2 == 2)
            {
                if(hour1 > 3)
                {
                    hour2 = 0;
                    hour1 = 0;
                }
            }
            else if (hour1 > 9)
            {
                hour2++;
                hour1 = 0;
            }
            return;

        }
        
        //min
        min1 = 3;
    }
}
