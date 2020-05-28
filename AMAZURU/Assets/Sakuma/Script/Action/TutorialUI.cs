﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// チュートリアルUI作成用のクラス
/// </summary>
public class TutorialUI : MonoBehaviour
{
    [SerializeField]
    GameObject TutorialObj;
    //チュートリアル画像の親のオブジェ
    [SerializeField]
    GameObject UIParent;
    //フェードする画像
    [SerializeField]
    Image FadeCan;
    //チュートリアルのフラグ
    bool TutorialFlg = false;

    int num = 0;
    GameObject[] UIObjs;
    void Start()
    {
    }

    void Update()
    {
        if (TutorialFlg)
        {
            for(int i=0;i< UIObjs.Length; i++)
            {
                UIObjs[i].SetActive(num==i);
            }


            if (Input.GetButtonDown("Circle"))
            {
                num++;
                if(num== UIObjs.Length)
                {
                    TutorialFlg = false;
                    UIParent.SetActive(false);
                    StartCoroutine("FadeOut");
                }
            }
        }
    }

    public void TutorialStart()
    {
        num = 0;
        TutorialObj.SetActive (true);
        FadeCan.color = new Color(0,0,0,0);
        UIObjs = new GameObject[StageMake.LoadStageData.TutorialObjs.Length];
        for (int i=0;i<StageMake.LoadStageData .TutorialObjs.Length; i++)
        {
            UIObjs[i]=Instantiate(StageMake.LoadStageData.TutorialObjs[i],UIParent .transform );
            UIObjs[i].SetActive(false);
        }
        UIParent.SetActive(false);
        StartCoroutine("FadeIn");
    }
    IEnumerator FadeIn()
    {
        float time = 0;
        while (SceneLoadManager.Instance.SceneLoadFlg)
        {
            yield return null;
        }
        while (time < 1)
        {
            FadeCan.color = new Color(0, 0, 0, 0.5f*time);
            time += Time.deltaTime;
            yield return null;
        }
        FadeCan.color = new Color(0, 0, 0, 0.5f);
        TutorialFlg = true;
        UIParent.SetActive(true );
    }
    IEnumerator FadeOut()
    {
        float time = 0;
        while (time < 1)
        {
            FadeCan.color = new Color(0, 0, 0, 0.5f * (1-time));
            time += Time.deltaTime;
            yield return null;
        }
        FadeCan.color = new Color(0, 0, 0, 0);
        TutorialObj.SetActive(false);
        PlayState.playState.Tutorial = false;
    }
}
