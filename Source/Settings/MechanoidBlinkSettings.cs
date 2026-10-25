using System;
using System.Collections.Generic;
using MechanoidBlink.Core;
using Verse;

namespace MechanoidBlink.Settings;

/// <summary>
/// Mod settings for Mechanoid Blink, persisted via RimWorld's Scribe system.
/// Implements ISettingsProvider to support unit testing and decoupled permission checks.
/// </summary>
public class MechanoidBlinkSettings : ModSettings, ISettingsProvider
{
    public bool enableFriendly = true;
    public bool enableHostile = false;
    public HashSet<string> allowedMechDefs = new(StringComparer.OrdinalIgnoreCase);
    public bool initialized = false;

    // ISettingsProvider implementation
    bool ISettingsProvider.EnableFriendly => enableFriendly;
    bool ISettingsProvider.EnableHostile => enableHostile;

    /// <summary>
    /// Checks whether the specified mechanoid defName is allowed to blink.
    /// If settings have never been initialized, returns true by default.
    /// </summary>
    public bool IsMechTypeAllowed(string defName)
    {
        if (string.IsNullOrEmpty(defName)) return false;
        if (!initialized) return true;
        return allowedMechDefs.Contains(defName);
    }

    /// <summary>
    /// Sets whether a specific mechanoid defName is allowed to blink.
    /// </summary>
    public void SetMechTypeAllowed(string defName, bool allowed)
    {
        if (string.IsNullOrEmpty(defName)) return;

        initialized = true;
        if (allowed)
        {
            allowedMechDefs.Add(defName);
        }
        else
        {
            allowedMechDefs.Remove(defName);
        }
    }

    /// <summary>
    /// Selects all available mechanoid defs.
    /// </summary>
    public void SelectAll(IEnumerable<string> allDefNames)
    {
        initialized = true;
        allowedMechDefs.Clear();
        if (allDefNames == null) return;

        foreach (var defName in allDefNames)
        {
            if (!string.IsNullOrEmpty(defName))
            {
                allowedMechDefs.Add(defName);
            }
        }
    }

    /// <summary>
    /// Deselects all mechanoids.
    /// </summary>
    public void DeselectAll()
    {
        initialized = true;
        allowedMechDefs.Clear();
    }

    /// <summary>
    /// Ensures that all available mechanoids are selected if the settings were not yet initialized.
    /// </summary>
    public void EnsureInitialized(IEnumerable<string> availableDefNames)
    {
        if (!initialized)
        {
            SelectAll(availableDefNames);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref enableFriendly, "enableFriendly", true);
        Scribe_Values.Look(ref enableHostile, "enableHostile", false);
        Scribe_Values.Look(ref initialized, "initialized", false);

        List<string>? list = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            list = new List<string>(allowedMechDefs);
        }

        Scribe_Collections.Look(ref list, "allowedMechDefs", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.LoadingVars && list != null)
        {
            allowedMechDefs = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        }
    }
}

