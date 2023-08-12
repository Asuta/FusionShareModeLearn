using UnityEngine;
using Fusion.Analyzer;

namespace Fusion.UnityPhysics {
  /// <summary>
  /// Fusion component for handling Physics.Simulate(). When added to a <see cref="NetworkRunner"/> GameObject, this will automatically disable 
  /// </summary>
  [DisallowMultipleComponent]
  public class RunnerSimulatePhysics3D : RunnerSimulatePhysicsBase<PhysicsScene> {

    [StaticField(StaticFieldResetMode.None)]
    static bool? _physicsAutoSimRestore;

    [StaticField(StaticFieldResetMode.None)]
    static bool? _physicsAutoSyncRestore;

    [StaticField(StaticFieldResetMode.None)]
    static int _enabledRunnersCount;

    void OnEnable() {
      if (++_enabledRunnersCount == 1) {
        _physicsAutoSimRestore  = Physics.autoSimulation;
        _physicsAutoSyncRestore = Physics.autoSyncTransforms;
      }
      Physics.autoSyncTransforms = false;
      Physics.autoSimulation = false;
    }

    void OnDisable() {
      if (--_enabledRunnersCount == 0) {
        Physics.autoSimulation     = _physicsAutoSimRestore.GetValueOrDefault(Physics.autoSimulation);
        Physics.autoSyncTransforms = _physicsAutoSyncRestore.GetValueOrDefault(Physics.autoSyncTransforms);
        _physicsAutoSimRestore     = default;
        _physicsAutoSyncRestore    = default;
      }
    }

    protected override void SimulatePrimaryScene(float deltaTime) {
      if (Runner.SceneManager.TryGetPhysicsScene3D(out var physicsScene)) {
        if (physicsScene.IsValid()) {
          physicsScene.Simulate(deltaTime);
        } else {
          Physics.Simulate(deltaTime);
        }
      }
    }

    protected override void SimulateAdditionalScenes(float deltaTime, Stages stage) {
      if (_additionalScenes == null || _additionalScenes.Count == 0) {
        return;
      }
      var defaultPhysicsScene = Physics.defaultPhysicsScene;
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
