
using UnityEngine;

namespace Fusion.UnityPhysics {

  public class NRB_Grabbable :  NetworkBehaviour, IStateAuthorityChanged {
    
    private float TestLastAuthorityRequestTime;
    
    [SerializeField] 
    public bool PredictProxies;
    
    [SerializeField] 
    public float CooldownPeriod = 1f;
    
    [Networked] 
    public TickTimer PickupCooldown { get; set; }

    private Rigidbody _rigidbody;
    public  Rigidbody Rigidbody => _rigidbody;

    public NetworkBehaviour CurrentParent { get; set; }
    
    private void Awake() {
      TryGetComponent(out _rigidbody);
    }
    
    // // TEST
    // private void OnCollisionEnter(Collision other) {
    //   if (other.rigidbody && other.rigidbody.GetComponent<NetworkRigidbody3D>().Object.HasInputAuthority) {
    //     Debug.LogWarning($"{Runner.Tick} {name} {other.rigidbody.name} "                                                +
    //                      $"rboff:{(other.rigidbody.position           - _rigidbody.position).magnitude} " +
    //                      $"troff:{(other.rigidbody.transform.position - transform.position).magnitude} "  +
    //                      $"RBX:{_rigidbody.position.x} trX:{_rigidbody.transform.position.x}");
    //   }
    // }

    public override void Spawned() {
      if (PredictProxies && IsProxy && Runner.Topology == Topologies.ClientServer) {
        Runner.SetIsSimulated(Object, true);
        Object.RenderTimeframe = RenderTimeframe.Local;
        Object.RenderSource    = RenderSource.Interpolated;
      }
      // Object.AssignInputAuthority(PlayerRef.None);
    }

    public bool TryGrab(NRB_Grabber grabber) {
      if (!PickupCooldown.ExpiredOrNotRunning(Runner)) {
        return false;
      }

      if (transform.parent == grabber.transform) {
        return false;
      }

      if (_rigidbody) {
        _rigidbody.isKinematic = true;
        transform.SetParent(grabber.transform); 
      }

      PickupCooldown = TickTimer.CreateFromSeconds(Runner, CooldownPeriod);     
          
      if (Runner.Config.Simulation.Topology == Topologies.Shared && grabber.HasStateAuthority) {
        // Shared Mode handling of grabbing
        
        // TEST - Prevent Request spam
        if (Time.time < TestLastAuthorityRequestTime + 1f) return false;
        TestLastAuthorityRequestTime = Time.time;
        
        // // TEST - removing parenting for the equation for now
        // transform.SetParent(grabber.transform);
        
        if (Object.HasStateAuthority == false) {
          Debug.LogWarning($"{Runner.LocalPlayer} {Runner.Tick} Requesting Authority");
          Object.RequestStateAuthority();          
        }

      } else {
        // Server/Host Mode handling of grabbing
        if (_rigidbody) {
          transform.SetParent(grabber.transform);
        }
      }
      return true;
    }

    public void StateAuthorityChanged() {
      Debug.LogWarning($"{Object.StateAuthority} Got Auth, need to do some stuff here still.");
    }
  }

}
