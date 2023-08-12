using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.UnityPhysics {
  
  public enum Stages {
    ForwardOnly      = 1,
    ForwardAndResims = 3,
    UnityFixedUpdate = 4,
  }
  
  public abstract class RunnerSimulatePhysicsBase: SimulationBehaviour, IBeforeTick  {
    /// <summary>
    /// Select when the primary PhysicsScene for the <see cref="NetworkRunner"/> will simulate.
    /// Typically this will be <see cref="Stages.ForwardAndResims"/>.
    /// </summary>
    [InlineHelp]
    [SerializeField] 
    public Stages SimulateStages = Stages.ForwardAndResims;

    /// <summary>
    /// Must be a greater than zero value (You cannot simulate using zero or negative values).
    /// Values less than zero will be clamped to zero. Default is 1.
    /// A value of zero will result in Physics.Simulate not being called at all.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    public float DeltaTimeMultiplier = 1;
    
    /// <summary>
    /// Delta-time used by FixedUpdateNetwork for physics simulation. By default, set to be the <see cref="Simulation.DeltaTime">simulation delta-time</see>.
    /// Override this if you want to control how much time passes in each tick (for bullet-time or time compression effects).
    /// You typically can just set the <see cref="DeltaTimeMultiplier"/> instead to speed up or slow down time.
    /// </summary>
    public virtual float PhysicsSimulationDeltaTime {
      get => Runner.DeltaTime;
    }


#region Simulation Callbacks
    
    /// <summary>
    /// Callback invoked prior to Simulate() being called. 
    /// </summary>
    public event Action OnBeforeSimulate; 
    /// <summary>
    /// Callback invoked prior to Simulate() being called. 
    /// </summary>
    public event Action OnAfterSimulate;

    // One-time callbacks
    private readonly Queue<Action> _onAfterSimulateCallbacks  = new Queue<Action>();
    private readonly Queue<Action> _onBeforeSimulateCallbacks = new Queue<Action>();

    /// <summary>
    /// Returns true FixedUpdateNetwork has executed for the current tick, and physics has simulated.
    /// </summary>
    public bool HasSimulatedThisTick { get; private set; }
    
    /// <summary>
    /// Register a one time callback which will be called immediately before the next physics simulation occurs.
    /// Use <see cref="HasSimulatedThisTick"/> to determine if simulation has already happened.
    /// </summary>
    public void QueueBeforeSimulationCallback(Action callback) {
      _onBeforeSimulateCallbacks.Enqueue(callback);
    }
    /// <summary>
    /// Register a one time callback which will be called immediately after the next physics simulation occurs.
    /// Use <see cref="HasSimulatedThisTick"/> to determine if simulation has already happened.
    /// </summary>
    public void QueueAfterSimulationCallback(Action callback) {
      _onAfterSimulateCallbacks.Enqueue(callback);
    }
    
#endregion

    protected abstract void SimulatePrimaryScene(    float deltaTime);
    protected abstract void SimulateAdditionalScenes(float deltaTime, Stages stage);
    
    protected virtual void FixedUpdate() {

      if (DeltaTimeMultiplier <= 0) {
        return;
      }
      
      var deltaTime = Time.fixedDeltaTime * DeltaTimeMultiplier;
      
      if ((SimulateStages & Stages.UnityFixedUpdate) != 0) {
        DoSimulatePrimaryScene(deltaTime);
      }
      SimulateAdditionalScenes(deltaTime, Stages.UnityFixedUpdate);
    }
    
    public override void FixedUpdateNetwork() {

      if (DeltaTimeMultiplier <= 0) {
        return;
      }
      
      var deltaTime    = PhysicsSimulationDeltaTime * DeltaTimeMultiplier;
      var currentStage = Runner.Stage == SimulationStages.Forward ? Stages.ForwardAndResims : Stages.ForwardOnly;
      
      if ((SimulateStages & currentStage) != 0) {
        DoSimulatePrimaryScene(deltaTime);
      }

      SimulateAdditionalScenes(deltaTime, currentStage);
    }
    
    public void BeforeTick() {
      HasSimulatedThisTick = false;
    }

    protected virtual void DoSimulatePrimaryScene(float deltaTime) {

      while (_onBeforeSimulateCallbacks.Count > 0) {
        _onBeforeSimulateCallbacks.Dequeue().Invoke();
      }
      OnBeforeSimulate?.Invoke();
      
      SimulatePrimaryScene(deltaTime);
      HasSimulatedThisTick = true;

      while (_onAfterSimulateCallbacks.Count > 0) {
        _onAfterSimulateCallbacks.Dequeue().Invoke();
      }
      OnAfterSimulate?.Invoke();
    }
  }
  // The base class with additional handling for additional scenes
  public abstract class RunnerSimulatePhysicsBase<TPhysicsScene> : RunnerSimulatePhysicsBase where TPhysicsScene : struct, IEquatable <TPhysicsScene> {
    
    public struct AdditionalScene {
      public TPhysicsScene PhysicsScene;
      public Stages Stages;
    }
    
    protected List<AdditionalScene> _additionalScenes;
    
    /// <summary>
    /// Register a Physics Scene to be simulated by Fusion.
    /// </summary>
    /// <param name="scene">The Physics Scene to include in simulation.</param>
    /// <param name="stages">Which timing segments physics simulation should occur in.
    /// Typically this will be Forward, if you want to simulate physics locally for non-networked objects (such as rag dolls)</param>
    public void RegisterAdditionalScene(TPhysicsScene scene, Stages stages = Stages.ForwardAndResims | Stages.ForwardOnly) {
      if (_additionalScenes == null) {
        _additionalScenes = new List<AdditionalScene>();
      } else {
        foreach (var entry in _additionalScenes) {
          if (entry.PhysicsScene.Equals(scene)) {
            Debug.LogWarning("Scene already registered.");
            return;
          }
        }
      }
      _additionalScenes.Add(new AdditionalScene(){PhysicsScene = scene, Stages = stages});
    }

    /// <summary>
    /// Unregister a Physics Scene, and it will not longer have calls made to Simulate() by this component.
    /// </summary>
    /// <param name="scene"></param>
    public void UnregisterAdditionalScene(TPhysicsScene scene) {
      if (_additionalScenes == null) {
        Debug.LogWarning("Scene was never registered, cannot unregister.");
        return;
      }

      int? found = null;
      for (int i = 0; i < _additionalScenes.Count; i++) {
        if (_additionalScenes[i].PhysicsScene.Equals(scene)) {
          found = i;
          break;
        }
      }

      if (found.HasValue) {
        _additionalScenes.RemoveAt(found.Value);
      }
    }
  }
}
