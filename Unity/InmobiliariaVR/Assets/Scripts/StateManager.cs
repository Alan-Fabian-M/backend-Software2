using UnityEngine;

// Maneja el estado global de la app (recorrido, personalizacion, pintura, menu)
// para que distintos scripts (movimiento, pintura de paredes, menus) sepan
// cuando deben activarse o desactivarse. Basado en el patron de StateManager
// de Room Designer (TeamFWS), adaptado sin dependencias de Meta XR.
public class StateManager : MonoBehaviour
{
    public delegate void OnStateChange(AppState newState);
    public event OnStateChange StateChanged;

    private static StateManager instance;
    private AppState previousState;

    public static StateManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<StateManager>();

                if (instance == null)
                {
                    GameObject singletonObject = new GameObject("StateManager");
                    instance = singletonObject.AddComponent<StateManager>();
                    DontDestroyOnLoad(singletonObject);
                }
            }

            return instance;
        }
    }

    public AppState CurrentState { get; private set; } = AppState.Recorrido;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void ChangeState(AppState newState)
    {
        if (CurrentState != newState)
        {
            previousState = CurrentState;
            CurrentState = newState;
            StateChanged?.Invoke(newState);
        }
    }

    public void RevertPreviousState()
    {
        ChangeState(previousState);
    }
}
