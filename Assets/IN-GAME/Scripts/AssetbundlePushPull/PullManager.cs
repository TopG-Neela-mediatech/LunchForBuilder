#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace AssetBundlePushPull
{
    public struct PullPlan
    {
        public List<string> added, replaced, removed;
    }

    public static class PullManager
    {
        private static HashSet<string> Names(string dir)
        {
            var set = new HashSet<string>();
            if (Directory.Exists(dir))
                foreach (string e in Directory.GetFileSystemEntries(dir))
                {
                    string n = Path.GetFileName(e);
                    if (!n.EndsWith(".meta")) set.Add(n);
                }
            return set;
        }

        public static PullPlan Plan(string serverRoot, string game, string sub, string dest)
        {
            string source = Path.Combine(Path.Combine(serverRoot, game), sub);
            var p = new PullPlan { added = new List<string>(), replaced = new List<string>(), removed = new List<string>() };
            var src = Names(source);
            var dst = Names(dest);
            foreach (string n in src) { if (dst.Contains(n)) p.replaced.Add(n); else p.added.Add(n); }
            foreach (string n in dst) if (!src.Contains(n)) p.removed.Add(n);
            return p;
        }

        public static string[] ScanGames(string serverRoot)
        {
            if (string.IsNullOrEmpty(serverRoot) || !Directory.Exists(serverRoot))
                return new string[0];

            string[] dirs = Directory.GetDirectories(serverRoot);
            var names = new string[dirs.Length];
            for (int i = 0; i < dirs.Length; i++)
                names[i] = Path.GetFileName(dirs[i]);
            return names;
        }

        public static string FolderDate(string serverRoot, string gameName, string sub)
        {
            string p = Path.Combine(Path.Combine(serverRoot, gameName), sub);
            if (!Directory.Exists(p)) return "not available";
            return Directory.GetLastWriteTime(p).ToString("yyyy-MM-dd HH:mm");
        }

        public static long SubTicks(string serverRoot, string gameName, string sub)
        {
            string p = Path.Combine(Path.Combine(serverRoot, gameName), sub);
            return Directory.Exists(p) ? Directory.GetLastWriteTime(p).Ticks : 0;
        }

        public static void RecordPull(string serverRoot, string gameName, bool didAb, bool didScript)
        {
            if (didAb) Settings.SetGameLastPullAb(gameName, SubTicks(serverRoot, gameName, "AssetBundles"));
            if (didScript) Settings.SetGameLastPullScript(gameName, SubTicks(serverRoot, gameName, "Scripts"));
            Settings.SetGamePulledOnce(gameName, true);
        }

        public static string UpdateTag(string serverRoot, string gameName)
        {
            // Never pulled into THIS project yet -> nothing to flag as updated.
            if (!Settings.GamePulledOnce(gameName)) return "";

            bool abNew = SubTicks(serverRoot, gameName, "AssetBundles") > Settings.GameLastPullAb(gameName);
            bool scrNew = SubTicks(serverRoot, gameName, "Scripts") > Settings.GameLastPullScript(gameName);

            if (abNew && scrNew) return "NEW UPDATE";
            if (abNew) return "NEW ASSETBUNDLE";
            if (scrNew) return "NEW SCRIPT";
            return "";
        }

        public static void Pull(string serverRoot, string gameName, string abDest, string scriptDest, bool pullAb, bool pullScript, bool mirror)
        {
            if (!pullAb && !pullScript)
            {
                EditorUtility.DisplayDialog("Pull", "Select AssetBundles and/or Scripts.", "OK");
                return;
            }
            if (string.IsNullOrEmpty(serverRoot) || !Directory.Exists(serverRoot))
            {
                EditorUtility.DisplayDialog("Pull", "Invalid Server Root.", "OK");
                return;
            }
            if (string.IsNullOrEmpty(gameName))
            {
                EditorUtility.DisplayDialog("Pull", "No game selected.", "OK");
                return;
            }

            string gameRoot = Path.Combine(serverRoot, gameName);
            string abSource = Path.Combine(gameRoot, "AssetBundles");
            string scriptSource = Path.Combine(gameRoot, "Scripts");

            bool doAb = pullAb && Directory.Exists(abSource) && !string.IsNullOrEmpty(abDest);
            bool doScript = pullScript && Directory.Exists(scriptSource) && !string.IsNullOrEmpty(scriptDest);

            int total = (doAb ? FileUtility.CountFiles(abSource) : 0) +
                        (doScript ? FileUtility.CountFiles(scriptSource) : 0);

            FileUtility.Begin("Pulling...", total);
            try
            {
                if (doAb) { if (mirror) FileUtility.MirrorContents(abSource, abDest); else FileUtility.CopyDirectory(abSource, abDest); }
                if (doScript) { if (mirror) FileUtility.MirrorContents(scriptSource, scriptDest); else FileUtility.CopyContentsReplacing(scriptSource, scriptDest); }
            }
            finally { FileUtility.End(); }

            if (FileUtility.Errors.Count == 0)
            {
                if (doAb) Settings.SetGameLastPullAb(gameName, SubTicks(serverRoot, gameName, "AssetBundles"));
                if (doScript) Settings.SetGameLastPullScript(gameName, SubTicks(serverRoot, gameName, "Scripts"));
                Settings.SetGamePulledOnce(gameName, true);
            }

            PushManager.Report("Pull");
        }
    }
}
#endif

