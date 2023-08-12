using UnityEngine;
using Fusion.Analyzer;

namespace Fusion.UnityPhysics {
  /// <summary>
  /// Fusion component for handling Physics2D.Simulate(). 
  /// </summary>
  [DisallowMultipleComponent]
  public class RunnerSimulatePhysics2D : RunnerSimulatePhysicsBase<PhysicsScene2D> {

    [StaticField(StaticFieldResetMode.None)]
    static SimulationMode2D? _physicsAutoSimRestore;

    [StaticField(StaticFieldResetMode.None)]
    static bool? _physicsAutoSyncRestore;

    [StaticField(StaticFieldResetMode.None)]
    static int _enabledRunnersCount;

    void OnEnable() {
      if (++_enabledRunnersCount == 1) {
        _physicsAutoSimRestore  = Physics2D.simulationMode;
        _physicsAutoSyncRestore = Physics2D.autoSyncTransforms;
      }
      Physics2D.simulationMode     = SimulationMode2D.Script;
      Physics2D.autoSyncTransforms = false;
    }

    void OnDisable() {
      if (--_enabledRunnersCount == 0) {
        Physics2D.simulationMode     = _physicsAutoSimRestore.GetValueOrDefault(Physics2D.simulationMode);
        Physics2D.autoSyncTransforms = _physicsAutoSyncRestore.GetValueOrDefault(Physics2D.autoSyncTransforms);
        _physicsAutoSimRestore       = default;
        _physicsAutoSyncRestore      = default;
      }
    }
    
    protected override void SimulatePrimaryScene(float deltaTime) {
      if (Runner.SceneManager.TryGetPhysicsScene2D(out var physicsScene)) {
        if (physicsScene.IsValid()) {
          physicsScene.Simulate(deltaTime);
        } else {
          Physics2D.Simulate(deltaTime);
        }
      }
    }

    protected override void SimulateAdditionalScenes(float deltaTime, Stages stage) {
      if (_additionalScenes == null || _additionalScenes.Count == 0) {
        return;
      }
      var defaultPhysicsScene = Physics2D.defaultPhysicsScene;
      foreach (var scene in _additionalScenes) {
        if ((scene.Stages & stage) != 0) {
          if (scene.PhysicsScene != defaultPhysicsScene || Physics.autoSimulation == false) {
            scene.PhysicsScene.Simulate(deltaTime);
          }
        }
      }
    }
  }
}
