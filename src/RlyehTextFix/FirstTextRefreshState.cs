#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utage;

namespace RlyehTextFix;

internal sealed class FirstTextRefreshState
{
    internal Text Source;
    internal string OriginalText = string.Empty;
    internal int Generation;
    internal int Attempts;
    internal float EligibleAt;
    internal float NextAttemptAt;
    internal float LastDispatchAt;
    internal bool TranslationSeen;
    internal bool RefreshInProgress;
    internal int LastRequestedLength;
}

