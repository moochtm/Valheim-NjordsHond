using BepInEx.Logging;
using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib.Tools;
using TMPro;
using System.Globalization;

/*
    TODO:
    - Add ModVersion to loading log message
*/


internal static class ModInfo
{
    public const string ModGUID = "com.scoobymooch.njords_hond";
    public const string ModName = "Njords Hond";
    public const string ModVersion = "0.1.0";
}

// Ensure dependency on ShipNavigator
[BepInDependency("JustCrazy.Valheim.ShipNavigator")]
[BepInPlugin(ModInfo.ModGUID, ModInfo.ModName, ModInfo.ModVersion)]
public class VegvisirPlugin : BaseUnityPlugin
{
    public static bool AutoPilotEnabled { get; private set; } = false;
    internal static bool _autoSetSailSpeed = true;

    private readonly Harmony _harmony = new Harmony(ModInfo.ModGUID);

    private FieldInfo steerDirectionField;

    private float _updateTimer = 0f;
    private static List<Vector3> _waypointTargets = new List<Vector3>();
    private static List<string> _waypointNames = new List<string>();
    private static int _currentWaypointIndex = 0;
    private static int _lastMessageWaypointIndex = -1;
    private bool _previousIsAutoSteerOn;

    private void Awake()
    {
        Log.CreateInstance(Logger);
        Log.Info($"Initializing mod. Version: {ModInfo.ModVersion}");

        _harmony.PatchAll();

        // Attempt to acquire steerDirectionField from ShipNavigator.Patches.Ship_Patch
        Type shipPatchType = AccessTools.TypeByName("ShipNavigator.Patches.Ship_Patch");
        if (shipPatchType != null)
        {
            steerDirectionField = shipPatchType.GetField("steerDirection", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (steerDirectionField == null)
                Log.Warning("Could not find 'steerDirection' field.");
            else
                Log.Info("steerDirectionField successfully acquired.");
        }
        else
        {
            Log.Warning("Could not find Ship_Patch type.");
        }
    }


    private string GetPath(Transform current)
    {
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.parent.name + "/" + path;
        }
        return path;
    }


    private void OnEnable()
    {
    }

    private void OnDisable()
    {
    }

    private void OnDestroy()
    {
        _harmony.UnpatchSelf();
        Log.Info("Unpatching mod.");
    }


    private void Update()
    {
        // Update once per second
        _updateTimer += Time.deltaTime;
        if (_updateTimer < 0.5f) return;
        _updateTimer = 0f;

        // Only run mod logic if the player is controlling a ship
        if (Player.m_localPlayer?.GetControlledShip() == null)
        {
            Log.Debug("Skipping update: player is not controlling a ship.");
            return;
        }

        // Check if ShipNavigator mod auto-steer is on
        bool isAutoSteerOn = false;
        Type shipPatchType = AccessTools.TypeByName("ShipNavigator.Patches.Ship_Patch");
        FieldInfo autoField = shipPatchType?.GetField("isAutoSteerOn", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (autoField != null)
        {
            isAutoSteerOn = (bool)autoField.GetValue(null);
            Log.Debug($"Auto-steer is {(isAutoSteerOn ? "ON" : "OFF")}");
        }

        // Set the HUD ship icon color to reflect mod/auto-steer state
        if (!isAutoSteerOn)
        {
            Log.Debug("ShipNavigator steering set to manual. AutoPilot set to off");
            if ( _previousIsAutoSteerOn) Log.Info("ShipNavigator steering set to manual. AutoPilot set to off");
            _previousIsAutoSteerOn = false;
            AutoPilotEnabled = false;
            ShipHudIcon.SetColor(new Color32(0xFF, 0xBB, 0x42, 0xFF)); // FFBB42 default
            return;
        }
        else if (!AutoPilotEnabled)
        {
            ShipHudIcon.SetColor(new Color32(0x22, 0x85, 0xC8, 0xFF)); // 2285C8
        }
        else
        {
            ShipHudIcon.SetColor(new Color32(0x96, 0xAB, 0x5B, 0xFF)); // 96AB5B
        }
        _previousIsAutoSteerOn = true;

        // Check if autopilot is on
        if (!AutoPilotEnabled)
        {
            Log.Debug("AutoPilot is OFF");
            return;
        }

        // Check if autopilot is on, and player is controlling a ship.
        if (AutoPilotEnabled && Player.m_localPlayer != null && Player.m_localPlayer.GetControlledShip() is Ship ship)
        {
            // If auto-set sail speed is enabled
            if (_autoSetSailSpeed)
            {
                float windAngle = ship.GetWindAngle(); // returns 0° = wind from behind, 180° = headwind
                float normalisedWindAngle = Mathf.DeltaAngle(0f, windAngle);
                bool headToWind = Mathf.Abs(Mathf.DeltaAngle(180f, windAngle)) <= 45f;

                Log.Debug($"Ship.GetWindAngle() = {windAngle:F1}° (normalized: {normalisedWindAngle:F1}°) → {(headToWind ? "Head to wind" : "Not head to wind")}");

                // Auto-set ship speed based on wind angle and waypoint count
                if (ship.IsOwner())
                {
                    var speedField = AccessTools.Field(ship.GetType(), "m_speed");
                    if (speedField != null)
                    {
                        var currentSpeed = (Ship.Speed)speedField.GetValue(ship);
                        var desiredSpeed = (_waypointTargets.Count == 0)
                            ? Ship.Speed.Stop
                            : (headToWind ? Ship.Speed.Slow : Ship.Speed.Full);
                        if (currentSpeed != desiredSpeed)
                        {
                            speedField.SetValue(ship, desiredSpeed);
                            Log.Debug($"Auto-set ship speed to {desiredSpeed} due to wind angle and waypoint count.");
                        }
                    }
                }
            }
        }

        if (AutoPilotEnabled)
        {
            if (!isAutoSteerOn) Log.Debug("Not steering: Auto-steer is OFF");
            else if (_waypointTargets.Count == 0) Log.Debug("Not steering: No waypoint targets");
            else if (steerDirectionField == null) Log.Debug("Not steering: steerDirectionField is NULL");
            else if (Player.m_localPlayer == null) Log.Debug("Not steering: local player is NULL");
            else
            {
                Vector3 target = _waypointTargets[_currentWaypointIndex];
                Log.Debug($"Target position: {target}, Player position: {Player.m_localPlayer.transform.position}");
                Vector3 toTarget = target - Player.m_localPlayer.transform.position;
                toTarget.y = 0;
                Log.Debug($"Vector to target (flattened): {toTarget}");
                float dist = toTarget.magnitude;
                Log.Debug($"Distance to target: {dist}");
                Log.Debug($"Current waypoint #{_currentWaypointIndex + 1}, distance: {dist:F1}m");

                if (dist < 10f)
                {
                    _currentWaypointIndex++;
                    if (_currentWaypointIndex >= _waypointTargets.Count)
                    {
                        Log.Info("Course complete.");

                        if (Player.m_localPlayer != null && Player.m_localPlayer.GetControlledShip() is Ship controlledShip && controlledShip.IsOwner())
                        {
                            var speedField = AccessTools.Field(controlledShip.GetType(), "m_speed");
                            if (speedField != null)
                            {
                                speedField.SetValue(controlledShip, Ship.Speed.Stop);
                                Log.Info("Final waypoint reached. Auto-set ship speed to Stop.");
                            }
                        }

                        Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "You have reached your destination.");
                        _waypointTargets.Clear();
                        _waypointNames.Clear();

                        // Disable ShipNavigator auto-steer at final waypoint
                        try
                        {
                            // shipPatchType already declared earlier in Update(), reuse it here
                            var autoSteerField = shipPatchType?.GetField("isAutoSteerOn", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                            if (autoSteerField != null)
                            {
                                autoSteerField.SetValue(null, false);
                                Log.Info("ShipNavigator auto-steer set to OFF (final waypoint reached).");
                            }
                            else
                            {
                                Log.Warning("Could not find 'isAutoSteerOn' field to disable auto-steer.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Error disabling ShipNavigator auto-steer at final waypoint: " + ex);
                        }

                        return;
                    }
                    target = _waypointTargets[_currentWaypointIndex];
                    toTarget = target - Player.m_localPlayer.transform.position;
                    toTarget.y = 0;
                }
                float headingDeg = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                if (headingDeg < 0f) headingDeg += 360f;
                // Show message only once per waypoint, when first selected or on arrival
                if (_currentWaypointIndex != _lastMessageWaypointIndex)
                {
                    string currentName = (_waypointNames.Count > _currentWaypointIndex) ? _waypointNames[_currentWaypointIndex] : $"#{_currentWaypointIndex + 1}";
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Setting course to new waypoint: {currentName}, {headingDeg:F0}° from north.");
                    Log.Info($"Setting course to new waypoint: {currentName}, {headingDeg:F0}° from north.");
                    _lastMessageWaypointIndex = _currentWaypointIndex;
                }
                toTarget.Normalize();
                Log.Debug($"Normalized direction vector: {toTarget}");
                steerDirectionField.SetValue(null, toTarget);
                Log.Debug($"Updated steerDirection to {toTarget} ({headingDeg:F1}° from north)");
            }
        }
    }

    public static void SetWaypointCourse(List<(string name, Vector3 pos)> targets)
    {
        Log.Info($"Waypoint targets received: {string.Join(", ", targets.Select(t => t.name))}");
        _waypointTargets.Clear();
        _waypointNames.Clear();
        _currentWaypointIndex = 0;
        _lastMessageWaypointIndex = -1;
        if (targets.Count > 0)
        {
            foreach (var t in targets)
            {
                _waypointNames.Add(t.name);
                _waypointTargets.Add(t.pos);
            }
            foreach (var (n, p) in targets)
                Log.Debug($"Waypoint: {n} at {p}");
            Log.Info($"Found {targets.Count} valid pins from input.");
            Log.Info($"Charting course to {_waypointTargets.Count} pins.");
        }
        else
        {
            Log.Warning("No valid pins found for course.");
        }
    }

    public static void ClearWaypointCourse()
    {
        _waypointTargets.Clear();
        _waypointNames.Clear();
        _currentWaypointIndex = 0;
        _lastMessageWaypointIndex = -1;
        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Waypoint course cleared.");
        Log.Info("Waypoint course cleared.");
    }
}


class Log
{
    public enum VegvisirLogLevel { Debug, Info, Warning, Error, Fatal }

    public static VegvisirLogLevel LogLevel { get; set; } = VegvisirLogLevel.Info;

    private static Log _instance;

    private ManualLogSource _source;

    public static Log CreateInstance(ManualLogSource source)
    {
        _instance = new Log
        {
            _source = source,
        };
        return _instance;
    }

    private Log() { }

    public static void Info(object msg)    { if (LogLevel <= VegvisirLogLevel.Info) _instance?._source?.LogInfo(FormatMessage(msg)); }

    public static void Message(object msg) { if (LogLevel <= VegvisirLogLevel.Info) _instance?._source?.LogMessage(FormatMessage(msg)); }

    public static void Debug(object msg)   { if (LogLevel <= VegvisirLogLevel.Debug) _instance?._source?.LogDebug(FormatMessage(msg)); }

    public static void Warning(object msg) { if (LogLevel <= VegvisirLogLevel.Warning) _instance?._source?.LogWarning(FormatMessage(msg)); }

    public static void Error(object msg)   { if (LogLevel <= VegvisirLogLevel.Error) _instance?._source?.LogError(FormatMessage(msg)); }

    public static void Fatal(object msg)   { if (LogLevel <= VegvisirLogLevel.Fatal) _instance?._source?.LogFatal(FormatMessage(msg)); }

    private static string FormatMessage(object msg) => $"[{DateTime.UtcNow}] [{ModInfo.ModName}] {msg}";
}

[HarmonyPatch(typeof(Chat), "InputText")]
public static class Chat_InputText_Patch
{
    [HarmonyPrefix]
    public static void Prefix(Chat __instance)
    {
        string text = __instance.m_input.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        if (text.StartsWith("/njordshond ", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/nh ", StringComparison.OrdinalIgnoreCase))
        {
            Log.Info($"Chat command received: {text}");
            var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var cmd = parts[1].ToLowerInvariant();
                if ((cmd == "setcourse" || cmd == "sc") && parts.Length >= 3)
                {
                    Log.Info("AutoPilot enabled. Adding waypoints...");
                    typeof(VegvisirPlugin).GetProperty("AutoPilotEnabled").SetValue(null, true);
                    var course = new List<(string name, Vector3 pos)>();
                    int startIndex = 2;

                    Log.Debug($"Searching pins for names: {string.Join(", ", parts.Skip(startIndex))}");
                    if (Minimap.instance == null)
                    {
                        Log.Warning("Minimap instance is null. Cannot find pins.");
                        return;
                    }

                    for (int i = startIndex; i < parts.Length; i++)
                    {
                        string name = parts[i];
                        var pins = Traverse.Create(Minimap.instance).Field<List<Minimap.PinData>>("m_pins").Value;
                        var pin = pins?.FirstOrDefault(p => p.m_name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (pin != null)
                        {
                            course.Add((pin.m_name, pin.m_pos));
                        }
                        else
                        {
                            Log.Warning($"No pin named '{name}' found.");
                        }
                    }
                    Log.Info($"Total matched pins: {course.Count}");
                    VegvisirPlugin.SetWaypointCourse(course);

                    // Set ShipNavigator auto-steer
                    try
                    {
                        var shipPatchType = AccessTools.TypeByName("ShipNavigator.Patches.Ship_Patch");
                        var autoSteerField = shipPatchType?.GetField("isAutoSteerOn", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (autoSteerField != null)
                        {
                            autoSteerField.SetValue(null, true);
                            Log.Info("ShipNavigator auto-steer set to ON.");
                        }
                        else
                        {
                            Log.Warning("Could not find 'isAutoSteerOn' field.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error setting ShipNavigator auto-steer ON: " + ex);
                    }

                }
                else if (cmd == "clearcourse" || cmd == "cc")
                {
                    Log.Info("Clearing course. AutoPilot disabled.");
                    typeof(VegvisirPlugin).GetProperty("AutoPilotEnabled").SetValue(null, false);
                    VegvisirPlugin.ClearWaypointCourse();
                    
                    // Set ShipNavigator auto-steer
                    try
                    {
                        var shipPatchType = AccessTools.TypeByName("ShipNavigator.Patches.Ship_Patch");
                        var autoSteerField = shipPatchType?.GetField("isAutoSteerOn", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (autoSteerField != null)
                        {
                            autoSteerField.SetValue(null, false);
                            Log.Info("ShipNavigator auto-steer set to OFF.");
                        }
                        else
                        {
                            Log.Warning("Could not find 'isAutoSteerOn' field.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error setting ShipNavigator auto-steer OFF: " + ex);
                    }
                }
            }
        }
    }
}

// Encapsulates ship HUD icon lookup and color setting
public static class ShipHudIcon
{
    private static GameObject _shipIcon;

    public static bool TryFind()
    {
        if (_shipIcon == null)
        {
            _shipIcon = GameObject.Find("/_GameMain/LoadingGUI/PixelFix/IngameGui/HUD/hudroot/ShipHud/WindIndicator/Ship");
        }
        return _shipIcon != null;
    }

    public static void SetColor(Color color)
    {
        if (!TryFind())
        {
            Log.Warning("Ship icon not found.");
            return;
        }

        var renderer = _shipIcon.GetComponent<UnityEngine.UI.Image>();
        if (renderer != null)
        {
            renderer.color = color;
        }
        else
        {
            Log.Warning("Ship icon found, but Image component missing.");
        }
    }
}
// Harmony patch to suppress ShipNavigator input when chat is focused
[HarmonyPatch]
public static class ShipNavigatorInputBlocker
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method("ShipNavigator.ShipNavigator:Update");
    }

    static bool Prefix()
    {
        if (Chat.instance?.m_input != null && Chat.instance.m_input.isFocused)
        {
            Log.Debug("Blocking ShipNavigator input: chat input is focused.");
            return false; // Skip ShipNavigator input handling
        }
        return true;
    }
}