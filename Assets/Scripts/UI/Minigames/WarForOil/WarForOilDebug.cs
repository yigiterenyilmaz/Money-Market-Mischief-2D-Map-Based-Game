using UnityEngine;
using UnityEngine.InputSystem;

public class WarForOilDebug : MonoBehaviour
{
    [Header("Quick Access")]
    public WarForOilDatabase database;
    public GameObject uiRoot;

    private WarForOilManager manager;

    void Start()
    {
        manager = WarForOilManager.Instance;
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // Use number keys instead of F-keys
        if (Keyboard.current.digit1Key.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
        {
            ToggleUI();
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
        {
            ForceStartWar();
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
        {
            ForceEvent();
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
        {
            ForceResult(true);
        }

        if (Keyboard.current.digit5Key.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
        {
            ForceResult(false);
        }

        if (Keyboard.current.digit6Key.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
        {
            ModifySupport(10f);
        }

        if (Keyboard.current.digit7Key.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
        {
            ModifySupport(-10f);
        }
    }

    [ContextMenu("Toggle UI")]
    public void ToggleUI()
    {
        uiRoot.SetActive(!uiRoot.activeSelf);
        Debug.Log($"[WarForOil Debug] UI {(uiRoot.activeSelf ? "ON" : "OFF")}");
    }

    [ContextMenu("Force Start War (First Country)")]
    public void ForceStartWar()
    {
        if (database.countries == null || database.countries.Count == 0)
        {
            Debug.LogError("[WarForOil Debug] No countries in database!");
            return;
        }

        var country = database.countries[0];
        Debug.Log($"[WarForOil Debug] Forcing war with {country.displayName}");

        manager.SelectCountry(country);

        typeof(WarForOilManager)
            .GetMethod("StartWar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(manager, null);
    }

    [ContextMenu("Force Trigger Event")]
    public void ForceEvent()
    {
        if (manager.GetCurrentState() != WarForOilState.WarProcess)
        {
            Debug.LogWarning("[WarForOil Debug] Not in war process state");
            return;
        }

        typeof(WarForOilManager)
            .GetMethod("TryTriggerWarEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(manager, null);

        Debug.Log("[WarForOil Debug] Forced event trigger");
    }

    [ContextMenu("Force Win")]
    public void ForceWin()
    {
        ForceResult(true);
    }

    [ContextMenu("Force Lose")]
    public void ForceLose()
    {
        ForceResult(false);
    }

    public void ForceResult(bool win)
    {
        if (manager.GetCurrentState() != WarForOilState.WarProcess)
        {
            Debug.LogWarning("[WarForOil Debug] Not in war process state");
            return;
        }

        var supportField = typeof(WarForOilManager)
            .GetField("supportStat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        supportField?.SetValue(manager, win ? 100f : 0f);

        typeof(WarForOilManager)
            .GetMethod("CalculateWarResult", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(manager, null);

        Debug.Log($"[WarForOil Debug] Forced {(win ? "WIN" : "LOSE")}");
    }

    [ContextMenu("Add Support +10")]
    public void AddSupport()
    {
        ModifySupport(10f);
    }

    [ContextMenu("Remove Support -10")]
    public void RemoveSupport()
    {
        ModifySupport(-10f);
    }

    public void ModifySupport(float amount)
    {
        var supportField = typeof(WarForOilManager)
            .GetField("supportStat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (supportField == null) return;

        float current = (float)supportField.GetValue(manager);
        float newValue = Mathf.Clamp(current + amount, 0f, 100f);
        supportField.SetValue(manager, newValue);

        Debug.Log($"[WarForOil Debug] Support: {current:F0} → {newValue:F0}");
    }

    [ContextMenu("Log Current State")]
    public void LogState()
    {
        Debug.Log($"[WarForOil Debug] State: {manager.GetCurrentState()}");
        Debug.Log($"[WarForOil Debug] Support: {manager.GetSupportStat():F0}%");
        Debug.Log($"[WarForOil Debug] Progress: {manager.GetWarProgress():P0}");
        Debug.Log($"[WarForOil Debug] Disabled: {manager.IsPermanentlyDisabled()}");
        Debug.Log($"[WarForOil Debug] Country: {manager.GetSelectedCountry()?.displayName ?? "None"}");
    }

    [ContextMenu("Reset Permanent Disable")]
    public void ResetDisable()
    {
        var field = typeof(WarForOilManager)
            .GetField("permanentlyDisabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(manager, false);

        Debug.Log("[WarForOil Debug] Reset permanent disable flag");
    }

    void OnGUI()
    {
        if (manager == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 320, 220));
        GUI.color = Color.black;
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUI.color = Color.white;
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(15, 15, 310, 210));
        GUILayout.Label("<color=yellow><b>War For Oil Debug</b></color>");
        GUILayout.Label($"State: {manager.GetCurrentState()}");
        GUILayout.Label($"Support: {manager.GetSupportStat():F0}%");
        GUILayout.Label($"Progress: {manager.GetWarProgress():P0}");
        GUILayout.Label($"Disabled: {manager.IsPermanentlyDisabled()}");
        GUILayout.Space(5);
        GUILayout.Label("<color=cyan>Shift+1</color> Toggle UI");
        GUILayout.Label("<color=cyan>Shift+2</color> Force War");
        GUILayout.Label("<color=cyan>Shift+3</color> Force Event");
        GUILayout.Label("<color=cyan>Shift+4</color> Win | <color=cyan>Shift+5</color> Lose");
        GUILayout.Label("<color=cyan>Shift+6</color> +Support | <color=cyan>Shift+7</color> -Support");
        GUILayout.EndArea();
    }
}