// #define NRB_DEBUGGING

using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Diagnostics;

namespace Fusion.UnityPhysics
{
  public abstract partial class NetworkRigidbody<RBType, PhysicsSimType> {

    // PhysX/Box2D abstractions

    protected abstract bool IsRigidbodyBelowSleepingThresholds(RBType rb);
    protected abstract bool IsStateBelowSleepingThresholds(NetworkRBData data);
      
    // NRB Render Logic
    
    public override void Render() {
      
      // Do not interpolate on Server (non-Host)
      if (_interpolationEnabled == false) {
        return;
      }

      // Do not interpolate if Object setting indicates not to.
      if (Object.RenderSource == RenderSource.Latest) {
        return;
      }
      
      var tr             = _transform;
      var it             = _interpolationTarget;
      var isInSimulation = Object.IsInSimulation;
      var useTarget      = !isInSimulation && _hasInterpolationTarget;

      if (TryGetSnapshotsBuffers(out var fr, out var to, out var alpha)) {
        
        var frData = fr.ReinterpretState<NetworkRBData>();
        var toData = to.ReinterpretState<NetworkRBData>();

        int frKey     = frData.TRSPData.TeleportKey;
        int toKey     = toData.TRSPData.TeleportKey;
        var syncScale = SyncScale;

        bool teleport = frKey != toKey;
        // Teleport Handling - Don't interpolate through non-moving teleports (indicated by positive key values).
        if (teleport && toKey >= 0) {
          toData = frData;
        }

        // Parenting specific handling
        
        if (SyncParent) {
          var currentParent = tr.parent;

          // If the parent is a non-null...
          if (frData.TRSPData.Parent.IsValid) {

            if (Runner.TryFindBehaviour(frData.TRSPData.Parent, out var found)) {
              var foundParent = found.transform;
              // Set the parent if it currently is not correct, before moving
              if (currentParent != foundParent) {
                tr.SetParent(foundParent);
                _rootIsDirtyFromInterpolation = true;

                // switching to moving by root while parented (and kinematic), set the interpolation target to origin
                // We most move by the root because all TRSP is in Local Space, and the interpolation target needs to be positioned
                // in World Space.
                if (it) {
                  it.SetLocalPositionAndRotation(default, default);
                  if (SyncScale) {
                    _interpolationTarget.localScale = new Vector3(1f, 1f, 1f);
                  }
                  _targIsDirtyFromInterpolation = false;
                }
              }

              // If the parent changes between From and To ... do no try to interpolate (different spaces)
              // We also are skipping sleep detection and teleport testing.
              if (frData.TRSPData.Parent != toData.TRSPData.Parent) {
                // When parented, ignore interpolation target and move the root in local (devs may want to change this behaviour themselves)
                
                // // RB should always be kinematic when parented.
                // if (GetRBIsKinematic(_rigidbody) == false) {
                //   SetRBIsKinematic(_rigidbody, true);
                // }
                tr.SetLocalPositionAndRotation(frData.TRSPData.Position, frData.TRSPData.Rotation);
                if (syncScale) {
                  tr.localScale = frData.TRSPData.Scale;
                }
                _rootIsDirtyFromInterpolation = true;
                return;
              }

              // If there is a parent, ignore the interpolation target.
              useTarget = false;

            } else {
              Debug.LogError($"Parent of this object is not present {frData.TRSPData.Parent} {frData.TRSPData.Parent.Behaviour}.");
              return;
            }           
            
          } else {
            // else the parent is null
            if (currentParent != null) {
              tr.SetParent(null);
              _rootIsDirtyFromInterpolation = true;
            }
            // If the parent changes between From and To ... do no try to interpolate (different spaces)
            if (frData.TRSPData.Parent != toData.TRSPData.Parent) {
              if (useTarget) {
                // There is no parent, so we can safely move the interp target in world space.
                // HOWEVER if players move the object in LateUpdate this will break of course.
                it.SetPositionAndRotation(frData.TRSPData.Position, frData.TRSPData.Rotation);
                
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (syncScale) {
                  Debug.LogWarning($"{GetType().Name} cannot sync scale when using an interpolation target.");
                }
#endif
                _targIsDirtyFromInterpolation = true;
              } else {
                tr.SetLocalPositionAndRotation(frData.TRSPData.Position, frData.TRSPData.Rotation);
                if (syncScale) {
                  tr.localScale = frData.TRSPData.Scale;
                }
                _rootIsDirtyFromInterpolation = true;
              }
              return;
            }
          }            
        }
        
        
        // if (useTarget == false) {
        //   // RB should always be kinematic when interpolating by the root.
        //   if (GetRBIsKinematic(_rigidbody) == false) {
        //     SetRBIsKinematic(_rigidbody, true);
        //   }
        // }

        // General Positon/Rotation Rendering
        
        Vector3    pos;
        Quaternion rot;

        if (teleport && toKey < 0) {
          // for moving teleports, lerp toward the Teleport values.
          pos = Vector3.Lerp(    frData.TRSPData.Position, toData.TeleportPosition, alpha);
          rot = Quaternion.Slerp(frData.TRSPData.Rotation, toData.TeleportRotation, alpha);
        } else {
          pos = Vector3.Lerp(    frData.TRSPData.Position, toData.TRSPData.Position, alpha);
          rot = Quaternion.Slerp(frData.TRSPData.Rotation, toData.TRSPData.Rotation, alpha);
        }

        // If we are using the interpolation target, just move the root in world space. No scaling (it is invalid).
        if (useTarget && Object.IsInSimulation == false) {
          it.SetPositionAndRotation(pos, rot);
          // SyncScale when using interpolation targets is always suspect, but we are allowing it here in case the dev has done things correctly.
          if (syncScale) {
            var scl = Vector3.Lerp(frData.TRSPData.Scale, toData.TRSPData.Scale, alpha);
            it.localScale = scl;
          }
          _targIsDirtyFromInterpolation = true;
        } 
        // else we are moving the transform itself and not the interp target.
        // Extra logic is needed to try an allow sleeping
        else {

          var scl = syncScale ? Vector3.Lerp(frData.TRSPData.Scale, toData.TRSPData.Scale, alpha) :  default;

          // Check thresholds to see if this object is coming to a rest, and stop interpolation to allow for sleep to occur.
          // Don't apply Pos/Rot/Scl if all of the indicated tests test below thresholds.
          if (!_hasInterpolationTarget && !_targIsDirtyFromInterpolation && UseRenderSleepThresholds) {
            var thresholds = RenderThresholds;
            if (
              (!thresholds.UseEnergy    || IsStateBelowSleepingThresholds(frData))                                 &&
              (thresholds.Position == 0 || (pos - tr.position).sqrMagnitude                 < thresholds.Position) && 
              (thresholds.Rotation == 0 || Quaternion.Angle(rot, tr.rotation)               < thresholds.Rotation) && 
              (thresholds.Scale    == 0 || !syncScale || (scl - tr.localScale).sqrMagnitude < thresholds.Scale)) {

              SetDebugSleepColor(true);
              return;
            }
          }

          SetDebugSleepColor(false);
          
          tr.SetLocalPositionAndRotation(pos, rot);
          if (syncScale) {
            tr.localScale = scl;
          }
          _rootIsDirtyFromInterpolation = true;
        }
        
      } else {
        Debug.LogWarning($"No interpolation data");
      }
    }

    
    // TODO: Remove these before release
    [Conditional("NRB_DEBUGGING")]
    private void SetDebugSleepColor(bool isSleeping) {
      if (isSleeping) {
        GetComponentInChildren<Renderer>().material.color = Color.magenta;
      } else {
        GetComponentInChildren<Renderer>().material.color = Color.white;
      }
    }
  }
}
