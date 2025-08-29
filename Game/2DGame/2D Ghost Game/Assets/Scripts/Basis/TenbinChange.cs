using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TenbinChange : MonoBehaviour
{
    private Image image;
    public GRManager grm;//—H—ì‚Ì“à•”ƒf[ƒ^ŠÇ—

    public Sprite soleft;//¶ŒX‚«‹}
    public Sprite left;//¶ŒX‚«
    public Sprite center;//…•½
    public Sprite right;//‰EŒX‚«
    public Sprite soright;//‰EŒX‚«‹}

    // Start is called before the first frame update
    void Start()
    {
        image = GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {
        if (grm.goodper < 20)
        {
            image.sprite = soright;
        }
        else if(20 <= grm.goodper && grm.goodper < 50)
        {
            image.sprite = right;
        }
        else if(grm.goodper == 50)
        {
            image.sprite = center;
        }
        else if(50 < grm.goodper && grm.goodper <= 80)
        {
            image.sprite = left;
        }
        else if(80 < grm.goodper)
        {
            image.sprite = soleft;
        }
    }
}
