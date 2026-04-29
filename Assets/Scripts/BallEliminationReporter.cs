using System;
using UnityEngine;

public static class BallEliminationEvents
{
    public static event Action<float, string> OnBallEliminated;

    public static void Raise(float timeNow, string ballName)
    {
        OnBallEliminated?.Invoke(timeNow, ballName);
    }
}

public class BallEliminationReporter : MonoBehaviour
{
    private bool _quitting;
    private bool _playerEliminated;

    public void MarkPlayerEliminated()
    {
        _playerEliminated = true;
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
    }

    private void OnDestroy()
    {
        if (_quitting) return;
        if (!_playerEliminated) return;
        BallEliminationEvents.Raise(Time.time, gameObject.name);
    }
}
