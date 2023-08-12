
using UnityEngine;

public class ForceLowFPS : MonoBehaviour {

  public int FrameRate = 10;

    private void Update() {
      if (FrameRate > 0) {
        QualitySettings.vSyncCount  = 2;
        Application.targetFrameRate = FrameRate;        
      }
    }
}
