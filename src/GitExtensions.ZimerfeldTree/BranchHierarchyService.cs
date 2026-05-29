// BranchHierarchyService.cs — Git operations for ZimerfeldTree plugin
// MIT License — Copyright (c) 2026 Zimerfeld

using System.Diagnostics;
using System.Xml.Linq;

namespace GitExtensions.ZimerfeldTree;

/// <summary>
/// Executes git commands and parses their output to supply branch data
/// to <see cref="BranchHierarchyForm"/>.
/// </summary>
public sealed class BranchHierarchyService
{
    public string WorkingDir { get; set; }

    public BranchHierarchyService(string workingDir)
    {
        WorkingDir = workingDir ?? string.Empty;
    }

    // ── Internal runner ──────────────────────────────────────────────────────

    private string RunGit(string arguments, out int exitCode)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = WorkingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git.");
        string stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        exitCode = proc.ExitCode;
        return stdout;
    }

    private (string stdout, string stderr, int code) RunGitFull(string arguments)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = WorkingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git.");
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return (stdout, stderr, proc.ExitCode);
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>Returns the name of the currently checked-out branch, or empty string.</summary>
    public string GetCurrentBranch()
    {
        try
        {
            return RunGit("rev-parse --abbrev-ref HEAD", out _).Trim();
        }
        catch { return string.Empty; }
    }

    /// <summary>Returns the number of pending changes (staged, unstaged and untracked) in the working tree.</summary>
    public int GetPendingChangesCount()
    {
        try
        {
            return SplitLines(RunGit("status --porcelain", out _)).Count();
        }
        catch { return 0; }
    }

    /// <summary>Opens the GitExtensions Commit window for the current working directory.</summary>
    public (bool ok, string err) OpenCommitWindow()
    {
        try
        {
            // The plugin DLL is hosted by GitExtensions.exe, so the host process points to it.
            string exe = Process.GetCurrentProcess().MainModule?.FileName ?? "GitExtensions.exe";
            var psi = new ProcessStartInfo(exe, "commit")
            {
                WorkingDirectory = WorkingDir,
                UseShellExecute = false
            };
            Process.Start(psi);
            return (true, string.Empty);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>Returns all local branches.</summary>
    public List<BranchInfo> GetLocalBranches()
    {
        var current = GetCurrentBranch();
        var result = new List<BranchInfo>();
        try
        {
            string raw = RunGit("branch --format=%(refname:short)", out _);
            foreach (var line in SplitLines(raw))
            {
                result.Add(new BranchInfo
                {
                    FullName = line,
                    IsCurrent = line == current,
                    Type = BranchType.Local
                });
            }
        }
        catch { /* repo may be empty or not initialized */ }
        return result;
    }

    /// <summary>Returns all remote-tracking branches.</summary>
    public List<BranchInfo> GetRemoteBranches()
    {
        var result = new List<BranchInfo>();
        try
        {
            string raw = RunGit("branch -r --format=%(refname:short)", out _);
            foreach (var line in SplitLines(raw))
            {
                if (line.Contains("->")) continue; // formato sem --format: "origin/HEAD -> origin/main"
                var slash = line.IndexOf('/');
                if (slash < 0) continue; // formato com --format: symref "origin/HEAD" sai como "origin" (sem barra)
                string remote     = line[..slash];
                string branchPart = line[(slash + 1)..];
                if (branchPart.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new BranchInfo
                {
                    FullName   = line,
                    Type       = BranchType.Remote,
                    RemoteName = remote
                });
            }
        }
        catch { }
        return result;
    }

    /// <summary>Returns all tags.</summary>
    public List<BranchInfo> GetTags()
    {
        var result = new List<BranchInfo>();
        try
        {
            string raw = RunGit("tag --sort=-version:refname", out _);
            foreach (var line in SplitLines(raw))
            {
                result.Add(new BranchInfo { FullName = line, Type = BranchType.Tag });
            }
        }
        catch { }
        return result;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public (bool ok, string error) Checkout(string branchName)
    {
        try
        {
            var (_, err, code) = RunGitFull($"checkout \"{EscapeArg(branchName)}\"");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>Creates a local tracking branch from a remote-tracking branch and checks it out.</summary>
    public (bool ok, string error) CheckoutRemoteAsLocal(string remoteBranch)
    {
        // remoteBranch = "origin/feature/login"  →  localName = "feature/login"
        string localName = remoteBranch.Contains('/')
            ? remoteBranch[(remoteBranch.IndexOf('/') + 1)..]
            : remoteBranch;
        try
        {
            var (_, err, code) = RunGitFull(
                $"checkout -b \"{EscapeArg(localName)}\" --track \"{EscapeArg(remoteBranch)}\"");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) CreateBranch(string newName, string fromRef)
    {
        try
        {
            var (_, err, code) = RunGitFull(
                $"branch \"{EscapeArg(newName)}\" \"{EscapeArg(fromRef)}\"");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>
    /// Creates a new local branch from <paramref name="fromRef"/> and immediately checks it out
    /// using a single <c>git checkout -b</c> command.
    /// </summary>
    public (bool ok, string error) CreateAndCheckoutBranch(string newName, string fromRef)
    {
        try
        {
            var (_, err, code) = RunGitFull(
                $"checkout -b \"{EscapeArg(newName)}\" \"{EscapeArg(fromRef)}\"");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) DeleteBranch(string branchName, bool isRemote = false)
    {
        try
        {
            string args;
            if (isRemote)
            {
                var slash = branchName.IndexOf('/');
                if (slash < 0) return (false, "Formato de branch remota inválido.");
                string remote = branchName[..slash];
                string branch = branchName[(slash + 1)..];
                args = $"push {remote} --delete \"{EscapeArg(branch)}\"";
            }
            else
            {
                args = $"branch -d \"{EscapeArg(branchName)}\"";
            }
            var (_, err, code) = RunGitFull(args);
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) DeleteBranchForce(string branchName)
    {
        try
        {
            var (_, err, code) = RunGitFull($"branch -D \"{EscapeArg(branchName)}\"");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) DeleteTag(string tagName)
    {
        try
        {
            var (_, err, code) = RunGitFull($"tag -d \"{EscapeArg(tagName)}\"");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) RenameBranch(string oldName, string newName)
    {
        try
        {
            var (_, err, code) = RunGitFull(
                $"branch -m \"{EscapeArg(oldName)}\" \"{EscapeArg(newName)}\"");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) MergeBranch(string branchName)
    {
        try
        {
            var (_, err, code) = RunGitFull($"merge \"{EscapeArg(branchName)}\"");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>Runs <c>git pull --tags</c> for the current branch, fetching all remote tags.</summary>
    public (bool ok, string error) Pull()
    {
        try
        {
            var (_, err, code) = RunGitFull("pull --tags");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>Runs <c>git push</c> for the current branch.</summary>
    public (bool ok, string error) Push()
    {
        try
        {
            var (_, err, code) = RunGitFull("push");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) RebaseBranch(string branchName)
    {
        try
        {
            var (_, err, code) = RunGitFull($"rebase \"{EscapeArg(branchName)}\"");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── Git Flow ───────────────────────────────────────────────────────────────

    /// <summary>The branch types supported by git flow, in display order.</summary>
    public static readonly string[] GitFlowTypes = ["feature", "bugfix", "release", "hotfix", "support"];

    /// <summary>Runs an arbitrary git command and returns the combined stdout+stderr and exit code.</summary>
    public (string output, int code) RunGitFlow(string arguments)
    {
        try
        {
            var (stdout, stderr, code) = RunGitFull(arguments);
            string combined = stdout.TrimEnd();
            stderr = stderr.TrimEnd();
            if (stderr.Length > 0)
                combined = combined.Length > 0 ? combined + "\n" + stderr : stderr;
            return (combined, code);
        }
        catch (Exception ex) { return (ex.Message, -1); }
    }

    /// <summary>Returns the full symbolic HEAD ref (e.g. "refs/heads/develop").</summary>
    public string GetHeadRef()
    {
        try
        {
            string s = RunGit("rev-parse --symbolic-full-name HEAD", out int code).Trim();
            if (code == 0 && s.Length > 0) return s;
        }
        catch { }
        var cur = GetCurrentBranch();
        return cur.Length > 0 ? "refs/heads/" + cur : string.Empty;
    }

    /// <summary>Returns the configured git flow prefix for a branch type, or the default "type/".</summary>
    public string GetGitFlowPrefix(string type)
    {
        try
        {
            string s = RunGit($"config --get gitflow.prefix.{type}", out int code).Trim();
            if (code == 0 && s.Length > 0) return s;
        }
        catch { }
        return type + "/";
    }

    /// <summary>Returns the names of configured remotes (e.g. "origin").</summary>
    public List<string> GetRemotes()
    {
        var result = new List<string>();
        try
        {
            foreach (var line in SplitLines(RunGit("remote", out _)))
                result.Add(line);
        }
        catch { }
        return result;
    }

    /// <summary>
    /// Returns the actual local branch name configured for a git flow role
    /// (e.g. "master" → reads <c>gitflow.branch.master</c> which may be set to "main").
    /// Falls back to the role name itself when the config key is missing.
    /// </summary>
    public string GetGitFlowBranchName(string role)
    {
        try
        {
            string s = RunGit($"config --get gitflow.branch.{role}", out int code).Trim();
            if (code == 0 && s.Length > 0) return s;
        }
        catch { }
        return role;
    }

    /// <summary>
    /// Returns the remote to use for push operations: "origin" when present,
    /// otherwise the first configured remote, or empty string if none.
    /// </summary>
    public string GetDefaultRemote()
    {
        var remotes = GetRemotes();
        if (remotes.Count == 0) return string.Empty;
        foreach (var r in remotes)
            if (string.Equals(r, "origin", StringComparison.Ordinal))
                return r;
        return remotes[0];
    }

    /// <summary>
    /// Returns tracking info for every local branch that has an upstream configured,
    /// including branches that are in sync (both counts = 0).
    /// Uses a single pipe-delimited <c>git for-each-ref</c> call.
    /// Key = short branch name, Value = (hasUpstream=true, ahead, behind).
    /// </summary>
    public Dictionary<string, (bool hasUpstream, int ahead, int behind)> GetBranchTrackingInfo()
    {
        var result = new Dictionary<string, (bool, int, int)>(StringComparer.Ordinal);
        try
        {
            // Pipe-delimited format: branchname|upstream_short|upstream_track
            // Pipe is not valid in git ref names on major hosting platforms, making it
            // a reliable field separator.  The outer quotes keep the --format as one argument.
            string raw = RunGit(
                "for-each-ref \"--format=%(refname:short)|%(upstream:short)|%(upstream:track)\" refs/heads/",
                out int code);
            if (code != 0) return result;

            foreach (var line in SplitLines(raw))
            {
                int p1 = line.IndexOf('|');
                if (p1 < 0) continue;
                int p2 = line.IndexOf('|', p1 + 1);
                if (p2 < 0) continue;

                string branch   = line[..p1];
                string upstream = line[(p1 + 1)..p2];
                string track    = line[(p2 + 1)..];

                if (string.IsNullOrEmpty(upstream)) continue; // no upstream configured

                int ahead  = 0;
                int behind = 0;
                var ma = System.Text.RegularExpressions.Regex.Match(track, @"ahead (\d+)");
                var mb = System.Text.RegularExpressions.Regex.Match(track, @"behind (\d+)");
                if (ma.Success) int.TryParse(ma.Groups[1].Value, out ahead);
                if (mb.Success) int.TryParse(mb.Groups[1].Value, out behind);

                result[branch] = (true, ahead, behind);
            }
        }
        catch { }
        return result;
    }

    /// <summary>Returns local branches matching a git flow prefix, with the prefix stripped off.</summary>
    public List<string> GetGitFlowBranches(string prefix)
    {
        var result = new List<string>();
        foreach (var b in GetLocalBranches())
            if (b.FullName.StartsWith(prefix, StringComparison.Ordinal))
                result.Add(b.FullName[prefix.Length..]);
        return result;
    }

    // ── Ancestry analysis (pure merge-base) ──────────────────────────────────

    /// <summary>
    /// Builds a map of branchName → parentBranchName (null = root) using merge-base only.
    /// Branch A is the parent of B when tip(A) == merge-base(A, B), meaning A's current
    /// tip is a direct ancestor of B.  Among all valid candidates the one with the fewest
    /// extra commits on top wins (closest fork point).
    /// </summary>
    public Dictionary<string, string?> BuildParentMap(List<BranchInfo> branches)
    {
        var tips   = GatherTips();
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var b in branches)
            result[b.FullName] = FindParent(b.FullName, branches, tips);

        BreakCycles(result);
        return result;
    }

    // Gets all local-branch tip SHAs in a single git call: name → full SHA.
    private Dictionary<string, string> GatherTips()
    {
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            string raw = RunGit("branch --format=\"%(refname:short) %(objectname)\"", out _);
            foreach (var line in SplitLines(raw))
            {
                int sp = line.IndexOf(' ');
                if (sp > 0) tips[line[..sp]] = line[(sp + 1)..].Trim();
            }
        }
        catch { }
        return tips;
    }

    // Gets all remote-tracking tip SHAs in a single git call: name → full SHA.
    private Dictionary<string, string> GatherRemoteTips()
    {
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            string raw = RunGit("branch -r --format=\"%(refname:short) %(objectname)\"", out _);
            foreach (var line in SplitLines(raw))
            {
                if (line.Contains("->")) continue;
                int sp = line.IndexOf(' ');
                string name = sp > 0 ? line[..sp] : line;
                int sl = name.IndexOf('/');
                if (sl < 0) continue; // symref sem barra (ex: "origin" para origin/HEAD)
                if (name[(sl + 1)..].Equals("HEAD", StringComparison.OrdinalIgnoreCase)) continue;
                if (sp > 0) tips[name] = line[(sp + 1)..].Trim();
            }
        }
        catch { }
        return tips;
    }

    /// <summary>
    /// Same algorithm as <see cref="BuildParentMap"/> but operates on remote-tracking branches.
    /// Each branch's <see cref="BranchInfo.FullName"/> must be the full remote ref
    /// (e.g. "origin/feature/login") so that <c>git merge-base</c> can resolve it.
    /// </summary>
    public Dictionary<string, string?> BuildRemoteParentMap(List<BranchInfo> branches)
    {
        var tips   = GatherRemoteTips();
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var b in branches)
            result[b.FullName] = FindParent(b.FullName, branches, tips);
        BreakCycles(result);
        return result;
    }

    /// <summary>
    /// For branch B, finds its parent: the candidate A whose merge-base with B is the
    /// deepest (most recent common ancestor) in the commit graph.
    /// <para>
    /// Candidates where B is already a direct ancestor of A are excluded — they would be
    /// children of B, not parents. When two candidates produce the same merge-base depth
    /// (tie), the one that is closest to the fork point wins (fewest commits it added since
    /// the fork).
    /// </para>
    /// </summary>
    private string? FindParent(
        string branch,
        List<BranchInfo> candidates,
        Dictionary<string, string> tips)
    {
        if (!tips.TryGetValue(branch, out string? branchTip)) return null;

        string? bestParent   = null;
        string? bestMb       = null;
        int     bestDistance = int.MaxValue; // lazy: only computed on a tie

        foreach (var cand in candidates)
        {
            if (cand.FullName == branch) continue;
            if (!tips.ContainsKey(cand.FullName)) continue;

            try
            {
                string mb = RunGit(
                    $"merge-base {EscapeArg(cand.FullName)} {EscapeArg(branch)}",
                    out int code).Trim();

                if (code != 0 || string.IsNullOrEmpty(mb)) continue;

                // Skip when B is a direct ancestor of the candidate
                // (merge-base == B's own tip means B is below the candidate, not above).
                if (mb == branchTip) continue;

                if (bestMb == null)
                {
                    bestMb = mb; bestParent = cand.FullName;
                    continue;
                }

                // Compare which merge-base is deeper (more recent).
                // merge-base(mb, bestMb) == bestMb  →  bestMb is ancestor of mb  →  mb is deeper
                // merge-base(mb, bestMb) == mb       →  mb is ancestor of bestMb  →  bestMb is deeper
                // both equal                          →  same commit (tie)
                string cmp = RunGit($"merge-base {mb} {bestMb}", out _).Trim();

                if (cmp == bestMb && cmp != mb) // mb strictly deeper → new best
                {
                    bestMb = mb; bestParent = cand.FullName; bestDistance = int.MaxValue;
                }
                else if (cmp == mb && cmp == bestMb) // same merge-base → tiebreak
                {
                    // Prefer the candidate that is closer to the fork point
                    // (fewest commits it added on top of the shared ancestor).
                    if (bestDistance == int.MaxValue)
                        bestDistance = CommitCount($"{bestMb}..{bestParent}");
                    int d = CommitCount($"{mb}..{EscapeArg(cand.FullName)}");
                    if (d < bestDistance)
                    {
                        bestParent = cand.FullName; bestDistance = d;
                    }
                }
                // else bestMb is deeper → keep current best
            }
            catch { }
        }

        return bestParent;
    }

    private int CommitCount(string revRange)
    {
        try
        {
            string s = RunGit($"rev-list --count {revRange}", out _).Trim();
            return int.TryParse(s, out int n) ? n : int.MaxValue;
        }
        catch { return int.MaxValue; }
    }

    private static void BreakCycles(Dictionary<string, string?> parentMap)
    {
        foreach (var key in parentMap.Keys.ToList())
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string? cur = key;
            while (cur != null)
            {
                if (!seen.Add(cur)) { parentMap[cur] = null; break; }
                parentMap.TryGetValue(cur, out cur);
            }
        }
    }

    // ── Settings reader ──────────────────────────────────────────────────────

    /// <summary>
    /// Reads the list of recently opened repositories from the GitExtensions settings file
    /// at <c>%APPDATA%\GitExtensions\GitExtensions\GitExtensions.settings</c>.
    /// Returns an empty list when the file cannot be parsed or does not exist.
    /// </summary>
    public static List<string> GetRepositoriesFromSettings()
    {
        var result = new List<string>();
        try
        {
            string settingsFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GitExtensions", "GitExtensions", "GitExtensions.settings");

            if (!File.Exists(settingsFile)) return result;

            var doc = XDocument.Load(settingsFile);

            // GitExtensions stores repository history under key "history" as an
            // XML-encoded string:
            //   <item>
            //     <key><string>history</string></key>
            //     <value><string>&lt;RepositoryHistory&gt;&lt;Repositories&gt;
            //       &lt;Repository&gt;&lt;Path&gt;C:\...\&lt;/Path&gt;&lt;/Repository&gt;
            //     &lt;/Repositories&gt;&lt;/RepositoryHistory&gt;</string></value>
            //   </item>

            var historyValue = doc
                .Descendants("item")
                .FirstOrDefault(item =>
                    item.Element("key")?.Element("string")?.Value
                        .Equals("history", StringComparison.OrdinalIgnoreCase) == true)
                ?.Element("value")
                ?.Element("string")
                ?.Value;

            if (!string.IsNullOrWhiteSpace(historyValue))
            {
                var inner = XDocument.Parse(historyValue);
                foreach (var pathEl in inner.Descendants("Path"))
                {
                    var path = pathEl.Value?.Trim();
                    if (!string.IsNullOrEmpty(path))
                        result.Add(path);
                }

                if (result.Count > 0)
                    return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            // Fallback: scan all element values that look like valid git working dirs
            foreach (var el in doc.Descendants())
            {
                var val = el.Value?.Trim();
                if (string.IsNullOrEmpty(val) || val.Length < 3 || val.Length > 260) continue;
                if (val.Contains('\n') || val.Contains('\r')) continue;
                try
                {
                    if (Directory.Exists(val) &&
                        (Directory.Exists(Path.Combine(val, ".git")) ||
                         File.Exists(Path.Combine(val, ".git"))))
                    {
                        result.Add(val);
                    }
                }
                catch { }
            }
        }
        catch { }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IEnumerable<string> SplitLines(string raw) =>
        raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
           .Select(l => l.Trim())
           .Where(l => !string.IsNullOrEmpty(l));

    /// <summary>Strips double-quote characters to prevent argument injection.</summary>
    private static string EscapeArg(string arg) => arg.Replace("\"", "");
}
