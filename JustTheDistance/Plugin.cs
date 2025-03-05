using System.Reflection;
using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using JetBrains.Annotations;
using JustTheDistance.Configuration;
using SiraUtil.Zenject;
using JustTheDistance.Installers;
using IPALogger = IPA.Logging.Logger;
using IPAConfig = IPA.Config.Config;

namespace JustTheDistance;

[Plugin(RuntimeOptions.DynamicInit), NoEnableDisable]
[UsedImplicitly]
internal class Plugin
{
    private static Harmony _harmony = null!;
    internal static IPALogger Log { get; private set; } = null!;

    [Init]
    public Plugin(IPALogger ipaLogger, IPAConfig ipaConfig, Zenjector zenjector)
    {
        Log = ipaLogger;
        zenjector.UseLogger(Log);
        
        PluginConfig c = ipaConfig.Generated<PluginConfig>();
        PluginConfig.Instance = c;

        zenjector.Install<MenuInstaller>(Location.Menu);
        
        Log.Info("Plugin loaded");
    }

    [OnEnable]
    public void OnEnable()
    {
        _harmony = new Harmony("TheBlackParrot.JustTheDistance");
        _harmony.PatchAll(Assembly.GetExecutingAssembly());
        
        Log.Info("Patches applied");
    }
    
    [OnDisable]
    public void OnDisable()
    {
        _harmony.UnpatchSelf();
    }
}