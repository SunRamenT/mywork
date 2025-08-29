using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class CollisonDetector : MonoBehaviour
{

    [SerializeField] public TriggerEvent onTriggerStay = new TriggerEvent();

    public void OnTriggerStay2D(Collider2D other)
    {
        onTriggerStay.Invoke(other);
    }

    [Serializable]
    public class TriggerEvent : UnityEvent<Collider2D>
    {

    }
}
