using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AutoBlink;
using HarmonyLib;
using Verse;

namespace MechanoidBlink.Core
{
  /// <summary>
  /// Provides zero-allocation, O(1) runtime accessors for internal types in the upstream
  /// <c>AutoBlink</c> assembly (<c>BlinkSourceDefCache</c>, <c>BlinkController</c>,
  /// <c>BlinkCommand</c>, and <c>AutoBlinkGameComponent</c>).
  /// </summary>
  /// <remarks>
  /// <para>
  /// Reflection metadata lookup and IL delegate emission are performed once during static
  /// initialization. Subsequent runtime calls execute compiled delegates or direct dictionary
  /// lookups in O(1) time and O(1) space without per-call heap allocations.
  /// </para>
  /// <para>
  /// Caveat: Because these members are <c>internal</c> to <c>AutoBlink.dll</c>, every accessor
  /// degrades gracefully and logs a diagnostic warning if an upstream version changes its
  /// internal member signatures.
  /// </para>
  /// </remarks>
  /// <example>
  /// <code>
  /// // Synchronize a newly injected mechanoid ThingDef with AutoBlink's source cache:
  /// AutoBlinkBridge.MarkThingDefHasSource(mechDef, hasSource: true);
  /// bool cached = AutoBlinkBridge.HasCachedThingSource(mechDef); // true
  /// </code>
  /// </example>
  public static class AutoBlinkBridge
  {
    private static readonly Type? CacheType;
    private static readonly Type? ControllerType;
    private static readonly Type? CommandType;
    private static readonly Type? GameComponentType;

    private static readonly Dictionary<ThingDef, bool>? CachedThingSources;
    private static readonly Action? RebuildCacheDelegate;
    private static readonly Func<ThingDef, bool>? HasThingSourceDelegate;

    private static readonly Func<object, Pawn?>? GetPawnDelegate;
    private static readonly Action<object>? RefreshProfileDelegate;
    private static readonly Func<object, AutoBlinkProfileDef?>? GetProfileDelegate;
    private static readonly Func<object, Gizmo?>? GetCommandDelegate;
    private static readonly Action<object>? SettingsChangedDelegate;
    private static readonly Action<Gizmo, AutoBlinkProfileDef>? PrepareCommandDelegate;

    private static readonly Func<object?>? GetCurrentGameComponentDelegate;
    private static readonly Action? RebuildMapsDelegate;
    private static readonly FieldInfo? ControllersDictionaryField;

    static AutoBlinkBridge()
    {
      CacheType = AccessTools.TypeByName("AutoBlink.BlinkSourceDefCache");
      ControllerType = AccessTools.TypeByName("AutoBlink.BlinkController");
      CommandType = AccessTools.TypeByName("AutoBlink.BlinkCommand");
      GameComponentType = AccessTools.TypeByName("AutoBlink.AutoBlinkGameComponent");

      if (CacheType is not null)
      {
        FieldInfo? thingsField = AccessTools.Field(CacheType, "things");
        if (thingsField is { IsStatic: true })
        {
          // Trigger AutoBlink.BlinkSourceDefCache static constructor before reading field value.
          System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(CacheType.TypeHandle);
          CachedThingSources = thingsField.GetValue(null) as Dictionary<ThingDef, bool>;
        }

        MethodInfo? rebuildMethod = AccessTools.Method(CacheType, "Rebuild", Type.EmptyTypes);
        if (rebuildMethod is not null)
        {
          RebuildCacheDelegate = CompileStaticAction("AutoBlink_CacheRebuild", rebuildMethod);
        }

        MethodInfo? hasSourceMethod = AccessTools.Method(CacheType, "HasSource", new[] { typeof(ThingDef) });
        if (hasSourceMethod is not null)
        {
          HasThingSourceDelegate = CompileStaticFunc<ThingDef, bool>("AutoBlink_CacheHasSource", hasSourceMethod);
        }
      }

      if (ControllerType is not null)
      {
        MethodInfo? getPawnMethod = AccessTools.PropertyGetter(ControllerType, "Pawn");
        FieldInfo? pawnField = AccessTools.Field(ControllerType, "<pawn>P");
        if (getPawnMethod is not null)
        {
          GetPawnDelegate = CompileInstanceFunc<Pawn?>("AutoBlink_ControllerGetPawn", ControllerType, getPawnMethod);
        }
        else if (pawnField is not null)
        {
          GetPawnDelegate = CompileFieldGetter<Pawn?>("AutoBlink_ControllerGetPawnField", ControllerType, pawnField);
        }

        MethodInfo? refreshProfileMethod = AccessTools.Method(ControllerType, "RefreshProfile", Type.EmptyTypes);
        if (refreshProfileMethod is not null)
        {
          RefreshProfileDelegate = CompileInstanceAction("AutoBlink_ControllerRefreshProfile", ControllerType, refreshProfileMethod);
        }

        MethodInfo? getProfileMethod = AccessTools.PropertyGetter(ControllerType, "Profile");
        if (getProfileMethod is not null)
        {
          GetProfileDelegate = CompileInstanceFunc<AutoBlinkProfileDef?>("AutoBlink_ControllerGetProfile", ControllerType, getProfileMethod);
        }

        MethodInfo? getCommandMethod = AccessTools.PropertyGetter(ControllerType, "Command");
        if (getCommandMethod is not null)
        {
          GetCommandDelegate = CompileInstanceFunc<Gizmo?>("AutoBlink_ControllerGetCommand", ControllerType, getCommandMethod);
        }

        MethodInfo? settingsChangedMethod = AccessTools.Method(ControllerType, "SettingsChanged", Type.EmptyTypes);
        if (settingsChangedMethod is not null)
        {
          SettingsChangedDelegate = CompileInstanceAction("AutoBlink_ControllerSettingsChanged", ControllerType, settingsChangedMethod);
        }
      }

      if (CommandType is not null)
      {
        MethodInfo? prepareMethod = AccessTools.Method(CommandType, "Prepare", new[] { typeof(AutoBlinkProfileDef) });
        if (prepareMethod is not null)
        {
          PrepareCommandDelegate = CompileCommandPrepare("AutoBlink_CommandPrepare", CommandType, prepareMethod);
        }
      }

      if (GameComponentType is not null)
      {
        MethodInfo? getCurrentMethod = AccessTools.PropertyGetter(GameComponentType, "Current");
        if (getCurrentMethod is not null)
        {
          GetCurrentGameComponentDelegate = CompileStaticObjectGetter("AutoBlink_GameCompCurrent", getCurrentMethod);
        }

        MethodInfo? rebuildMapsMethod = AccessTools.Method(GameComponentType, "RebuildMaps", Type.EmptyTypes);
        if (rebuildMapsMethod is not null)
        {
          RebuildMapsDelegate = CompileStaticAction("AutoBlink_GameCompRebuildMaps", rebuildMapsMethod);
        }

        ControllersDictionaryField = AccessTools.Field(GameComponentType, "controllers");
      }
    }

    /// <summary>
    /// Gets a value indicating whether <c>BlinkSourceDefCache.things</c> can be updated directly in O(1) time.
    /// </summary>
    public static bool CanUpdateThingSourceCacheDirectly => CachedThingSources is not null;

    /// <summary>
    /// Updates <c>AutoBlink.BlinkSourceDefCache</c> for a specific <see cref="ThingDef"/> in O(1) time and O(1) space.
    /// </summary>
    /// <param name="def">The <see cref="ThingDef"/> whose cached AutoBlink source presence should be updated.</param>
    /// <param name="hasSource"><see langword="true"/> if the definition has a <see cref="CompProperties_AutoBlinkSource"/>; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the cache entry was updated directly; otherwise, <see langword="false"/>.</returns>
    public static bool MarkThingDefHasSource(ThingDef? def, bool hasSource = true)
    {
      if ((def is null) || (CachedThingSources is null))
      {
        return false;
      }

      CachedThingSources[def] = hasSource;
      return true;
    }

    /// <summary>
    /// Rebuilds the entire <c>AutoBlink.BlinkSourceDefCache</c> across all loaded definitions.
    /// </summary>
    /// <returns><see langword="true"/> if the rebuild method was found and invoked; otherwise, <see langword="false"/>.</returns>
    public static bool RebuildSourceDefCache()
    {
      if (RebuildCacheDelegate is null)
      {
        Log.Warning("[MechanoidBlink] Could not locate AutoBlink.BlinkSourceDefCache.Rebuild().");
        return false;
      }

      RebuildCacheDelegate();
      return true;
    }

    /// <summary>
    /// Queries <c>AutoBlink.BlinkSourceDefCache.HasSource(ThingDef)</c> in O(1) time.
    /// </summary>
    /// <param name="def">The <see cref="ThingDef"/> to inspect.</param>
    /// <returns><see langword="true"/> if <c>AutoBlink</c> recognizes <paramref name="def"/> as having an AutoBlink source; otherwise, <see langword="false"/>.</returns>
    public static bool HasCachedThingSource(ThingDef? def)
    {
      if ((def is null) || (HasThingSourceDelegate is null))
      {
        return false;
      }

      return HasThingSourceDelegate(def);
    }

    /// <summary>
    /// Retrieves the <see cref="Pawn"/> associated with an <c>AutoBlink.BlinkController</c> instance in O(1) time.
    /// </summary>
    /// <param name="controller">The boxed or untyped <c>AutoBlink.BlinkController</c> instance.</param>
    /// <returns>The owning <see cref="Pawn"/>, or <see langword="null"/> if <paramref name="controller"/> is invalid.</returns>
    public static Pawn? GetPawn(object? controller)
    {
      if ((controller is null) || (GetPawnDelegate is null) || (ControllerType is null) || !ControllerType.IsInstanceOfType(controller))
      {
        return null;
      }

      return GetPawnDelegate(controller);
    }

    /// <summary>
    /// Invokes <c>BlinkController.RefreshProfile()</c> on the specified controller instance.
    /// </summary>
    /// <param name="controller">The <c>AutoBlink.BlinkController</c> instance to refresh.</param>
    public static void RefreshProfile(object? controller)
    {
      if ((controller is null) || (RefreshProfileDelegate is null) || (ControllerType is null) || !ControllerType.IsInstanceOfType(controller))
      {
        return;
      }

      RefreshProfileDelegate(controller);
    }

    /// <summary>
    /// Retrieves the active <see cref="AutoBlinkProfileDef"/> from an <c>AutoBlink.BlinkController</c> instance in O(1) time.
    /// </summary>
    /// <param name="controller">The <c>AutoBlink.BlinkController</c> instance.</param>
    /// <returns>The resolved <see cref="AutoBlinkProfileDef"/>, or <see langword="null"/> if none is active.</returns>
    public static AutoBlinkProfileDef? GetProfile(object? controller)
    {
      if ((controller is null) || (GetProfileDelegate is null) || (ControllerType is null) || !ControllerType.IsInstanceOfType(controller))
      {
        return null;
      }

      return GetProfileDelegate(controller);
    }

    /// <summary>
    /// Retrieves the <c>BlinkCommand</c> gizmo owned by an <c>AutoBlink.BlinkController</c> instance in O(1) time.
    /// </summary>
    /// <param name="controller">The <c>AutoBlink.BlinkController</c> instance.</param>
    /// <returns>The <see cref="Gizmo"/> command instance, or <see langword="null"/> if unavailable.</returns>
    public static Gizmo? GetCommand(object? controller)
    {
      if ((controller is null) || (GetCommandDelegate is null) || (ControllerType is null) || !ControllerType.IsInstanceOfType(controller))
      {
        return null;
      }

      return GetCommandDelegate(controller);
    }

    /// <summary>
    /// Prepares an <c>AutoBlink.BlinkCommand</c> gizmo with label, description, and icon from <paramref name="profile"/>.
    /// </summary>
    /// <param name="command">The <c>BlinkCommand</c> gizmo instance.</param>
    /// <param name="profile">The active <see cref="AutoBlinkProfileDef"/> to bind to the command.</param>
    /// <returns><see langword="true"/> if the command was prepared; otherwise, <see langword="false"/>.</returns>
    public static bool PrepareCommand(Gizmo? command, AutoBlinkProfileDef? profile)
    {
      if ((command is null) || (profile is null) || (PrepareCommandDelegate is null) || (CommandType is null) || !CommandType.IsInstanceOfType(command))
      {
        return false;
      }

      PrepareCommandDelegate(command, profile);
      return true;
    }

    /// <summary>
    /// Requests a map-wide AutoBlink controller rebuild if a game is currently loaded.
    /// </summary>
    public static void RebuildActiveMapsIfPresent()
    {
      if ((Current.Game is null) || (RebuildMapsDelegate is null))
      {
        return;
      }

      RebuildMapsDelegate();
    }

    /// <summary>
    /// Notifies all active mechanoid <c>BlinkController</c> instances in the current game that
    /// blink settings have changed, immediately cancelling disallowed charges and waking eligible walkers.
    /// </summary>
    public static void NotifyMechanoidControllersSettingsChanged()
    {
      if ((Current.Game is null) || (GetCurrentGameComponentDelegate is null) || (ControllersDictionaryField is null) || (SettingsChangedDelegate is null))
      {
        return;
      }

      object? gameComp = GetCurrentGameComponentDelegate();
      if (gameComp is null)
      {
        return;
      }

      if (ControllersDictionaryField.GetValue(gameComp) is not IDictionary controllers)
      {
        return;
      }

      foreach (DictionaryEntry entry in controllers)
      {
        if ((entry.Key is Pawn { RaceProps.IsMechanoid: true }) && (entry.Value is not null))
        {
          SettingsChangedDelegate(entry.Value);
        }
      }
    }

    private static Action CompileStaticAction(string name, MethodInfo method)
    {
      DynamicMethod dm = new(name, typeof(void), Type.EmptyTypes, typeof(AutoBlinkBridge).Module, skipVisibility: true);
      ILGenerator il = dm.GetILGenerator();
      il.Emit(OpCodes.Call, method);
      il.Emit(OpCodes.Ret);
      return (Action)dm.CreateDelegate(typeof(Action));
    }

    private static Func<TArg, TResult> CompileStaticFunc<TArg, TResult>(string name, MethodInfo method)
    {
      DynamicMethod dm = new(name, typeof(TResult), new[] { typeof(TArg) }, typeof(AutoBlinkBridge).Module, skipVisibility: true);
      ILGenerator il = dm.GetILGenerator();
      il.Emit(OpCodes.Ldarg_0);
      il.Emit(OpCodes.Call, method);
      il.Emit(OpCodes.Ret);
      return (Func<TArg, TResult>)dm.CreateDelegate(typeof(Func<TArg, TResult>));
    }

    private static Func<object?> CompileStaticObjectGetter(string name, MethodInfo method)
    {
      DynamicMethod dm = new(name, typeof(object), Type.EmptyTypes, typeof(AutoBlinkBridge).Module, skipVisibility: true);
      ILGenerator il = dm.GetILGenerator();
      il.Emit(OpCodes.Call, method);
      il.Emit(OpCodes.Ret);
      return (Func<object?>)dm.CreateDelegate(typeof(Func<object?>));
    }

    private static Func<object, TResult> CompileInstanceFunc<TResult>(string name, Type targetType, MethodInfo method)
    {
      DynamicMethod dm = new(name, typeof(TResult), new[] { typeof(object) }, typeof(AutoBlinkBridge).Module, skipVisibility: true);
      ILGenerator il = dm.GetILGenerator();
      il.Emit(OpCodes.Ldarg_0);
      il.Emit(OpCodes.Castclass, targetType);
      il.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
      il.Emit(OpCodes.Ret);
      return (Func<object, TResult>)dm.CreateDelegate(typeof(Func<object, TResult>));
    }

    private static Func<object, TResult> CompileFieldGetter<TResult>(string name, Type targetType, FieldInfo field)
    {
      DynamicMethod dm = new(name, typeof(TResult), new[] { typeof(object) }, typeof(AutoBlinkBridge).Module, skipVisibility: true);
      ILGenerator il = dm.GetILGenerator();
      il.Emit(OpCodes.Ldarg_0);
      il.Emit(OpCodes.Castclass, targetType);
      il.Emit(OpCodes.Ldfld, field);
      il.Emit(OpCodes.Ret);
      return (Func<object, TResult>)dm.CreateDelegate(typeof(Func<object, TResult>));
    }

    private static Action<object> CompileInstanceAction(string name, Type targetType, MethodInfo method)
    {
      DynamicMethod dm = new(name, typeof(void), new[] { typeof(object) }, typeof(AutoBlinkBridge).Module, skipVisibility: true);
      ILGenerator il = dm.GetILGenerator();
      il.Emit(OpCodes.Ldarg_0);
      il.Emit(OpCodes.Castclass, targetType);
      il.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
      il.Emit(OpCodes.Ret);
      return (Action<object>)dm.CreateDelegate(typeof(Action<object>));
    }

    private static Action<Gizmo, AutoBlinkProfileDef> CompileCommandPrepare(string name, Type commandType, MethodInfo method)
    {
      DynamicMethod dm = new(
          name,
          typeof(void),
          new[] { typeof(Gizmo), typeof(AutoBlinkProfileDef) },
          typeof(AutoBlinkBridge).Module,
          skipVisibility: true);
      ILGenerator il = dm.GetILGenerator();
      il.Emit(OpCodes.Ldarg_0);
      il.Emit(OpCodes.Castclass, commandType);
      il.Emit(OpCodes.Ldarg_1);
      il.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
      il.Emit(OpCodes.Ret);
      return (Action<Gizmo, AutoBlinkProfileDef>)dm.CreateDelegate(typeof(Action<Gizmo, AutoBlinkProfileDef>));
    }
  }
}
