using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class RotateObjects : MonoBehaviour
{
    
    public static float deltaRotation_ = 0;
    private float deltaRotation = 0;
	public float RotationX = 0;  


    // Update is called once per frame
    void FixedUpdate()
    {

        //the rotation speed of the object is the same on different devices
        deltaRotation = deltaRotation + 0.2f;
        transform.rotation = Quaternion.Euler(RotationX, deltaRotation, 0f);   //rotate object 0 : 360
        deltaRotation_ = deltaRotation;


    }


}
