using UnityEngine;

public class NRB_DimIfAsleep : MonoBehaviour {
  private Renderer  _renderer;
  private Rigidbody _rb;

  private void Awake() {
    _renderer = GetComponentInChildren<Renderer>();
    _rb       = GetComponent<Rigidbody>();
  }

  private void Update() {

    _renderer.material.color = (_rb.IsSleeping()) ? Color.gray : Color.yellow;
  }
}
