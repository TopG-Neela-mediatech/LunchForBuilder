#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AssetBundlePushPull
{
    public class AssetBundlePushPullWindow : EditorWindow
    {
        private int _tab;
        private readonly string[] _tabs = { "Push", "Pull" };

        private List<string> _scripts;
        private Vector2 _scroll;

        private string[] _games = new string[0];
        private int _gameIndex;
        private bool _pullAb = true;
        private bool _pullScript = true;
        private string _scannedRoot;
        private Vector2 _gameScroll;

        // Cached so OnGUI never hits disk / allocates styles every repaint.
        private bool _online;
        private bool _stylesReady;
        private GUIStyle _statusStyle, _tagStyle;
        private struct GameInfo { public string tag, abDate, scrDate; }
        private readonly Dictionary<string, GameInfo> _info = new Dictionary<string, GameInfo>();

        [MenuItem("Tools/AssetBundle Push Pull")]
        private static void Open()
        {
            GetWindow<AssetBundlePushPullWindow>("AssetBundle Push Pull").minSize = new Vector2(420, 400);
        }

        private void OnEnable()
        {
            _scripts = Settings.ScriptList;
            RefreshGames();
        }

        // Re-cache status/tags when returning to the window (e.g. after a Pull),
        // so freshly-changed folder state shows without per-repaint disk IO.
        private void OnFocus()
        {
            RecheckStatus();
            RebuildInfo();
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            _statusStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            _tagStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.3f, 0.8f, 0.3f) } };
        }

        private void OnGUI()
        {
            EnsureStyles();
            _tab = GUILayout.Toolbar(_tab, _tabs);
            EditorGUILayout.Space();

            if (_tab == 0) DrawPush();
            else DrawPull();
        }

        private void DrawPush()
        {
            Settings.ServerRoot = PathField("Server Root Path", Settings.ServerRoot);
            DrawServerStatus();
            Settings.GameName = EditorGUILayout.TextField("Game Name", Settings.GameName);
            Settings.AssetBundleFolder = PathField("AssetBundle Folder", Settings.AssetBundleFolder);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scripts / Assets to Share", EditorStyles.boldLabel);
            DrawScriptList();

            EditorGUILayout.Space();
            if (GUILayout.Button("Push", GUILayout.Height(30)))
            {
                Settings.ScriptList = _scripts;
                PushManager.Push(Settings.ServerRoot, Settings.GameName, Settings.AssetBundleFolder, _scripts);
            }
        }

        private void DrawScriptList()
        {
            Rect drop = EditorGUILayout.GetControlRect(false, 40);
            GUI.Box(drop, "Drag & Drop folders / assets here");
            HandleDrop(drop);

            int up = -1, down = -1, remove = -1;
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(160));
            for (int i = 0; i < _scripts.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(_scripts[i]);
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("▲", GUILayout.Width(24))) up = i;
                using (new EditorGUI.DisabledScope(i == _scripts.Count - 1))
                    if (GUILayout.Button("▼", GUILayout.Width(24))) down = i;
                if (GUILayout.Button("X", GUILayout.Width(24))) remove = i;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (up > 0) Swap(up, up - 1);
            else if (down >= 0) Swap(down, down + 1);
            else if (remove >= 0) { _scripts.RemoveAt(remove); Save(); }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Folder")) AddPath(EditorUtility.OpenFolderPanel("Add Folder", "", ""));
            if (GUILayout.Button("Add File")) AddPath(EditorUtility.OpenFilePanel("Add File", "", ""));
            EditorGUILayout.EndHorizontal();
        }

        private void HandleDrop(Rect rect)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;
            if (e.type == EventType.DragUpdated) DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            else if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (string p in DragAndDrop.paths) AddPath(p);
                e.Use();
            }
        }

        private void AddPath(string p)
        {
            if (!string.IsNullOrEmpty(p) && !_scripts.Contains(p)) { _scripts.Add(p); Save(); }
        }

        private void Swap(int a, int b) { (_scripts[a], _scripts[b]) = (_scripts[b], _scripts[a]); Save(); }
        private void Save() => Settings.ScriptList = _scripts;

        private static bool IsServerOnline()
        {
            return !string.IsNullOrEmpty(Settings.ServerRoot) && System.IO.Directory.Exists(Settings.ServerRoot);
        }

        private void DrawServerStatus()
        {
            bool set = !string.IsNullOrEmpty(Settings.ServerRoot);
            string msg = !set ? "Server root not set." : _online ? "● Server connected" : "● Server offline / not connected";
            _statusStyle.normal.textColor = _online ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(msg, _statusStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(64)))
            {
                RecheckStatus();   // fresh, uncached connection check
                RebuildInfo();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void RecheckStatus() => _online = IsServerOnline();

        private void RebuildInfo()
        {
            _info.Clear();
            if (!_online) return;
            string root = Settings.ServerRoot;
            foreach (string g in _games)
                _info[g] = new GameInfo
                {
                    tag = PullManager.UpdateTag(root, g),
                    abDate = PullManager.FolderDate(root, g, "AssetBundles"),
                    scrDate = PullManager.FolderDate(root, g, "Scripts")
                };
        }

        private static string PathField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            value = EditorGUILayout.TextField(label, value);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string picked = EditorUtility.OpenFolderPanel(label, value, "");
                if (!string.IsNullOrEmpty(picked)) value = picked;
            }
            EditorGUILayout.EndHorizontal();
            return value;
        }

        private void DrawPull()
        {
            if (Settings.ServerRoot != _scannedRoot) RefreshGames();

            EditorGUILayout.BeginHorizontal();
            Settings.ServerRoot = EditorGUILayout.TextField("Server Root", Settings.ServerRoot);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string picked = EditorUtility.OpenFolderPanel("Server Root", Settings.ServerRoot, "");
                if (!string.IsNullOrEmpty(picked)) { Settings.ServerRoot = picked; RefreshGames(); }
            }
            if (GUILayout.Button("Scan", GUILayout.Width(50))) RefreshGames();
            EditorGUILayout.EndHorizontal();

            DrawServerStatus();
            if (!_online) return;

            if (_games.Length == 0)
            {
                EditorGUILayout.HelpBox("No games found on server.", MessageType.Info);
                return;
            }

            _gameScroll = EditorGUILayout.BeginScrollView(_gameScroll);
            foreach (string g in _games) DrawGameBlock(g);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Pull All Selected", GUILayout.Height(30)))
                PullSelected();
        }

        // Batch-pulls every ticked game in one pass, then refreshes the
        // AssetDatabase a single time (instead of once per game).
        private void PullSelected()
        {
            string root = Settings.ServerRoot;
            int pulled = 0;
            foreach (string g in _games)
            {
                if (!Settings.GameSelected(g)) continue;
                bool ab = Settings.GamePullAb(g);
                bool scr = Settings.GamePullScript(g);
                if (!ab && !scr) continue;
                string abDest = Settings.GameAbDest(g), scrDest = Settings.GameScriptDest(g);
                if (ab && string.IsNullOrEmpty(abDest))
                { EditorUtility.DisplayDialog("Pull All Selected", g + ": AssetBundle destination path is empty. Set it before pulling.", "OK"); return; }
                if (scr && string.IsNullOrEmpty(scrDest))
                { EditorUtility.DisplayDialog("Pull All Selected", g + ": Script destination path is empty. Set it before pulling.", "OK"); return; }
                PullManager.Pull(root, g, abDest, scrDest, ab, scr, false);
                pulled++;
            }

            if (pulled == 0)
            {
                EditorUtility.DisplayDialog("Pull All Selected",
                    "Tick at least one game (with AssetBundles and/or Scripts enabled).", "OK");
                return;
            }

            AssetDatabase.Refresh();
            RebuildInfo();
            Repaint();
        }

        private void DrawGameBlock(string g)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            Settings.SetGameSelected(g, EditorGUILayout.Toggle(Settings.GameSelected(g), GUILayout.Width(18)));
            bool exp = EditorGUILayout.Foldout(Settings.GameExpanded(g), g, true, EditorStyles.foldoutHeader);
            Settings.SetGameExpanded(g, exp);
            _info.TryGetValue(g, out var gi);
            if (!string.IsNullOrEmpty(gi.tag))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("● " + gi.tag, _tagStyle);
            }
            EditorGUILayout.EndHorizontal();
            if (!exp) { EditorGUILayout.EndVertical(); return; }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("AssetBundle", GUILayout.Width(75));
            EditorGUILayout.LabelField(gi.abDate, GUILayout.Width(110));
            Settings.SetGameAbDest(g, EditorGUILayout.TextField(Settings.GameAbDest(g)));
            if (GUILayout.Button("...", GUILayout.Width(26)))
            {
                string p = EditorUtility.OpenFolderPanel("AssetBundle Destination", Settings.GameAbDest(g), "");
                if (!string.IsNullOrEmpty(p)) Settings.SetGameAbDest(g, p);
            }
            Settings.SetGamePullAb(g, EditorGUILayout.Toggle(Settings.GamePullAb(g), GUILayout.Width(20)));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scripts", GUILayout.Width(75));
            EditorGUILayout.LabelField(gi.scrDate, GUILayout.Width(110));
            Settings.SetGameScriptDest(g, EditorGUILayout.TextField(Settings.GameScriptDest(g)));
            if (GUILayout.Button("...", GUILayout.Width(26)))
            {
                string p = EditorUtility.OpenFolderPanel("Script Destination", Settings.GameScriptDest(g), "");
                if (!string.IsNullOrEmpty(p)) Settings.SetGameScriptDest(g, p);
            }
            Settings.SetGamePullScript(g, EditorGUILayout.Toggle(Settings.GamePullScript(g), GUILayout.Width(20)));
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Pull " + g))
            {
                bool ab = Settings.GamePullAb(g), scr = Settings.GamePullScript(g);
                if (!ab && !scr)
                    EditorUtility.DisplayDialog("Pull", "Select AssetBundles and/or Scripts.", "OK");
                else if (ab && string.IsNullOrEmpty(Settings.GameAbDest(g)))
                    EditorUtility.DisplayDialog("Pull", "AssetBundle destination path is empty. Set it before pulling.", "OK");
                else if (scr && string.IsNullOrEmpty(Settings.GameScriptDest(g)))
                    EditorUtility.DisplayDialog("Pull", "Script destination path is empty. Set it before pulling.", "OK");
                else
                    PullConfirmWindow.Open(Settings.ServerRoot, g, Settings.GameAbDest(g), Settings.GameScriptDest(g), ab, scr);
            }
            EditorGUILayout.EndVertical();
        }

        private void RefreshGames()
        {
            _games = PullManager.ScanGames(Settings.ServerRoot);
            _gameIndex = 0;
            _scannedRoot = Settings.ServerRoot;
            RecheckStatus();
            RebuildInfo();
        }
    }
}
#endif

