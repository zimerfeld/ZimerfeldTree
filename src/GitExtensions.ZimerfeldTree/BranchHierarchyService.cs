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
                if (line.Contains("->")) continue; // skip HEAD pointers
                var slash = line.IndexOf('/');
                string? remote = slash >= 0 ? line[..slash] : null;
                result.Add(new BranchInfo
                {
                    FullName = line,
                    Type = BranchType.Remote,
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

    public (bool ok, string error) RebaseBranch(string branchName)
    {
        try
        {
            var (_, err, code) = RunGitFull($"rebase \"{EscapeArg(branchName)}\"");
            return code == 0 ? (true, string.Empty) : (false, err.Trim());
        }
        catch (Exception ex) { return (false, ex.Message); }
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
