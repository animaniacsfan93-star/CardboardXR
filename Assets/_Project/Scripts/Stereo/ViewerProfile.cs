using System;
using System.Collections.Generic;

namespace CardboardXR.Stereo
{
    // A viewer the user keeps under a name: what the QR scanner read, or what the tuning panel was
    // set to by hand.
    [Serializable]
    public class ViewerProfile
    {
        public string name = "";
        public ViewerParams viewer = new();

        public ViewerProfile()
        {
        }

        public ViewerProfile(string name, ViewerParams viewer)
        {
            this.name = name;
            this.viewer = viewer;
        }
    }

    // The viewers saved, and which one is in use: what ViewerProfileStore keeps between launches, and
    // what the profiles list and the tuning panel change. Kept apart from PlayerPrefs so it can be
    // tested, and from the scenes so the list can be rebuilt whenever it changes.
    [Serializable]
    public class ViewerProfileSet
    {
        public const string k_DefaultNamePrefix = "Viewer ";

        public List<ViewerProfile> profiles = new();
        public string active = "";

        public ViewerProfile Active => Find(active);

        public ViewerProfile Find(string name)
        {
            for (var i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null && profiles[i].name == name)
                    return profiles[i];
            }
            return null;
        }

        // The name a new viewer gets: the prefix and the first number not taken.
        public string NewName()
        {
            for (var number = 1; ; number++)
            {
                var name = k_DefaultNamePrefix + number;
                if (Find(name) == null)
                    return name;
            }
        }

        // Adds the viewer under the name, replacing one already under it, and makes it the one in use.
        public ViewerProfile Save(string name, ViewerParams viewer)
        {
            name = CleanName(name);
            var profile = Find(name);
            if (profile == null)
            {
                profile = new ViewerProfile(name, viewer.Copy());
                profiles.Add(profile);
            }
            else
            {
                profile.viewer = viewer.Copy();
            }
            active = name;
            return profile;
        }

        public bool Remove(string name)
        {
            var profile = Find(name);
            if (profile == null)
                return false;
            profiles.Remove(profile);
            if (active == name)
                active = "";
            return true;
        }

        // Drops what can't be used, repairs the rest, and gives every viewer a name of its own, so a
        // list read from storage (or edited by hand) is always safe to show and to switch between.
        public void Sanitize()
        {
            for (var i = profiles.Count - 1; i >= 0; i--)
            {
                var profile = profiles[i];
                if (profile == null || profile.viewer == null)
                {
                    profiles.RemoveAt(i);
                    continue;
                }
                profile.viewer.Repair();
                profile.name = CleanName(profile.name);
            }

            for (var i = 0; i < profiles.Count; i++)
            {
                var baseName = profiles[i].name;
                var name = baseName;
                for (var suffix = 2; Taken(name, i); suffix++)
                    name = $"{baseName} {suffix}";
                profiles[i].name = name;
            }

            if (Active == null)
                active = profiles.Count > 0 ? profiles[0].name : "";
        }

        // A name as it's kept: trimmed, and one of its own if it had none.
        public string CleanName(string name)
        {
            name = (name ?? "").Trim();
            return name.Length == 0 ? NewName() : name;
        }

        bool Taken(string name, int before)
        {
            for (var j = 0; j < before; j++)
            {
                if (profiles[j].name == name)
                    return true;
            }
            return false;
        }
    }
}
