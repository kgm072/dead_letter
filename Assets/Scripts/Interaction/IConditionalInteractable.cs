using UnityEngine;

namespace DeadLetter.Interaction
{
    /// <summary>Optional gate for interactables that are only usable during a specific state.</summary>
    public interface IConditionalInteractable
    {
        bool CanInteract(GameObject interactor);
    }

    /// <summary>
    /// Allows a disabled/unavailable interactable to yield the interaction ray to a
    /// specifically approved target immediately behind it. This is intentionally
    /// opt-in so walls, ordinary props, and unrelated interactables remain blocked.
    /// </summary>
    public interface IInteractionRaycastPassthrough
    {
        bool AllowsInteractionRaycastPassthrough(MonoBehaviour target);
    }
}
