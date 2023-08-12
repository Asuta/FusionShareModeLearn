namespace Fusion {
  using System;
  using UnityEngine;

  public class NetworkObjectProviderDefault : Fusion.Behaviour, INetworkObjectProvider {

    /// <summary>
    /// If enabled, the provider will delay acquiring a prefab instance if the scene manager is busy.
    /// </summary>
    [InlineHelp]
    public bool DelayIfSceneManagerIsBusy = true;
    
    public virtual NetworkObjectAcquireResult AcquirePrefabInstance(NetworkRunner runner, in NetworkPrefabAcquireContext context, out NetworkObject instance) {
      
      instance = null;
      
      if (DelayIfSceneManagerIsBusy && runner.SceneManager.IsBusy) {
        return NetworkObjectAcquireResult.Retry;
      }
      
      // TODO: ref counting of prefab instances
      var result = runner.Config.PrefabTable.TryGetPrefab(context.PrefabId, out var prefab, isSynchronous: context.IsSynchronous);

      switch (result) {
        case NetworkPrefabTableGetPrefabResult.Success:
          Assert.Check(prefab);
          instance = GameObject.Instantiate(prefab);
          if (context.DontDestroyOnLoad) {
            runner.MakeDontDestroyOnLoad(instance.gameObject);
          } else {
            runner.MoveToRunnerScene(instance.gameObject);
          }

          return NetworkObjectAcquireResult.Success;
        
        case NetworkPrefabTableGetPrefabResult.InProgress:
          return NetworkObjectAcquireResult.Retry;
        
        case NetworkPrefabTableGetPrefabResult.NotFound:
          Log.Warn($"Prefab {context.PrefabId} not found");
          return NetworkObjectAcquireResult.Failed;
        
        case NetworkPrefabTableGetPrefabResult.LoadError:
          Log.Warn($"Prefab {context.PrefabId} failed to load");
          return NetworkObjectAcquireResult.Failed;
      }

      throw new NotImplementedException();
    }

    public virtual void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context) {
      var instance = context.Object;

      // TODO: ref count decrease

      if (!context.IsBeingDestroyed) {
        // needs actual destroy
        GameObject.Destroy(instance.gameObject);
      }
    }
  }
}