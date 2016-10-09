using UnityEngine;
using System.Collections;

public class KeepScreenOn : MonoBehaviour
{
    void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}
