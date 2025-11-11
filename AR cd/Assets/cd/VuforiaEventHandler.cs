using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

public class VuforiaEventHandler : DefaultObserverEventHandler
{
    [Header("AR唱片控制器")]
    public ARRecordController recordController;

    protected override void OnTrackingFound()
    {
        base.OnTrackingFound();

        if (recordController != null)
        {
            recordController.OnTargetFound();
        }
    }

    protected override void OnTrackingLost()
    {
        base.OnTrackingLost();

        if (recordController != null)
        {
            recordController.OnTargetLost();
        }
    }
}