using MechanoidBlink.Core;
using MechanoidBlink.Settings;
using MechanoidBlink.UI;
using UnityEngine;
using Verse;

namespace MechanoidBlink;

/// <summary>
/// RimWorld Mod entry point for Mechanoid Blink.
/// </summary>
public class MechanoidBlinkMod : Mod
{
    public static MechanoidBlinkMod Instance { get; private set; } = null!;
    public static MechanoidBlinkSettings Settings { get; private set; } = null!;
    public static IMechanoidBlinkPermissionChecker PermissionChecker { get; set; } = null!;

    public MechanoidBlinkMod(ModContentPack content) : base(content)
    {
        Instance = this;
        Settings = GetSettings<MechanoidBlinkSettings>();
        PermissionChecker = new MechanoidBlinkPermissionChecker(Settings, RimWorldFactionHostilityChecker.Instance);
    }

    public override string SettingsCategory()
    {
        return "MechanoidBlink_ModName".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        MechanoidBlinkSettingsUi.DoSettingsWindowContents(inRect, Settings);
    }

    /// <summary>
    /// Persists mod settings and batch-updates all active mechanoids so newly enabled
    /// or disabled mechanoids immediately receive or lose the Mechanoid Blink Hediff.
    /// </summary>
    public override void WriteSettings()
    {
        base.WriteSettings();
        MechanoidBlinkHediffManager.UpdateAllMechanoids();
    }
}
