#define FUSION_TRACE_SCENE_MANAGER

namespace Fusion {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using UnityEngine;
  using UnityEngine.SceneManagement;
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
  using System.Threading.Tasks;
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
  using UnityEngine.ResourceManagement.ResourceLocations;
  using UnityEngine.ResourceManagement.ResourceProviders;
#endif

  public class NetworkSceneManagerDefault : Fusion.Behaviour, INetworkSceneManager {
    /// <summary>
    /// If enabled and there is an already loaded scene that matches what the scene manager has intended to load,
    /// that scene will be used instead and load will be avoided.
    /// </summary>
    [InlineHelp]
    [ToggleLeft]
    public bool IsSceneTakeOverEnabled = true;

    /// <summary>
    /// Should all scene load errors be logged into the console? If disabled, errors can still be retrieved via the
    /// <see cref="NetworkSceneAsyncOp.Error"/> or <see cref="NetworkSceneAsyncOp.AddOnCompleted"/>.
    /// </summary>
    [InlineHelp]
    [ToggleLeft]
    public bool LogSceneLoadErrors = true;

    /// <summary>
    /// All the scenes loaded by all the managers. Used when <see cref="IsSceneTakeOverEnabled"/> is enabled.
    /// </summary>
    private static Dictionary<Scene, NetworkSceneManagerDefault> _allOwnedScenes = new Dictionary<Scene, NetworkSceneManagerDefault>(new FusionUnitySceneManagerUtils.SceneEqualityComparer());

    /// <summary>
    /// In multiple peer mode, each runner maintains its own scene where all the newly loaded scenes
    /// are moved to. This is to make sure physics are properly sandboxed.
    /// </summary>
    private List<MultiPeerSceneRoot> _multiPeerSceneRoots = new List<MultiPeerSceneRoot>();

    /// <summary>
    /// List of running coroutines. Only one is actually executed at a time.
    /// </summary>
    private List<ICoroutine> _runningCoroutines = new List<ICoroutine>();

    /// <summary>
    /// For remote clients, this manager first unloads old scenes then loads the new ones. It might happen that all
    /// the current scenes need to be unloaded and in such case a temp scene needs to be created to ensure at least one
    /// scene loaded at all times. 
    /// </summary>
    private Scene _tempUnloadScene;

    /// <summary>
    /// Scene used when Multiple Peer mode is used. Each loaded scene is merged into this one, allowing
    /// for multiple runners to have separate cross-scene physics.
    /// </summary>
    public Scene MultiPeerScene { get; private set; }

    /// <summary>
    /// Root for DontDestroyOnLoad objects. Instantiated on <see cref="MultiPeerScene"/>.
    /// </summary>
    public Transform MultiplePeerDontDestroyOnLoadRoot { get; private set; }

    /// <summary>
    /// The last read scene info. Updated in <see cref="OnSceneInfoChanged"/>.
    /// </summary>
    public NetworkSceneInfoDefault LastSceneInfo { get; private set; }

    public NetworkRunner Runner { get; private set; }

    private bool      IsMultiplePeer => Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Multiple;
    private bool      _hadAnyOnSceneInfoChanged;
    private bool      _isLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStatics() {
      _allOwnedScenes.Clear();
    }

    static NetworkSceneManagerDefault() {
      SceneManager.sceneUnloaded += (s) => _allOwnedScenes.Remove(s);
    }

    #region INetworkSceneManager

    public virtual void Initialize(NetworkRunner runner) {
      Log.TraceSceneManager(runner, $"Initialize with {runner}");
      
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
      LoadAddressableScenePathsAsync();
#endif

      Debug.Assert(Runner == null);
      Runner = runner;
      
      _hadAnyOnSceneInfoChanged = false;
      
      // assign an empty scene with a separate physics stage immediately, so that they won't spawn anything on the currently active scene
      // an lose track of it
      if (IsMultiplePeer) {
        var scene = SceneManager.CreateScene($"{runner.name}_{runner.LocalPlayer}",
          new CreateSceneParameters(LocalPhysicsMode.Physics2D | LocalPhysicsMode.Physics3D));
        Log.TraceSceneManager(Runner, $"Assigned an initial scene: {scene.Dump()}");

        MultiPeerScene                    = scene;
        MultiplePeerDontDestroyOnLoadRoot = new GameObject("[DontDestroyOnLoad]").transform;
        SceneManager.MoveGameObjectToScene(MultiplePeerDontDestroyOnLoadRoot.gameObject, MultiPeerScene);
      }
    }

    public virtual void Shutdown() {
      
      Log.TraceSceneManager(Runner, $"Shutdown with {Runner}");
      
      Runner = null;

      // clear owned scenes in case this manager is reused
      var ownedScenes = _allOwnedScenes
                       .Where(x => x.Value == this)
                       .Select(x => x.Key)
                       .ToList();
      
      foreach (var ownedScene in ownedScenes) {
        _allOwnedScenes.Remove(ownedScene);
      }
      
      _multiPeerSceneRoots.Clear();
      
      LastSceneInfo                     = default;
      MultiplePeerDontDestroyOnLoadRoot = null;

      var sceneToUnload = MultiPeerScene;
      MultiPeerScene = default;
      
      if (sceneToUnload.isLoaded) {
        if (!sceneToUnload.CanBeUnloaded()) {
          SceneManager.CreateScene($"FusionSceneManager_TempEmptyScene");
        }
        SceneManager.UnloadSceneAsync(sceneToUnload);
      }
    }

    public virtual bool IsBusy {
      get {
        if (_isLoading) {
          return true;
        }
        
        if (!_hadAnyOnSceneInfoChanged) {
          return true;
        }

        if (IsMultiplePeer && _multiPeerSceneRoots.Count == 0) {
          // nothing to spawn on
          return true;
        }

        return false;
      }
    }

    public virtual bool TryGetPhysicsScene2D(out PhysicsScene2D scene2D) {
      if (IsMultiplePeer) {
        if (MultiPeerScene.IsValid()) {
          scene2D = MultiPeerScene.GetPhysicsScene2D();
          return true;
        } else {
          scene2D = default;
          return false;
        }
      } else {
        scene2D = Physics2D.defaultPhysicsScene;
        return true;
      }
    }

    public virtual bool TryGetPhysicsScene3D(out PhysicsScene scene3D) {
      if (IsMultiplePeer) {
        if (MultiPeerScene.IsValid()) {
          scene3D = MultiPeerScene.GetPhysicsScene();
          return true;
        } else {
          scene3D = default;
          return false;
        }
      } else {
        scene3D = Physics.defaultPhysicsScene;
        return true;
      }
    }

    public virtual Scene MainRunnerScene {
      get {
        if (IsMultiplePeer) {
          return MultiPeerScene;
        } else {
          return SceneManager.GetActiveScene();
        }
      }
    }

    public virtual void MakeDontDestroyOnLoad(GameObject obj) {
      if (IsMultiplePeer) {
        Debug.Assert(obj.transform.parent == null || obj.transform.parent == MultiplePeerDontDestroyOnLoadRoot);
        obj.transform.SetParent(MultiplePeerDontDestroyOnLoadRoot, true);
      } else {
        DontDestroyOnLoad(obj);
      }
    }

    public virtual void MoveToRunnerScene(GameObject obj) {
      if (IsMultiplePeer) {
        var sceneRoot = GetMultiplePeerRootForObject(obj, _multiPeerSceneRoots);
        if (sceneRoot == null) {
          Log.WarnSceneManager(Runner, $"Failed to get a root scene object for this multiple peer runner or {nameof(GetMultiplePeerRootForObject)} returned null. Making \"{obj.name}\" DontDestroyOnLoad instead. " +
            $"Make sure there is an initial scene passed with {nameof(StartGameArgs)}.{nameof(StartGameArgs.Scene)} and {nameof(OnSceneInfoChanged)} succeeds.");
          MakeDontDestroyOnLoad(obj);
        } else {
          obj.transform.SetParent(sceneRoot, true);
        } 
      } else {
        // do nothing, all the scenes are considered runner scenes
      }
    }

    public virtual bool IsRunnerScene(Scene scene) {
      if (IsMultiplePeer) {
        return scene == MultiPeerScene;
      } else {
        return true;
      }
    }

    public virtual void OnSceneInfoChanged() {
      _hadAnyOnSceneInfoChanged = true;
      
      if (!Runner.TryGetSceneInfo(out NetworkSceneInfoDefault sceneInfo)) {
        Log.WarnSceneManager(Runner, $"Failed to get {nameof(NetworkSceneInfoDefault)}, not updating scenes");
        return;
      }

      Log.TraceSceneManager(Runner, $"OnSceneInfoChanged: {sceneInfo} (prev: {LastSceneInfo}");
      var prev = LastSceneInfo;
      LastSceneInfo = sceneInfo;

      StartTracedCoroutine(SwitchSceneAsync(prev, sceneInfo));
    }

    public virtual NetworkSceneAsyncOp LoadScene(SceneRef sceneRef, LoadSceneParameters parameters) {
      Log.TraceSceneManager(Runner, $"Load scene {sceneRef} called with parameters: {parameters.Dump()}");

      if (Runner.IsShutdown) {
        return FailOp(sceneRef, new InvalidOperationException(
          $"Can't load scene {sceneRef} because the runner is shutting down. Scene info changes would never reach clients."));
      }
      
      if (Runner.IsSceneAuthority) {
        // implementer here needs to decide whether to set the networked scene info immediately
        // (so that clients start loading scene asap) or wait until the scene is fully loaded
        // on the server/mster client and only then updated the shared state;
        // here we decided not to wait and update the state
        Runner.TryGetSceneInfo(out NetworkSceneInfoDefault sceneInfo);
        var oldSceneInfo = sceneInfo;

        if (!sceneInfo.AddSceneRef(sceneRef, parameters.loadSceneMode, parameters.localPhysicsMode)) {
          return FailOp(sceneRef, new ArgumentException($"Failed to add {sceneRef}", nameof(sceneRef)));
        }

        if (!Runner.SetSceneInfo(sceneInfo)) {
          return FailOp(sceneRef,
            new InvalidOperationException($"Failed to set scene info: {sceneInfo}. If the call " +
              $"to this method happened before the first tick, the state object holding might not " +
              $"have yet been created"));
        } else {
          Log.TraceSceneManager(Runner, $"Set scene info to: {sceneInfo} (from {oldSceneInfo})");
        }

        LastSceneInfo = sceneInfo;
        var coroutine = StartTracedCoroutine(SwitchSceneAsync(oldSceneInfo, sceneInfo));
        return NetworkSceneAsyncOp.FromCoroutine(sceneRef, coroutine);
      } else {
        return FailOp(sceneRef,
          new InvalidOperationException($"Only the scene master can load scenes"));
      }
    }


    public virtual NetworkSceneAsyncOp UnloadScene(SceneRef sceneRef) {
      Log.TraceSceneManager(Runner, $"Unload scene {sceneRef} called");

      if (Runner.IsShutdown) {
        return FailOp(sceneRef, new InvalidOperationException(
          $"Can't unload scene {sceneRef} because the runner is shutting down. Scene info changes would never reach clients."));
      }
      
      if (Runner.IsSceneAuthority) {
        Runner.TryGetSceneInfo(out NetworkSceneInfoDefault sceneInfo);

        var oldSceneInfo = sceneInfo;

        if (!sceneInfo.RemoveSceneRef(sceneRef)) {
          return FailOp(sceneRef, new ArgumentException($"Failed to remove {sceneRef}", nameof(sceneRef)));
        }

        if (!Runner.SetSceneInfo(sceneInfo)) {
          return FailOp(sceneRef,
            new InvalidOperationException($"Failed to set scene info: {sceneInfo}. If the call " +
                                          $"to this method happened before the first tick, the state object holding might not " +
                                          $"have yet been created"));
        }

        LastSceneInfo = sceneInfo;
        var coroutine = StartTracedCoroutine(SwitchSceneAsync(oldSceneInfo, sceneInfo));
        return NetworkSceneAsyncOp.FromCoroutine(sceneRef, coroutine);
      } else {
        return FailOp(sceneRef, new InvalidOperationException($"Only the scene master can unload scenes"));
      }
    }

    public virtual SceneRef GetSceneRef(string sceneNameOrPath) {
      int buildIndex = FusionUnitySceneManagerUtils.GetSceneBuildIndex(sceneNameOrPath);
      if (buildIndex >= 0) {
        return SceneRef.FromIndex(buildIndex);
      }
      
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
      // this may be a blocking call due to WaitForCompletion being used internally
      if (!_addressableScenesTask.IsValueCreated) {
        Log.WarnSceneManager(Runner, $"Going to block the thread in wait for addressable scene paths being resolved, call and await {nameof(LoadAddressableScenePathsAsync)} to avoid this.");
      }
      var addressableScenes = _addressableScenesTask.Value.Result;
      var index             = FusionUnitySceneManagerUtils.GetSceneIndex(addressableScenes, sceneNameOrPath);
      if (index >= 0) {
        return SceneRef.FromPath(addressableScenes[index]);
      }
#endif

      return SceneRef.None;
    }

    #endregion

    /// <summary>
    /// Selects a scene root object to use for an object passed to <see cref="MoveToRunnerScene"/> in MultiplePeer mode.
    /// The default implementation returns the first scene root, if available.
    /// Override if you load multiple scenes per runner and need objects to spawn on a specific scene.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="sceneRoots"></param>
    /// <returns></returns>
    protected virtual Transform GetMultiplePeerRootForObject(GameObject obj, IReadOnlyList<MultiPeerSceneRoot> sceneRoots) {
      if (sceneRoots.Count == 0) {
        return null;
      }
      // use the first scene by default
      return sceneRoots[0].transform;
    }

    protected virtual IEnumerator SwitchSceneAsync(NetworkSceneInfoDefault prevInfo, NetworkSceneInfoDefault sceneInfo) {

      Log.TraceSceneManager(Runner, $"SwitchSceneAsync: {sceneInfo} (prev: {prevInfo}");
      
      // only unload scenes explicitly if the first scene is not in a single load mode; otherwise
      // the load process itself will unload the previous scenes
      bool loadingInSingleModeWillUnloadAllTheScenes = false;
      if (sceneInfo.SceneCount > 0 && sceneInfo.SceneParams[0].LoadSceneMode == LoadSceneMode.Single) {
        if (prevInfo.SceneCount == 0 || prevInfo.Scenes[0] != sceneInfo.Scenes[0] || prevInfo.SceneParams[0] != sceneInfo.SceneParams[0]) {
          loadingInSingleModeWillUnloadAllTheScenes = true;
        }
      }
      
      if (loadingInSingleModeWillUnloadAllTheScenes == false) {
        for (int i = 0; i < prevInfo.SceneCount; ++i) {
          if (sceneInfo.IndexOf(prevInfo.Scenes[i], prevInfo.SceneParams[i]) >= 0) {
            continue;
          }

          // this way of invoking a nested coroutine ensures exceptions
          // are propagated to the caller
          var inner = UnloadSceneCoroutine(prevInfo.Scenes[i]);
          using (inner as IDisposable) {
            while (inner.MoveNext()) {
              yield return inner.Current;
            }
          }
        }
      }

      for (int i = 0; i < sceneInfo.SceneCount; ++i) {
        if (prevInfo.IndexOf(sceneInfo.Scenes[i], sceneInfo.SceneParams[i]) >= 0) {
          continue;
        }
        
        var inner = LoadSceneCoroutine(sceneInfo.Scenes[i], sceneInfo.SceneParams[i]);
        using (inner as IDisposable) {
          while (inner.MoveNext()) {
            yield return inner.Current;
          }
        }
      }
    }

    protected virtual IEnumerator LoadSceneCoroutine(SceneRef sceneRef, NetworkSceneInfoDefaultSceneParams sceneParams) {
      Runner.InvokeSceneLoadStart(sceneRef);

      Scene scene = default;

      using (MakeLoadingScope()) {
        Log.TraceSceneManager(Runner, $"LoadSceneCoroutine called with {sceneRef}, {sceneParams}");
        var localPhysicsMode = sceneParams.LocalPhysicsMode;
        var loadSceneMode    = sceneParams.LoadSceneMode;

        if (IsMultiplePeer) {
          if (localPhysicsMode != LocalPhysicsMode.None) {
            throw new ArgumentException($"Local physics mode is not supported in multiple peer mode",
              nameof(sceneParams));
          }

          if (loadSceneMode == LoadSceneMode.Single) {
            // all the current scenes need to be "unloaded", except possibly for the one
            // that matches the sceneRef, if scene take over is enabled
            loadSceneMode = LoadSceneMode.Additive;

            try {
              foreach (var root in _multiPeerSceneRoots) {
                Log.TraceSceneManager(Runner, $"Destroying scene {sceneRef} root {root.name} due to single-mode load");
                Destroy(root.gameObject);
              }

              // wait for each root to be destroyed
              foreach (var root in _multiPeerSceneRoots) {
                while (root != null) {
                  yield return null;
                }
              }
            } finally {
              _multiPeerSceneRoots.Clear();
            }
          }
        }

        if (IsSceneTakeOverEnabled) {
          // check if a loaded scene can be taken over
          Scene candidate = FindSceneToTakeOver(sceneRef);
          if (candidate.IsValid()) {
            Log.TraceSceneManager(Runner, $"Taking over {sceneRef}: {candidate.Dump()}");

            if (candidate.GetLocalPhysicsMode() != localPhysicsMode) {
              throw new InvalidOperationException($"Tried to take over {candidate.Dump()} for {sceneRef}, but physics mode were different: {candidate.GetLocalPhysicsMode()} != {localPhysicsMode}");
            }

            scene = candidate;
            MarkSceneAsOwned(sceneRef, candidate);

            if (loadSceneMode == LoadSceneMode.Single && !IsMultiplePeer) {
              // need to unload scenes manually, multiple peer mode is handled at the beginning of this method, because
              // it always needs to the manual cleanup for single mode
              for (int i = 0; i < SceneManager.sceneCount; i++) {
                var toUnload = SceneManager.GetSceneAt(i);
                if (toUnload != candidate) {
                  Log.TraceSceneManager(Runner, $"Unloading {sceneRef} ({toUnload.Dump()}) due to single-mode take over of {candidate.Dump()}");
                  yield return SceneManager.UnloadSceneAsync(toUnload);
                }
              }
            }
          }
        }

        if (!scene.IsValid()) {
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
          if (loadSceneMode == LoadSceneMode.Single) {
            // single mode unloads all the scenes anyway
            _addressableOperations.Clear();
          }
#endif

          if (sceneRef.IsIndex) {
            Log.TraceSceneManager(Runner, $"Loading scene {sceneRef} with build index {sceneRef.AsIndex} with mode {loadSceneMode}");
            var op = SceneManager.LoadSceneAsync(sceneRef.AsIndex,
              new LoadSceneParameters(loadSceneMode, localPhysicsMode));
            if (op == null) {
              throw new InvalidOperationException($"Scene not found: {sceneRef.AsIndex}");
            }

            Debug.Assert(SceneManager.sceneCount > 0);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            MarkSceneAsOwned(sceneRef, scene);

            Debug.Assert(scene.buildIndex == sceneRef.AsIndex);

            while (!op.isDone) {
              OnLoadSceneProgress(sceneRef, op.progress);
              yield return null;
            }
          } else {
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
            if (!_addressableScenesTask.IsValueCreated) {
              Log.WarnSceneManager(Runner, $"Going to block the thread in wait for addressable scene paths being resolved, call and await {nameof(LoadAddressableScenePathsAsync)} to avoid this.");
            }
            string sceneAddress = null;
            foreach (var path in _addressableScenesTask.Value.Result) {
              if (sceneRef.IsPath(path)) {
                sceneAddress = path;
                break;
              }
            }
            
            if (sceneAddress == null) {
              throw new InvalidOperationException($"Unable to find addressable scene path for {sceneRef}");
            }

            Log.TraceSceneManager(Runner, $"Loading scene {sceneRef} from addressable: {sceneAddress}");

#if FUSION_ENABLE_ADDRESSABLES_LOCAL_PHYSICS
            var loadSceneParameters = new LoadSceneParameters(loadSceneMode, localPhysicsMode);
#else
            if (localPhysicsMode != LocalPhysicsMode.None) {
              throw new InvalidOperationException($"{nameof(LocalPhysicsMode)} is not supported in this version of Addressables");
            }
            var loadSceneParameters = loadSceneMode;
#endif
            var op = Addressables.LoadSceneAsync(sceneAddress, loadSceneParameters);

            // to get the scene a callback is used, as it fires immediately when loading finished,
            // compared to waiting for the coroutine to resume
            scene = default;
            op.Completed += op => {
              if (op.Status == AsyncOperationStatus.Succeeded) {
                scene = op.Result.Scene;
                MarkSceneAsOwned(sceneRef, scene);
              }
            };

            op.Destroyed += _ => {
              // this will happen in MP mode when scenes are merged or when a scene is loaded in a single mode
              if (_addressableOperations.Remove(sceneRef)) {
                Log.TraceSceneManager(Runner, $"Destroyed Addressables op for {sceneRef}");
              }
            };

            _addressableOperations.Add(sceneRef, op);

            while (!op.IsDone) {
              OnLoadSceneProgress(sceneRef, op.PercentComplete);
              yield return null;
            }

            if (!op.IsValid()) {
              throw new InvalidOperationException($"Loading operation for {sceneRef} has been destroyed");
            }

            if (op.Status == AsyncOperationStatus.Failed) {
              Addressables.Release(op);
              throw new InvalidOperationException($"Failed to load scene from addressable: {sceneAddress}");
            }
#else
            throw new InvalidOperationException($"SceneRef {sceneRef} points to an addressable scene, but FUSION_ENABLE_ADDRESSABLES is not defined");
#endif
          }
        }
      }

      yield return StartCoroutine(OnSceneLoaded(sceneRef, scene, sceneParams));
    }

    protected virtual IEnumerator UnloadSceneCoroutine(SceneRef sceneRef) {
      Log.TraceSceneManager(Runner, $"UnloadSceneCoroutine called for {sceneRef}");

      using (MakeLoadingScope()) {
        if (IsMultiplePeer) {
          // in multiple peer, the unload simply destroys the scene root
          for (int i = 0; i < _multiPeerSceneRoots.Count; ++i) {
            var root = _multiPeerSceneRoots[i];
            if (root.SceneRef == sceneRef) {
              _multiPeerSceneRoots.RemoveAt(i);
              Log.TraceSceneManager(Runner, $"Destroying scene root {root.name} for {sceneRef}");

              Log.TraceSceneManager(Runner, $"Started unloading {root.Scene.ToString()} for {sceneRef}");
              Destroy(root.gameObject);
              while (root != null) {
                yield return null;
              }

              Log.TraceSceneManager(Runner, $"Finished unloading {root.Scene.ToString()} for {sceneRef}");
              yield break;
            }
          }

          throw new ArgumentOutOfRangeException($"Did not find a scene to unload: {sceneRef}", nameof(sceneRef));
        } else {
          Scene sceneToUnload = default;

          // find the scene to unload
          for (int i = 0; i < SceneManager.sceneCount; ++i) {
            var scene = SceneManager.GetSceneAt(i);
            if (GetSceneRef(scene.path) == sceneRef) {
              sceneToUnload = scene;
              break;
            }
          }

          if (!sceneToUnload.IsValid()) {
            throw new ArgumentOutOfRangeException($"Did not find a scene to unload: {sceneRef}", nameof(sceneRef));
          }

          Log.TraceSceneManager(Runner, $"Started unloading {sceneToUnload.Dump()} for {sceneRef}");

          if (!sceneToUnload.CanBeUnloaded()) {
            Log.WarnSceneManager(Runner, $"Scene {sceneToUnload.Dump()} can't be unloaded for {sceneRef}, creating a temporary scene to unload it");
            Debug.Assert(!_tempUnloadScene.IsValid());
            _tempUnloadScene = SceneManager.CreateScene($"FusionSceneManager_TempEmptyScene");
          }

#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
          if (_addressableOperations.TryGetValue(sceneRef, out var asyncOp)) {
            Log.TraceSceneManager(Runner, $"Unloading addressable scene {sceneToUnload.Dump()} for {sceneRef}");
            yield return Addressables.UnloadSceneAsync(asyncOp);
          } else
#endif
          {
            Log.TraceSceneManager(Runner, $"Unloading {sceneToUnload.Dump()} for {sceneRef}");
            var op = SceneManager.UnloadSceneAsync(sceneToUnload);
            if (op == null) {
              throw new InvalidOperationException($"Failed to unload {sceneToUnload.Dump()}");
            }

            yield return op;
          }

          Log.TraceSceneManager(Runner, $"Finished unloading {sceneToUnload.Dump()} for {sceneRef}");
        }
      }
    }

    protected virtual IEnumerator OnSceneLoaded(SceneRef sceneRef, Scene scene, NetworkSceneInfoDefaultSceneParams sceneParams) {
      Log.TraceSceneManager(Runner, $"Finished loading, starting processing {scene.Dump()} for {sceneRef}");

      var sceneObjects = scene.GetComponents<NetworkObject>(includeInactive: true, out var rootObjects);

      // since it is impossible to get objects in deterministic order (sibling index is 0 for all root objects in builds),
      // scene objects need to be sorted by something that will guarantee the order
      Array.Sort(sceneObjects, NetworkObjectSortKeyComparer.Instance);
      
      if (IsMultiplePeer) {
        // create a root GO for all the gameObjects in the newly loaded scene
        var newSceneRoot = new GameObject($"[{scene.name}]").AddComponent<MultiPeerSceneRoot>();
        newSceneRoot.SceneRef    = sceneRef;
        newSceneRoot.SceneHandle = scene.handle;
        newSceneRoot.Scene       = scene;
        newSceneRoot.ScenePath   = scene.path;

        SceneManager.MoveGameObjectToScene(newSceneRoot.gameObject, scene);

        foreach (var rootGameObject in rootObjects) {
          rootGameObject.transform.SetParent(newSceneRoot.transform, true);
        }

        // store the info
        _multiPeerSceneRoots.Add(newSceneRoot);

        Log.TraceSceneManager(Runner, $"Merging {scene.Dump()} to {MultiPeerScene.Dump()} for {sceneRef}");
        SceneManager.MergeScenes(scene, MultiPeerScene);
      }
      
      // register scene objects; this will deactivate GameObjects for clients
      // the additional loadId parameter is passed to ensure each scene load
      // yields unique type ids for scene objects
      Runner.RegisterSceneObjects(sceneRef, sceneObjects, loadId: sceneParams.SceneInfoVersion);
      
      Log.TraceSceneManager(Runner, $"Finished loading & processing {scene.Dump()} for {sceneRef}");
      Runner.InvokeSceneLoadDone(new SceneLoadDoneArgs(sceneRef, sceneObjects, scene, rootObjects));
      yield break;
    }

    protected virtual void OnLoadSceneProgress(SceneRef sceneRef, float progress) {
      Log.TraceSceneManager(Runner, $"Loading scene progress {sceneRef} ({progress:P2})");
    }

    private Scene FindSceneToTakeOver(SceneRef sceneRef) {
      for (int i = 0; i < SceneManager.sceneCount; ++i) {
        var candidate = SceneManager.GetSceneAt(i);
        if (!candidate.isLoaded) {
          continue;
        }

        if (GetSceneRef(candidate.path) != sceneRef) {
          continue;
        }

        if (_allOwnedScenes.ContainsKey(candidate)) {
          continue;
        }

        return candidate;
      }

      return default;
    }

    private ICoroutine StartTracedCoroutine(IEnumerator inner) {
      var coro = new FusionCoroutine(inner);

      _runningCoroutines.Add(coro);

      coro.Completed += x => {

        if (LogSceneLoadErrors && x.Error != null) {
          Log.ErrorSceneManager(Runner, $"Failed async op: {x.Error}");
        }
        
        // remove this one from the list
        var index = _runningCoroutines.IndexOf((ICoroutine)x);
        Debug.Assert(index == 0, "Expected the completed coroutine to be the first in the list");
        _runningCoroutines.RemoveAt(index);

        // start the next one
        if (index < _runningCoroutines.Count) {
          Log.TraceSceneManager(Runner, $"Starting enqueued coroutine {index} of {_runningCoroutines.Count}");
          StartCoroutine(_runningCoroutines[index]);
        }
      };

      if (_runningCoroutines.Count == 1) {
        // start immediately
        StartCoroutine(coro);
      } else {
        Log.TraceSceneManager(Runner, $"Enqueued coroutine, there are already {_runningCoroutines.Count - 1} running");
      }

      return coro;
    }

    protected LoadingScope MakeLoadingScope() {
      return new LoadingScope(this);
    }

    protected void MarkSceneAsOwned(SceneRef sceneRef, Scene scene) {
      if (_allOwnedScenes.TryGetValue(scene, out var manager)) {
        Log.WarnSceneManager(Runner, $"Scene {scene.Dump()} (for {sceneRef}) already owned by {manager}");
      } else {
        _allOwnedScenes.Add(scene, this);
      }
    }

    private NetworkSceneAsyncOp FailOp(SceneRef sceneRef, Exception exception) {
      if (LogSceneLoadErrors) {
        Log.ErrorSceneManager(Runner, $"Failed with: {exception}");
      }

      return NetworkSceneAsyncOp.FromError(sceneRef, exception);
    }

#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
    /// <summary>
    /// A label by which addressable scenes can be discovered.
    /// </summary>
    [InlineHelp]
    public string AddressableScenesLabel = "FusionScenes";
    
    public NetworkSceneManagerDefault() {
      _addressableScenesTask = new Lazy<Task<string[]>>(() => GetAddressableScenes());
    }
    
    public Task LoadAddressableScenePathsAsync() {
      return _addressableScenesTask.Value;
    }

    protected virtual Task<string[]> GetAddressableScenes() {
      Log.TraceSceneManager(Runner, $"Locating addressable scenes with label: {AddressableScenesLabel}");
      
      var tcs    = new TaskCompletionSource<string[]>();
      var result = Addressables.LoadResourceLocationsAsync(AddressableScenesLabel, typeof(SceneInstance));
        
      result.Completed += op => {
        try {
          if (op.Status == AsyncOperationStatus.Failed) {
            tcs.SetException(op.OperationException);
          } else {
            var paths = op.Result.Select(x => x.PrimaryKey).ToArray();
            Log.TraceSceneManager(Runner, $"Found {paths.Length} addressable scenes: {string.Join(", ", paths)}");
            tcs.SetResult(paths);
          }
        } finally {
          Addressables.Release(op);
        }
      };
        
      return tcs.Task;
    } 
    
    private Lazy<Task<string[]>>                                      _addressableScenesTask;
    private Dictionary<SceneRef, AsyncOperationHandle<SceneInstance>> _addressableOperations = new();
#endif

    protected sealed class MultiPeerSceneRoot : MonoBehaviour {
      public SceneRef SceneRef;
      public string   ScenePath;
      public int      SceneHandle;
      public Scene    Scene;
    }

    protected struct LoadingScope : IDisposable {
      private readonly NetworkSceneManagerDefault _manager;

      public LoadingScope(NetworkSceneManagerDefault manager) {
        _manager            = manager;
        _manager._isLoading = true;
        Log.TraceSceneManager(manager.Runner, "Loading scope started");
      }

      public void Dispose() {
        _manager._isLoading = false;
        Log.TraceSceneManager(_manager.Runner, "Loading scope ended");
      }
    }
  }
}