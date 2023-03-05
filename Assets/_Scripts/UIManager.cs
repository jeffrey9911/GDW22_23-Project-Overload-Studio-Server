using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    public static TMP_InputField _CONSOLE;
    public static Scrollbar _CONSCROLL;

    private void Awake()
    {
        if (!instance)
        {
            instance = this;
        }

        _CONSOLE = GameObject.Find("INF_Console").GetComponent<TMP_InputField>();
        _CONSCROLL = _CONSOLE.transform.Find("Scrollbar").GetComponent<Scrollbar>();
    }

    public void ConPrint(string cont)
    {
        _CONSOLE.text += "\n" + cont;

        _CONSCROLL.value = 1f;
    }
}
