// GitFlowForm.cs — Git Flow operations window for ZimerfeldTree plugin
// MIT License — Copyright (c) 2026 Zimerfeld

namespace GitExtensions.ZimerfeldTree;

/// <summary>
/// Modal window that drives <c>git flow</c> commands: starting feature/release/hotfix
/// branches and publishing, pulling and finishing existing ones.  The raw command output
/// is shown so the user can see exactly what git flow did.
/// </summary>
public sealed class GitFlowForm : Form
{
    private readonly BranchHierarchyService _svc;

    // ── Header ──
    private Label     _lblHead   = null!;
    private LinkLabel _lnkAbout  = null!;

    // ── Start branch ──
    private GroupBox _grpStart      = null!;
    private ComboBox _cboStartType  = null!;
    private Label    _lblStartPrefix = null!;
    private TextBox  _txtStartName  = null!;
    private Button   _btnStart      = null!;
    private CheckBox _chkBasedOn    = null!;
    private ComboBox _cboBasedOn    = null!;

    // ── Manage existing branches ──
    private GroupBox _grpManage      = null!;
    private ComboBox _cboManageType  = null!;
    private Label    _lblManagePrefix = null!;
    private ComboBox _cboManageBranch = null!;
    private Button   _btnPublish     = null!;
    private ComboBox _cboRemote      = null!;
    private Button   _btnPull        = null!;
    private Button   _btnFinish      = null!;
    private CheckBox _chkPush        = null!;
    private CheckBox _chkSquash      = null!;

    // ── Result ──
    private GroupBox _grpResult = null!;
    private TextBox  _txtResult = null!;

    private Button _btnClose = null!;

    public GitFlowForm(BranchHierarchyService svc)
    {
        _svc = svc;

        Text            = "GitFlow";
        Size            = new Size(662, 624);
        MinimumSize     = new Size(560, 512);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox     = true;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Segoe UI", 9f);

        BuildHeader();
        BuildStartGroup();
        BuildManageGroup();
        BuildResultGroup();
        BuildCloseButton();

        Load += (_, _) => InitData();
    }

    // ── Build UI ────────────────────────────────────────────────────────────

    private void BuildHeader()
    {
        _lblHead = new Label
        {
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds    = new Rectangle(120, 10, 400, 20),
            Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _lnkAbout = new LinkLabel
        {
            Text      = "About GitFlow",
            AutoSize  = true,
            Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            Location  = new Point(ClientSize.Width - 110, 12)
        };
        _lnkAbout.LinkClicked += (_, _) => ShowAbout();

        Controls.AddRange([_lblHead, _lnkAbout]);
    }

    private void BuildStartGroup()
    {
        _grpStart = new GroupBox
        {
            Bounds = new Rectangle(8, 36, 638, 128),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var lblType = new Label { Text = "Start branch:",  Bounds = new Rectangle(12, 24, 90, 20) };
        _cboStartType = new ComboBox
        {
            Bounds        = new Rectangle(108, 21, 180, 24),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cboStartType.Items.AddRange([.. BranchHierarchyService.GitFlowTypes]);
        _cboStartType.SelectedIndexChanged += (_, _) =>
            _lblStartPrefix.Text = _svc.GetGitFlowPrefix(_cboStartType.Text);

        var lblName = new Label { Text = "Expected name:", Bounds = new Rectangle(12, 60, 90, 20) };
        _lblStartPrefix = new Label
        {
            TextAlign = ContentAlignment.MiddleRight,
            Bounds    = new Rectangle(100, 60, 60, 22)
        };
        _txtStartName = new TextBox
        {
            Bounds = new Rectangle(164, 58, 360, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _btnStart = new Button
        {
            Text   = "Start!",
            Bounds = new Rectangle(534, 56, 90, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnStart.Click += (_, _) => DoStart();

        _chkBasedOn = new CheckBox
        {
            Text   = "based on:",
            Bounds = new Rectangle(108, 96, 90, 22)
        };
        _chkBasedOn.CheckedChanged += (_, _) => _cboBasedOn.Enabled = _chkBasedOn.Checked;

        _cboBasedOn = new ComboBox
        {
            Bounds        = new Rectangle(200, 94, 324, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled       = false,
            Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _grpStart.Controls.AddRange(
            [lblType, _cboStartType, lblName, _lblStartPrefix, _txtStartName, _btnStart,
             _chkBasedOn, _cboBasedOn]);
        Controls.Add(_grpStart);
    }

    private void BuildManageGroup()
    {
        _grpManage = new GroupBox
        {
            Text   = "Manage existing branches:",
            Bounds = new Rectangle(8, 172, 638, 200),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _cboManageType = new ComboBox
        {
            Bounds        = new Rectangle(210, 22, 180, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor        = AnchorStyles.Top | AnchorStyles.Right
        };
        _cboManageType.Items.AddRange([.. BranchHierarchyService.GitFlowTypes]);
        _cboManageType.SelectedIndexChanged += (_, _) => ReloadManageBranches();

        var lblBranch = new Label
        {
            Text      = "branch:",
            TextAlign = ContentAlignment.MiddleRight,
            Bounds    = new Rectangle(40, 58, 60, 20),
            Anchor    = AnchorStyles.Top | AnchorStyles.Right
        };
        _lblManagePrefix = new Label
        {
            Text      = "/",
            TextAlign = ContentAlignment.MiddleRight,
            Bounds    = new Rectangle(104, 58, 100, 20),
            Anchor    = AnchorStyles.Top | AnchorStyles.Right
        };
        _cboManageBranch = new ComboBox
        {
            Bounds        = new Rectangle(210, 56, 410, 24),
            DropDownStyle = ComboBoxStyle.DropDown,
            Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _btnPublish = new Button { Text = "Publish", Bounds = new Rectangle(40, 110, 110, 26) };
        _btnPublish.Click += (_, _) => DoPublish();

        var lblRemote = new Label
        {
            Text      = "Remote to pull from :",
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds    = new Rectangle(190, 90, 200, 20)
        };
        _cboRemote = new ComboBox
        {
            Bounds        = new Rectangle(200, 112, 180, 24),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _btnPull = new Button { Text = "Pull", Bounds = new Rectangle(248, 140, 90, 26) };
        _btnPull.Click += (_, _) => DoPull();

        _btnFinish = new Button
        {
            Text   = "Finish",
            Bounds = new Rectangle(498, 110, 110, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnFinish.Click += (_, _) => DoFinish();

        _chkPush = new CheckBox
        {
            Text   = "Push after finish",
            Bounds = new Rectangle(498, 140, 140, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _chkSquash = new CheckBox
        {
            Text   = "Squash",
            Bounds = new Rectangle(498, 164, 140, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        _grpManage.Controls.AddRange(
        [
            _cboManageType, lblBranch, _lblManagePrefix, _cboManageBranch,
            _btnPublish, lblRemote, _cboRemote, _btnPull,
            _btnFinish, _chkPush, _chkSquash
        ]);
        Controls.Add(_grpManage);
    }

    private void BuildResultGroup()
    {
        _grpResult = new GroupBox
        {
            Text   = "Result of git flow command run",
            Bounds = new Rectangle(8, 380, 638, 156),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        _txtResult = new TextBox
        {
            Multiline  = true,
            ReadOnly   = true,
            ScrollBars = ScrollBars.Both,
            WordWrap   = false,
            BackColor  = SystemColors.Window,
            Bounds     = new Rectangle(10, 22, 618, 124),
            Anchor     = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Font       = new Font("Consolas", 9f)
        };
        _grpResult.Controls.Add(_txtResult);
        Controls.Add(_grpResult);
    }

    private void BuildCloseButton()
    {
        _btnClose = new Button
        {
            Text         = "Close",
            Bounds       = new Rectangle(286, 544, 90, 28),
            Anchor       = AnchorStyles.Bottom,
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(_btnClose);
        CancelButton = _btnClose;
    }

    // ── Data ────────────────────────────────────────────────────────────────

    private void InitData()
    {
        _lblHead.Text = "HEAD:  " + _svc.GetHeadRef();

        var remotes = _svc.GetRemotes();
        _cboRemote.Items.AddRange([.. remotes]);
        if (remotes.Count > 0)
            _cboRemote.SelectedIndex = remotes.FindIndex(r => r == "origin") is var i && i >= 0 ? i : 0;

        _cboBasedOn.Items.Clear();
        _cboBasedOn.Items.Add("develop");
        foreach (var b in _svc.GetLocalBranches())
            if (!_cboBasedOn.Items.Contains(b.FullName))
                _cboBasedOn.Items.Add(b.FullName);
        _cboBasedOn.SelectedIndex = 0; // "develop" is the default base
        _cboBasedOn.Enabled = _chkBasedOn.Checked;

        _cboStartType.SelectedIndex  = 0; // triggers prefix update
        _cboManageType.SelectedIndex = 0; // triggers branch reload
    }

    private void ReloadManageBranches()
    {
        if (_cboManageType.SelectedIndex < 0) return;

        string prefix = _svc.GetGitFlowPrefix(_cboManageType.Text);
        _lblManagePrefix.Text = prefix;

        _cboManageBranch.Items.Clear();
        foreach (var name in _svc.GetGitFlowBranches(prefix))
            _cboManageBranch.Items.Add(name);
        if (_cboManageBranch.Items.Count > 0)
            _cboManageBranch.SelectedIndex = 0;
        else
            _cboManageBranch.Text = string.Empty;
    }

    // ── Actions ─────────────────────────────────────────────────────────────

    private void DoStart()
    {
        string type = _cboStartType.Text;
        string name = Clean(_txtStartName.Text);
        if (name.Length == 0)
        {
            MessageBox.Show("Informe o nome da branch.", "GitFlow",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string baseArg = string.Empty;
        if (_chkBasedOn.Checked)
        {
            string baseBranch = Clean(_cboBasedOn.Text);
            if (baseBranch.Length > 0) baseArg = $" \"{baseBranch}\"";
        }

        RunFlow($"flow {type} start \"{name}\"{baseArg}");
        _txtStartName.Clear();
    }

    private void DoPublish()
    {
        string type = _cboManageType.Text;
        string name = Clean(_cboManageBranch.Text);
        if (name.Length == 0) return;
        RunFlow($"flow {type} publish \"{name}\"");
    }

    private void DoPull()
    {
        string type   = _cboManageType.Text;
        string name   = Clean(_cboManageBranch.Text);
        string remote = Clean(_cboRemote.Text);
        if (name.Length == 0 || remote.Length == 0) return;
        RunFlow($"flow {type} pull {remote} \"{name}\"");
    }

    private void DoFinish()
    {
        string type = _cboManageType.Text;
        string name = Clean(_cboManageBranch.Text);
        if (name.Length == 0) return;

        string flags = string.Empty;
        if (_chkSquash.Checked) flags += "-S ";
        if (_chkPush.Checked)   flags += "-p ";
        RunFlow($"flow {type} finish {flags}\"{name}\"");
    }

    private void RunFlow(string args)
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            var (output, code) = _svc.RunGitFlow(args);
            string body = output.Length == 0
                ? (code == 0 ? "(comando concluído)" : "(sem saída)")
                : output.Replace("\n", "\r\n");
            _txtResult.Text = $"command - git {args}\r\n\r\n{body}";
        }
        finally
        {
            Cursor = Cursors.Default;
        }

        _lblHead.Text = "HEAD:  " + _svc.GetHeadRef();
        ReloadManageBranches();
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            "git flow organiza o trabalho em branches de tipo feature, release, hotfix, " +
            "bugfix e support.\n\n" +
            "• start   — cria a branch a partir da base do tipo\n" +
            "• publish — envia a branch para o remoto\n" +
            "• pull    — traz a branch do remoto\n" +
            "• finish  — mescla de volta e remove a branch\n\n" +
            "Requer a extensão git-flow instalada.",
            "About GitFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>Removes double quotes to keep command arguments safe.</summary>
    private static string Clean(string s) => s.Trim().Replace("\"", "");
}
