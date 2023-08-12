namespace Fusion {
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEngine;
  using UnityEngine.AddressableAssets;

  public class NetworkPrefabSourceUnityAddressable : NetworkPrefabSourceUnityBase {

    public AssetReferenceGameObject Address;

    public override string EditorSummary => $"[Address: {Address}]";

    public override void Load(in NetworkPrefabLoadContext context) {
      Debug.Assert(!Address.OperationHandle.IsValid());
      var op = Address.LoadAssetAsync();
      if (op.IsDone) {
        context.Loaded(op.Result);
      } else {
        if (context.IsSynchronous) {
          var result = op.WaitForCompletion();
          context.Loaded(result);
        } else {
          var c = context;
          op.Completed += (_op) => {
            c.Loaded(_op.Result);
          };
        }
      }
    }

    public override void Unload(NetworkPrefabId id) {
      Address.ReleaseAsset();
    }
  }

#endif
}