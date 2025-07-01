using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Singleton
    {
        get => _singleton;
        set
        {
            if (value == null)
                _singleton = null;
            else if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Destroy(value);
                Debug.LogError($"There should only ever be one instance of {nameof(CameraFollow)}!");
            }
        }
    }
    private static CameraFollow _singleton;

    [SerializeField] private Highlighter highlighter;

    private Transform target;
    private Player player;

    private void Awake()
    {
        // 1. Check if a Singleton already exists
        if (Singleton != null && Singleton != this)
        {
            // If another instance already exists and it's not this one, destroy this new one.
            // This handles cases where you might accidentally have two in the same scene,
            // or if a new scene loads one.
            Destroy(this.gameObject);
            Debug.LogWarning($"Duplicate {nameof(CameraFollow)} detected. Destroying new instance: {gameObject.name}");
            return;
        }

        // 2. If no Singleton exists, or this is the existing Singleton being re-Awakened (e.g., after a domain reload),
        // assign this instance as the Singleton.
        if (Singleton == null)
        {
            Singleton = this;
            // 3. Make this GameObject persistent across scene loads
            DontDestroyOnLoad(gameObject);
            Debug.Log($"Set {nameof(CameraFollow)} Singleton: {gameObject.name}");
        }
        else
        {
            // This case should ideally not happen if DontDestroyOnLoad works as expected
            // but is a safeguard if the Singleton was set, but then Destroyed, and this Awake runs before a new one is made.
            // Or if domain reloads are messing with static state.
            Debug.LogWarning($"Awake called on existing {nameof(CameraFollow)} Singleton: {gameObject.name}");
        }
    }

    private void OnDestroy()
    {
        if (Singleton == this)
            Singleton = null;
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.SetPositionAndRotation(target.position, target.rotation);
            highlighter.UpdateHightlightable(transform.position, transform.forward, player);
        }
    }

    public void SetTarget(Transform newTarget, Player player)
    {
        target = newTarget;
        this.player = player;
    }
}
