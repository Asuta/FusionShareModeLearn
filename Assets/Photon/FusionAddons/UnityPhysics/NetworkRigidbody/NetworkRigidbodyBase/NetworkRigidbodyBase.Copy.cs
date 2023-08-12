using UnityEngine;

namespace Fusion.UnityPhysics
{
  public abstract partial class NetworkRigidbodyBase {
    public abstract Vector3    RBPosition    { get; set; }
    public abstract Quaternion RBRotation    { get; set; }
    public abstract bool       RBIsKinematic { get; set; }
  }

  public abstract partial class NetworkRigidbody<RBType, PhysicsSimType> : IBeforeAllTicks, IAfterTick, IAfterAllTicks {

    // PhysX/Box2D abstractions
    
    protected abstract void ApplyRBPositionRotation(RBType rb, Vector3 pos, Quaternion rot);
    
    protected abstract void CaptureRBPositionRotation(RBType rb, ref NetworkRBData data);
    protected abstract void CaptureExtras(RBType             rb, ref NetworkRBData data);
    protected abstract void ApplyExtras(RBType               rb, ref NetworkRBData data);

    protected abstract NetworkRigidbodyFlags GetRBFlags(RBType rb);
    
    protected abstract bool GetRBIsKinematic(RBType rb);
    protected abstract void SetRBIsKinematic(RBType rb, bool kinematic);
    
    protected abstract int  GetRBConstraints(RBType rb);
    protected abstract void SetRBConstraints(RBType rb, int constraints);
    
    protected abstract bool IsRBSleeping(RBType rb);
    protected abstract void ForceRBSleep(RBType rb);
    protected abstract void ForceRBWake( RBType rb);
    
    // Main NRB logic
    
    void IBeforeAllTicks.BeforeAllTicks(bool resimulation, int tickCount) {
      
      // Recenter the interpolation target. TODO: Can get more selective/efficient with this later
      if (_targIsDirtyFromInterpolation) {
        _interpolationTarget.SetLocalPositionAndRotation(default, default);
        if (SyncScale) {
          _interpolationTarget.localScale = new Vector3(1f, 1f, 1f);
        }
      }
    
      // A dirty root should always reset, going into simulation (for both state authority and predicted)
      // Predicted objects should always reset at the start of re-simulation - in all cases.
      if (_rootIsDirtyFromInterpolation || (_clientPrediction && resimulation)) {
        CopyToEngine(resimulation);
      }
    }

    // Copy values every tick on predicted objects, so snapshots can be captured for interpolation
    // TODO: This can likely be more selective about when it does this, currently pretty brute force
    public void AfterTick() {
      if (!HasStateAuthority && Object.IsInSimulation /*&& !(Runner.IsResimulation && Runner.IsFirstTick)*/) {
        CopyToBuffer(true);
      }
    }

    // TODO: I am not sure HOW this is working. I would expect this to leave some gaps in re-simulated snapshots, but it seems to not be a problem.
    // Likely because Forward Tick Count rarely exceeds 1, and this will only be a problem if an Update processes multiple forward ticks on the prediction client.
    // This will probably only ever produce noticeable issues when missed predictions occur.
    // Also very possible that even capturing the state of NRB every tick would make no difference, as the Snapshots use for interpolation may not even be captured for those ticks.
    void IAfterAllTicks.AfterAllTicks(bool resimulation, int tickCount) {
      
      // resimulation bool is redundant here, but is more efficient than HasStateAuthority, so using that first test to avoid the second when possible.
      if (resimulation == false && HasStateAuthority) {
        CopyToBuffer(false);
      }
    }

    /// <summary>
    /// Copies the state of the Rigidbody to the Fusion state.
    /// </summary>
    /// <param name="predicted">Capturing predicted changes is only used for interpolation, so we only need the TRSP info and no extras (vel/angVel)</param>
    private void CopyToBuffer(bool predicted) {
      
      var tr    = _transform;
      var rb    = _rigidbody;
      var flags = GetRBFlags(rb);

      // Capture RB State
      if ((flags & NetworkRigidbodyFlags.IsKinematic) != 0) {
        Data.TRSPData.Position = tr.localPosition;
        Data.TRSPData.Rotation = tr.localRotation;
      } else {
        CaptureRBPositionRotation(rb, ref Data);
        
        // We don't need to store/network any physics info if there is no client prediction...
        // UNLESS we need to rewind the root transform because it is being used for interpolation.
        // then it is needed for the StateAuthority in OnBeforeAllTicks after Render.
        if (!predicted && (_clientPrediction || (_hasInterpolationTarget == false && Object.HasStateAuthority))) {
          CaptureExtras(rb, ref Data);
        }
      }

      if (SyncScale) {
        Data.TRSPData.Scale = tr.localScale;
      }

      // Capture Parenting and handle auto AOI override
      if (IsMainTRSP && SyncParent) {
        
        // We automatically are any parent NOs as the AOIOverride, which means that if AOI is enabled,
        // player interest in this object will be determined by player interest in the parent NO.
        // This is an option on NT, but hard coded AutoAOIOverride for NRB here.
        var  parent   = tr.parent;
        bool hasParent = parent;
        if (hasParent && _aoiEnabled) {
          ResolveAOIOverride(this, parent);
        } else {
          SetAreaOfInterestOverride(default);
        }
        
        // Detect if direct parent is a valid "mount" (must be valid NetworkBehaviour)
        if (hasParent && parent.TryGetComponent<NetworkBehaviour>(out var parentNB)) {
          Data.TRSPData.Parent = parentNB;
        }  else  {
          Data.TRSPData.Parent = default;
        }
      } else {
        SetAreaOfInterestOverride(default);
      }
      

      // When sleeping, the uncompressed value is used.
      if ((flags & NetworkRigidbodyFlags.IsSleeping) != 0) {
        Data.FullPrecisionPosition = tr.localPosition;
        Data.FullPrecisionRotation = tr.localRotation;
      }

      Data.Flags = (flags, GetRBConstraints(rb));
    }
    
    
    /// <summary>
    /// Copies the Fusion snapshot state onto the Rigidbody.
    /// </summary>
    /// <param name="predictionReset">Indicates if this is a reset from a remote server state, in which case everything needs to be reverted to state
    /// Otherwise, if it is for the State Authority or a non-simulated proxy - only the TRSP needs to be reset from interpolation changes. (not velocity etc)</param>
    private void CopyToEngine(bool predictionReset) {
      
      var (flags, constraints) = Data.Flags;
      var tr = _transform;
      var rb = _rigidbody;
      
      Vector3    pos;
      Quaternion rot;
      bool       isParented = false;
      
      // For non-kinematic states, test for sleep conditions - otherwise just push the local state right to the transform for kinematic.
      if (SyncParent) {
        var currentParent = tr.parent;
        if (Data.TRSPData.Parent.IsValid) {
          if (Runner.TryFindBehaviour(Data.TRSPData.Parent, out var found)) {
            var foundTransform = found.transform;
            if (foundTransform != currentParent) {
              tr.SetParent(foundTransform);    
              if (_hasInterpolationTarget) {
                _interpolationTarget.SetLocalPositionAndRotation(default, default);
              }
            }
          } else {
            Debug.LogError($"Cannot find NetworkBehaviour.");
            OnParentNotFound();
          }
          isParented = true;
    
        } else {
          if (currentParent) {
            tr.SetParent(null); 
          }
        }
      }
      
      var networkedIsSleeping  = (flags & NetworkRigidbodyFlags.IsSleeping)  != 0;
      var networkedIsKinematic = (flags & NetworkRigidbodyFlags.IsKinematic) != 0;
      // We need to analyze the current sleeping conditions before restoring velocities and other fields.
      var currentIsSleeping = IsRBSleeping(rb);

      
      // If the State Authority is asleep, it will have valid uncompressed pos/rot values.
      if (networkedIsSleeping) {
        pos = Data.FullPrecisionPosition;
        rot = Data.FullPrecisionRotation;
      } else {
        pos = Data.TRSPData.Position;
        rot = Data.TRSPData.Rotation;
      }
            
      // Both local and networked state are sleeping and in agreement - avoid waking the RB locally.
      // This test of position and rotation can possibly be removed by developers without consequence for many use cases.
      bool avoidWaking = !_rootIsDirtyFromInterpolation && currentIsSleeping && networkedIsSleeping && tr.localPosition == pos && tr.localRotation == rot;
      
      if (networkedIsKinematic != GetRBIsKinematic(rb)) {
        SetRBIsKinematic(rb, networkedIsKinematic);;
      }

      // Apply position and rotation
      if (avoidWaking == false) {
        tr.SetLocalPositionAndRotation(pos, rot);
        // Don't push world space onto RB if nested (values are local space)
        if (isParented == false) {
          ApplyRBPositionRotation(rb, pos, rot);
        }
        _rootIsDirtyFromInterpolation = false;
      }

      if (SyncScale) {
        tr.localScale = Data.TRSPData.Scale;
      }
    
      // Only apply extras and test for sleep handling for prediction resimulations
      // Not when just undoing interpolation TRSP changes.
      if (predictionReset && networkedIsKinematic == false) {

        ApplyExtras(rb, ref Data);
        SetRBConstraints(rb, constraints);

        // Local state is already in agreement with network, can skip sleep handling
        if (avoidWaking) {
          return;
        }
        
        // If sleeping states disagree, we need to intervene.
        if (currentIsSleeping != networkedIsSleeping) {
          if (networkedIsSleeping == false) {
            ForceRBWake(rb);
          } else if (IsRigidbodyBelowSleepingThresholds(rb)) {
            // Devs may want to comment this out, if their physics sim experiences hitching when waking objects with collisions.
            // This is here to make resting states 100% accurate, but ForceSleep can cause a hitch in re-simulations under very
            // specific conditions.
            ForceRBSleep(rb);
          }          
        }
      }
    }

  }
}
