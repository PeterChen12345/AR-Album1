using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

public class CustomTrackableEventHandler : DefaultObserverEventHandler
{
    [Header("AR唱片控制器")]
    public ARMusicPlayerController musicController;

    protected override void OnTrackingFound()
    {
        base.OnTrackingFound();

        if (musicController != null)
        {
            musicController.OnTargetFound();
        }
    }

    protected override void OnTrackingLost()
    {
        base.OnTrackingLost();

        if (musicController != null)
        {
            musicController.OnTargetLost();
        }
    }
}
