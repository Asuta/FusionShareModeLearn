
using Fusion.UnityPhysics;
using UnityEngine;

namespace Fusion.UnityPhysics {
  [DefaultExecutionOrder(6000)]
  public class NRB_Grabber : NetworkBehaviour {

    
    public Vector3 ThrowVelocity = new Vector3(0f, 1f, 3f);
    public Vector3 ThrowOffset = default;

    public override void Spawned() {
      // Runner.GetComponent<RunnerSimulatePhysics3D>().OnAfterSimulate += TestForGrabbables;
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
      // Runner.GetComponent<RunnerSimulatePhysics3D>().OnAfterSimulate -= TestForGrabbables;
    }

    public override void FixedUpdateNetwork() {
      if (GetInput<NRB_NetworkInput>(out var input)) {

        if (input.IsDown(NRB_NetworkInput.BUTTON_RELOAD)) {

          var held = GetComponentInChildren<NRB_Grabbable>();
          if (held && held.PickupCooldown.ExpiredOrNotRunning(Runner)) {
            var heldTransform = held.transform;
            var heldRigidbody = held.GetComponent<Rigidbody>();
            
            held.transform.SetParent(null);
            if (heldRigidbody) {
              heldRigidbody.position = heldTransform.position += transform.rotation * ThrowOffset;
              heldRigidbody.isKinematic = false;
              heldRigidbody.velocity     = transform.rotation * ThrowVelocity;              
            }

            held.PickupCooldown = TickTimer.CreateFromSeconds(Runner, held.CooldownPeriod);
          }
        }
      }
      
      TestForGrabbables();
    }

    private Collider[] _reusableColliders = new Collider[4];
    private void TestForGrabbables() {
      var hits = Runner.GetPhysicsScene().OverlapSphere(transform.position, transform.localScale.x *.5f, _reusableColliders, int.MaxValue, QueryTriggerInteraction.UseGlobal);
      if (hits > 0) {
        for (int i = 0; i < hits; i++) {
          var hit = _reusableColliders[i];
          if (hit.TryGetComponent<NRB_Grabbable>(out var grabbable)) {
            grabbable.TryGrab(this);
          }
        }
      }
    }
  }
  
}
