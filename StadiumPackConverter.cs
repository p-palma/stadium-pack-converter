// stadium-pack-converter (Kojuro skin): turns any FM stadium picture pack into the Kojuro skin's format.
//
// The skin's stadium card asks FM for graphics/pictures/continent/<club id>/logo (screens.md). FM27 only reads a
// photo that sits next to the config.xml that maps it, so the photos are copied into one folder,
// graphics/Stadium Pack, as stadium_<club id>.<ext>, with one config mapping each to its club.
//
// Sources: every config.xml under the folders the user picks. A record whose "to" is
// [graphics/]pictures/club/<id>/stadium links a photo to a club (team/<id>/stadium is used only when no club record
// exists for that id). Records to pictures/stadium/<id> link a stadium, not a club: FM27 doesn't tell the skin a
// club's stadium, so those are only counted. Folders with "stadium" in their path and no config.xml also count files
// named <club id>.<ext>. The first source in the list wins a club.
//
// Build: build.bat (C# 5, the compiler that ships with Windows). Test mode:
//   stadium-pack-converter.exe --source <dir> [--source <dir> ...] --out <folder> --report <file>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// File properties (Windows Explorer > Properties > Details). An exe with none looks suspicious to antivirus heuristics.
[assembly: AssemblyTitle("Stadium Pack Converter")]
[assembly: AssemblyDescription("Converts a Football Manager stadium picture pack for the Kojuro skin's stadium card")]
[assembly: AssemblyCompany("KojuroPP")]
[assembly: AssemblyProduct("Kojuro Skin for FM27")]
[assembly: AssemblyCopyright("Free and unofficial. twitch.tv/kojuropp")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Kojuro
{
    public class Result
    {
        public int Clubs, Sources, Configs, Duplicates, StadiumLinked, Missing, LooseFiles, BadIds;
        public List<string> Notes = new List<string>();

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("Done: {0} clubs now have a stadium photo.", Clubs));
            sb.AppendLine(string.Format("  {0} config files read.", Configs));
            if (LooseFiles > 0) sb.AppendLine(string.Format("  {0} of them from files named after the club ID.", LooseFiles));
            if (Duplicates > 0) sb.AppendLine(string.Format("  {0} photos skipped: a pack higher in the list already had that club.", Duplicates));
            if (StadiumLinked > 0) sb.AppendLine(string.Format("  {0} photos skipped: linked to a stadium, not a club (FM27 can't place those).", StadiumLinked));
            if (BadIds > 0) sb.AppendLine(string.Format("  {0} lines skipped: the club ID in the pack is malformed (starts with 0).", BadIds));
            if (Missing > 0) sb.AppendLine(string.Format("  {0} lines in the packs point to a photo file that isn't there.", Missing));
            foreach (var n in Notes) sb.AppendLine("  " + n);
            sb.AppendLine("Start FM27 and open a club page.");
            return sb.ToString();
        }
    }

    public static class Converter
    {
        public const string OutName = "Stadium Pack";
        static readonly string[] Exts = { ".jpg", ".jpeg", ".png", ".bmp", ".tga" };
        static readonly Regex Record = new Regex(@"<record\b([^>]*)/?>", RegexOptions.IgnoreCase);
        static readonly Regex Attr = new Regex(@"\b(from|to)\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase);
        static readonly Regex ClubTo = new Regex(@"^(?:graphics/)?pictures/(club|team)/(\d+)/stadium$", RegexOptions.IgnoreCase);
        static readonly Regex StadiumTo = new Regex(@"^(?:graphics/)?pictures/stadium/\d+/", RegexOptions.IgnoreCase);
        static readonly Regex Comment = new Regex(@"<!--.*?-->", RegexOptions.Singleline);

        public static string DefaultGraphics()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                "Sports Interactive", "Football Manager 27", "graphics");
        }

        static void Walk(string dir, string skip, Action<string> onDir)
        {
            if (string.Equals(Path.GetFullPath(dir).TrimEnd('\\'), skip, StringComparison.OrdinalIgnoreCase)) return;
            onDir(dir);
            string[] subs;
            try { subs = Directory.GetDirectories(dir); } catch { return; }
            Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
            foreach (var s in subs)
            {
                try { if ((File.GetAttributes(s) & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                Walk(s, skip, onDir);
            }
        }

        static string FindPhoto(string folder, string from)
        {
            string rel = from.Replace('/', Path.DirectorySeparatorChar).Trim();
            if (rel.Length == 0) return null;
            string bas;
            try { bas = Path.Combine(folder, rel); } catch { return null; }
            string ext = Path.GetExtension(bas).ToLowerInvariant();
            if (Exts.Contains(ext) && File.Exists(bas)) return bas;
            foreach (var e in Exts) if (File.Exists(bas + e)) return bas + e;
            return null;
        }

        // Returns club id -> photo path, in priority order.
        public static Dictionary<string, string> Scan(IList<string> sources, string output, Result r, Action<string> log)
        {
            var club = new Dictionary<string, string>();
            var team = new Dictionary<string, string>();
            string skip = Path.GetFullPath(output).TrimEnd('\\');
            foreach (var src in sources)
            {
                if (!Directory.Exists(src)) { r.Notes.Add("Folder not found: " + src); continue; }
                int before = club.Count;
                var dirs = new List<string>();
                Walk(src, skip, d => dirs.Add(d));
                foreach (var d in dirs)
                {
                    string cfg = Path.Combine(d, "config.xml");
                    if (File.Exists(cfg))
                    {
                        r.Configs++;
                        string text;
                        try { text = Comment.Replace(File.ReadAllText(cfg), ""); } catch { continue; }
                        foreach (Match m in Record.Matches(text))
                        {
                            string from = null, to = null;
                            foreach (Match a in Attr.Matches(m.Groups[1].Value))
                            {
                                string v = a.Groups[2].Success ? a.Groups[2].Value : a.Groups[3].Value;
                                if (a.Groups[1].Value.ToLowerInvariant() == "from") from = v; else to = v;
                            }
                            if (from == null || to == null) continue;
                            to = to.Trim().Replace('\\', '/');
                            var cm = ClubTo.Match(to);
                            if (!cm.Success)
                            {
                                if (StadiumTo.IsMatch(to)) r.StadiumLinked++;
                                continue;
                            }
                            string id = cm.Groups[2].Value;
                            if (id.Length > 1 && id[0] == '0') { r.BadIds++; continue; }  // a typo in the pack: FM never asks for it
                            bool isClub = cm.Groups[1].Value.ToLowerInvariant() == "club";
                            var target = isClub ? club : team;
                            if (target.ContainsKey(id)) { if (isClub) r.Duplicates++; continue; }
                            string photo = FindPhoto(d, from);
                            if (photo == null) { r.Missing++; continue; }
                            target[id] = photo;
                        }
                    }
                    else if (d.IndexOf("stadium", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string[] files;
                        try { files = Directory.GetFiles(d); } catch { continue; }
                        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                        foreach (var f in files)
                        {
                            string name = Path.GetFileNameWithoutExtension(f);
                            if (!Exts.Contains(Path.GetExtension(f).ToLowerInvariant()) || name.Length == 0 || !name.All(char.IsDigit)) continue;
                            if (club.ContainsKey(name)) { r.Duplicates++; continue; }
                            club[name] = f; r.LooseFiles++;
                        }
                    }
                }
                r.Sources++;
                if (log != null) log(string.Format("{0}: {1} clubs", src, club.Count - before));
            }
            foreach (var kv in team) if (!club.ContainsKey(kv.Key)) club[kv.Key] = kv.Value;
            return club;
        }

        public const string Marker = "<!-- made by stadium-pack-converter (Kojuro skin) -->";

        // true when the folder has a config.xml this app did not write (a pack's own): never overwrite it
        public static bool HasForeignConfig(string folder)
        {
            string cfg = Path.Combine(folder, "config.xml");
            if (!File.Exists(cfg)) return false;
            try
            {
                using (var rd = new StreamReader(cfg))
                {
                    char[] b = new char[400];
                    int n = rd.Read(b, 0, b.Length);
                    return new string(b, 0, n).IndexOf(Marker) < 0;
                }
            }
            catch { return true; }
        }

        public static string DefaultOutput() { return Path.Combine(DefaultGraphics(), OutName); }

        // output = the folder the photos and config go into (the user's choice; default graphics\Stadium Pack)
        public static Result Run(IList<string> sources, string output, Action<int, int> progress, Action<string> log)
        {
            var r = new Result();
            if (HasForeignConfig(output)) throw new IOException("That folder already has a config.xml from another pack. Choose an empty folder.");
            var photos = Scan(sources, output, r, log);
            var asm = Assembly.GetExecutingAssembly();

            Directory.CreateDirectory(output);
            foreach (var f in Directory.GetFiles(output, "stadium_*")) { try { File.Delete(f); } catch { } }

            var sb = new StringBuilder();
            sb.Append("<record>\n\t" + Marker + "\n\t<boolean id=\"preload\" value=\"false\"/>\n\t<boolean id=\"amap\" value=\"false\"/>\n\t<list id=\"maps\">\n");
            int done = 0, total = photos.Count;
            var written = new HashSet<string>();
            foreach (var kv in photos.OrderBy(k => long.Parse(k.Key)))
            {
                done++;
                if (written.Contains(kv.Key)) continue;
                try { File.Copy(kv.Value, Path.Combine(output, "stadium_" + kv.Key + Path.GetExtension(kv.Value).ToLowerInvariant()), true); }
                catch (Exception e) { r.Notes.Add("Could not copy " + kv.Value + ": " + e.Message); continue; }
                written.Add(kv.Key);
                AppendRecords(sb, kv.Key);
                if (progress != null && done % 50 == 0) progress(done, total);
            }
            sb.Append("\t</list>\n</record>\n");
            File.WriteAllText(Path.Combine(output, "config.xml"), sb.ToString(), new UTF8Encoding(false));
            using (var s = asm.GetManifestResourceStream("CREDITS.txt"))
            using (var o = File.Create(Path.Combine(output, "CREDITS.txt")))
                if (s != null) s.CopyTo(o);
            r.Clubs = written.Count;
            if (progress != null) progress(total, total);
            return r;
        }

        static void AppendRecords(StringBuilder sb, string id)
        {
            sb.AppendFormat("\t\t<record from=\"stadium_{0}\" to=\"graphics/pictures/continent/{0}/logo\"/>\n", id);
            sb.AppendFormat("\t\t<record from=\"stadium_{0}\" to=\"graphics/pictures/continent/{0}/icon\"/>\n", id);
        }
    }

    // Design C of the "stadium-pack-converter" canvas: one folder box, one button, then a done screen.
    public class MainForm : Form
    {
        static readonly Color Bg = Color.FromArgb(21, 23, 26), Field = Color.FromArgb(15, 17, 19), Line = Color.FromArgb(58, 62, 68),
                              Text1 = Color.FromArgb(232, 234, 237), Text2 = Color.FromArgb(154, 160, 166), Accent = Color.FromArgb(79, 227, 238);
        Panel form = new Panel(), done = new Panel();
        TextBox folder = new TextBox(), dest = new TextBox();
        Button choose = new Button(), chooseDest = new Button(), convert = new Button();
        Label doneTitle = new Label(), doneText = new Label();
        BackgroundWorker worker = new BackgroundWorker();
        string lastDest = "";

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1;
            try { DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { }  // dark title bar on Windows 10/11
        }

        public MainForm()
        {
            Text = "stadium-pack-converter";
            Font = new Font("Segoe UI", 10f);
            BackColor = Bg; ForeColor = Text1;
            ClientSize = new Size(520, 336);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            foreach (var p in new[] { form, done }) { p.SetBounds(0, 0, 520, 336); p.BackColor = Bg; Controls.Add(p); }

            form.Controls.Add(new Label { Text = "Stadium photos for the Kojuro skin", Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                                          ForeColor = Text1, Left = 26, Top = 22, Width = 470, Height = 34 });
            form.Controls.Add(new Label { Text = "Your stadium pack folder", ForeColor = Text2, Left = 28, Top = 72, Width = 470, Height = 22 });
            folder.SetBounds(28, 100, 350, 30); folder.BackColor = Field; folder.ForeColor = Text1; folder.BorderStyle = BorderStyle.FixedSingle;
            form.Controls.Add(folder);
            Style(choose, "Choose…", false); choose.SetBounds(388, 98, 104, 31); choose.Click += (s, e) => Choose();
            form.Controls.Add(choose);
            form.Controls.Add(new Label { Text = "Save to", ForeColor = Text2, Left = 28, Top = 144, Width = 470, Height = 22 });
            dest.SetBounds(28, 172, 350, 30); dest.BackColor = Field; dest.ForeColor = Text1; dest.BorderStyle = BorderStyle.FixedSingle;
            dest.Text = Converter.DefaultOutput(); form.Controls.Add(dest);
            Style(chooseDest, "Choose…", false); chooseDest.SetBounds(388, 170, 104, 31); chooseDest.Click += (s, e) => ChooseDest();
            form.Controls.Add(chooseDest);
            Style(convert, "Convert Stadium Pack", true); convert.SetBounds(28, 262, 464, 46); convert.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            convert.Click += (s, e) => Start();
            form.Controls.Add(convert);

            done.Visible = false;
            var check = new Panel { Left = 236, Top = 34, Width = 48, Height = 48, BackColor = Bg };
            check.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(Accent, 2.4f))
                {
                    e.Graphics.DrawEllipse(pen, 3, 3, 41, 41);
                    e.Graphics.DrawLines(pen, new[] { new PointF(15, 24.5f), new PointF(21.5f, 31f), new PointF(33, 18) });
                }
            };
            done.Controls.Add(check);
            doneTitle.SetBounds(10, 96, 500, 34); doneTitle.TextAlign = ContentAlignment.MiddleCenter; doneTitle.ForeColor = Text1;
            doneTitle.Font = new Font("Segoe UI", 14f, FontStyle.Bold); done.Controls.Add(doneTitle);
            doneText.SetBounds(10, 134, 500, 170); doneText.TextAlign = ContentAlignment.TopCenter; doneText.ForeColor = Text2; done.Controls.Add(doneText);

            folder.Text = Suggest();
            Shown += (s, e) => convert.Focus();  // don't open with the path selected
            worker.WorkerReportsProgress = true;
            worker.DoWork += (s, e) =>
            {
                var a = (object[])e.Argument;
                e.Result = Converter.Run(new List<string> { (string)a[0] }, (string)a[1],
                    (d, t) => worker.ReportProgress(t == 0 ? 100 : d * 100 / t), null);
            };
            worker.ProgressChanged += (s, e) => { convert.Text = string.Format("Converting… {0}%", e.ProgressPercentage); };
            worker.RunWorkerCompleted += (s, e) =>
            {
                Style(convert, "Convert Stadium Pack", true); convert.Enabled = true;
                if (e.Error != null) { MessageBox.Show(this, "Something went wrong:\n" + e.Error.Message, Text); return; }
                var r = (Result)e.Result;
                if (r.Clubs == 0)
                {
                    MessageBox.Show(this, "No stadium photos linked to clubs were found in that folder.\n\nChoose the folder of your stadium pack (the one with its config.xml files, or a folder above it).", Text);
                    return;
                }
                doneTitle.Text = string.Format("{0:N0} clubs have a stadium photo", r.Clubs);
                string saved = lastDest;
                string parent = Path.GetDirectoryName(Path.GetFullPath(lastDest).TrimEnd('\\')) ?? "";
                bool inGraphics = string.Equals(parent.TrimEnd('\\'), Path.GetFullPath(Converter.DefaultGraphics()).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                doneText.Text = inGraphics
                    ? "Start FM27 (restart it if it was open) and open a club page.\n\nSaved in " + saved
                    : "Saved in " + saved + "\n\nCopy that folder into your FM27 graphics folder, then start FM27.";
                form.Visible = false; done.Visible = true;
            };
        }

        static void Style(Button b, string text, bool primary)
        {
            b.Text = text; b.FlatStyle = FlatStyle.Flat; b.Cursor = Cursors.Hand;
            b.BackColor = primary ? Accent : Bg; b.ForeColor = primary ? Color.FromArgb(11, 12, 13) : Text1;
            b.FlatAppearance.BorderSize = primary ? 0 : 1; b.FlatAppearance.BorderColor = Line;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(130, 236, 243) : Color.FromArgb(32, 35, 39);
        }

        // a pack already in the FM27 graphics folder, else empty
        string Suggest()
        {
            try
            {
                foreach (var d in Directory.GetDirectories(Converter.DefaultGraphics()))
                {
                    string n = Path.GetFileName(d);
                    if (n.IndexOf("stadium", StringComparison.OrdinalIgnoreCase) >= 0 && !n.Equals(Converter.OutName, StringComparison.OrdinalIgnoreCase))
                        return d;
                }
            }
            catch { }
            return "";
        }

        void Choose()
        {
            using (var d = new FolderBrowserDialog { Description = "Choose your stadium pack folder" })
            {
                if (Directory.Exists(folder.Text)) d.SelectedPath = folder.Text;
                else if (Directory.Exists(dest.Text)) d.SelectedPath = dest.Text;
                if (d.ShowDialog(this) == DialogResult.OK) folder.Text = d.SelectedPath;
            }
        }

        void ChooseDest()
        {
            using (var d = new FolderBrowserDialog { Description = "Choose the folder to save the stadium photos in (normally graphics\\Stadium Pack)" })
            {
                if (Directory.Exists(dest.Text)) d.SelectedPath = dest.Text;
                if (d.ShowDialog(this) == DialogResult.OK) dest.Text = d.SelectedPath;
            }
        }

        void Start()
        {
            string src = folder.Text.Trim().Trim('"');
            if (!Directory.Exists(src)) { MessageBox.Show(this, "Choose your stadium pack folder first.", Text); return; }
            string gfx = dest.Text.Trim().Trim('"');
            if (gfx.Length == 0) { MessageBox.Show(this, "Choose where to save it.", Text); return; }
            string full = Path.GetFullPath(gfx).TrimEnd('\\'), srcFull = Path.GetFullPath(src).TrimEnd('\\');
            if (string.Equals(full, srcFull, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Save to can't be the stadium pack folder itself. Choose another folder (normally graphics\\Stadium Pack).", Text);
                return;
            }
            if (Directory.Exists(gfx) && Converter.HasForeignConfig(gfx))
            {
                MessageBox.Show(this, "That Save to folder already has a config.xml from another pack, so it can't be used.\nChoose an empty folder (normally graphics\\Stadium Pack).", Text);
                return;
            }
            string parent = Path.GetDirectoryName(full);
            if (!Directory.Exists(gfx) && (parent == null || !Directory.Exists(parent))
                && MessageBox.Show(this, "This folder doesn't exist yet:\n" + gfx + "\n\nCreate it?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            lastDest = gfx;
            convert.Enabled = false; convert.Text = "Converting…";
            worker.RunWorkerAsync(new object[] { src, gfx });
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                var src = new List<string>(); string outDir = null, report = null;
                for (int i = 0; i + 1 < args.Length; i += 2)
                {
                    if (args[i] == "--source") src.Add(args[i + 1]);
                    else if (args[i] == "--out") outDir = args[i + 1];
                    else if (args[i] == "--report") report = args[i + 1];
                }
                var log = new StringBuilder();
                Result r;
                try { r = Converter.Run(src, outDir ?? Converter.DefaultOutput(), null, m => log.AppendLine(m)); }
                catch (Exception e) { if (report != null) File.WriteAllText(report, "ERROR: " + e.Message); return 1; }
                log.Append(r.Summary());
                if (report != null) File.WriteAllText(report, log.ToString());
                return 0;
            }
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
            return 0;
        }
    }
}
