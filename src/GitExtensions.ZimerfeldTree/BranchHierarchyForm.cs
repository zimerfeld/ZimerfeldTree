// BranchHierarchyForm.cs — Main WinForms window for ZimerfeldTree plugin
// MIT License — Copyright (c) 2026 Zimerfeld

using System.ComponentModel;
using System.Text.Json;

namespace GitExtensions.ZimerfeldTree;

/// <summary>
/// Non-modal window that displays local branches, remote-tracking branches and tags in a
/// path-based hierarchy.  Stays open alongside GitExtensions while the user works.
/// </summary>
public sealed class BranchHierarchyForm : Form
{
    // ── Services ─────────────────────────────────────────────────────────────
    private readonly BranchHierarchyService _svc;
    private readonly Action? _notifyRepoChanged; // called after checkout so GitExtensions refreshes
    /// <summary>
    /// Delegate provided by the plugin that opens the native GitExtensions commit dialog in-process.
    /// Returns true = commits were made, false = dialog closed without committing, null = unavailable (fall back).
    /// </summary>
    private readonly Func<IWin32Window, bool?>? _openCommitDialog;

    // ── Cached data ───────────────────────────────────────────────────────────
    private List<BranchInfo>             _localBranches  = [];
    private List<BranchInfo>             _remoteBranches = [];
    private List<BranchInfo>             _tags           = [];
    private Dictionary<string, string?>  _localParentMap  = []; // real git ancestry
    private Dictionary<string, string?>  _remoteParentMap = [];
    private bool                         _gitFlowForced   = false;
    private bool                         _gitFlowUserToggled = false; // user clicked the button → stop auto-organizing

    // ── Controls ─────────────────────────────────────────────────────────────
    private Panel            _topPanel    = null!;
    private Label            _lblWD       = null!;
    private ComboBox         _cboRepo     = null!;
    private Label            _lblBranch   = null!;
    private Panel            _filterPanel = null!;
    private TextBox          _txtFilter   = null!;
    private Button           _btnRefresh  = null!;
    private Panel            _warnPanel   = null!;
    private Label            _warnLabel   = null!;
    private Button           _btnGitFlow          = null!;
    private Button           _btnGitFlowDedicated = null!;
    private Panel            _gitFlowButtonPanel  = null!;
    private Button           _btnPull             = null!;
    private Button           _btnPush             = null!;
    private Button           _btnCommitDedicated  = null!;
    private TreeView         _tree        = null!;
    private StatusStrip      _status      = null!;
    private ToolStripStatusLabel _statusLbl = null!;

    // ── Loading overlay ───────────────────────────────────────────────────────
    private Panel       _loadingOverlay   = null!;
    private ProgressBar _progressBar      = null!;
    private Label       _loadingTitle     = null!;
    private Label       _loadingStatus    = null!;
    private Button      _btnCancelRefresh = null!;
    private bool        _isRefreshing;
    private CancellationTokenSource? _refreshCts;

    // ── Tree expand/collapse state persistence ────────────────────────────────
    /// <summary>Per-repo set of expanded node paths (key = workingDir, value = stable path strings).</summary>
    private Dictionary<string, HashSet<string>> _treeStateByRepo = [];
    /// <summary>True while we are restoring saved state — suppresses AfterExpand/AfterCollapse saves.</summary>
    private bool _restoringState;
    /// <summary>Debounce timer that delays disk writes when many nodes expand/collapse rapidly.</summary>
    private System.Windows.Forms.Timer? _saveDebounce;
    private static readonly string StateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GitExtensions", "ZimerfeldTree.treestate.json");

    // ── Bottom panel ──────────────────────────────────────────────────────────
    private Panel  _bottomPanel = null!;
    private Button _btnClose    = null!;

    // ── Tree section roots ────────────────────────────────────────────────────
    private TreeNode _localRoot   = null!;
    private TreeNode _remotesRoot = null!;
    private TreeNode _tagsRoot    = null!;

    // ── Context menu ──────────────────────────────────────────────────────────
    private ContextMenuStrip   _ctxMenu     = null!;
    private ToolStripMenuItem  _miCommit    = null!;
    private ToolStripMenuItem  _miCheckout  = null!;
    private ToolStripMenuItem  _miNewBranch = null!;
    private ToolStripMenuItem  _miMerge     = null!;
    private ToolStripMenuItem  _miRebase    = null!;
    private ToolStripMenuItem  _miRename    = null!;
    private ToolStripMenuItem  _miDelete    = null!;
    private ToolStripMenuItem  _miGitFlow   = null!;
    private ToolStripMenuItem  _miExpand    = null!;
    private ToolStripMenuItem  _miCollapse  = null!;
    private ToolStripMenuItem  _miRefresh   = null!;

    // ─────────────────────────────────────────────────────────────────────────
    public BranchHierarchyForm(string workingDir, Action? notifyRepoChanged = null,
        Func<IWin32Window, bool?>? openCommitDialog = null)
    {
        _svc = new BranchHierarchyService(workingDir);
        _notifyRepoChanged  = notifyRepoChanged;
        _openCommitDialog   = openCommitDialog;
        _treeStateByRepo    = LoadTreeState();
        InitializeComponent();
        LoadRepositories();
        FormClosed += (_, _) => { _saveDebounce?.Dispose(); SaveTreeState(); };
        // Initial tree load is triggered by the Shown event so the window skeleton
        // is visible to the user before we start reading the repository.
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by the plugin when GitExtensions switches the active repository.</summary>
    public void UpdateWorkingDir(string newDir)
    {
        _svc.WorkingDir = newDir;
        _gitFlowUserToggled = false; // re-enable auto-organization for the new repo
        if (!_cboRepo.Items.Contains(newDir))
            _cboRepo.Items.Add(newDir);
        _cboRepo.SelectedItem = newDir;
    }

    /// <summary>
    /// Re-reads branches from git asynchronously and rebuilds the tree.
    /// Shows the "Carregando…" overlay while reading. Concurrent calls are collapsed into one.
    /// </summary>
    public void RefreshTree() => _ = RefreshTreeAsync(showOverlay: true);

    /// <summary>
    /// Loads all branch/tag data on a background thread, optionally showing a centered
    /// progress overlay, then rebuilds the tree on the UI thread.
    /// </summary>
    private async Task RefreshTreeAsync(bool showOverlay)
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        // Cancel any previous refresh that might still be in-flight, then create a fresh token.
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        if (showOverlay)
        {
            _progressBar.Value        = 0;
            _loadingStatus.Text       = "Iniciando...";
            _btnCancelRefresh.Enabled = true;
            _btnCancelRefresh.Text    = "Cancelar";
            _loadingOverlay.Location  = new Point(
                (ClientSize.Width  - _loadingOverlay.Width)  / 2,
                (ClientSize.Height - _loadingOverlay.Height) / 2);
            _loadingOverlay.Visible = true;
            _loadingOverlay.BringToFront();
            SetFormEnabled(false);
        }

        List<BranchInfo>            local  = [];
        List<BranchInfo>            remote = [];
        List<BranchInfo>            tags   = [];
        Dictionary<string, string?> lMap   = [];
        Dictionary<string, string?> rMap   = [];

        IProgress<(int pct, string msg)>? ip = showOverlay
            ? new Progress<(int pct, string msg)>(p =>
              {
                  _progressBar.Value  = p.pct;
                  _loadingStatus.Text = p.msg;
              })
            : null;

        try
        {
            await Task.Run(() =>
            {
                ip?.Report((10, "Carregando branches locais..."));
                local  = _svc.GetLocalBranches();
                token.ThrowIfCancellationRequested();
                ip?.Report((30, "Carregando branches remotas..."));
                remote = _svc.GetRemoteBranches();
                token.ThrowIfCancellationRequested();
                ip?.Report((50, "Carregando tags..."));
                tags   = _svc.GetTags();
                token.ThrowIfCancellationRequested();
                ip?.Report((65, "Calculando hierarquia local..."));
                lMap   = _svc.BuildParentMap(local);
                token.ThrowIfCancellationRequested();
                ip?.Report((80, "Calculando hierarquia remota..."));
                rMap   = _svc.BuildRemoteParentMap(remote);
                token.ThrowIfCancellationRequested();
                ip?.Report((92, "Obtendo informações de sincronização..."));
                var tracking = _svc.GetBranchTrackingInfo();
                foreach (var b in local)
                    if (tracking.TryGetValue(b.FullName, out var ti))
                    {
                        b.HasUpstream  = ti.hasUpstream;
                        b.AheadCount   = ti.ahead;
                        b.BehindCount  = ti.behind;
                    }
                ip?.Report((100, "Concluído."));
            }, token);
        }
        catch (OperationCanceledException)
        {
            // User cancelled — silently restore UI without touching the existing tree data.
            _isRefreshing = false;
            if (!IsDisposed && showOverlay)
            {
                _loadingOverlay.Visible = false;
                SetFormEnabled(true);
            }
            return;
        }
        catch (Exception ex)
        {
            _isRefreshing = false;
            if (!IsDisposed)
            {
                if (showOverlay)
                {
                    _loadingOverlay.Visible = false;
                    SetFormEnabled(true);
                }
                MessageBox.Show($"Erro ao carregar dados do repositório:\n{ex.Message}",
                    "ZimerfeldTree", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        if (IsDisposed) { _isRefreshing = false; return; }

        _localBranches   = local;
        _remoteBranches  = remote;
        _tags            = tags;
        _localParentMap  = lMap;
        _remoteParentMap = rMap;

        _tree.BeginUpdate();
        try
        {
            UpdateGitFlowWarning();
            var localMap  = _gitFlowForced ? BuildGitFlowParentMap(_localBranches)         : _localParentMap;
            var remoteMap = _gitFlowForced ? BuildGitFlowRemoteParentMap(_remoteBranches)   : _remoteParentMap;
            RebuildAllSections(_txtFilter?.Text.Trim() ?? string.Empty, localMap, remoteMap);
            ExpandRoots();
            UpdateStatus();
            UpdateBranchLabel();
            UpdatePullPushButtons();
        }
        finally { _tree.EndUpdate(); }

        if (showOverlay)
        {
            _loadingOverlay.Visible = false;
            SetFormEnabled(true);
        }
        _isRefreshing = false;
    }

    // ── Initialization ────────────────────────────────────────────────────────

    private void InitializeComponent()
    {
        SuspendLayout();

        Text            = "ZimerfeldTree — Branch Hierarchy";
        Size            = new Size(580, 720);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;   // standard OS close button, no resize
        MaximizeBox     = false;
        MinimizeBox     = false;
        KeyPreview      = true;
        Font            = new Font("Segoe UI", 9f);
        Icon            = TreeOfLifeIcon.ForForm();

        BuildTopPanel();
        BuildFilterPanel();
        BuildWarnPanel();
        BuildGitFlowButtonPanel();
        BuildTreeView();
        BuildContextMenu();
        BuildStatusStrip();
        BuildBottomPanel();
        BuildLoadingOverlay();
        SetTabOrder();

        // Layout order (Dock fills from bottom and top inward, Fill takes the remainder).
        // Added last = topmost for DockStyle.Top; visual order top→bottom:
        //   _topPanel, _filterPanel, _warnPanel, _gitFlowButtonPanel, _tree (Fill), _bottomPanel, _status
        Controls.Add(_tree);                // Fill
        Controls.Add(_gitFlowButtonPanel);  // Top — just above the tree
        Controls.Add(_warnPanel);           // Top
        Controls.Add(_filterPanel);         // Top
        Controls.Add(_topPanel);            // Top (topmost)
        Controls.Add(_status);         // Bottom
        Controls.Add(_bottomPanel);    // Bottom (above status)
        Controls.Add(_loadingOverlay); // Floats above everything (BringToFront when shown)

        CancelButton = _btnClose;

        // Trigger the async initial load once the window is fully painted.
        Shown += (_, _) => _ = RefreshTreeAsync(showOverlay: true);

        ResumeLayout(false);
        PerformLayout();
    }

    private void BuildTopPanel()
    {
        _topPanel = new Panel { Dock = DockStyle.Top, Height = 72 };

        var table = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 3,
            Padding     = new Padding(6, 4, 6, 4),
            Margin      = Padding.Empty
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblWD = new Label
        {
            Text     = "Working Directory:",
            AutoSize = true,
            Font     = new Font(Font, FontStyle.Bold),
            Margin   = new Padding(0, 0, 0, 2)
        };

        _cboRepo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock          = DockStyle.Fill,
            Margin        = new Padding(0, 0, 0, 2)
        };
        _cboRepo.SelectedIndexChanged += CboRepo_SelectedIndexChanged;

        _lblBranch = new Label
        {
            AutoSize = true,
            Text     = "Branch: ",
            Margin   = Padding.Empty
        };

        table.Controls.Add(_lblWD,     0, 0);
        table.Controls.Add(_cboRepo,   0, 1);
        table.Controls.Add(_lblBranch, 0, 2);

        _topPanel.Controls.Add(table);
    }

    private void BuildFilterPanel()
    {
        _filterPanel = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(4, 2, 4, 2) };

        _txtFilter = new TextBox
        {
            Dock            = DockStyle.Fill,
            PlaceholderText = "Filtrar branches..."
        };
        _txtFilter.TextChanged += (_, _) => ApplyFilter(_txtFilter.Text.Trim());

        _btnRefresh = new Button
        {
            Text   = "↺",
            Dock   = DockStyle.Right,
            Width  = 32,
            Height = 24,
            Font   = new Font(Font, FontStyle.Bold)
        };
        _btnRefresh.Click += (_, _) => RefreshTree();

        _filterPanel.Controls.Add(_txtFilter);
        _filterPanel.Controls.Add(_btnRefresh);
    }

    private void BuildWarnPanel()
    {
        _warnLabel = new Label
        {
            Dock      = DockStyle.Fill,
            Text      = string.Empty,
            ForeColor = Color.DarkRed,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(4, 0, 0, 0),
            AutoSize  = false
        };

        _btnGitFlow = new Button
        {
            Dock  = DockStyle.Right,
            Width = 160,
            Text  = "Organizar como GitFlow"
        };
        _btnGitFlow.Click += BtnGitFlow_Click;

        _warnPanel = new Panel
        {
            Dock    = DockStyle.Top,
            Height  = 26,
            Visible = false,
            Padding = new Padding(4, 2, 4, 2)
        };
        _warnPanel.Controls.Add(_warnLabel);
        _warnPanel.Controls.Add(_btnGitFlow);
    }

    private void BuildGitFlowButtonPanel()
    {
        _btnPull = new Button { Text = "Pull", Width = 80, Height = 24, Visible = false };
        _btnPull.Click += (_, _) => DoPull();

        _btnPush = new Button { Text = "Push", Width = 80, Height = 24, Visible = false };
        _btnPush.Click += (_, _) => DoPush();

        _btnCommitDedicated = new Button { Text = "Commit", Width = 80, Height = 24, Visible = false };
        _btnCommitDedicated.Click += (_, _) => DoCommit();

        _btnGitFlowDedicated = new Button
        {
            Text    = "GitFlow",
            Width   = 120,
            Height  = 24,
            Anchor  = AnchorStyles.None,
            Font    = new Font(Font, FontStyle.Bold),
            Visible = false
        };
        _btnGitFlowDedicated.Click += (_, _) => DoGitFlow();

        _gitFlowButtonPanel = new Panel { Dock = DockStyle.Top, Height = 32 };
        _gitFlowButtonPanel.Controls.AddRange([_btnPull, _btnPush, _btnCommitDedicated, _btnGitFlowDedicated]);

        // All buttons left-aligned with uniform 4 px gap; only visible ones consume space.
        _gitFlowButtonPanel.Layout += (_, _) =>
        {
            int y = (_gitFlowButtonPanel.Height - 24) / 2;
            int x = 8;
            if (_btnPull.Visible)
            {
                _btnPull.Location = new Point(x, y); x += _btnPull.Width + 4;
                _btnPush.Location = new Point(x, y); x += _btnPush.Width + 4;
            }
            if (_btnCommitDedicated.Visible)
            {
                _btnCommitDedicated.Location = new Point(x, y); x += _btnCommitDedicated.Width + 4;
            }
            _btnGitFlowDedicated.Location = new Point(x, y);
        };
    }

    private void BuildTreeView()
    {
        _tree = new TreeView
        {
            Dock          = DockStyle.Fill,
            ShowLines     = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HideSelection = false,
            DrawMode      = TreeViewDrawMode.OwnerDrawText,
            Font          = new Font("Segoe UI", 9f),
            ImageList     = NodeIcons.GetList()
        };

        _tree.DrawNode              += Tree_DrawNode;
        _tree.NodeMouseDoubleClick  += Tree_NodeMouseDoubleClick;
        _tree.KeyDown               += Tree_KeyDown;
        _tree.MouseDown             += Tree_MouseDown;
        _tree.AfterExpand           += Tree_AfterExpand;
        _tree.AfterCollapse         += Tree_AfterCollapse;

        _localRoot = new TreeNode("LOCAL (0)")
        {
            Tag = SectionTag.Local,
            ImageIndex = NodeIcons.SectionLocal, SelectedImageIndex = NodeIcons.SectionLocal
        };
        _remotesRoot = new TreeNode("REMOTES (0)")
        {
            Tag = SectionTag.Remotes,
            ImageIndex = NodeIcons.SectionRemotes, SelectedImageIndex = NodeIcons.SectionRemotes
        };
        _tagsRoot = new TreeNode("TAGS (0)")
        {
            Tag = SectionTag.Tags,
            ImageIndex = NodeIcons.SectionTags, SelectedImageIndex = NodeIcons.SectionTags
        };

        _tree.Nodes.AddRange([_localRoot, _remotesRoot, _tagsRoot]);
    }

    private void BuildContextMenu()
    {
        _miCommit    = new ToolStripMenuItem("Commit");
        _miCheckout  = new ToolStripMenuItem("Checkout");
        _miNewBranch = new ToolStripMenuItem("Nova branch daqui…");
        _miMerge     = new ToolStripMenuItem("Mesclar na branch atual");
        _miRebase    = new ToolStripMenuItem("Rebase na branch atual");
        _miRename    = new ToolStripMenuItem("Renomear…");
        _miDelete    = new ToolStripMenuItem("Excluir…");
        _miGitFlow   = new ToolStripMenuItem("GitFlow…");
        _miExpand    = new ToolStripMenuItem("Expandir tudo");
        _miCollapse  = new ToolStripMenuItem("Recolher tudo");
        _miRefresh   = new ToolStripMenuItem("Atualizar");

        _miCommit   .Click += (_, _) => DoCommit();
        _miCheckout .Click += (_, _) => DoCheckout();
        _miNewBranch.Click += (_, _) => DoNewBranch();
        _miMerge    .Click += (_, _) => DoMerge();
        _miRebase   .Click += (_, _) => DoRebase();
        _miRename   .Click += (_, _) => DoRename();
        _miDelete   .Click += (_, _) => DoDelete();
        _miGitFlow  .Click += (_, _) => DoGitFlow();
        _miExpand  .Click += (_, _) => _tree.SelectedNode?.ExpandAll();
        _miCollapse.Click += (_, _) => { if (_tree.SelectedNode is { } n) CollapseRecursive(n); };
        _miRefresh  .Click += (_, _) => RefreshTree();

        _ctxMenu = new ContextMenuStrip();
        _ctxMenu.Opening += CtxMenu_Opening;
        _ctxMenu.Items.AddRange(
        [
            _miCommit,
            new ToolStripSeparator(),
            _miCheckout, _miNewBranch,
            new ToolStripSeparator(),
            _miMerge, _miRebase,
            new ToolStripSeparator(),
            _miRename, _miDelete,
            new ToolStripSeparator(),
            _miGitFlow,
            new ToolStripSeparator(),
            _miExpand, _miCollapse, _miRefresh
        ]);

        _tree.ContextMenuStrip = _ctxMenu;
    }

    private void BuildStatusStrip()
    {
        _status    = new StatusStrip();
        _statusLbl = new ToolStripStatusLabel
        {
            Text      = "Local: 0  |  Remoto: 0  |  Tags: 0",
            Spring    = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _status.Items.Add(_statusLbl);
    }

    private void BuildBottomPanel()
    {
        _btnClose = new Button
        {
            Text         = "Fechar",
            Width        = 80,
            Height       = 26,
            DialogResult = DialogResult.Cancel
        };
        _btnClose.Click += (_, _) => Close();

        _bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 36 };
        _bottomPanel.Controls.Add(_btnClose);

        // Keep button right-aligned with margin whenever the panel is laid out.
        _bottomPanel.Layout += (_, _) =>
            _btnClose.Location = new Point(
                _bottomPanel.Width  - _btnClose.Width  - 8,
                (_bottomPanel.Height - _btnClose.Height) / 2);
    }

    private void BuildLoadingOverlay()
    {
        _loadingTitle = new Label
        {
            Text      = "Carregando dados do repositório",
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds    = new Rectangle(10, 10, 340, 20),
            Font      = new Font(Font, FontStyle.Bold)
        };

        _progressBar = new ProgressBar
        {
            Bounds  = new Rectangle(10, 38, 340, 20),
            Minimum = 0,
            Maximum = 100,
            Value   = 0,
            Style   = ProgressBarStyle.Continuous
        };

        _loadingStatus = new Label
        {
            Text      = "Iniciando...",
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = SystemColors.GrayText,
            Bounds    = new Rectangle(10, 64, 340, 18)
        };

        _btnCancelRefresh = new Button
        {
            Text   = "Cancelar",
            Bounds = new Rectangle(130, 90, 100, 26)
        };
        _btnCancelRefresh.Click += (_, _) =>
        {
            _btnCancelRefresh.Enabled = false;
            _btnCancelRefresh.Text    = "Cancelando…";
            _refreshCts?.Cancel();
        };

        _loadingOverlay = new Panel
        {
            Size        = new Size(360, 126),
            BackColor   = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            Visible     = false
        };
        _loadingOverlay.Controls.AddRange([_loadingTitle, _progressBar, _loadingStatus, _btnCancelRefresh]);
    }

    // ── GitFlow enforcement ───────────────────────────────────────────────────

    private void BtnGitFlow_Click(object? sender, EventArgs e)
    {
        _gitFlowUserToggled = true; // manual choice overrides auto-organization
        _gitFlowForced = !_gitFlowForced;
        RefreshTree();
    }

    private void UpdateGitFlowWarning()
    {
        var violations = GetGitFlowViolations();

        // Auto-organize: when the real hierarchy violates GitFlow, switch to the GitFlow
        // view automatically. Skipped once the user has explicitly chosen a view.
        if (!_gitFlowUserToggled && violations.Count > 0)
            _gitFlowForced = true;

        if (violations.Count == 0 && !_gitFlowForced)
        {
            _warnPanel.Visible = false;
            return;
        }

        _warnPanel.Visible = true;
        if (_gitFlowForced)
        {
            _warnLabel.Text     = violations.Count > 0
                ? $"Hierarquia fora do GitFlow ({violations.Count}) — exibindo organização GitFlow."
                : "Exibindo hierarquia GitFlow forçada.";
            _warnLabel.ForeColor = Color.DarkBlue;
            _btnGitFlow.Text    = "Restaurar hierarquia real";
        }
        else
        {
            string msg = violations.Count == 1
                ? violations[0]
                : $"Hierarquia fora do GitFlow ({violations.Count} violações).";
            _warnLabel.Text     = $"⚠ {msg}";
            _warnLabel.ForeColor = Color.DarkRed;
            _btnGitFlow.Text    = "Organizar como GitFlow";
        }
    }

    private List<string> GetGitFlowViolations()
    {
        var violations = new List<string>();

        // ── Local ────────────────────────────────────────────────────────────
        string? master  = _localBranches.FirstOrDefault(b => b.FullName is "master" or "main")?.FullName;
        string? develop = _localBranches.FirstOrDefault(b => b.FullName == "develop")?.FullName;

        if (master != null && _localParentMap.TryGetValue(master, out var mp) && mp != null)
            violations.Add($"LOCAL '{master}' deveria ser raiz, mas tem pai '{mp}'.");

        if (develop != null)
        {
            _localParentMap.TryGetValue(develop, out var dp);
            if (dp != master)
                violations.Add($"LOCAL 'develop' deveria ser filho de '{master ?? "master/main"}', está em '{dp ?? "(raiz)"}'.");
        }

        foreach (var b in _localBranches)
        {
            if (!b.FullName.StartsWith("feature/")) continue;
            _localParentMap.TryGetValue(b.FullName, out var fp);
            if (fp != develop)
                violations.Add($"LOCAL '{b.FullName}' deveria ser filho de 'develop'.");
        }

        // ── Remotes (por grupo) ───────────────────────────────────────────────
        foreach (var grp in _remoteBranches.GroupBy(b => b.RemoteName ?? "origin"))
        {
            string r        = grp.Key;
            var    branches = grp.ToList();
            string? rmaster  = branches.FirstOrDefault(b => b.DisplayName is "master" or "main")?.FullName;
            string? rdevelop = branches.FirstOrDefault(b => b.DisplayName == "develop")?.FullName;

            if (rmaster != null && _remoteParentMap.TryGetValue(rmaster, out var rmp) && rmp != null)
                violations.Add($"REMOTE '{r}/master' deveria ser raiz, mas tem pai '{rmp}'.");

            if (rdevelop != null)
            {
                _remoteParentMap.TryGetValue(rdevelop, out var rdp);
                if (rdp != rmaster)
                    violations.Add($"REMOTE '{r}/develop' deveria ser filho de '{r}/{master ?? "master/main"}', está em '{rdp ?? "(raiz)"}'.");
            }

            foreach (var b in branches)
            {
                if (!b.DisplayName.StartsWith("feature/")) continue;
                _remoteParentMap.TryGetValue(b.FullName, out var rfp);
                if (rfp != rdevelop)
                    violations.Add($"REMOTE '{b.FullName}' deveria ser filho de '{r}/develop'.");
            }
        }

        return violations;
    }

    private static Dictionary<string, string?> BuildGitFlowRemoteParentMap(List<BranchInfo> branches)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var grp in branches.GroupBy(b => b.RemoteName ?? "origin"))
        {
            var groupList = grp.ToList();
            string? master  = groupList.FirstOrDefault(b => b.DisplayName is "master" or "main")?.FullName;
            string? develop = groupList.FirstOrDefault(b => b.DisplayName == "develop")?.FullName;

            foreach (var b in groupList)
            {
                string? parent;
                if      (b.FullName == master)                   parent = null;
                else if (b.FullName == develop)                  parent = master;
                else if (b.DisplayName.StartsWith("feature/"))   parent = develop;
                else if (b.DisplayName.StartsWith("release/"))   parent = develop;
                else if (b.DisplayName.StartsWith("hotfix/"))    parent = master;
                else                                             parent = null;
                result[b.FullName] = parent;
            }
        }
        return result;
    }

    private static Dictionary<string, string?> BuildGitFlowParentMap(List<BranchInfo> branches)
    {
        string? master  = branches.FirstOrDefault(b => b.FullName is "master" or "main")?.FullName;
        string? develop = branches.FirstOrDefault(b => b.FullName == "develop")?.FullName;

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var b in branches)
        {
            string name = b.FullName;
            string? parent;
            if      (name == master)                 parent = null;
            else if (name == develop)                parent = master;
            else if (name.StartsWith("feature/"))   parent = develop;
            else if (name.StartsWith("release/"))   parent = develop;
            else if (name.StartsWith("hotfix/"))    parent = master;
            else                                     parent = null;
            result[name] = parent;
        }
        return result;
    }

    // ── Repository combo ──────────────────────────────────────────────────────

    private void LoadRepositories()
    {
        _cboRepo.Items.Clear();
        var repos = BranchHierarchyService.GetRepositoriesFromSettings();

        if (!string.IsNullOrEmpty(_svc.WorkingDir) &&
            !repos.Contains(_svc.WorkingDir, StringComparer.OrdinalIgnoreCase))
        {
            repos.Insert(0, _svc.WorkingDir);
        }

        foreach (var r in repos) _cboRepo.Items.Add(r);

        _cboRepo.SelectedItem = _svc.WorkingDir.Length > 0 ? _svc.WorkingDir : _cboRepo.Items[0];
    }

    private void CboRepo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cboRepo.SelectedItem is string dir && dir != _svc.WorkingDir)
        {
            _svc.WorkingDir = dir;
            _gitFlowUserToggled = false; // re-enable auto-organization for the new repo
            RefreshTree();
        }
    }

    // ── Tree building ─────────────────────────────────────────────────────────

    private void RebuildAllSections(string filter, Dictionary<string, string?> localMap, Dictionary<string, string?> remoteMap)
    {
        BuildLocalSection(filter, localMap);
        BuildRemotesSection(filter, remoteMap);
        BuildTagsSection(filter);
    }

    private void BuildLocalSection(string filter, Dictionary<string, string?> localMap)
    {
        var list = Filter(_localBranches, filter);
        _localRoot.Text = $"LOCAL ({list.Count})";
        _localRoot.Nodes.Clear();

        if (list.Count == 0)
        { _localRoot.Nodes.Add(EmptyNode("(nenhuma branch local encontrada)")); return; }

        foreach (var n in BuildAncestryTree(list, localMap, b => b.FullName))
            _localRoot.Nodes.Add(n);
    }

    private void BuildRemotesSection(string filter, Dictionary<string, string?> remoteMap)
    {
        var list = Filter(_remoteBranches, filter);
        _remotesRoot.Text = $"REMOTES ({list.Count})";
        _remotesRoot.Nodes.Clear();

        if (list.Count == 0)
        { _remotesRoot.Nodes.Add(EmptyNode("(nenhuma branch remota encontrada)")); return; }

        foreach (var group in list.GroupBy(b => b.RemoteName ?? "origin").OrderBy(g => g.Key))
        {
            var remoteNode = new TreeNode(group.Key)
            {
                Tag                = SectionTag.RemoteGroup,
                ImageIndex         = NodeIcons.Remote,
                SelectedImageIndex = NodeIcons.Remote
            };
            var groupList  = group.ToList();

            foreach (var n in BuildAncestryTree(groupList, remoteMap, b => b.DisplayName))
                remoteNode.Nodes.Add(n);

            _remotesRoot.Nodes.Add(remoteNode);
        }
    }

    private void BuildTagsSection(string filter)
    {
        var list = Filter(_tags, filter);
        _tagsRoot.Text = $"TAGS ({list.Count})";
        _tagsRoot.Nodes.Clear();

        if (list.Count == 0)
        { _tagsRoot.Nodes.Add(EmptyNode("(nenhuma tag encontrada)")); return; }

        var noChildren = new Dictionary<string, List<BranchInfo>>(StringComparer.Ordinal);
        foreach (var n in PathGroup(list, noChildren, t => t.FullName))
            _tagsRoot.Nodes.Add(n);
    }

    // ── Combined ancestry + path tree builder ─────────────────────────────────

    /// <summary>
    /// Builds the section tree combining two relationships:
    /// <list type="bullet">
    /// <item>vertical nesting by git ancestry (<paramref name="parentMap"/>): a branch is nested
    /// under its parent branch when that parent is also displayed;</item>
    /// <item>horizontal grouping by '/' in the name: among the children of a given parent, names
    /// that share a path prefix are grouped under folder nodes
    /// (e.g. <c>feature/teste</c> → folder "feature" containing leaf "teste").</item>
    /// </list>
    /// <paramref name="getPath"/> returns the name used for '/' splitting and leaf labels
    /// (full name for locals, remote-stripped DisplayName for remotes).
    /// </summary>
    private List<TreeNode> BuildAncestryTree(
        List<BranchInfo> branches,
        Dictionary<string, string?> parentMap,
        Func<BranchInfo, string> getPath)
    {
        var present    = new HashSet<string>(branches.Select(b => b.FullName), StringComparer.Ordinal);
        var childrenOf = new Dictionary<string, List<BranchInfo>>(StringComparer.Ordinal);
        var roots      = new List<BranchInfo>();

        foreach (var b in branches)
        {
            string? parent = parentMap.TryGetValue(b.FullName, out var p) ? p : null;
            if (parent != null && present.Contains(parent))
            {
                if (!childrenOf.TryGetValue(parent, out var lst)) { lst = []; childrenOf[parent] = lst; }
                lst.Add(b);
            }
            else
            {
                roots.Add(b);
            }
        }

        return PathGroup(roots, childrenOf, getPath);
    }

    /// <summary>
    /// Groups a set of sibling branches by '/' path segments into folder nodes, then nests each
    /// branch's ancestry children (from <paramref name="childrenOf"/>) recursively under its leaf.
    /// </summary>
    private List<TreeNode> PathGroup(
        List<BranchInfo> siblings,
        Dictionary<string, List<BranchInfo>> childrenOf,
        Func<BranchInfo, string> getPath)
    {
        var root = new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in siblings.OrderBy(getPath, StringComparer.OrdinalIgnoreCase))
        {
            var parts  = getPath(b).Split('/');
            var cursor = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!cursor.TryGetValue(parts[i], out var child) ||
                    child is not SortedDictionary<string, object> childDict)
                {
                    childDict = new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    cursor[parts[i]] = childDict;
                }
                cursor = (SortedDictionary<string, object>)cursor[parts[i]];
            }
            cursor[parts[^1]] = b; // leaf
        }
        return WalkPathDict(root, childrenOf, getPath);
    }

    private List<TreeNode> WalkPathDict(
        SortedDictionary<string, object> dict,
        Dictionary<string, List<BranchInfo>> childrenOf,
        Func<BranchInfo, string> getPath)
    {
        var nodes = new List<TreeNode>();
        foreach (var kvp in dict)
        {
            if (kvp.Value is BranchInfo b)
            {
                var node = CreateLeafNode(b, kvp.Key);
                if (childrenOf.TryGetValue(b.FullName, out var kids))
                    foreach (var n in PathGroup(kids, childrenOf, getPath))
                        node.Nodes.Add(n);
                nodes.Add(node);
            }
            else if (kvp.Value is SortedDictionary<string, object> sub)
            {
                int fi = GetFolderIconIndex(kvp.Key);
                var folder = new TreeNode(kvp.Key)
                {
                    Tag                = SectionTag.Folder,
                    ImageIndex         = fi,
                    SelectedImageIndex = fi
                };
                foreach (var n in WalkPathDict(sub, childrenOf, getPath))
                    folder.Nodes.Add(n);
                nodes.Add(folder);
            }
        }
        return nodes;
    }

    // ── Node factories ────────────────────────────────────────────────────────

    /// <summary>Creates a leaf branch/tag node showing the last path segment as its label.</summary>
    private TreeNode CreateLeafNode(BranchInfo info, string label)
    {
        // Tracking indicators — shown only when there is actual divergence:
        //   ↑N = commits ahead (to push)   ↓M = commits behind (to pull)
        //   Both omitted when the branch is in sync with its upstream.
        string tracking = string.Empty;
        if (info.Type == BranchType.Local && info.HasUpstream &&
            (info.AheadCount > 0 || info.BehindCount > 0))
        {
            var sb = new System.Text.StringBuilder(" (");
            if (info.BehindCount > 0) sb.Append($"↓{info.BehindCount}");
            if (info.AheadCount  > 0) sb.Append($"↑{info.AheadCount}");
            sb.Append(')');
            tracking = sb.ToString();
        }

        string displayLabel = info.IsCurrent ? $"[{label}]" : label;
        string text         = displayLabel + tracking;

        int imgIdx = GetBranchIconIndex(info);

        return new TreeNode(text)
        {
            Tag                = info,
            NodeFont           = info.IsCurrent ? new Font(_tree.Font, FontStyle.Bold) : null,
            ForeColor          = info.IsCurrent ? SystemColors.Highlight : _tree.ForeColor,
            ImageIndex         = imgIdx,
            SelectedImageIndex = imgIdx
        };
    }

    /// <summary>
    /// Selects the <see cref="NodeIcons"/> index for a branch or tag leaf node based on
    /// the branch name conventions (master, develop, feature/*, etc.).
    /// </summary>
    private static int GetBranchIconIndex(BranchInfo info)
    {
        // For remotes, compare against the display name (strips the remote prefix).
        string name = (info.Type == BranchType.Remote
            ? info.DisplayName : info.FullName).ToLowerInvariant();

        if (name is "master" or "main")         return NodeIcons.BranchMaster;
        if (name is "develop" or "development") return NodeIcons.BranchDevelop;
        if (name.StartsWith("feature/"))        return NodeIcons.BranchFeature;
        if (name.StartsWith("bugfix/")  ||
            name.StartsWith("bug/"))            return NodeIcons.BranchBugfix;
        if (name.StartsWith("release/"))        return NodeIcons.BranchRelease;
        if (name.StartsWith("hotfix/"))         return NodeIcons.BranchHotfix;
        if (name.StartsWith("support/"))        return NodeIcons.BranchSupport;

        return info.Type switch
        {
            BranchType.Remote => NodeIcons.RemoteBranch,
            BranchType.Tag    => NodeIcons.Tag,
            _                 => NodeIcons.Branch,
        };
    }

    /// <summary>
    /// Selects the <see cref="NodeIcons"/> index for a path-segment folder node based on
    /// the folder name (e.g. "feature" → leaf icon, "hotfix" → warning icon).
    /// </summary>
    private static int GetFolderIconIndex(string folderName)
    {
        return folderName.ToLowerInvariant() switch
        {
            "feature"  or "features"             => NodeIcons.BranchFeature,
            "bugfix"   or "bug"   or "bugs"       => NodeIcons.BranchBugfix,
            "release"  or "releases"             => NodeIcons.BranchRelease,
            "hotfix"   or "hotfixes"             => NodeIcons.BranchHotfix,
            "support"                            => NodeIcons.BranchSupport,
            _                                    => NodeIcons.Folder,
        };
    }

    private static TreeNode EmptyNode(string text) =>
        new(text) { Tag = SectionTag.Empty, ForeColor = Color.Gray };

    // ── Filter ────────────────────────────────────────────────────────────────

    private void ApplyFilter(string filter)
    {
        _tree.BeginUpdate();
        try
        {
            var localMap  = _gitFlowForced ? BuildGitFlowParentMap(_localBranches)          : _localParentMap;
            var remoteMap = _gitFlowForced ? BuildGitFlowRemoteParentMap(_remoteBranches) : _remoteParentMap;
            RebuildAllSections(filter, localMap, remoteMap);
            ExpandRoots();
        }
        finally { _tree.EndUpdate(); }
    }

    private static List<BranchInfo> Filter(List<BranchInfo> source, string filter) =>
        string.IsNullOrEmpty(filter)
            ? source
            : source.Where(b => b.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ExpandRoots()
    {
        // While filtering, always expand everything so results are visible.
        string filter = _txtFilter?.Text.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(filter))
        {
            _localRoot.ExpandAll();
            _remotesRoot.ExpandAll();
            _tagsRoot.ExpandAll();
            return;
        }
        _treeStateByRepo.TryGetValue(_svc.WorkingDir, out var saved);
        RestoreTreeState(saved);
    }

    private void RestoreTreeState(HashSet<string>? expandedPaths)
    {
        if (expandedPaths is null || expandedPaths.Count == 0)
        {
            // Default first-time behaviour
            _localRoot.ExpandAll();
            _remotesRoot.Expand();
            _tagsRoot.Expand();
            return;
        }
        _restoringState = true;
        try
        {
            _tree.CollapseAll();
            RestoreNodeExpansion(_tree.Nodes, expandedPaths);
        }
        finally { _restoringState = false; }
    }

    private void RestoreNodeExpansion(TreeNodeCollection nodes, HashSet<string> paths)
    {
        foreach (TreeNode node in nodes)
        {
            string? path = GetNodeStablePath(node);
            if (path != null && paths.Contains(path))
            {
                node.Expand();
                RestoreNodeExpansion(node.Nodes, paths);
            }
        }
    }

    /// <summary>
    /// Computes a stable string key for a tree node that survives tree rebuilds.
    /// Uses the section tag for root nodes, the remote name for remote-group nodes,
    /// the folder text for folder nodes, and BranchInfo.FullName for leaf nodes.
    /// Returns null for nodes that should not be tracked (empty placeholders).
    /// </summary>
    private static string? GetNodeStablePath(TreeNode node)
    {
        var parts = new List<string>();
        TreeNode? cur = node;
        while (cur != null)
        {
            string? seg;
            if (cur.Tag is BranchInfo bi)
            {
                seg = bi.FullName;
            }
            else if (cur.Tag is string s)
            {
                seg = s switch
                {
                    SectionTag.Local       => "LOCAL",
                    SectionTag.Remotes     => "REMOTES",
                    SectionTag.Tags        => "TAGS",
                    SectionTag.RemoteGroup => cur.Text,
                    SectionTag.Folder      => cur.Text,
                    _                      => null   // Empty or unknown
                };
            }
            else
            {
                return null;
            }
            if (seg is null) return null;
            parts.Add(seg);
            cur = cur.Parent;
        }
        parts.Reverse();
        return string.Join("|", parts);
    }

    private void ScheduleSaveDebounce()
    {
        if (_saveDebounce is null)
        {
            _saveDebounce = new System.Windows.Forms.Timer { Interval = 500 };
            _saveDebounce.Tick += (_, _) => { _saveDebounce.Stop(); SaveTreeState(); };
        }
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private static Dictionary<string, HashSet<string>> LoadTreeState()
    {
        try
        {
            if (!File.Exists(StateFilePath)) return [];
            string json = File.ReadAllText(StateFilePath);
            var raw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
            if (raw is null) return [];
            return raw.ToDictionary(
                kv => kv.Key,
                kv => new HashSet<string>(kv.Value, StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase);
        }
        catch { return []; }
    }

    private void SaveTreeState()
    {
        try
        {
            var raw = _treeStateByRepo.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
            string dir = Path.GetDirectoryName(StateFilePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(StateFilePath, JsonSerializer.Serialize(raw));
        }
        catch { }
    }

    /// <summary>
    /// Recursively collapses <paramref name="node"/> and all of its descendants
    /// (depth-first so child state is set before parent collapse).
    /// </summary>
    private static void CollapseRecursive(TreeNode node)
    {
        foreach (TreeNode child in node.Nodes)
            CollapseRecursive(child);
        node.Collapse();
    }

    /// <summary>
    /// Sets TabIndex on every interactive control: top→bottom visually,
    /// right→left within each row.
    /// </summary>
    private void SetTabOrder()
    {
        // Panels on the form — visual order top→bottom
        _topPanel           .TabIndex = 0;
        _filterPanel        .TabIndex = 1;
        _warnPanel          .TabIndex = 2;
        _gitFlowButtonPanel .TabIndex = 3;
        _tree               .TabIndex = 4;
        _bottomPanel        .TabIndex = 5;

        // Top panel (only interactive control)
        _cboRepo.TabIndex = 0;

        // Filter panel — right→left
        _btnRefresh.TabIndex = 0;
        _txtFilter .TabIndex = 1;

        // Warn panel — right→left
        _btnGitFlow.TabIndex = 0;

        // GitFlow button panel — right→left
        _btnGitFlowDedicated.TabIndex = 0;
        _btnCommitDedicated .TabIndex = 1;
        _btnPush            .TabIndex = 2;
        _btnPull            .TabIndex = 3;

        // Bottom panel
        _btnClose.TabIndex = 0;
    }

    /// <summary>Enables or disables all interactive controls while the loading overlay is active.</summary>
    private void SetFormEnabled(bool enabled)
    {
        _cboRepo            .Enabled = enabled;
        _txtFilter          .Enabled = enabled;
        _btnRefresh         .Enabled = enabled;
        _btnGitFlow         .Enabled = enabled;
        _btnGitFlowDedicated.Enabled = enabled;
        _tree               .Enabled = enabled;
        _btnClose           .Enabled = enabled;
    }

    private void UpdateStatus()
        => _statusLbl.Text =
            $"Local: {_localBranches.Count}  |  Remoto: {_remoteBranches.Count}  |  Tags: {_tags.Count}";

    private void UpdateBranchLabel()
        => _lblBranch.Text = $"Branch: {_svc.GetCurrentBranch()}";

    private void UpdatePullPushButtons()
    {
        var current    = _localBranches.FirstOrDefault(b => b.IsCurrent);
        bool hasBranch = current != null;
        _btnPull            .Visible = hasBranch;
        _btnPush            .Visible = hasBranch;
        _btnCommitDedicated .Visible = hasBranch;
        _btnGitFlowDedicated.Visible = hasBranch;
        _gitFlowButtonPanel.PerformLayout(); // reposition buttons after visibility change
        if (!hasBranch) return;

        int behind = current!.BehindCount;
        int ahead  = current.AheadCount;
        _btnPull.Text = behind > 0 ? $"Pull ↓{behind}" : "Pull";
        _btnPush.Text = ahead  > 0 ? $"Push ↑{ahead}"  : "Push";
        _btnCommitDedicated.Text = $"Commit ({_svc.GetPendingChangesCount()})";
    }

    private BranchInfo? SelectedBranch()
        => _tree.SelectedNode?.Tag as BranchInfo;

    // ── Tree drawing (bold + highlight for current branch) ────────────────────

    private void Tree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node is null) return;

        bool selected = (e.State & TreeNodeStates.Selected) != 0;
        bool current  = e.Node.Tag is BranchInfo bi && bi.IsCurrent;

        Font   font  = current ? new Font(_tree.Font, FontStyle.Bold) : _tree.Font;
        Color  fore  = selected ? SystemColors.HighlightText : (current ? SystemColors.Highlight : _tree.ForeColor);
        Color  back  = selected ? SystemColors.Highlight : _tree.BackColor;

        using var bg = new SolidBrush(back);
        e.Graphics.FillRectangle(bg, e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.Node.Text, font, e.Bounds, fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);

        if (current) font.Dispose();
    }

    // ── Tree events ───────────────────────────────────────────────────────────

    private void Tree_AfterExpand(object? sender, TreeViewEventArgs e)
    {
        if (_restoringState || e.Node is null) return;
        string? path = GetNodeStablePath(e.Node);
        if (path is null) return;
        if (!_treeStateByRepo.TryGetValue(_svc.WorkingDir, out var set))
        { set = []; _treeStateByRepo[_svc.WorkingDir] = set; }
        set.Add(path);
        ScheduleSaveDebounce();
    }

    private void Tree_AfterCollapse(object? sender, TreeViewEventArgs e)
    {
        if (_restoringState || e.Node is null) return;
        string? path = GetNodeStablePath(e.Node);
        if (path is null) return;
        if (_treeStateByRepo.TryGetValue(_svc.WorkingDir, out var set))
            set.Remove(path);
        ScheduleSaveDebounce();
    }

    private void Tree_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node?.Tag is BranchInfo) DoCheckout();
    }

    private void Tree_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && _tree.SelectedNode?.Tag is BranchInfo) DoCheckout();
    }

    private void Tree_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var node = _tree.GetNodeAt(e.X, e.Y);
            if (node != null) _tree.SelectedNode = node;
        }
    }

    private void CtxMenu_Opening(object? sender, CancelEventArgs e)
    {
        var info     = SelectedBranch();
        bool branch  = info != null;
        bool local   = info?.Type == BranchType.Local;
        bool remote  = info?.Type == BranchType.Remote;
        bool tag     = info?.Type == BranchType.Tag;

        _miCommit.Text = $"Commit ({_svc.GetPendingChangesCount()})";

        _miCheckout .Visible = branch;
        _miNewBranch.Visible = local || tag;
        _miMerge    .Visible = local;
        _miRebase   .Visible = local;
        _miRename   .Visible = local;
        _miDelete   .Visible = local || remote || tag;
        _miGitFlow  .Visible = branch;

        FixContextMenuSeparators();
    }

    /// <summary>
    /// Hides any separator that has no visible non-separator items on one or both sides.
    /// Prevents orphan separator lines when menu groups are entirely hidden.
    /// </summary>
    private void FixContextMenuSeparators()
    {
        var items = _ctxMenu.Items;
        foreach (ToolStripItem item in items)
        {
            if (item is not ToolStripSeparator sep) continue;
            int idx = items.IndexOf(sep);
            bool beforeOk = false, afterOk = false;
            for (int i = idx - 1; i >= 0; i--)
                if (items[i] is not ToolStripSeparator && items[i].Visible) { beforeOk = true; break; }
            for (int i = idx + 1; i < items.Count; i++)
                if (items[i] is not ToolStripSeparator && items[i].Visible) { afterOk = true; break; }
            sep.Visible = beforeOk && afterOk;
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void DoPull()
    {
        _btnPull.Enabled = false;
        _ = Task.Run(() => _svc.Pull()).ContinueWith(t =>
        {
            var (ok, err) = t.Result;
            BeginInvoke(() =>
            {
                _btnPull.Enabled = true;
                RefreshTree();
                _notifyRepoChanged?.Invoke();
                if (!ok && !string.IsNullOrEmpty(err))
                    ShowError("Pull falhou", err);
            });
        });
    }

    private void DoPush()
    {
        _btnPush.Enabled = false;
        _ = Task.Run(() => _svc.Push()).ContinueWith(t =>
        {
            var (ok, err) = t.Result;
            BeginInvoke(() =>
            {
                _btnPush.Enabled = true;
                RefreshTree();
                _notifyRepoChanged?.Invoke();
                if (!ok && !string.IsNullOrEmpty(err))
                    ShowError("Push falhou", err);
            });
        });
    }

    private void DoCommit()
    {
        // Prefer the in-process native commit dialog: it has the full plugin system loaded,
        // so Commit Template plugins (e.g. Zimerfeld: Auto-resumo) are visible.
        if (_openCommitDialog != null)
        {
            bool? result = _openCommitDialog(this);
            if (result.HasValue)
            {
                if (result.Value) { RefreshTree(); _notifyRepoChanged?.Invoke(); }
                return;
            }
        }
        // Fallback: spawn a new GitExtensions process (plugins won't load in that mode).
        var (ok, err) = _svc.OpenCommitWindow();
        if (!ok) ShowError("Erro ao abrir a janela de Commit", err);
    }

    private void DoCheckout()
    {
        var info = SelectedBranch();
        if (info is null) return;

        var (ok, err) = info.Type == BranchType.Remote
            ? _svc.CheckoutRemoteAsLocal(info.FullName)
            : _svc.Checkout(info.FullName);

        if (ok)
        {
            RefreshTree();
            _notifyRepoChanged?.Invoke();
        }
        else if (!string.IsNullOrEmpty(err))
        {
            // If branch cannot be deleted due to uncommitted changes, offer force option
            if (err.Contains("not fully merged", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(err, "Checkout falhou", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(err, "Checkout falhou", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void DoNewBranch()
    {
        var info = SelectedBranch();
        if (info is null) return;

        using var dlg = new InputDialog("Nova branch",
            $"Nome da nova branch a partir de '{info.FullName}':");
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var (ok, err) = _svc.CreateAndCheckoutBranch(dlg.Value.Trim(), info.FullName);
        if (ok)
        {
            RefreshTree();
            _notifyRepoChanged?.Invoke();
        }
        else ShowError("Erro ao criar branch", err);
    }

    private void DoGitFlow()
    {
        using var dlg = new GitFlowForm(_svc);

        // Place the two windows side by side, both centered on the current screen.
        var wa     = Screen.FromControl(this).WorkingArea;
        int gap    = 8;
        int totalW = Width + gap + dlg.Width;

        if (wa.Width >= totalW)
        {
            int leftX = wa.Left + (wa.Width  - totalW) / 2;
            int topY  = wa.Top  + Math.Max(0, (wa.Height - Math.Max(Height, dlg.Height)) / 2);
            Location     = new Point(leftX, topY);
            dlg.Location = new Point(leftX + Width + gap, topY);
        }
        else
        {
            // Screen too narrow — centre GitFlow over this window instead
            dlg.Location = new Point(
                Math.Max(wa.Left, Location.X + (Width  - dlg.Width)  / 2),
                Math.Max(wa.Top,  Location.Y + (Height - dlg.Height) / 2));
        }

        dlg.ShowDialog(this);
        RefreshTree();
        _notifyRepoChanged?.Invoke();
    }

    private void DoMerge()
    {
        var info = SelectedBranch();
        if (info?.Type != BranchType.Local) return;

        if (Confirm($"Mesclar '{info.FullName}' na branch atual '{_svc.GetCurrentBranch()}'?", "Confirmar Merge"))
        {
            var (ok, err) = _svc.MergeBranch(info.FullName);
            if (ok) RefreshTree();
            else ShowError("Erro no merge", err);
        }
    }

    private void DoRebase()
    {
        var info = SelectedBranch();
        if (info?.Type != BranchType.Local) return;

        if (Confirm($"Rebase em cima de '{info.FullName}' (branch atual: '{_svc.GetCurrentBranch()}')?",
                "Confirmar Rebase"))
        {
            var (ok, err) = _svc.RebaseBranch(info.FullName);
            if (ok) RefreshTree();
            else ShowError("Erro no rebase", err);
        }
    }

    private void DoRename()
    {
        var info = SelectedBranch();
        if (info?.Type != BranchType.Local) return;

        using var dlg = new InputDialog("Renomear branch",
            $"Novo nome para '{info.FullName}':", info.FullName);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var (ok, err) = _svc.RenameBranch(info.FullName, dlg.Value.Trim());
        if (ok) RefreshTree();
        else ShowError("Erro ao renomear", err);
    }

    private void DoDelete()
    {
        var info = SelectedBranch();
        if (info is null) return;

        if (!Confirm($"Excluir '{info.FullName}'?", "Confirmar exclusão")) return;

        (bool ok, string err) result = info.Type switch
        {
            BranchType.Tag    => _svc.DeleteTag(info.FullName),
            BranchType.Remote => _svc.DeleteBranch(info.FullName, isRemote: true),
            _                 => _svc.DeleteBranch(info.FullName, isRemote: false)
        };

        if (result.ok)
        {
            RefreshTree();
        }
        else if (result.err.Contains("not fully merged", StringComparison.OrdinalIgnoreCase))
        {
            if (Confirm($"A branch não está totalmente mesclada. Forçar exclusão de '{info.FullName}'?",
                        "Excluir forçado"))
            {
                var (ok2, err2) = _svc.DeleteBranchForce(info.FullName);
                if (ok2) RefreshTree();
                else ShowError("Erro ao excluir", err2);
            }
        }
        else
        {
            ShowError("Erro ao excluir", result.err);
        }
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private bool Confirm(string text, string caption) =>
        MessageBox.Show(text, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    private void ShowError(string caption, string message) =>
        MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);

    // ─────────────────────────────────────────────────────────────────────────
    // Tag sentinel values for non-branch tree nodes
    // ─────────────────────────────────────────────────────────────────────────
    private static class SectionTag
    {
        public const string Local       = "section:local";
        public const string Remotes     = "section:remotes";
        public const string Tags        = "section:tags";
        public const string RemoteGroup = "section:remote-group";
        public const string Folder      = "section:folder";
        public const string Empty       = "section:empty";
    }
}

// ── Simple single-line input dialog ──────────────────────────────────────────

/// <summary>Minimal modal dialog that asks the user for a text value.</summary>
internal sealed class InputDialog : Form
{
    private readonly Label   _label;
    private readonly TextBox _input;

    public string Value => _input.Text;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        Text            = title;
        Size            = new Size(420, 148);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Segoe UI", 9f);
        Icon            = TreeOfLifeIcon.ForForm();

        _label = new Label  { Text = prompt, Bounds = new Rectangle(12, 12, 388, 20) };
        _input = new TextBox { Text = defaultValue, Bounds = new Rectangle(12, 36, 388, 22) };

        var ok     = new Button { Text = "OK",       Bounds = new Rectangle(228, 70, 82, 28), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancelar", Bounds = new Rectangle(318, 70, 82, 28), DialogResult = DialogResult.Cancel };

        Controls.AddRange([_label, _input, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
