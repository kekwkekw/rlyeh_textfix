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

internal sealed class StoryProgressState
{
    internal Text Source;
    internal int OriginalTotalLength = -1;
    internal int TranslatedTotalLength = -1;
    internal int LastRequestedOriginalLength;
    internal int LastMappedTranslatedLength = -1;
    internal int Generation;
    internal int CompletionLoggedGeneration = -1;
    internal int LastProgressLoggedOriginal = -1;
    internal StoryTargetKind Target;
    internal bool TranslationActive;
    internal bool AwaitingOriginalAfterClear = true;
    internal string OriginalText = string.Empty;
    internal string TranslatedText = string.Empty;
}

