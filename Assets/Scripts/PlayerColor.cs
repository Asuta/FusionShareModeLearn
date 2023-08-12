using Fusion;
using UnityEngine;

public class PlayerColor : NetworkBehaviour
{
    private ChangeDetector _changeDetector;
    
    public MeshRenderer MeshRenderer;

    [Networked]
    public Color NetworkedColor { get; set; }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Changing the material color here directly does not work since this code is only executed on the client pressing the button and not on every client.
            NetworkedColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);
        }

        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(NetworkedColor):
                    MeshRenderer.material.color = NetworkedColor;
                    break;
            }
        }
    }
}