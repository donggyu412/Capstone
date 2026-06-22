using UnityEngine;

public class BrushTipSensor : MonoBehaviour
{
    public bool isTouchingCanvas = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "Canvas")
            isTouchingCanvas = true;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.gameObject.name == "Canvas")
            isTouchingCanvas = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == "Canvas")
            isTouchingCanvas = false;
    }
}