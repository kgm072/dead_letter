using UnityEngine;

namespace DeadLetter.Interaction
{
    /// <summary>Implemented by scene objects that respond to the player's right-click interaction.</summary>
    public interface IInteractable
    {
        void Interact(GameObject interactor);
    }
}
