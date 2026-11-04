using System.Collections.Generic;
using UnityEngine;

namespace CardboardXR.Stereo
{
    // The viewers the user has saved, by name, and the one in use: kept in PlayerPrefs as JSON between
    // launches. The QR scanner saves what it read here, and the tuning panel saves what it was set to
    // by hand, so both kinds sit in one list to switch between (ViewerSetupController), and the app
    // starts with whichever was last chosen. A viewer an older build saved from a QR code, before
    // there were profiles, is moved into the list the first time it's read.
    public static class ViewerProfileStore
    {
        const string k_SavedKey = "CardboardXR.Viewers";
        // Where a viewer scanned by an older build was kept.
        const string k_OldViewerUrlKey = "CardboardXR.ViewerUrl";

        public static ViewerProfileSet Load()
        {
            var json = PlayerPrefs.GetString(k_SavedKey, "");
            ViewerProfileSet set = null;
            if (json.Length > 0)
            {
                try
                {
                    set = JsonUtility.FromJson<ViewerProfileSet>(json);
                }
                catch (System.ArgumentException e)
                {
                    Debug.LogWarning($"[Viewer] Saved viewers couldn't be read ({e.Message}); starting the list again");
                }
            }

            set ??= new ViewerProfileSet();
            var migrated = MigrateOldViewer(set);
            set.Sanitize();
            if (migrated)
                Store(set);
            return set;
        }

        public static void Store(ViewerProfileSet set)
        {
            set.Sanitize();
            PlayerPrefs.SetString(k_SavedKey, JsonUtility.ToJson(set));
            PlayerPrefs.Save();
        }

        // Adds the viewer under the name, replacing one already under it, makes it the one in use, and
        // keeps them all.
        public static ViewerProfile Save(string name, ViewerParams viewer)
        {
            var set = Load();
            var profile = set.Save(name, viewer);
            Store(set);
            return profile;
        }

        public static void SetActive(string name)
        {
            var set = Load();
            if (set.Find(name) == null)
                return;
            set.active = name;
            Store(set);
        }

        public static bool Remove(string name)
        {
            var set = Load();
            if (!set.Remove(name))
                return false;
            Store(set);
            return true;
        }

        // The viewer in use, or null if there's none saved.
        public static ViewerProfile Active => Load().Active;

        public static string ActiveName => Load().active;

        public static IReadOnlyList<ViewerProfile> Profiles => Load().profiles;

        // A name no saved viewer has.
        public static string NewName() => Load().NewName();

        // A QR-scanned viewer saved by an older build, as a profile named after it.
        static bool MigrateOldViewer(ViewerProfileSet set)
        {
            var url = PlayerPrefs.GetString(k_OldViewerUrlKey, "");
            if (url.Length == 0)
                return false;
            PlayerPrefs.DeleteKey(k_OldViewerUrlKey);
            if (ViewerParamsCodec.TryDecodeUrl(url, out var viewer))
            {
                var name = $"{viewer.vendor} {viewer.model}".Trim();
                set.Save(name, viewer);
                Debug.Log($"[Viewer] Kept the viewer an older build saved as {set.active}");
            }
            return true;
        }
    }
}
