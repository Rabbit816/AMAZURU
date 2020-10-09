﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlashTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        late = 0;
    }

    float late = 0;
    [SerializeField]
    Material material;
    // Update is called once per frame
    void Update()
    {
        late = (late + Time.deltaTime > 1) ? 1 : late + Time.deltaTime;
        material.SetFloat("_Table", late);
        if(Input.GetKeyDown(KeyCode.A))
        {
            late = 0;
        }
    }
}