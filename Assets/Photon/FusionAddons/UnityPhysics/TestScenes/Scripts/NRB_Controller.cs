namespace Fusion.UnityPhysics {
  using UnityEngine;


  [ScriptHelp(BackColor = ScriptHeaderBackColor.Steel)]
  public class NRB_Controller : Fusion.NetworkBehaviour {
    protected Rigidbody _rigidBody;
    protected Rigidbody2D _rigidbody2D;
    protected NetworkTransform _nt;

    // [Networked]
    // public Vector3 MovementDirection { get; set; }

    public bool TransformLocal = false;

    // [DrawIf(nameof(ShowSpeed), Hide = true)]
    public float Speed = 16f;

    // bool ShowSpeed => this && !TryGetComponent<NetworkCharacterController>(out _);

    public void Awake() {
      CacheComponents();
    }

    public override void Spawned() {
      CacheComponents();
    }

    private void CacheComponents() {
      if (!_rigidBody) _rigidBody = GetComponent<Rigidbody>();
      if (!_rigidbody2D) _rigidbody2D = GetComponent<Rigidbody2D>();
      if (!_nt) _nt = GetComponent<NetworkTransform>();
    }

    public override void FixedUpdateNetwork() {

      Vector3 direction;
      if (GetInput(out NRB_NetworkInput input)) {
        direction = default;

        if (input.IsDown(NRB_NetworkInput.BUTTON_FORWARD)) {
          direction += TransformLocal ? transform.forward : Vector3.forward;
        }

        if (input.IsDown(NRB_NetworkInput.BUTTON_BACKWARD)) {
          direction -= TransformLocal ? transform.forward : Vector3.forward;
        }

        if (input.IsDown(NRB_NetworkInput.BUTTON_LEFT)) {
          direction -= TransformLocal ? transform.right : Vector3.right;
        }

        if (input.IsDown(NRB_NetworkInput.BUTTON_RIGHT)) {
          direction += TransformLocal ? transform.right : Vector3.right;
        }
        
        if (direction == default) {
          return;
        }
        
        direction = direction.normalized;

        // MovementDirection = direction;

        if (input.IsDown(NRB_NetworkInput.BUTTON_JUMP)) {
          direction += (TransformLocal ? transform.up : Vector3.up);
        }
      } else {
        return;
      }

      if (direction == default) {
        return;
      } 
      
      if (_rigidBody && !_rigidBody.isKinematic) {
        Debug.Log($"Applying Force");
        _rigidBody.AddForce(direction * Speed);
      } 
      
      else if (_rigidbody2D && !_rigidbody2D.isKinematic) {
        Vector2 direction2d = new Vector2(direction.x, direction.y + direction.z);
        _rigidbody2D.AddForce(direction2d * Speed);
      } 
      
      else {
        transform.position += (direction * Speed * Runner.DeltaTime);
      }
    }
  }
}
