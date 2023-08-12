namespace Fusion.Editor {
#if FUSION_WEAVER && UNITY_EDITOR
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;
  using EditorUtility = UnityEditor.EditorUtility;

  [InitializeOnLoad]
  public partial class FusionHubWindow : EditorWindow {

    const int NAV_WIDTH = 256 + 2;

    private static bool? ready; // true after InitContent(), reset onDestroy, onEnable, etc.

    private static Vector2 windowSize;
    private static Vector2 windowPosition = new Vector2(100, 100);

    int currentSection;
    // Indicates that the AppId is invalid and needs to be presented on the welcome screen.
    static bool _showAppIdInWelcome;

    [MenuItem("Fusion/Fusion Hub &f", false, 0)]
    public static void Open() {
      if (Application.isPlaying) {
        return;
      }

      FusionHubWindow window = GetWindow<FusionHubWindow>(true, WINDOW_TITLE, true);
      window.position = new Rect(windowPosition, windowSize);
      _showAppIdInWelcome = !IsAppIdValid();
      window.Show();
    }

    private static void ReOpen() {
      if (ready.HasValue && ready.Value == false) {
        Open();
      }

      EditorApplication.update -= ReOpen;
    }


    private void OnEnable() {
      ready = false;
      windowSize = new Vector2(800, 540);

      this.minSize = windowSize;

      // Pre-load Release History
      this.PrepareReleaseHistoryText();
      wantsMouseMove = true;
    }

    private void OnDestroy() {
      ready = false;
    }

    private void OnGUI() {

      GUI.skin = FusionHubSkin;

      try {
        InitContent();

        windowPosition = this.position.position;

        // full window wrapper
        EditorGUILayout.BeginHorizontal(GUI.skin.window);
        {
          // Left Nav menu
          EditorGUILayout.BeginVertical(GUILayout.MaxWidth(NAV_WIDTH), GUILayout.MinWidth(NAV_WIDTH));
          DrawHeader();
          DrawLeftNavMenu();
          EditorGUILayout.EndVertical();

          // Right Main Content
          EditorGUILayout.BeginVertical();
          DrawContent();
          EditorGUILayout.EndVertical();

        }
        EditorGUILayout.EndHorizontal();

        DrawFooter();

      } catch (Exception) {
        // ignored
      }

      // Force repaints while mouse is over the window, to keep Hover graphics working (Unity quirk)
      var timeSinceStartup = Time.realtimeSinceStartupAsDouble;
      if (Event.current.type == EventType.MouseMove && timeSinceStartup > _nextForceRepaint) {
        // Cap the repaint rate a bit since we are forcing repaint on mouse move
        _nextForceRepaint = timeSinceStartup + .05f;
        Repaint();
      }
    }

    private double _nextForceRepaint;
    private Vector2 _scrollRect;

    private void DrawContent() {
      {
        var section = _sections[currentSection];
        GUILayout.Label(section.Description, headerTextStyle);

        EditorGUILayout.BeginVertical(FusionHubSkin.box);
        _scrollRect = EditorGUILayout.BeginScrollView(_scrollRect);
        section.DrawMethod.Invoke();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
      }
    }

    void DrawWelcomeSection() {

      // Top Welcome content box
      GUILayout.Label(WELCOME_TEXT);
      GUILayout.Space(16);

      if (_showAppIdInWelcome)
        DrawSetupAppIdBox();
    }

    void DrawSetupSection() {
      DrawSetupAppIdBox();
      DrawButtonAction(Icon.FusionIcon, "Fusion Network Project Settings", "Network settings specific to Fusion.", 
        callback: () => NetworkProjectConfigUtilities.PingGlobalConfigAsset(true));
      DrawButtonAction(Icon.PhotonCloud, "Photon App Settings", "Network settings specific to the Photon transport.",
        callback: () => { EditorGUIUtility.PingObject(Photon.Realtime.PhotonAppSettings.Instance); Selection.activeObject = Photon.Realtime.PhotonAppSettings.Instance; });

    }

    void DrawDocumentationSection() {
      DrawButtonAction(Icon.Documentation, "Fusion Introduction", "The Fusion Introduction web page.", callback: OpenURL(UrlFusionIntro));
      DrawButtonAction(Icon.Documentation, "SDK and Release Notes", "Link to the latest Fusion version SDK.", callback: OpenURL(UrlFusionSDK));
      DrawButtonAction(Icon.Documentation, "API Reference", "The API library reference documentation.", callback: OpenURL(UrlFusionDocApi));
    }

    void DrawSamplesSection() {

      GUILayout.Label("Tutorials", headerLabelStyle);
      DrawButtonAction(Icon.Samples, "Fusion 100 Tutorial", "Fusion Fundamentals Tutorial", callback: OpenURL(UrlFusion100));
      
      //GUILayout.Label("Samples", headerLabelStyle);
      //DrawButtonAction(Icon.Samples, "Fusion Samples", "Collection of Demos and Tech Samples", callback: OpenURL(UrlSampleSection));

      // Hidden for now
      /*
      DrawButtonAction(Icon.Samples, "Fusion 100 Tutorial", "Fusion Fundamentals", callback: OpenURL(UrlFusion100));
      DrawButtonAction(Icon.Samples, "Fusion Application Loop", "Matchmaking, Room Creation, Scene Loading, and Shutdown", callback: OpenURL(UrlFusionLoop));

      GUILayout.Label("Samples", headerLabelStyle);
      //DrawButtonAction(Resources.Load<Texture2D>("FusionHubSampleIcons/tanknarok-logo"), "Fusion Tanknarok", callback: OpenURL(UrlTanks));
      DrawButtonAction(Icon.Samples, "Tanknarok", "Vehicle Control, and Predicted Projectile Spawns", callback: OpenURL(UrlTanks));
      DrawButtonAction(Icon.Samples, "Fusion Karts", "Advanced Player Rigidbody Prediction", callback: OpenURL(UrlKarts));
      DrawButtonAction(Icon.Samples, "DragonHunters VR", "VR Movement, and Object Manipulation", callback: OpenURL(UrlDragonHuntersVR));
      GUILayout.Space(15);
      */

      //DrawButtonAction(Icon.Samples, "Hello Fusion Demo", callback: OpenURL(UrlHelloFusion));
      //DrawButtonAction(Icon.Samples, "Hello Fusion VR Demo", callback: OpenURL(UrlHelloFusionVr));
    }

    void DrawRealtimeReleaseSection() {
      GUILayout.BeginVertical();
      {
        GUILayout.Space(5);

        DrawReleaseHistoryItem("Added:", releaseHistoryTextAdded);
        DrawReleaseHistoryItem("Changed:", releaseHistoryTextChanged);
        DrawReleaseHistoryItem("Fixed:", releaseHistoryTextFixed);
        DrawReleaseHistoryItem("Removed:", releaseHistoryTextRemoved);
        DrawReleaseHistoryItem("Internal:", releaseHistoryTextInternal);
      }
      GUILayout.EndVertical();
    }

    void DrawFusionReleaseSection() {
      GUILayout.Label(fusionReleaseHistory, releaseNotesStyle);
    }

    void DrawReleaseHistoryItem(string label, List<string> items) {
      if (items != null && items.Count > 0) {
        GUILayout.BeginVertical();
        {
          GUILayout.Space(5);

          foreach (string text in items) {
            GUILayout.Label(string.Format("- {0}.", text), textLabelStyle);
          }
        }
        GUILayout.EndVertical();
      }
    }

    void DrawSupportSection() {

      GUILayout.BeginVertical();
      GUILayout.Space(5);
      GUILayout.Label(SUPPORT, textLabelStyle);
      GUILayout.EndVertical();

      GUILayout.Space(15);

      DrawButtonAction(Icon.Community, DISCORD_HEADER, DISCORD_TEXT, callback: OpenURL(UrlDiscordGeneral));
      DrawButtonAction(Icon.Documentation, DOCUMENTATION_HEADER, DOCUMENTATION_TEXT, callback: OpenURL(UrlFusionDocsOnline));
    }

    void DrawSetupAppIdBox() {
      var realtimeSettings = Photon.Realtime.PhotonAppSettings.Instance;
      var realtimeAppId = realtimeSettings.AppSettings.AppIdFusion;
      // Setting up AppId content box.
      EditorGUILayout.BeginVertical(FusionHubSkin.GetStyle("SteelBox") /*contentBoxStyle*/) ;
      {
        GUILayout.Label(REALTIME_APPID_SETUP_INSTRUCTIONS);

        DrawButtonAction(Icon.PhotonCloud, "Open the Photon Dashboard", callback: OpenURL(UrlDashboard));
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal(FusionHubSkin.GetStyle("SteelBox"));
        {
          EditorGUI.BeginChangeCheck();
          GUILayout.Label("Fusion App Id:", GUILayout.Width(120));
          var icon = IsAppIdValid() ? CorrectIcon : EditorGUIUtility.FindTexture("console.erroricon.sml");
          GUILayout.Label(icon, GUILayout.Width(24), GUILayout.Height(24));
          var editedAppId = EditorGUILayout.DelayedTextField("", realtimeAppId, FusionHubSkin.textField, GUILayout.Height(24));
          if (EditorGUI.EndChangeCheck()) {
            realtimeSettings.AppSettings.AppIdFusion = editedAppId;
            EditorUtility.SetDirty(realtimeSettings);
            AssetDatabase.SaveAssets();
          }
        }
        EditorGUILayout.EndHorizontal();
      }
      EditorGUILayout.EndVertical();
    }

    void DrawLeftNavMenu() {
      for (int i = 0; i < _sections.Length; ++i) {
        var section = _sections[i];
        if (DrawNavButton(section, currentSection == i)) {
          // Check if appid is valid whenever we change sections. It no longer needs to be shown on welcome page once it is set.
          _showAppIdInWelcome = !IsAppIdValid();
          currentSection = i;
        }
      }
    }

    void DrawHeader() {
      GUILayout.Label(GetIcon(Icon.ProductLogo), _navbarHeaderGraphicStyle);
    }

    void DrawFooter() {
      GUILayout.BeginHorizontal(FusionHubSkin.window);
      {
        GUILayout.Label("\u00A9 2022, Exit Games GmbH. All rights reserved.");
      }
      GUILayout.EndHorizontal();
    }

    bool DrawNavButton(Section section, bool currentSection) {
      var content = new GUIContent() {
        text  = "  " + section.Title,
        image = GetIcon(section.Icon),
      };

      var renderStyle = currentSection ? buttonActiveStyle : GUI.skin.button;
      return GUILayout.Button(content, renderStyle);
    }

    void DrawButtonAction(Icon icon, string header, string description = null, bool? active = null, Action callback = null, int? width = null) {
      DrawButtonAction(GetIcon(icon), header, description, active, callback, width);
    }

    static void DrawButtonAction(Texture2D icon, string header, string description = null, bool? active = null, Action callback = null, int? width = null) {

      var padding = GUI.skin.button.padding.top + GUI.skin.button.padding.bottom;
      var height = icon.height + padding;

      var renderStyle = active.HasValue && active.Value == true ? buttonActiveStyle : GUI.skin.button;
      // Draw text separately (not part of button guiconent) to have control over the space between the icon and the text.
      var rect = EditorGUILayout.GetControlRect(false, height, width.HasValue ? GUILayout.Width(width.Value) : GUILayout.ExpandWidth(true));
      bool clicked = GUI.Button(rect, icon, renderStyle);
      GUI.Label(new Rect(rect) { xMin = rect.xMin + icon.width + 20 }, description == null ? "<b>" + header +"</b>" : string.Format("<b>{0}</b>\n{1}", header, "<color=#aaaaaa>" + description + "</color>"));
      if (clicked && callback != null) {
        callback.Invoke();
      }
    }
  }
#endif
}