#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AssetBundlePushPull
{
    public class PullConfirmWindow : EditorWindow
    {
        private class Node
        {
            public string name;
            public string status;   // New / Replace / Remove / "" (unchanged folder)
            public bool folder;
            public bool selected;
            public bool expanded = true;
            public bool mixed;      // display: some (not all) descendants selected
            public bool allSel;     // display: all descendants selected
            public List<Node> children = new List<Node>();
        }

        private class Section
        {
            public string title, sub, source, dest;
            public bool expanded = true;
            public List<Node> nodes = new List<Node>();
        }

        private string _serverRoot, _game;
        private readonly List<Section> _sections = new List<Section>();
        private Vector2 _scroll;
        private int _ri;

        private static readonly Color CNew = new Color(0.36f, 0.78f, 0.45f);
        private static readonly Color CRep = new Color(0.95f, 0.76f, 0.32f);
        private static readonly Color CRem = new Color(0.93f, 0.40f, 0.40f);
        private Texture2D _texNew, _texRep, _texRem, _texRow;

        // cached styles/content (built once, reused every repaint -> ~0 GC)
        private bool _stylesReady;
        private GUIStyle _stName, _stNameDim, _stTag, _stHeader, _stHeaderSub, _stDim;
        private GUIContent _icoFolder, _icoScript;
        private string _summary;

        public static void Open(string serverRoot, string game, string abDest, string scrDest, bool pullAb, bool pullScript)
        {
            var w = CreateInstance<PullConfirmWindow>();
            w.titleContent = new GUIContent("Import from Server");
            w._serverRoot = serverRoot; w._game = game;
            if (pullAb) w.BuildSection("AssetBundles", "AssetBundles", abDest);
            if (pullScript) w.BuildSection("Scripts", "Scripts", scrDest);
            w.minSize = new Vector2(580, 500);
            w.ShowUtility();
        }

        // ---------- build tree ----------

        private void BuildSection(string title, string sub, string dest)
        {
            var s = new Section
            {
                title = title, sub = sub, dest = dest,
                source = Path.Combine(Path.Combine(_serverRoot, _game), sub)
            };
            foreach (string n in Union(s.source, dest))
                s.nodes.Add(BuildNode(s.source, dest, n));
            _sections.Add(s);
        }

        private static List<string> Union(string a, string b)
        {
            var set = new SortedSet<string>();
            foreach (string dir in new[] { a, b })
                if (Directory.Exists(dir))
                    foreach (string e in Directory.GetFileSystemEntries(dir))
                    {
                        string n = Path.GetFileName(e);
                        if (!n.EndsWith(".meta")) set.Add(n);
                    }
            return new List<string>(set);
        }

        private static Node BuildNode(string srcParent, string dstParent, string name)
        {
            string src = Path.Combine(srcParent, name);
            string dst = Path.Combine(dstParent, name);
            bool sDir = Directory.Exists(src), dDir = Directory.Exists(dst);
            bool inSrc = sDir || File.Exists(src);
            bool inDst = dDir || File.Exists(dst);
            bool folder = sDir || dDir;

            string status = !inDst ? "New" : !inSrc ? "Remove" : (folder ? "" : "Replace");
            var node = new Node { name = name, status = status, folder = folder, selected = status != "Remove" };

            if (folder)
                foreach (string child in Union(sDir ? src : "", dDir ? dst : ""))
                    node.children.Add(BuildNode(src, dst, child));

            // folders sink below files' selection default handled by children
            return node;
        }

        // ---------- textures ----------

        private Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }

        private void OnEnable()
        {
            _texNew = Tex(CNew); _texRep = Tex(CRep); _texRem = Tex(CRem);
            _texRow = Tex(new Color(1, 1, 1, 0.03f));
        }

        // Built lazily on first OnGUI (EditorStyles is guaranteed valid there).
        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            _stName = new GUIStyle(EditorStyles.label);
            _stNameDim = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
            _stTag = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 9, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.1f, 0.1f, 0.1f) } };
            _stHeader = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 0, 0, 0) };
            _stHeaderSub = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, padding = new RectOffset(0, 14, 0, 0), normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            _stDim = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }, padding = new RectOffset(28, 0, 0, 0) };
            _icoFolder = EditorGUIUtility.IconContent("Folder Icon");
            _icoScript = EditorGUIUtility.IconContent("cs Script Icon");
        }

        // One allocation-free bottom-up pass per repaint: computes folder tri-state
        // (all / none / mixed) from leaf selection. Returns (selected, total) leaves.
        private static void Refresh(Node node, out int sel, out int tot)
        {
            if (node.children.Count == 0)
            {
                tot = 1; sel = node.selected ? 1 : 0; return;
            }
            sel = 0; tot = 0;
            for (int i = 0; i < node.children.Count; i++)
            {
                Refresh(node.children[i], out int s, out int t);
                sel += s; tot += t;
            }
            node.allSel = tot > 0 && sel == tot;
            node.mixed = sel != 0 && sel != tot;
        }

        // ---------- GUI ----------

        private void OnGUI()
        {
            EnsureStyles();
            foreach (var s in _sections)
                for (int i = 0; i < s.nodes.Count; i++)
                    Refresh(s.nodes[i], out _, out _);

            DrawHeader();
            DrawToolbar();

            _ri = 0;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var s in _sections) DrawSection(s);
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawHeader()
        {
            var r = EditorGUILayout.GetControlRect(false, 44);
            EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f));
            GUI.Label(r, _game, _stHeader);
            GUI.Label(r, Summary(), _stHeaderSub);
        }

        private string Summary()
        {
            if (_summary != null) return _summary;   // counts are static -> build once
            int n = 0, rp = 0, rm = 0;
            foreach (var s in _sections) foreach (var node in s.nodes) Count(node, ref n, ref rp, ref rm);
            return _summary = n + " new · " + rp + " replace · " + rm + " remove";
        }

        private void Count(Node node, ref int n, ref int rp, ref int rm)
        {
            if (node.status == "New") n++;
            else if (node.status == "Replace") rp++;
            else if (node.status == "Remove") rm++;
            foreach (var c in node.children) Count(c, ref n, ref rp, ref rm);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("All", EditorStyles.toolbarButton, GUILayout.Width(46))) SetAll(true);
            if (GUILayout.Button("None", EditorStyles.toolbarButton, GUILayout.Width(46))) SetAll(false);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void SetAll(bool v)
        {
            foreach (var s in _sections) foreach (var node in s.nodes) SetRec(node, v);
        }

        private static void SetRec(Node node, bool v)
        {
            node.selected = v;
            foreach (var c in node.children) SetRec(c, v);
        }

        private void DrawSection(Section s)
        {
            s.expanded = EditorGUILayout.Foldout(s.expanded, s.title, true, EditorStyles.foldoutHeader);
            if (!s.expanded) return;

            if (s.nodes.Count == 0)
            {
                EditorGUILayout.LabelField("Already up to date.", _stDim);
                return;
            }
            foreach (var node in s.nodes) DrawNode(node, 1);
        }

        private void DrawNode(Node node, int depth)
        {
            var row = EditorGUILayout.GetControlRect(false, 20);
            if (_ri++ % 2 == 1) GUI.DrawTexture(row, _texRow);

            // Project-window style indentation: 14px per depth level.
            float x = row.x + 6 + depth * 14;

            // Native foldout triangle (only for folders that actually have contents).
            if (node.folder && node.children.Count > 0)
                node.expanded = EditorGUI.Foldout(new Rect(x, row.y, 14, row.height), node.expanded, GUIContent.none, false);

            float cbX = x + 14;

            // Folders show a tri-state checkbox derived from their descendants.
            bool shown = node.folder ? node.allSel : node.selected;
            EditorGUI.showMixedValue = node.folder && node.mixed;
            bool nv = EditorGUI.Toggle(new Rect(cbX, row.y + 2, 16, 16), shown);
            EditorGUI.showMixedValue = false;
            if (nv != shown)
            {
                if (node.folder) SetRec(node, nv);
                else node.selected = nv;
            }

            var ico = node.folder ? _icoFolder : _icoScript;
            if (ico != null && ico.image != null)
                GUI.Label(new Rect(cbX + 20, row.y + 2, 16, 16), ico.image);

            bool active = node.folder ? (node.allSel || node.mixed) : node.selected;
            GUI.Label(new Rect(cbX + 40, row.y, row.width - cbX - 40 - 92, row.height), node.name, active ? _stName : _stNameDim);

            if (!string.IsNullOrEmpty(node.status))
                DrawTag(new Rect(row.xMax - 88, row.y + 3, 80, 15), node.status);

            if (node.folder && node.expanded)
                foreach (var c in node.children) DrawNode(c, depth + 1);
        }

        private void DrawTag(Rect r, string status)
        {
            // Reuse one style; only swap the cached background (no per-frame allocation).
            _stTag.normal.background = status == "New" ? _texNew : status == "Replace" ? _texRep : _texRem;
            string label = status == "New" ? "NEW" : status == "Replace" ? "REPLACE" : "REMOVE";
            GUI.Label(r, label, _stTag);
        }

        private void DrawFooter()
        {
            var line = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(line, new Color(0, 0, 0, 0.3f));
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("Checked items are applied.  New / Replace = copied in.  Remove = deleted from your project.", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(32))) Close();
            GUI.backgroundColor = new Color(0.45f, 0.75f, 0.5f);
            if (GUILayout.Button("Import Selected", GUILayout.Height(32))) Apply();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        // ---------- apply ----------

        private void Apply()
        {
            int total = 0;
            foreach (var s in _sections) foreach (var node in s.nodes) total += CountSel(node, s.source);

            FileUtility.Begin("Importing...", total);
            bool didAb = false, didScript = false;
            try
            {
                foreach (var s in _sections)
                {
                    bool touched = false;
                    foreach (var node in s.nodes)
                        touched |= ApplyNode(node, s.source, s.dest);
                    if (touched && s.sub == "AssetBundles") didAb = true;
                    if (touched && s.sub == "Scripts") didScript = true;
                }
            }
            finally { FileUtility.End(); }

            if (FileUtility.Errors.Count == 0)
                PullManager.RecordPull(_serverRoot, _game, didAb, didScript);

            AssetDatabase.Refresh();
            Close();
            PushManager.Report("Import");
        }

        private int CountSel(Node node, string srcParent)
        {
            string src = Path.Combine(srcParent, node.name);
            if (!node.folder) return (node.selected && node.status != "Remove") ? 1 : 0;
            int c = 0;
            foreach (var child in node.children) c += CountSel(child, src);
            return c;
        }

        private bool ApplyNode(Node node, string srcParent, string dstParent)
        {
            string src = Path.Combine(srcParent, node.name);
            string dst = Path.Combine(dstParent, node.name);

            if (!node.folder)
            {
                if (!node.selected) return false;
                if (node.status == "Remove") FileUtility.DeletePath(dst);
                else FileUtility.CopySingle(src, dst);
                return true;
            }

            if (node.status == "Remove")
            {
                if (node.selected) { FileUtility.DeletePath(dst); return true; }
                return false;
            }

            bool touched = false;
            foreach (var child in node.children)
                touched |= ApplyNode(child, src, dst);

            if (node.status == "New" && node.selected)
            {
                try { Directory.CreateDirectory(dst); } catch { }
                try { if (File.Exists(src + ".meta")) File.Copy(src + ".meta", dst + ".meta", true); } catch { }
            }
            return touched;
        }
    }
}
#endif
