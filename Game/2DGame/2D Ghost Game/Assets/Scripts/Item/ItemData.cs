using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemData : MonoBehaviour
{

    public bool catched = false;
    public int value = 10;
    public int data = 0;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void OnTriggerEnter2D(Collider2D col)
    {
        if (catched == false && col.CompareTag("Tile"))
        {
            Destroy(gameObject);
        }
    }
}
