using UnityEngine;

namespace Fusion.UnityPhysics {
  

  public abstract partial class NetworkRigidbody<RBType, PhysicsSimType> : NetworkRigidbodyBase,/* IStateAuthorityChanged,*/ ISimulationExit
    where RBType          : Component
    where PhysicsSimType  : RunnerSimulatePhysicsBase 
  {
    
    // Cached
    protected RBType         _rigidbody;
    protected PhysicsSimType _physicsSimulator;
    
    protected virtual void Awake() {
      TryGetComponent(out _transform);
      TryGetComponent(out _rigidbody);
    }

    void ISimulationExit.SimulationExit() {
      // RBs removed from simulation will stop getting Copy calls, and will only be running Render
      // So we need to set them as kinematic here (to avoid relentless checks in Render)
      SetRBIsKinematic(_rigidbody, true);
    }
    
    public override void Spawned() {
      base.Spawned();
      
      if (IsProxy) {
         SetRBIsKinematic(_rigidbody, true);
      }
      
      EnsureHasRunnerSimulatePhysics();
      _clientPrediction = (_physicsSimulator.SimulateStages & Stages.ForwardOnly) != 0;
      
      if (HasStateAuthority) {
        CopyToBuffer(false);
      } else {
        // Mark the root as dirty to force CopyToEngine to update the transform.
        _rootIsDirtyFromInterpolation = true;
        CopyToEngine(true);
        // This has to be here after CopyToEngine, or it will set Kinematic right back.
        if (Object.IsInSimulation == false) {
          SetRBIsKinematic(_rigidbody, true);
        }
      }
    }
            
    //  public virtual void StateAuthorityChanged() {
    //   Debug.LogError($"Auth Change {Runner.LocalPlayer} {name} {HasStateAuthority} {HasInputAuthority}");
    //
    //   if (Object.IsProxy) {
    //     SetRBIsKinematic(_rigidbody, true);
    //   }
    // }
    
    private void EnsureHasRunnerSimulatePhysics() {
      if (_physicsSimulator) {
        return;
      }
      
      if (Runner.TryGetComponent(out PhysicsSimType existing)) {
        _physicsSimulator = existing;
        return ;
      }
      Debug.LogError($"No {typeof(PhysicsSimType).Name} present on NetworkRunner, but is required by {GetType().Name} on gameObject '{name}'. Adding one using default settings.");
      _physicsSimulator = Runner.gameObject.AddComponent<PhysicsSimType>();
      Runner.AddGlobal(_physicsSimulator);
    }
    
    /// <summary>
    /// Developers can override this method to add handling for parent not existing locally.
    /// </summary>
    protected virtual void OnParentNotFound() {
      Debug.LogError($"Parent does not exist locally");
    }
  }
}
