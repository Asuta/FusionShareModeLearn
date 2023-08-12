using Fusion;
using Fusion.UnityPhysics;
using UnityEngine;

public class NRB_HandMover : NetworkBehaviour {
  
  public float   Rate        = 1f;
  public Vector3 positionMin = new Vector3(.5f, 0, 0); 
  public Vector3 positionMax = new Vector3(1.5f, 0, 0);

  public float scaleMin = 0.6f;
  public float scaleMax = 2.0f;

  [Networked] public float Phase { get; set; }
  
  public override void FixedUpdateNetwork() {

    if (GetInput<NRB_NetworkInput>(out var input)) {
      if (input.Buttons.IsSet(NRB_NetworkInput.BUTTON_ACTION1)) {
        Phase += Runner.DeltaTime * Rate;
        if (Phase > 1) Phase = 1;
      } else if (input.Buttons.IsSet(NRB_NetworkInput.BUTTON_ACTION2)){
        Phase -= Runner.DeltaTime * Rate;
        if (Phase < 0) Phase = 0;
      }
      transform.localPosition = Vector3.Lerp(positionMin, positionMax, Phase);
      var scale = Mathf.Lerp(scaleMin, scaleMax, Phase);
      transform.localScale = new Vector3(scale, scale, scale);
    }
  }
}
