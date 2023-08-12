
namespace Fusion.UnityPhysics {
  using System;
  using System.Collections.Generic;
  using Fusion.Sockets;
  using UnityEngine;
  using Random = UnityEngine.Random;

  /// <summary>
  /// A simple example of Fusion input collection. This component should be placed on the same GameObject as the <see cref="NetworkRunner"/>.
  /// </summary>
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Steel)]
  public class NRB_CollectInputs : Fusion.Behaviour, INetworkRunnerCallbacks {

    /// <summary>
    /// If user no key events have been detected, random inputs will be generated after this many seconds. 0 is disabled.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    public float GenerateInputsAfter;

    private float _timeOfLastPlayerInput;
    private float _moveRemainder;
    private float _strafeRemainder;
    private float _actionRemainder;

    private void Start() {
      _timeOfLastPlayerInput = Time.time;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) {
      
      var inputs = new NRB_NetworkInput();

      if (Input.GetKey(KeyCode.W)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_FORWARD, true);
      }

      if (Input.GetKey(KeyCode.S)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_BACKWARD, true);
      }

      if (Input.GetKey(KeyCode.A)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_LEFT, true);
      }

      if (Input.GetKey(KeyCode.D)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_RIGHT, true);
      }

      if (Input.GetKey(KeyCode.Space)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_JUMP, true);
      }

      if (Input.GetKey(KeyCode.C)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_CROUCH, true);
      }

      if (Input.GetKey(KeyCode.E)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_ACTION1, true);
      }

      if (Input.GetKey(KeyCode.Q)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_ACTION2, true);
      }

      if (Input.GetKey(KeyCode.F)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_ACTION3, true);
      }

      if (Input.GetKey(KeyCode.G)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_ACTION4, true);
      }
      
      if (Input.GetKey(KeyCode.T)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_ACTION5, true);
      }

      if (Input.GetKey(KeyCode.R)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_RELOAD, true);
      }

      if (Input.GetMouseButton(0)) {
        inputs.Buttons.Set(NRB_NetworkInput.BUTTON_FIRE, true);
      }

      if (GenerateInputsAfter > 0) {
        inputs = GenerateInput(runner, inputs);
      }

      input.Set(inputs);
    }

    private NRB_NetworkInput GenerateInput(NetworkRunner runner, NRB_NetworkInput inputs) {
        if (!inputs.Equals(default)) {
          _timeOfLastPlayerInput = Time.time;
          
        } else if (Time.time - _timeOfLastPlayerInput > GenerateInputsAfter) {
          if (_moveRemainder == 0) {
            _moveRemainder = Random.Range(-2f, 2f);
          }
          if (_strafeRemainder == 0) {
            _strafeRemainder = Random.Range(-2f, 2f);
          }
          if (_actionRemainder == 0) {
            _actionRemainder = Random.Range(-2f, 2f);
          }

          var dt = runner.DeltaTime;
          if (_moveRemainder > 0) {
            inputs.Buttons.Set(NRB_NetworkInput.BUTTON_FORWARD, true);
            _moveRemainder -= dt;
            if (_moveRemainder < 0) _moveRemainder = 0;
          } else {
            inputs.Buttons.Set(NRB_NetworkInput.BUTTON_BACKWARD, true);
            _moveRemainder += dt;
            if (_moveRemainder > 0) _moveRemainder = 0;
          }
          
          if (_strafeRemainder > 0) {
            inputs.Buttons.Set(NRB_NetworkInput.BUTTON_LEFT, true);
            _strafeRemainder -= dt;
            if (_strafeRemainder < 0) _strafeRemainder = 0;
          } else {
            inputs.Buttons.Set(NRB_NetworkInput.BUTTON_RIGHT, true);
            _strafeRemainder += dt;
            if (_strafeRemainder > 0) _strafeRemainder = 0;
          }
          
          if (_actionRemainder > 0) {
            inputs.Buttons.Set(NRB_NetworkInput.BUTTON_ACTION1, true);
            _actionRemainder -= dt;
            if (_actionRemainder < 0) _actionRemainder = 0;
          } else {
            inputs.Buttons.Set(NRB_NetworkInput.BUTTON_ACTION2, true);
            _actionRemainder += dt;
            if (_actionRemainder > 0) _actionRemainder = 0;
          }

          inputs.Buttons.Set(NRB_NetworkInput.BUTTON_RELOAD, Random.value > .99f);
          inputs.Buttons.Set(NRB_NetworkInput.BUTTON_JUMP,   Random.value > .99f);
          inputs.Buttons.Set(NRB_NetworkInput.BUTTON_FIRE,   Random.value > .95f);

        }

        return inputs;
    }
  
    #region Unused Callbacks
    
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner      runner)                                                                                        { }
    public void OnConnectFailed(NetworkRunner          runner, NetAddress                               remoteAddress, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner         runner, NetworkRunnerCallbackArgs.ConnectRequest request,       byte[]                 token)  { }
    public void OnDisconnectedFromServer(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner,  NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerJoined(NetworkRunner           runner, PlayerRef            player)         { }
    public void OnPlayerLeft(NetworkRunner             runner, PlayerRef            player)         { }
    public void OnUserSimulationMessage(NetworkRunner  runner, SimulationMessagePtr message)        { }
    public void OnShutdown(NetworkRunner               runner, ShutdownReason       shutdownReason) { }
    public void OnSessionListUpdated(NetworkRunner     runner, List<SessionInfo>    sessionList)    { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
  
    #endregion
  }

  /// <summary>
  /// Example definition of an INetworkStruct.
  /// </summary>
  public struct NRB_NetworkInput : INetworkInput, IEquatable<NRB_NetworkInput> {

    public const int BUTTON_USE = 0;
    public const int BUTTON_FIRE = 1;
    public const int BUTTON_FIRE_ALT = 2;

    public const int BUTTON_FORWARD = 3;
    public const int BUTTON_BACKWARD = 4;
    public const int BUTTON_LEFT = 5;
    public const int BUTTON_RIGHT = 6;

    public const int BUTTON_JUMP = 7;
    public const int BUTTON_CROUCH = 8;
    public const int BUTTON_WALK = 9;

    public const int BUTTON_ACTION1 = 10;
    public const int BUTTON_ACTION2 = 11;
    public const int BUTTON_ACTION3 = 12;
    public const int BUTTON_ACTION4 = 14;
    public const int BUTTON_ACTION5 = 15;

    public const int BUTTON_RELOAD = 16;

    public NetworkButtons Buttons;
    public byte Weapon;
    public Angle Yaw;
    public Angle Pitch;

    public bool IsUp(int button) {
      return Buttons.IsSet(button) == false;
    }

    public bool IsDown(int button) {
      return Buttons.IsSet(button);
    }

    public bool Equals(NRB_NetworkInput other)
    {
      return Buttons.Equals(other.Buttons) && Weapon == other.Weapon && Yaw.Equals(other.Yaw) && Pitch.Equals(other.Pitch);
    }

    public override bool Equals(object obj)
    {
      return obj is NRB_NetworkInput other && Equals(other);
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(Buttons, Weapon, Yaw, Pitch);
    }
  }
}