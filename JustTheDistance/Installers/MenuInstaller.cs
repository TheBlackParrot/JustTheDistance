using JetBrains.Annotations;
using Zenject;
using JustTheDistance.UI;

namespace JustTheDistance.Installers;

[UsedImplicitly]
internal class MenuInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesTo<ModSettingsManager>().AsSingle();
        Container.BindInterfacesTo<ReactionTimeSlider>().AsSingle();
    }
}