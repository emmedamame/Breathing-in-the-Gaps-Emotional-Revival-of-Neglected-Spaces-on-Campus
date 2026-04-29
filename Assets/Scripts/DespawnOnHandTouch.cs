using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach this to the ball. When an XR interactor (Direct or Ray) hovers/selects it, the ball disappears.
/// If no interactable exists, one XRSimpleInteractable is added automatically.
/// </summary>
public class DespawnOnHandTouch : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private bool despawnOnHover = true;
    [SerializeField] private bool despawnOnSelect = false;

    [Header("Despawn")]
    [SerializeField] private float destroyDelaySeconds = 0f;

    private XRBaseInteractable _interactable;
    private bool _despawned;

    private void Awake()
    {
        var interactables = GetComponents<XRBaseInteractable>();
        if (interactables != null && interactables.Length > 0)
            _interactable = interactables[0];

        if (_interactable == null)
            _interactable = gameObject.AddComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        if (_interactable == null) return;
        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        if (_interactable == null) return;
        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (!despawnOnHover) return;
        TryDespawn();
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (!despawnOnSelect) return;
        TryDespawn();
    }

    private void TryDespawn()
    {
        if (_despawned) return;
        _despawned = true;
        var reporter = GetComponent<BallEliminationReporter>();
        if (reporter != null) reporter.MarkPlayerEliminated();
        Destroy(gameObject, Mathf.Max(0f, destroyDelaySeconds));
    }
}

