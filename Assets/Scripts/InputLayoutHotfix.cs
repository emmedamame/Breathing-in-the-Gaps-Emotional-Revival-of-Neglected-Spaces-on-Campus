using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

public static class InputLayoutHotfix
{
    private static bool installed;

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InstallInEditor()
    {
        Install();
    }
#endif

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallAtRuntime()
    {
        Install();
    }

    private static void Install()
    {
        if (installed)
        {
            return;
        }

        InputSystem.onFindLayoutForDevice += RemapXInputLayout;
        installed = true;
    }

    private static string RemapXInputLayout(
        ref InputDeviceDescription description,
        string matchedLayout,
        InputDeviceExecuteCommandDelegate executeDeviceCommand)
    {
        if (description.interfaceName == "XInput")
        {
            return "Gamepad";
        }

        return null;
    }
}
