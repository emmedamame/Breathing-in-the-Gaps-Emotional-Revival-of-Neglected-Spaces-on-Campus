using UnityEditor;
using UnityEngine;

public class ClearPlasticSessionState
{
    private const string KEY_NEW = "PlasticSCM.ProcessHubCommand.IsAlreadyExecuted";
    private const string KEY_OLD = "PlasticSCM.ProcessCommand.IsAlreadyExecuted";

    [MenuItem("Tools/Clear Plastic SCM Session State")]
    public static void ClearSessionState()
    {
        EditorPrefs.DeleteKey(KEY_NEW);
        EditorPrefs.DeleteKey(KEY_OLD);
        Debug.Log("Plastic SCM session state cleared. Please restart Unity.");
    }
}
