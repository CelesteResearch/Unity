using System;
using System.IO;
using System.Threading.Tasks;
using Celeste.Formats.Save.Serialization;
using Celeste.Formats.Settings;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Menus.Options;
using Celeste.Game.Save.Data;
using Celeste.Game.Settings;
using Celeste.Simulation.UI.Menus.Text;
using UnityEngine;

namespace Celeste.Unity.Web
{
    internal sealed class UnityWebSaveTransferMenu : IMenuOptionsExtension
    {
        private readonly UnityWebGameHost host;
        private readonly IMenuOptionsExtension resources;
        private bool busy;
        private int slot;

        internal UnityWebSaveTransferMenu(UnityWebGameHost host, IMenuOptionsExtension resources)
        {
            this.host = host;
            this.resources = resources;
        }

        public void AddOptions(TextMenuPresentation menu, bool inGame)
        {
            resources.AddOptions(menu, inGame);
            var dialog = ((IOriginalGameTextEnvironment)menu.EngineContext).Text.Dialog;
            bool chinese = dialog.Language?.Id == "schinese";
            string Text(string zh, string en) => chinese ? zh : en;
            menu.Add(new TextMenuPresentation.SubHeader(Text("设置与存档导入 / 导出", "Import / export settings and saves")));
            menu.Add(new TextMenuPresentation.SubHeader(
                    Text("可导入原作游戏的设置与存档（.celeste 文件）", "Transfer .celeste files to and from the original game"), false)
                { CompactSectionStart = false });
            string idle = inGame
                ? Text("导入请返回主菜单 → 设置", "To import, open Settings from the main menu")
                : Text("导入将替换现有数据，完成后重新加载游戏", "Import replaces settings or the selected save, then reloads");
            var status = new TextMenuPresentation.SubHeader(idle, false)
                { CompactSectionStart = false };
            var selector = new TextMenuPresentation.Slider(Text("导入 / 导出的存档位置", "Save slot to import / export"),
                value => (value + 1) + " (" + value + ".celeste)", 0, 2, slot);
            selector.Change(value => slot = value);
            selector.OnUpdate = () => selector.Disabled = busy;
            menu.Add(selector);

            void Add(string label, bool import, bool settings)
            {
                var button = new TextMenuPresentation.Button(label);
                button.OnPressed = () => _ = TransferAsync(import, settings, chinese, status, menu, idle);
                button.OnUpdate = () => button.Disabled = busy || import && inGame;
                button.Disabled = busy || import && inGame;
                menu.Add(button);
            }

            Add(Text("导出设置文件", "Export settings file"), false, true);
            Add(Text("导入设置文件", "Import settings file"), true, true);
            Add(Text("导出所选存档", "Export selected save"), false, false);
            Add(Text("导入到所选存档位置", "Import into selected save slot"), true, false);
            menu.Add(status);
        }

        private async Task TransferAsync(bool import, bool settings, bool chinese,
            TextMenuPresentation.SubHeader status, TextMenuPresentation menu, string idle)
        {
            if (busy) return;
            busy = true;
            int selectedSlot = slot;
            string name = settings
                ? "settings"
                : selectedSlot.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string temporary = Path.Combine(Application.temporaryCachePath, "celeste-import.celeste");
            bool shutdownStarted = false;
            string failure = chinese ? "无法读取文件，请重试" : "Unable to read the file; please retry";

            void Status(string message)
            {
                status.Title = message;
                menu.RecalculateSize();
            }

            try
            {
                var game = host.ActualProcess;
                var storage = game.UserIOStorage;
                if (!import)
                {
                    Status(chinese ? "正在导出文件…" : "Exporting file...");
                    failure = chinese ? "导出失败，请重试" : "Export failed; please retry";
                    if (!settings && game.SaveContext.CurrentSave?.FileSlot == selectedSlot)
                    {
                        game.UserIO.SaveHandler(true, false);
                        while (game.UserIO.Saving) await Task.Delay(16);
                    }

                    await game.WaitSourceBackgroundThreadsAsync();
                    if (!settings && !File.Exists(storage.GetSaveFilePath(name)))
                    {
                        Status(chinese ? "所选位置没有存档，请选择其他位置" : "This slot is empty; select another slot");
                        return;
                    }

                    byte[] bytes = settings
                        ? storage.Serialize(game.SettingsProcess.Instance)
                        : File.ReadAllBytes(storage.GetSaveFilePath(name));
                    UnityWebBrowserInterop.DownloadSaveFile(name + ".celeste", bytes);
                    Status((chinese ? "已导出：" : "Exported: ") + name + ".celeste");
                    return;
                }

                Status(chinese ? "请选择要导入的 .celeste 文件" : "Choose a .celeste file to import");
                if (!await UnityWebBrowserInterop.SelectSaveFileAsync(temporary))
                {
                    Status(idle);
                    return;
                }

                byte[] imported = File.ReadAllBytes(temporary);
                failure = settings
                    ? (chinese ? "文件不是有效的设置，请选择 settings.celeste" : "Invalid settings file; choose settings.celeste")
                    : (chinese ? "文件不是有效的存档，请选择原作存档文件" : "Invalid save file; choose an original game save");
                if (settings) FileSettingsCodec.Deserialize(imported);
                else SaveXmlCodec.Deserialize(imported);
                string target = settings
                    ? (chinese ? "当前网页的设置" : "this browser's settings")
                    : (chinese
                        ? "当前网页的第 " + (selectedSlot + 1) + " 个存档（" + name + ".celeste）"
                        : "save slot " + (selectedSlot + 1) + " (" + name + ".celeste) in this browser");
                if (!UnityWebBrowserInterop.ConfirmSaveImport(chinese
                        ? "导入文件将替换" + target + "。\n如需保留现有数据，请先取消并导出备份。\n导入完成后会重新加载游戏。是否继续？"
                        : "The imported file will replace " + target +
                          ".\nTo keep your current data, cancel and export a backup first.\nThe page will reload after import. Continue?"))
                {
                    Status(idle);
                    return;
                }

                Status(chinese ? "正在导入，完成后重新加载游戏…" : "Importing; the page will reload when ready...");
                failure = chinese ? "导入失败，请检查网页保存空间后再试" : "Import did not complete; check browser storage and retry";
                // Finish all source saves before replacing files, including shutdown settings writes.
                shutdownStarted = true;
                await host.StopAsync();
                bool saved = settings
                    ? storage.Save<SettingsState>(name, imported)
                    : storage.Save<OriginalSaveDataState>(name, imported);
                if (!saved) throw new IOException("Unable to save imported data.");
                await UnityWebBrowserInterop.SyncPersistentFilesAsync(false);
                UnityWebBrowserInterop.ReloadPage();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                status.Title = failure;
                if (shutdownStarted)
                    UnityWebBrowserInterop.ShowSaveError(chinese
                        ? "导入失败，请保留原文件，检查网页保存空间后重新加载游戏再试。"
                        : "Import did not complete. Keep your original file, check browser storage, then reload and retry.");
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                busy = false;
                if (!shutdownStarted) menu.RecalculateSize();
            }
        }
    }
}
