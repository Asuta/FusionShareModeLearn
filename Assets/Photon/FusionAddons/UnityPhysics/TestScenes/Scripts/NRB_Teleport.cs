using Fusion;
using Fusion.UnityPhysics;
using UnityEngine;

// [DefaultExecutionOrder(2000)]
public class NRB_Teleport : NetworkBehaviour {

  [Networked]
  public TickTimer Cooldown { get; set; }
  
  public bool      MovingTeleport = true;
  
  public Vector3 TeleportOffset = new Vector3(0f, 1f, 2f);
  
  private NetworkRigidbodyBase _nrb;

  private void Awake() {
    _nrb = GetComponent<NetworkRigidbodyBase>();
  }

  public override void FixedUpdateNetwork() {
    if (GetInput(out NRB_NetworkInput input)) {
      if (input.IsDown(NRB_NetworkInput.BUTTON_ACTION5)) {
        if (Cooldown.ExpiredOrNotRunning(Runner)) {
          Cooldown = TickTimer.CreateFromSeconds(Runner, 1f);
          if (MovingTeleport) {
            _nrb.MovingTeleport(_nrb.RBPosition + _nrb.RBRotation * TeleportOffset);
          } else {
            _nrb.Teleport(_nrb.RBPosition + _nrb.RBRotation * TeleportOffset);
          }          
        }
      }
    }
  }
}
