using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[ExecuteInEditMode]
public class WindowSwitcher : MonoBehaviour
{
    public enum Style { Circular, Flat, Triangular };

    public Style myStyle;

    
    public GameObject[] Top;
    public GameObject[] Mid;
    public GameObject[] Bot;

    public bool RandomizeWindow;
    public GameObject Glass;
    public Material[] GlassMaterial;

    private void OnValidate()
    {
        ApplyStyle(myStyle);
    }

    private void Start()
    {
        ApplyStyle(myStyle);
    }

    void ApplyStyle(Style selectedStyle)
    {
        for (int i = 0; i < Top.Length; i++)
        {
            if (i == (int)selectedStyle)
            {
                Top[i].SetActive(true);
            }
            else
            {
                Top[i].SetActive(false);
            }
        }

        //MID LOGIC
        for (int i = 0; i < Mid.Length; i++)
        {
            if (i == (int)selectedStyle)
            {
                Mid[i].SetActive(true);
            }
            else
            {
                Mid[i].SetActive(false);
            }
        }

        //BOT LOGIC
        for (int i = 0; i < Bot.Length; i++)
        {
            if (i == (int)selectedStyle)
            {
                Bot[i].SetActive(true);
            }
            else
            {
                Bot[i].SetActive(false);
            }
        }
    }

    // This method is for demonstration and can be called from other scripts or events
    public void SetStyle(Style newStyle)
    {
        myStyle = newStyle;
        ApplyStyle(myStyle);
    }

    void Update()
    {
        if (RandomizeWindow == true)
        {
            Glass.GetComponent<MeshRenderer>().material = GlassMaterial[Random.Range(0,3)];
        }

        Random.seed = Random.Range(10, 250);
    }


}
     





