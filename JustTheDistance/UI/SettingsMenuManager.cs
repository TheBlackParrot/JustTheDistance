using System;
using System.ComponentModel;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.GameplaySetup;
using JetBrains.Annotations;
using JustTheDistance.Configuration;
using UnityEngine;
using Zenject;

namespace JustTheDistance.UI;

internal abstract class Utils
{
    private static PluginConfig Config => PluginConfig.Instance;
    private static float CalculateHalfJump(float bpm, float njs)
    {
        float beatsPerSecond = 60f / bpm;
        float halfJump = 4f;
        
        while (njs * beatsPerSecond * halfJump > 17.999)
        {
            halfJump /= 2;
        }
        
        return Mathf.Max(halfJump, 0.25f);
    }
    public static float CalculateSnappedRT(float bpm, float njs, int reactionTime)
    {
        if (njs == 0f)
        {
            njs = 16f;
        }
        float beatsPerSecond = 60f / bpm;
        float snappingAmount = Config.SnapToNearestNoteType;
        float halfJump = CalculateHalfJump(bpm, njs);
        
        float jumpDistanceFromRT = reactionTime * (2 * njs) / 1000;
        
        float jumpDurationConstant = halfJump * beatsPerSecond * 2f;

        float jumpDuration = jumpDistanceFromRT / njs;
        float jumpDurationMultiplier = jumpDuration / jumpDurationConstant;
        
        float val = (halfJump * jumpDurationMultiplier) - halfJump;
        val = Mathf.Round(val * snappingAmount) / snappingAmount;

        return (njs * beatsPerSecond * (halfJump + val) * 2f) / (2 * njs) * 1000;
    }
}

[UsedImplicitly]
internal class ModSettingsManager : IInitializable, IDisposable, INotifyPropertyChanged
{
    private static PluginConfig Config => PluginConfig.Instance;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    private readonly GameplaySetup _gameplaySetup;
    private readonly GameplaySetupViewController _gameplaySetupViewController;
    private readonly StandardLevelDetailViewController _standardLevelDetailViewController;

    private const string MenuName = nameof(JustTheDistance);
    private const string ResourcePath = nameof(JustTheDistance) + ".UI.BSML.Settings.bsml";

    private static float _lastJumpSpeed = 10f;
    private static float _lastTempo = 120f;
    private static int _snappedRT;

    [UsedImplicitly]
    private static string TimeFormatter(int x)
    {
        string mainStr = Config.SnapToNearest
            ? $"{(_snappedRT >= 1000 ? (_snappedRT / 1000f).ToString("F2") + "s" : _snappedRT.ToString("N0") + "ms")}"
            : $"{(x >= 1000 ? (x / 1000f).ToString("F2") + "s" : x.ToString("N0") + "ms")}";
        
        return $"{mainStr}";   
    }

    [UsedImplicitly]
    private string NoteFormatter(int x)
    {
        return x switch
        {
            1 => "Whole",
            2 => "Half",
            3 => "Third",
            4 => "Quarter",
            5 => "Fifth",
            6 => "Sixth",
            7 => "Seventh",
            8 => "Eighth",
            _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
        };
    }

    [UIComponent("rtSlider")]
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private SliderSetting _sliderSetting = null!;
    
    // ReSharper disable once ConvertToPrimaryConstructor
    // (just easier for me to read, sorry)
    public ModSettingsManager(GameplaySetup gameplaySetup, StandardLevelDetailViewController standardLevelDetailViewController, GameplaySetupViewController gameplaySetupViewController)
    {
        _gameplaySetup = gameplaySetup;
        _standardLevelDetailViewController = standardLevelDetailViewController;
        _gameplaySetupViewController = gameplaySetupViewController;
    }
    
    public void Initialize()
    {
        _gameplaySetup.AddTab(MenuName, ResourcePath, this);

        _gameplaySetupViewController.didActivateEvent += DidActivate;

        _standardLevelDetailViewController.didChangeDifficultyBeatmapEvent += BeatmapDidUpdateDifficulty;
        _standardLevelDetailViewController.didChangeContentEvent += BeatmapDidUpdateContent;
        
        Plugin.Log.Info("Initialized settings tab");
    }

    public void Dispose()
    {
        _gameplaySetup.RemoveTab(MenuName);
    }

    private void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (!firstActivation) { return; }
        
        // not working
        /*_sliderSetting.Slider.minValue = Config.MinRTSliderValue;
        _sliderSetting.Slider.maxValue = Config.MaxRTSliderValue;
        _sliderSetting.Increments = Config.RTSliderIncrement;
        _sliderSetting.Slider.Refresh();*/
    }

    private void UpdateRTSliderText()
    {
        if (_sliderSetting == null)
        {
            Plugin.Log.Info($"{nameof(_sliderSetting)} is null");
            return;
        }

        _sliderSetting.ApplyValue();
        _sliderSetting.Slider.Refresh();
    }
    
    private void BeatmapDidUpdateDifficulty(StandardLevelDetailViewController arg1)
    {
        BeatmapKey beatmapKey = _standardLevelDetailViewController.beatmapKey;
        BeatmapBasicData selectedLevel = _standardLevelDetailViewController._beatmapLevel.GetDifficultyBeatmapData(beatmapKey.beatmapCharacteristic, beatmapKey.difficulty);

        _lastJumpSpeed = selectedLevel.noteJumpMovementSpeed;
        _lastTempo = _standardLevelDetailViewController._beatmapLevel.beatsPerMinute;
        
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTime)));
        UpdateRTSliderText();
    }

    private void BeatmapDidUpdateContent(StandardLevelDetailViewController arg1, StandardLevelDetailViewController.ContentType contentType)
    {
        _lastTempo = _standardLevelDetailViewController._beatmapLevel.beatsPerMinute;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTime)));
        UpdateRTSliderText();
    }

    protected bool Enabled
    {
        get => Config.Enabled;
        set
        {
            Config.Enabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
        }
    }

    protected int ReactionTime
    {
        get => Config.ReactionTime;
        set
        {
            Config.ReactionTime = Math.Max(value, 300);
            
            _snappedRT = Mathf.RoundToInt(Utils.CalculateSnappedRT(_lastTempo, _lastJumpSpeed, Config.ReactionTime));
            
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTime)));
        }
    }
    
    protected bool SnapToNearest
    {
        get => Config.SnapToNearest;
        set
        {
            Config.SnapToNearest = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SnapToNearest)));
            
            UpdateRTSliderText();
        }
    }

    protected int SnapToNearestNoteType
    {
        get => Config.SnapToNearestNoteType;
        set
        {
            Config.SnapToNearestNoteType = Math.Clamp(value, 1, 8);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SnapToNearestNoteType)));
            
            UpdateRTSliderText();
        }
    }
}