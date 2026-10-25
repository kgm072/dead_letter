using UnityEngine;

namespace DeadLetter.Interaction
{
    /// <summary>Optional gate for interactables that are only usable during a specific state.</summary>
    public interface IConditionalInteractable
    {
        bool CanInteract(GameObject interactor);
    }
}
