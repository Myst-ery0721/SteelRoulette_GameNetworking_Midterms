using Unity.Netcode.Components;

// Lets each player move their own character (Write: Owner).
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}