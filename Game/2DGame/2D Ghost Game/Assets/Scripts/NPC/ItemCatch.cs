using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemCatch : MonoBehaviour
{
    public 

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OntriggerStay2D(Collider2D other)
    {
        if (Input.GetButtonDown("Fire3"))
        {
            Debug.Log("Shift");
            if (!other.gameObject.GetComponent<SpringJoint2D>())
            {
                Debug.Log("Spring!");
                gameObject.AddComponent<SpringJoint2D>();
                other.gameObject.AddComponent<SpringJoint2D>();
            }
        }
        else if (Input.GetButtonUp("Fire3"))
        {
            Debug.Log("Bye");
            other.transform.SetParent(null);
        }
    }
}
