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

internal sealed class TextSetterPatchState
{
    internal StoryTargetKind Target;
    internal int InstanceId;
    internal string PreviousText = string.Empty;
    internal string IncomingText = string.Empty;
    internal int PreviousParsedLength = -1;
    internal int PreviousLengthOfView;
    internal bool PreviousContainsHangul;
}

