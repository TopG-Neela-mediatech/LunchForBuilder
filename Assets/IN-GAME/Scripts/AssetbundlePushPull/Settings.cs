#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AssetBundlePushPull
{
    // All tool settings are stored in a per-project JSON file under the project's
    // ProjectSettings/ folder (NOT EditorPrefs), so clearing prefs never resets
    // the tool and each project keeps its own values. Public API is unchanged.
    public static class Settings
    {
        private const string DefaultServerRoot = @"\\NAS\Gaming\AssetBundlesStorage";

        [Serializable] private class Entry { public string k; public string v; }
        [Serializable] private class Store { public List<Entry> items = new List<Entry>(); }

        private static Dictionary<string, string> _map;

        private static string FilePath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "ProjectSettings", "AssetBundlePushPull.json");

        private static Dictionary<string, string> Map
        {
            get
            {
                if (_map != null) return _map;
                _map = new Dictionary<string, string>();
                try
                {
                    if (File.Exists(FilePath))
                    {
                        var store = JsonUtility.FromJson<Store>(File.ReadAllText(FilePath));
                        if (store != null && store.items != null)
                            foreach (var e in store.items) _map[e.k] = e.v;
                    }
                }
                catch { }
                return _map;
            }
        }

        private static void Save()
        {
            try
            {
                var store = new Store();
                foreach (var kv in Map) store.items.Add(new Entry { k = kv.Key, v = kv.Value });
                File.WriteAllText(FilePath, JsonUtility.ToJson(store, true));
            }
            catch { }
        }

        private static string GetStr(string key, string def) => Map.TryGetValue(key, out var v) ? v : def;

        // Only writes to disk when the value actually changes (setters are called
        // every OnGUI repaint) -> no per-frame disk IO.
        private static void SetStr(string key, string val)
        {
            if (Map.TryGetValue(key, out var cur) && cur == val) return;
            Map[key] = val;
            Save();
        }

        private static bool GetBool(string key, bool def) => Map.TryGetValue(key, out var v) ? v == "1" : def;
        private static void SetBool(string key, bool val) => SetStr(key, val ? "1" : "0");

        public static string ServerRoot
        {
            get => GetStr("ServerRoot", DefaultServerRoot);
            set => SetStr("ServerRoot", value);
        }

        public static string GameName
        {
            get => GetStr("GameName", "");
            set => SetStr("GameName", value);
        }

        public static string AssetBundleFolder
        {
            get => GetStr("AssetBundleFolder", "");
            set => SetStr("AssetBundleFolder", value);
        }

        public static string PullServerRoot
        {
            get => GetStr("PullServerRoot", "");
            set => SetStr("PullServerRoot", value);
        }

        public static string PullGameName
        {
            get => GetStr("PullGameName", "");
            set => SetStr("PullGameName", value);
        }

        public static string AssetBundleDestination
        {
            get => GetStr("AssetBundleDestination", "");
            set => SetStr("AssetBundleDestination", value);
        }

        public static string ScriptDestination
        {
            get => GetStr("ScriptDestination", "");
            set => SetStr("ScriptDestination", value);
        }

        public static string GameAbDest(string g) => GetStr("ABDest_" + g, "");
        public static void SetGameAbDest(string g, string v) => SetStr("ABDest_" + g, v);

        public static string GameScriptDest(string g) => GetStr("ScrDest_" + g, "");
        public static void SetGameScriptDest(string g, string v) => SetStr("ScrDest_" + g, v);

        public static bool GamePullAb(string g) => GetBool("PullAb_" + g, true);
        public static void SetGamePullAb(string g, bool v) => SetBool("PullAb_" + g, v);

        public static bool GamePullScript(string g) => GetBool("PullScr_" + g, true);
        public static void SetGamePullScript(string g, bool v) => SetBool("PullScr_" + g, v);

        public static bool GameExpanded(string g) => GetBool("Exp_" + g, false);
        public static void SetGameExpanded(string g, bool v) => SetBool("Exp_" + g, v);

        public static bool GameSelected(string g) => GetBool("Sel_" + g, false);
        public static void SetGameSelected(string g, bool v) => SetBool("Sel_" + g, v);

        public static long GameLastPullAb(string g) => long.Parse(GetStr("LastPullAb_" + g, "0"));
        public static void SetGameLastPullAb(string g, long v) => SetStr("LastPullAb_" + g, v.ToString());

        public static long GameLastPullScript(string g) => long.Parse(GetStr("LastPullScr_" + g, "0"));
        public static void SetGameLastPullScript(string g, long v) => SetStr("LastPullScr_" + g, v.ToString());

        public static bool GamePulledOnce(string g) => GetBool("Pulled_" + g, false);
        public static void SetGamePulledOnce(string g, bool v) => SetBool("Pulled_" + g, v);

        public static List<string> ScriptList
        {
            get
            {
                string raw = GetStr("ScriptList", "");
                var list = new List<string>();
                if (!string.IsNullOrEmpty(raw))
                    list.AddRange(raw.Split('\n'));
                return list;
            }
            set => SetStr("ScriptList", string.Join("\n", value));
        }
    }
}
#endif
