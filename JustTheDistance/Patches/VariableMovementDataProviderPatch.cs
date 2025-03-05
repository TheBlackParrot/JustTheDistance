using System;
using HarmonyLib;
using JustTheDistance.Configuration;
using UnityEngine;

namespace JustTheDistance.Patches;

[HarmonyPatch]
internal class VariableMovementDataProviderPatch
{
    private static PluginConfig Config => PluginConfig.Instance;

    // based off JDFixer's method
    [HarmonyPatch(typeof(VariableMovementDataProvider), "Init")]
    internal static void Prefix(float bpm, ref float noteJumpMovementSpeed, ref float noteJumpValue)
    {
        if (!Config.Enabled)
        {
            return;
        }
        
        float beatsPerSecond = 60f / bpm;
        float halfJump = 4f;
        
        while (noteJumpMovementSpeed * beatsPerSecond * halfJump > 17.999)
        {
            halfJump /= 2;
        }
        halfJump = Mathf.Max(halfJump, 0.25f);

        float jumpDistanceFromRT = Math.Max(Config.ReactionTime, 300) * (2 * noteJumpMovementSpeed) / 1000;
        
        float jumpDurationConstant = halfJump * beatsPerSecond * 2f;

        float jumpDuration = jumpDistanceFromRT / noteJumpMovementSpeed;
        float jumpDurationMultiplier = jumpDuration / jumpDurationConstant;

        noteJumpValue = (halfJump * jumpDurationMultiplier) - halfJump;
        if (Config.SnapToNearest)
        {
            // saved as an int, we need a float here. ez conversion
            float snappingAmount = Config.SnapToNearestNoteType;
            
            noteJumpValue = Mathf.Round(noteJumpValue * snappingAmount) / snappingAmount;
        }
        
        Plugin.Log.Info($"NJV: {noteJumpValue}");
    }
}