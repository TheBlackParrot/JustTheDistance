using JetBrains.Annotations;
using Zenject;
using JustTheDistance.UI;

namespace JustTheDistance.Installers;

[UsedImplicitly]
internal class MenuInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<ModSettingsManager>().AsSingle();
        Container.BindInterfacesTo<ReactionTimeSlider>().AsSingle();
    }
}