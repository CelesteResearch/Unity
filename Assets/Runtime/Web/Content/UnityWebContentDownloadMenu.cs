using System;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Menus.Options;
using Celeste.Simulation.UI.Menus.Text;

namespace Celeste.Unity.Web.Content
{
    internal sealed class UnityWebContentDownloadMenu : IMenuOptionsExtension
    {
        private readonly UnityWebContentDownloadController controller;

        public UnityWebContentDownloadMenu(UnityWebContentDownloadController controller)
            => this.controller = controller ?? throw new ArgumentNullException(nameof(controller));

        public void AddOptions(TextMenuPresentation menu, bool inGame)
        {
            var dialog = ((IOriginalGameTextEnvironment)menu.EngineContext).Text.Dialog;
            var view = new UnityWebContentDownloadMenuView(controller, menu, dialog);
            view.Refresh(false);
            menu.Add(view.Section);
            menu.Add(view.Action);
            menu.Add(view.Status);
            menu.Add(view.Progress);
            menu.Add(view.Speed);
            menu.Add(view.Remaining);
            menu.Add(view.Reused);
            view.Action.OnPressed = view.Press;
            view.Action.OnUpdate = () => view.Refresh(true);
        }
    }
}
