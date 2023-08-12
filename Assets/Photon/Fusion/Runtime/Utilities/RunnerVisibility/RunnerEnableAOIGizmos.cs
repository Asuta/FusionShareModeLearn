namespace Fusion {
  using System;
  using System.Collections.Generic;
  using UnityEngine;

  [ScriptHelp(BackColor = ScriptHeaderBackColor.Sand)]
  [DisallowMultipleComponent]
  public class RunnerEnableAOIGizmos : SimulationBehaviour {
#if UNITY_EDITOR

    [Flags]
    public enum GizmoOptionsEnum {
      ShowActiveServerZones    = 1,
      ShowPlayerInterest = 2,
    }

    [System.Serializable]
    public struct CustomOptions {
      public Color ServerZonesColor;
      public Color PlayerInterestColor;
    }
    
    [ExpandableEnum(AlwaysExpanded = true)]
    public GizmoOptionsEnum GizmoOptions = GizmoOptionsEnum.ShowActiveServerZones | GizmoOptionsEnum.ShowPlayerInterest;

    public CustomOptions Customization = new CustomOptions() {
      ServerZonesColor    = new Color(0.25f, 0.25f, 0.25f, 0.75f), 
      PlayerInterestColor = new Color(255f / 255f, 21f / 255f, 21 / 255f, 0.2f),
    };

    private List<(Vector3 center, Vector3 size, int playerCount, int objectCount)> _reusableGizmoData;

    private void OnEnabled() {
      
    }
    
    private void OnDrawGizmos() {

      if (enabled == false) {
        return;
      }
      
      if (GizmoOptions == 0) {
        return;
      }
      
      var runner = Runner;

      if ((object)runner == null || runner.IsRunning == false || !runner.GetVisible()) {
        return;
      }
      
      var datas  = _reusableGizmoData ??= new List<(Vector3 center, Vector3 size, int playerCount, int objectCount)>();
      var colors = Customization;

      runner.GetAreaOfInterestGizmoData(datas);

      for (int i = 0; i < datas.Count; i++) {
        var data = datas[i];
        var c    = datas[i].center;
        var s    = datas[i].size;

        // Draw server actives zone boxes
        if (data.objectCount > 0) {
          Gizmos.color = colors.ServerZonesColor;
          Gizmos.DrawWireCube(data.center, data.size);
        }

        // Draw player interest regions
        if (data.playerCount > 0) {
          Gizmos.color = colors.PlayerInterestColor;
          Gizmos.DrawCube(c, s);
        }
      }
      Gizmos.color = Color.white;
    }
#endif
  }
}