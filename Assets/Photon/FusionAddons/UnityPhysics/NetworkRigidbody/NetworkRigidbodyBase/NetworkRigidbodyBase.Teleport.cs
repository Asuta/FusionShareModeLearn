using System;
using UnityEngine;

namespace Fusion.UnityPhysics
{
  public partial class NetworkRigidbody<RBType, PhysicsSimType> {
    
    private (Vector3? position, Quaternion? rotation, bool moving) _deferredTeleport;
    
    /// <summary>
    /// Initiate a basic immediate teleport.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    public override void Teleport(Vector3? position = null, Quaternion? rotation = null) {
      if (position.HasValue) {
        _transform.position    = position.Value;
        RBPosition             = position.Value;
      }
      if (rotation.HasValue) {
        _transform.rotation    = rotation.Value;
        RBRotation             = rotation.Value;
      }
    }

    /// <summary>
    /// Initiate a moving teleport. If this method is called before <see cref="RunnerSimulatePhysics3D"/> or <see cref="RunnerSimulatePhysics2D"/>
    /// have simulated physics, then this teleport will be deferred until after physics is simulated for this rigidbody.
    /// This allows the results of the simulation to be captured before applying the teleport values.
    /// </summary>
    public override void MovingTeleport(Vector3? position = null, Quaternion? rotation = null) {
      if (Object.IsInSimulation == false) {
        return;
      }
      
      _deferredTeleport = (position, rotation, true);
      // for moving, be sure to apply AFTER simulation runs, we need to capture the sim results before teleporting.
      if (_physicsSimulator.HasSimulatedThisTick) {
        ApplyDeferredTeleport();        
      } else {
        _physicsSimulator.QueueAfterSimulationCallback(ApplyDeferredTeleport);
      }
    }

    private void ApplyDeferredTeleport() {
      bool moving = _deferredTeleport.moving;
      
      if (moving) {
        // For moving teleports this is happening after Physics.Simulate
        // So we can capture the results of the simulation before applying the teleport.
        Data.TeleportPosition = _transform.position;
        Data.TeleportRotation = _transform.rotation;
      } 

      if (_deferredTeleport.position.HasValue) {
        _transform.position    = _deferredTeleport.position.Value;
        RBPosition             = _deferredTeleport.position.Value;
        Data.TRSPData.Position = _deferredTeleport.position.Value;
      }
      if (_deferredTeleport.rotation.HasValue) {
        _transform.rotation    = _deferredTeleport.rotation.Value;
        RBRotation             = _deferredTeleport.rotation.Value;
        Data.TRSPData.Rotation = _deferredTeleport.rotation.Value;
      }
      IncrementTeleportKey(moving);
    }
    
    private void IncrementTeleportKey(bool moving) {
      // Keeping the key well under 1 byte in size 
      var key = Math.Abs(Data.TRSPData.TeleportKey) + 1;
      if (key > 30) {
        key = 1;
      }
      // Positive indicates non-moving teleport, Negative indicates moving teleport
      Data.TRSPData.TeleportKey = moving ? -key : key;
    }

  }
}