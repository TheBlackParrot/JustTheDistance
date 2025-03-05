using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using JetBrains.Annotations;
// ReSharper disable RedundantDefaultMemberInitializer

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace JustTheDistance.Configuration;

[UsedImplicitly]
internal class PluginConfig
{
    public static PluginConfig Instance { get; set; } = null!;
    
    public virtual bool Enabled { get; set; } = true;
    public int ReactionTime { get; set; } = 600;
    public virtual bool SnapToNearest { get; set; } = false;
    public virtual int SnapToNearestNoteType { get; set; } = 4;
    public virtual int MinRTSliderValue { get; set; } = 300;
    public virtual int MaxRTSliderValue { get; set; } = 1500;
    public virtual int RTSliderIncrement { get; set; } = 10;
}