using System;
using System.Globalization;
using Celeste.Localization.Languages;
using Celeste.Localization.Lookup;
using Celeste.Simulation.UI.Menus.Text;
using UnityEngine;

namespace Celeste.Unity.Web.Content
{
    internal sealed class UnityWebContentDownloadMenuView
    {
        private readonly UnityWebContentDownloadController controller;
        private readonly TextMenuPresentation menu;
        private readonly DialogCatalog dialog;
        private WebContentDownloadProgress last;
        private DialogLanguage lastLanguage;
        private bool initialized;
        private readonly WebDownloadEstimate estimate = new();
        private double nextEstimate;

        internal readonly TextMenuPresentation.SubHeader Section = new("");
        internal readonly TextMenuPresentation.Button Action = new("");
        internal readonly TextMenuPresentation.SubHeader Status = new("", false) { CompactSectionStart = false };
        internal readonly TextMenuPresentation.SubHeader Progress = new("", false) { CompactSectionStart = false };
        internal readonly TextMenuPresentation.SubHeader Reused = new("", false) { CompactSectionStart = false };
        internal readonly TextMenuPresentation.SubHeader Speed = new("", false) { CompactSectionStart = false };
        internal readonly TextMenuPresentation.SubHeader Remaining = new("", false) { CompactSectionStart = false };

        internal UnityWebContentDownloadMenuView(UnityWebContentDownloadController controller,
            TextMenuPresentation menu, DialogCatalog dialog)
        {
            this.controller = controller;
            this.menu = menu;
            this.dialog = dialog;
        }

        internal void Press()
        {
            var state = controller.ReadProgress().State;
            if (state == WebContentDownloadState.Running) controller.Cancel();
            else if (state != WebContentDownloadState.Completed) controller.Start();
            Refresh(true);
        }

        internal void Refresh(bool resize)
        {
            var value = controller.ReadProgress();
            double now = Time.realtimeSinceStartupAsDouble;
            bool updateEstimate = !initialized || value.State != last.State ||
                                  value.State == WebContentDownloadState.Running && now >= nextEstimate;
            if (updateEstimate)
            {
                estimate.Update(now, value);
                nextEstimate = now + .25;
            }

            if (!updateEstimate && initialized && ReferenceEquals(lastLanguage, dialog.Language) &&
                value.State == last.State && value.ReadyBytes == last.ReadyBytes &&
                value.TotalBytes == last.TotalBytes &&
                value.CachedBytes == last.CachedBytes && value.ReadyFiles == last.ReadyFiles &&
                value.TotalFiles == last.TotalFiles && value.Error == last.Error &&
                value.ReceivedBytes == last.ReceivedBytes && value.DownloadedBytes == last.DownloadedBytes) return;
            if (!initialized || !ReferenceEquals(lastLanguage, dialog.Language))
                UnityWebContentDownloadText.Apply(dialog, dialog.Language);
            initialized = true;
            last = value;
            lastLanguage = dialog.Language;
            Section.Title = Text("section", "Web resources");
            Action.Disabled = value.State == WebContentDownloadState.Completed;
            Action.Label = value.State switch
            {
                WebContentDownloadState.Running => Text("cancel", "Cancel download"),
                WebContentDownloadState.Completed => Text("complete_button", "Download complete"),
                WebContentDownloadState.Cancelled => Text("resume", "Resume download"),
                WebContentDownloadState.Failed => Text("retry", "Retry download"),
                _ => Text("start", "Download all resources")
            };
            Status.Title = value.State switch
            {
                WebContentDownloadState.Running => Text("running", "Downloading resources"),
                WebContentDownloadState.Completed => Text("complete", "All resources downloaded"),
                WebContentDownloadState.Cancelled => Text("cancelled", "Cancelled; cached resources are kept"),
                WebContentDownloadState.Failed => IsQuotaFailure(value.Error)
                    ? Text("quota", "Browser storage is full; free space and retry")
                    : Text("failed", "Download failed; you can retry"),
                _ => Text("idle", "Save resources in this browser")
            };
            Progress.Visible = value.TotalBytes > 0;
            int percent = value.TotalBytes > 0
                ? (int)Math.Min(100, Math.Max(0, Math.Floor(value.ReadyBytes * 100d / value.TotalBytes)))
                : 0;
            Progress.Title = string.Format(CultureInfo.InvariantCulture,
                Text("progress", "{0}%  Cached {1} / {2}  ({3} / {4} files)"),
                percent, Size(value.ReadyBytes), Size(value.TotalBytes), value.ReadyFiles, value.TotalFiles);
            Reused.Visible = value.CachedBytes > 0;
            Reused.Title = string.Format(CultureInfo.InvariantCulture,
                Text("reused_progress", "Reused from cache: {0}"), Size(value.CachedBytes));
            Speed.Visible = Remaining.Visible = value.State == WebContentDownloadState.Running;
            Speed.Title = estimate.BytesPerSecond > 0
                ? string.Format(CultureInfo.InvariantCulture, Text("speed", "Download rate: ~{0}/s"),
                    Rate(estimate.BytesPerSecond))
                : value.DownloadedBytes == 0 && value.ReceivedBytes > value.CachedBytes
                    ? Text("rate_unknown", "Download rate unavailable")
                    : value.DownloadedBytes == 0 && value.CachedBytes > 0
                        ? Text("reading_cache", "Reading local cache")
                        : Text("waiting", "Waiting for download data");
            Remaining.Title = value.TotalBytes > 0 && value.ReceivedBytes >= value.TotalBytes
                ? Text("saving", "Checking and saving files")
                : estimate.RemainingSeconds >= 0
                    ? string.Format(CultureInfo.InvariantCulture, Text("eta", "Time left: ~{0}"),
                        Duration(estimate.RemainingSeconds))
                    : Text("estimating", "Estimating time left");
            if (resize) menu.RecalculateSize();
        }

        private string Text(string key, string fallback)
        {
            string name = "web_content_download_" + key;
            return dialog.Has(name) ? dialog.Clean(name) : fallback;
        }

        private static bool IsQuotaFailure(string error) =>
            error != null && error.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Size(long bytes) => bytes >= 1073741824
            ? (bytes / 1073741824d).ToString("0.00", CultureInfo.InvariantCulture) + " GiB"
            : (bytes / 1048576d).ToString("0.0", CultureInfo.InvariantCulture) + " MiB";

        private static string Rate(double bytes) => bytes >= 1048576
            ? (bytes / 1048576).ToString("0.0", CultureInfo.InvariantCulture) + " MiB"
            : (bytes / 1024).ToString("0.0", CultureInfo.InvariantCulture) + " KiB";

        private static string Duration(double seconds)
        {
            long value = (long)Math.Min(359999, Math.Ceiling(seconds));
            return value >= 3600
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", value / 3600, value / 60 % 60,
                    value % 60)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", value / 60, value % 60);
        }
    }
}
