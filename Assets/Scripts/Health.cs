using System;
using Fusion;
using UnityEngine;

public class Health : NetworkBehaviour
{
    private ChangeDetector _changeDetector;
    
    [Networked]
    public float NetworkedHealth { get; set; } = 100;
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void DealDamageRpc(float damage)
    {
        // The code inside here will run on the client which owns this object (has state and input authority).
        Debug.Log("Received DealDamageRpc on StateAuthority, modifying Networked variable");
        NetworkedHealth -= damage;
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    private void Update()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(NetworkedHealth):
                    // Here you would add code to update the player's healthbar.
                    Debug.Log($"Health changed to: {NetworkedHealth}");
                    break;
            }
        }
    }
}