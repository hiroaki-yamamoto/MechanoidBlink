using System;
using System.Collections.Generic;
using System.Linq;
using MechanoidBlink.Core;
using MechanoidBlink.Settings;
using UnityEngine;
using Verse;

namespace MechanoidBlink.UI;

/// <summary>
/// Handles the rendering of Mod Options UI following native RimWorld styling.
/// </summary>
public static class MechanoidBlinkSettingsUi
{
    private static Vector2 scrollPosition = Vector2.zero;
    private static string filterText = string.Empty;

    public static void DoSettingsWindowContents(Rect inRect, MechanoidBlinkSettings settings)
    {
        if (settings == null) return;

        // Ensure default initialization with all available mechanoids if not yet initialized
        settings.EnsureInitialized(MechanoidRegistry.AllMechanoidDefNames);

        var listing = new Listing_Standard();
        listing.Begin(inRect);

        // 1. Checkbox: Integrate into friendly mechanoids
        listing.CheckboxLabeled(
            "MechanoidBlink_EnableFriendly".Translate(),
            ref settings.enableFriendly,
            "MechanoidBlink_EnableFriendly_Desc".Translate());

        // 2. Checkbox: Integrate into hostile mechanoids
        listing.CheckboxLabeled(
            "MechanoidBlink_EnableHostile".Translate(),
            ref settings.enableHostile,
            "MechanoidBlink_EnableHostile_Desc".Translate());

        listing.Gap(12f);

        // 3. Selection box: Mechanoid types
        listing.Label("MechanoidBlink_SelectMechanoids".Translate());

        // Button row: Select All / Deselect All / Search
        Rect actionRow = listing.GetRect(30f);
        float buttonWidth = 130f;
        float spacing = 10f;

        Rect selectAllRect = new Rect(actionRow.x, actionRow.y, buttonWidth, 30f);
        if (Widgets.ButtonText(selectAllRect, "MechanoidBlink_SelectAll".Translate()))
        {
            settings.SelectAll(MechanoidRegistry.AllMechanoidDefNames);
        }

        Rect deselectAllRect = new Rect(selectAllRect.xMax + spacing, actionRow.y, buttonWidth, 30f);
        if (Widgets.ButtonText(deselectAllRect, "MechanoidBlink_DeselectAll".Translate()))
        {
            settings.DeselectAll();
        }

        // Search bar
        float searchLabelWidth = 60f;
        float searchStartX = deselectAllRect.xMax + spacing * 2;
        float searchWidth = actionRow.xMax - searchStartX;
        if (searchWidth > 120f)
        {
            Rect searchLabelRect = new Rect(searchStartX, actionRow.y + 4f, searchLabelWidth, 24f);
            Widgets.Label(searchLabelRect, "MechanoidBlink_Search".Translate());

            Rect searchInputRect = new Rect(searchLabelRect.xMax + 5f, actionRow.y, actionRow.xMax - (searchLabelRect.xMax + 5f), 28f);
            filterText = Widgets.TextField(searchInputRect, filterText);
        }

        listing.Gap(8f);

        // Calculate area for mechanoid selection list
        float remainingHeight = inRect.height - listing.CurHeight - 10f;
        Rect scrollContainerRect = listing.GetRect(Math.Max(150f, remainingHeight));
        Widgets.DrawMenuSection(scrollContainerRect);

        DrawMechanoidList(scrollContainerRect.ContractedBy(4f), settings);

        listing.End();
    }

    private static void DrawMechanoidList(Rect outRect, MechanoidBlinkSettings settings)
    {
        // MechanoidRegistry.AllMechanoids strictly excludes corpses
        var allMechs = MechanoidRegistry.AllMechanoids;
        var filteredMechs = allMechs;

        if (!string.IsNullOrWhiteSpace(filterText))
        {
            filteredMechs = allMechs
                .Where(d => (d.label != null && d.label.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                         || (d.defName != null && d.defName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
        }

        float rowHeight = 28f;
        float viewHeight = filteredMechs.Count * rowHeight;
        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

        float curY = 0f;
        for (int i = 0; i < filteredMechs.Count; i++)
        {
            var def = filteredMechs[i];
            Rect rowRect = new Rect(0f, curY, viewRect.width, rowHeight);

            // Alternate row background for readability
            if (i % 2 == 1)
            {
                Widgets.DrawLightHighlight(rowRect);
            }

            bool isChecked = settings.IsMechTypeAllowed(def.defName);
            bool newChecked = isChecked;

            // Draw checkbox and label
            string labelText = def.LabelCap.Resolve();
            if (string.IsNullOrEmpty(labelText))
            {
                labelText = def.defName;
            }

            // Display mod origin if available
            string? sourceMod = def.modContentPack?.Name;
            string displayLabel = string.IsNullOrEmpty(sourceMod)
                ? labelText
                : $"{labelText} ({sourceMod})";

            Widgets.CheckboxLabeled(rowRect, displayLabel, ref newChecked);

            if (newChecked != isChecked)
            {
                settings.SetMechTypeAllowed(def.defName, newChecked);
            }

            if (!string.IsNullOrEmpty(def.description))
            {
                TooltipHandler.TipRegion(rowRect, def.description);
            }

            curY += rowHeight;
        }

        Widgets.EndScrollView();
    }
}

