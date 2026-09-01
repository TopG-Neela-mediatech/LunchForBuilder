#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace AssetBundlePushPull
{
    public static class FileUtility
    {
        public static List<string> Errors = new List<string>();

        private static int _done;
        private static int _total;
        private static string _title;
        private static int _lastPct;

        public static void Begin(string title, int total)
        {
            Errors.Clear();
            _done = 0;
            _total = total < 1 ? 1 : total;
            _title = title;
            _lastPct = -1;
        }

        public static void End()
        {
            EditorUtility.ClearProgressBar();
        }

        public static int CountFiles(string source, bool ignoreMeta = false)
        {
            if (!Directory.Exists(source)) return File.Exists(source) ? 1 : 0;
            int c = 0;
            foreach (string f in Directory.GetFiles(source))
                if (!(ignoreMeta && f.EndsWith(".meta"))) c++;
            foreach (string d in Directory.GetDirectories(source))
                c += CountFiles(d, ignoreMeta);
            return c;
        }

        public static void CopyDirectory(string source, string dest, bool ignoreMeta = false)
        {
            try { Directory.CreateDirectory(dest); }
            catch (System.Exception ex) { Errors.Add(dest + " : " + ex.Message); return; }

            foreach (string file in Directory.GetFiles(source))
            {
                if (ignoreMeta && file.EndsWith(".meta")) continue;
                CopyFile(file, Path.Combine(dest, Path.GetFileName(file)));
            }

            foreach (string dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)), ignoreMeta);
        }

        public static void MirrorContents(string source, string dest)
        {
            if (Directory.Exists(dest))
            {
                var keep = new System.Collections.Generic.HashSet<string>();
                foreach (string e in Directory.GetFileSystemEntries(source))
                    keep.Add(Path.GetFileName(e));

                foreach (string d in Directory.GetDirectories(dest))
                {
                    string n = Path.GetFileName(d);
                    if (!keep.Contains(n))
                    {
                        try { Directory.Delete(d, true); File.Delete(d + ".meta"); }
                        catch (System.Exception ex) { Errors.Add(d + " : " + ex.Message); }
                    }
                }
                foreach (string f in Directory.GetFiles(dest))
                {
                    if (f.EndsWith(".meta")) continue;
                    if (!keep.Contains(Path.GetFileName(f)))
                    {
                        try { File.Delete(f); File.Delete(f + ".meta"); }
                        catch (System.Exception ex) { Errors.Add(f + " : " + ex.Message); }
                    }
                }
            }
            CopyContentsReplacing(source, dest);
        }

        // Recursively makes 'dest' an exact mirror of 'source': copies new/changed
        // files (incremental via CopyFile) and deletes anything in dest that no
        // longer exists in source. Only the files that actually differ are written.
        public static void MirrorDirectory(string source, string dest)
        {
            try { Directory.CreateDirectory(dest); }
            catch (System.Exception ex) { Errors.Add(dest + " : " + ex.Message); return; }

            var keep = new HashSet<string>();
            foreach (string e in Directory.GetFileSystemEntries(source))
                keep.Add(Path.GetFileName(e));

            foreach (string f in Directory.GetFiles(dest))
                if (!keep.Contains(Path.GetFileName(f)))
                    try { File.Delete(f); } catch (System.Exception ex) { Errors.Add(f + " : " + ex.Message); }

            foreach (string d in Directory.GetDirectories(dest))
                if (!keep.Contains(Path.GetFileName(d)))
                    try { Directory.Delete(d, true); } catch (System.Exception ex) { Errors.Add(d + " : " + ex.Message); }

            foreach (string f in Directory.GetFiles(source))
                CopyFile(f, Path.Combine(dest, Path.GetFileName(f)));

            foreach (string d in Directory.GetDirectories(source))
                MirrorDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
        }

        // Mirrors 'dest' to the merged top-level contents of 'sources' (folders are
        // merged by their contents, single files by name), incrementally. Removes
        // stale top-level entries no source provides any more.
        public static void MirrorInto(string dest, List<string> sources)
        {
            try { Directory.CreateDirectory(dest); }
            catch (System.Exception ex) { Errors.Add(dest + " : " + ex.Message); return; }

            var keep = new HashSet<string>();
            foreach (string s in sources)
            {
                if (string.IsNullOrEmpty(s)) continue;
                if (Directory.Exists(s))
                    foreach (string e in Directory.GetFileSystemEntries(s)) keep.Add(Path.GetFileName(e));
                else if (File.Exists(s)) keep.Add(Path.GetFileName(s));
            }

            foreach (string f in Directory.GetFiles(dest))
                if (!keep.Contains(Path.GetFileName(f)))
                    try { File.Delete(f); } catch (System.Exception ex) { Errors.Add(f + " : " + ex.Message); }

            foreach (string d in Directory.GetDirectories(dest))
                if (!keep.Contains(Path.GetFileName(d)))
                    try { Directory.Delete(d, true); } catch (System.Exception ex) { Errors.Add(d + " : " + ex.Message); }

            foreach (string s in sources)
            {
                if (string.IsNullOrEmpty(s)) continue;
                if (Directory.Exists(s))
                {
                    foreach (string f in Directory.GetFiles(s))
                        CopyFile(f, Path.Combine(dest, Path.GetFileName(f)));
                    foreach (string d in Directory.GetDirectories(s))
                        MirrorDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
                }
                else if (File.Exists(s)) CopyFile(s, Path.Combine(dest, Path.GetFileName(s)));
            }
        }

        public static void CopyContentsReplacing(string source, string dest)
        {
            try { Directory.CreateDirectory(dest); }
            catch (System.Exception ex) { Errors.Add(dest + " : " + ex.Message); return; }

            foreach (string file in Directory.GetFiles(source))
                CopyFile(file, Path.Combine(dest, Path.GetFileName(file)));

            foreach (string dir in Directory.GetDirectories(source))
            {
                string target = Path.Combine(dest, Path.GetFileName(dir));
                try { if (Directory.Exists(target)) Directory.Delete(target, true); }
                catch (System.Exception ex) { Errors.Add(target + " : " + ex.Message); }
                CopyDirectory(dir, target);
            }
        }

        public static void CopyPath(string source, string destRoot)
        {
            if (Directory.Exists(source))
                CopyDirectory(source, Path.Combine(destRoot, Path.GetFileName(source)));
            else if (File.Exists(source))
            {
                try { Directory.CreateDirectory(destRoot); } catch { }
                CopyFile(source, Path.Combine(destRoot, Path.GetFileName(source)));
            }
        }

        public static int CountEntry(string dir, string name) => CountFiles(Path.Combine(dir, name));

        public static void CopyEntry(string sourceDir, string destDir, string name)
        {
            try { Directory.CreateDirectory(destDir); } catch { }
            string src = Path.Combine(sourceDir, name);
            string dst = Path.Combine(destDir, name);

            if (Directory.Exists(src))
            {
                try { if (Directory.Exists(dst)) Directory.Delete(dst, true); }
                catch (System.Exception ex) { Errors.Add(dst + " : " + ex.Message); }
                CopyDirectory(src, dst);
            }
            else if (File.Exists(src)) CopyFile(src, dst);

            CopyMeta(src + ".meta", dst + ".meta");
        }

        public static void RemoveEntry(string destDir, string name)
        {
            string p = Path.Combine(destDir, name);
            try
            {
                if (Directory.Exists(p)) Directory.Delete(p, true);
                else if (File.Exists(p)) File.Delete(p);
                if (File.Exists(p + ".meta")) File.Delete(p + ".meta");
            }
            catch (System.Exception ex) { Errors.Add(p + " : " + ex.Message); }
        }

        public static void CopySingle(string src, string dst)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(dst)); } catch { }
            CopyFile(src, dst);
            CopyMeta(src + ".meta", dst + ".meta");
        }

        public static void DeletePath(string full)
        {
            try
            {
                if (Directory.Exists(full)) Directory.Delete(full, true);
                else if (File.Exists(full)) File.Delete(full);
                if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
            }
            catch (System.Exception ex) { Errors.Add(full + " : " + ex.Message); }
        }

        // True when dst already holds an identical copy of src (same length +
        // timestamp). File.Copy preserves timestamps, so unchanged files match
        // exactly. Only reads metadata (no file contents) -> cheap vs a copy.
        private static bool SameFile(string src, string dst)
        {
            try
            {
                return File.Exists(dst)
                    && File.GetLastWriteTimeUtc(src) == File.GetLastWriteTimeUtc(dst)
                    && new FileInfo(src).Length == new FileInfo(dst).Length;
            }
            catch { return false; }
        }

        private static void CopyMeta(string srcMeta, string dstMeta)
        {
            try
            {
                if (File.Exists(srcMeta) && !SameFile(srcMeta, dstMeta))
                    File.Copy(srcMeta, dstMeta, true);
            }
            catch { }
        }

        private static void CopyFile(string src, string dst)
        {
            _done++;
            // Only refresh the progress bar when the whole-percent advances.
            // Bounds redraws to ~100 regardless of file count (per-file redraws
            // dominated large pulls). Copy behaviour/output is unchanged.
            int pct = _done * 100 / _total;
            if (pct != _lastPct)
            {
                _lastPct = pct;
                EditorUtility.DisplayProgressBar(_title, Path.GetFileName(src), pct * 0.01f);
            }

            // Incremental copy: skip files already identical at the destination.
            if (SameFile(src, dst)) return;

            try { File.Copy(src, dst, true); }
            catch (System.Exception ex) { Errors.Add(src + " : " + ex.Message); }
        }
    }
}
#endif

