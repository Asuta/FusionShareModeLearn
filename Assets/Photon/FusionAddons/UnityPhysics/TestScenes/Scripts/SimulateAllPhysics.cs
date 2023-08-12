using UnityEngine;

namespace Fusion
{
  /// <summary>
  /// Use this component on the NetworkRunner to automatically simulate physics using the FixedUpdate callback.
  /// Multi-Peer mode uses a separate physics scene for each NetworkRunner, which AutoSimulate does not include.
  /// </summary>
  [InlineHelp]
  [DefaultExecutionOrder(5000)]
  public class SimulateAllPhysics : SimulationBehaviour {
    private bool RunnerReady;
      
    public override void FixedUpdateNetwork() {
      RunnerReady = true;
    }

    void FixedUpdate() {
      if (RunnerReady == false) return;
        
      if (Physics.autoSimulation) {
        Runner.GetPhysicsScene().Simulate(Runner.DeltaTime);
      }
    }
  }
}