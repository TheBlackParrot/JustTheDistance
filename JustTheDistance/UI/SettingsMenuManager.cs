using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using JetBrains.Annotations;
using JustTheDistance.Configuration;
using TMPro;
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
internal class ReactionTimeSlider : IInitializable, IDisposable, INotifyPropertyChanged
{
    private static PluginConfig Config => PluginConfig.Instance;
    public static ReactionTimeSlider? Instance;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    private readonly StandardLevelDetailViewController _standardLevelDetailViewController;
    private readonly LevelSelectionNavigationController _levelSelectionNavigationController;
    private readonly BSMLParser _bsmlParser;
    
    private const string ResourcePath = nameof(JustTheDistance) + ".UI.BSML.ReactionTimeSlider.bsml";
    
    private static float _lastJumpSpeed = 10f;
    private static float _lastTempo = 120f;
    private static int _snappedRT;
    
    [UsedImplicitly]
    private static string TimeFormatter(int x)
    {
        int value = Config.SnapToNearest ? _snappedRT : x;
        return $"{(value >= 1000 ? (value / 1000f).ToString("F2") + "s" : value.ToString("N0") + "ms")}";
    }
    
    // ReSharper disable FieldCanBeMadeReadOnly.Local
    [UIComponent("rtSlider")]
    private SliderSetting _sliderSetting = null!;
    [UIComponent("enableToggle")]
    private ToggleSetting _enableToggle = null!;
    // ReSharper restore FieldCanBeMadeReadOnly.Local
    
    private TextMeshProUGUI _sliderText = null!;

    [UIValue("minRTValue")] [UsedImplicitly] private int SliderMinValue => Config.MinRTSliderValue;
    [UIValue("maxRTValue")] [UsedImplicitly] private int SliderMaxValue => Config.MaxRTSliderValue;
    [UIValue("rtIncrements")] [UsedImplicitly] private int SliderIncrements => Config.RTSliderIncrement;
    
    // ReSharper disable once ConvertToPrimaryConstructor
    public ReactionTimeSlider(StandardLevelDetailViewController standardLevelDetailViewController,
        LevelSelectionNavigationController levelSelectionNavigationController,
        BSMLParser bsmlParser)
    {
        _standardLevelDetailViewController = standardLevelDetailViewController;
        _levelSelectionNavigationController = levelSelectionNavigationController;
        _bsmlParser = bsmlParser;
    }
    
    public void Initialize()
    {
        Instance = this;
        
        _levelSelectionNavigationController.didActivateEvent += DidActivate;

        _standardLevelDetailViewController.didChangeDifficultyBeatmapEvent += BeatmapDidUpdateDifficulty;
        _standardLevelDetailViewController.didChangeContentEvent += BeatmapDidUpdateContent;
        
        _bsmlParser.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), ResourcePath),
            _levelSelectionNavigationController.rectTransform.gameObject, this);
        
        Plugin.Log.Info("Initialized RT slider");
    }

    public void Dispose()
    {
        _levelSelectionNavigationController.didActivateEvent -= DidActivate;

        _standardLevelDetailViewController.didChangeDifficultyBeatmapEvent -= BeatmapDidUpdateDifficulty;
        _standardLevelDetailViewController.didChangeContentEvent -= BeatmapDidUpdateContent;
    }
    
    private void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SliderMinValue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SliderMaxValue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SliderIncrements)));
                
            _sliderText = _sliderSetting.GetComponentInChildren<TextMeshProUGUI>();
            _sliderText.text = "";
            
            _enableToggle.Text = "";
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTime)));
    }

    internal void UpdateRTSliderText()
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

    private static async Task<BeatmapLevel> WaitForBeatmapLoaded(StandardLevelDetailViewController standardLevelDetailViewController)
    {
        while (standardLevelDetailViewController._beatmapLevel == null)
        {
            // this seems... Wrong
            await Task.Yield();
        }

        return standardLevelDetailViewController._beatmapLevel;
    }

    private async void BeatmapDidUpdateContent(StandardLevelDetailViewController arg1, StandardLevelDetailViewController.ContentType contentType)
    {
        try
        {
            BeatmapLevel beatmapLevel = await WaitForBeatmapLoaded(_standardLevelDetailViewController);
            _lastTempo = beatmapLevel.beatsPerMinute;
        
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTime)));
            UpdateRTSliderText();
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e);
        }
    }
    
    protected bool Enabled
    {
        get => Config.Enabled;
        set
        {
            Config.Enabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
            
            _sliderSetting.Interactable = value;
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
}

[UsedImplicitly]
internal class ModSettingsManager : IInitializable, IDisposable, INotifyPropertyChanged
{
    private static PluginConfig Config => PluginConfig.Instance;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    private const string MenuName = nameof(JustTheDistance);
    private const string ResourcePath = nameof(JustTheDistance) + ".UI.BSML.Settings.bsml";

    [UsedImplicitly]
    private string NoteFormatter(int x)
    {
        return x switch
        {
            1 => "Whole",
            2 => "Half",
            3 => "Third",
            4 => "Fourth",
            5 => "Fifth",
            6 => "Sixth",
            7 => "Seventh",
            8 => "Eighth",
            _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
        };
    }
    
    public void Initialize()
    {
        BeatSaberMarkupLanguage.Settings.BSMLSettings.Instance.AddSettingsMenu(MenuName, ResourcePath, this);
        Plugin.Log.Info("Initialized settings tab");
    }

    public void Dispose()
    {
        BeatSaberMarkupLanguage.Settings.BSMLSettings.Instance?.RemoveSettingsMenu(this);
    }
    
    protected bool SnapToNearest
    {
        get => Config.SnapToNearest;
        set
        {
            Config.SnapToNearest = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SnapToNearest)));
            
            ReactionTimeSlider.Instance?.UpdateRTSliderText();
        }
    }

    protected int SnapToNearestNoteType
    {
        get => Config.SnapToNearestNoteType;
        set
        {
            Config.SnapToNearestNoteType = Math.Clamp(value, 1, 8);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SnapToNearestNoteType)));
            
            ReactionTimeSlider.Instance?.UpdateRTSliderText();
        }
    }
}