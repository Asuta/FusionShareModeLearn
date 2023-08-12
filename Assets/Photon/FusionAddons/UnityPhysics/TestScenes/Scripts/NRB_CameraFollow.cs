
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class NRB_CameraFollow : NetworkBehaviour {

  public Vector3 cameraOffset = new Vector3(0, 1, -1);
  
  public Camera Camera;
  public override void Spawned() {
    if (HasInputAuthority) {
      Camera = FindObjectOfTypeInScene<Camera>(gameObject.scene);
    }
  }

  public void LateUpdate() {
    if (Camera) {
      Camera.transform.position = transform.position + cameraOffset;
      Camera.transform.LookAt(transform);      
    }
  }
  
  static List<GameObject> _roots = new List<GameObject>();
  public static T FindObjectOfTypeInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : class
  {
    scene.GetRootGameObjects(_roots);
    for (int i = 0; i < _roots.Count; ++i)
    {
      var result = _roots[i].GetComponentInChildren<T>();
      if (result != null)
      {
        return result;
      }
    }
    return null;
  }
}
