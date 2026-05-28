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
    private Button   _btnTrack       = null!;
    private Button   _btnUpdate      = null!;
    private Button   _btnFinish      = null!;
    private CheckBox _chkKeep        = null!;
    private CheckBox _chkNoFetch     = null!;

    // ── Result ──
    private GroupBox _grpResult = null!;
    private TextBox  _txtResult = null!;

    public GitFlowForm(BranchHierarchyService svc)
    {
        _svc = svc;

        Text            = "GitFlow";
        Size            = new Size(662, 824);
        MinimumSize     = new Size(560, 640);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox     = true;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Segoe UI", 9f);
        KeyPreview      = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        BuildHeader();
        BuildStartGroup();
        BuildManageGroup();
        BuildResultGroup();

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

        _btnPublish = new Button { Text = "Publish", Bounds = new Rectangle(40, 104, 110, 26) };
        _btnPublish.Click += (_, _) => DoPublish();

        _btnTrack = new Button { Text = "Track", Bounds = new Rectangle(170, 104, 110, 26) };
        _btnTrack.Click += (_, _) => DoTrack();

        _btnUpdate = new Button { Text = "Update", Bounds = new Rectangle(300, 104, 110, 26) };
        _btnUpdate.Click += (_, _) => DoUpdate();

        _btnFinish = new Button
        {
            Text   = "Finish",
            Bounds = new Rectangle(498, 104, 110, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnFinish.Click += (_, _) => DoFinish();

        _chkKeep = new CheckBox
        {
            Text   = "Keep branch after finish",
            Bounds = new Rectangle(398, 136, 210, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _chkNoFetch = new CheckBox
        {
            Text   = "No fetch (--no-fetch)",
            Bounds = new Rectangle(398, 160, 210, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        var lblHint = new Label
        {
            Text      = "Track: cria branch local da remota   •   Update: traz mudanças da branch pai",
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = SystemColors.GrayText,
            Bounds    = new Rectangle(12, 162, 370, 18)
        };

        _grpManage.Controls.AddRange(
        [
            _cboManageType, lblBranch, _lblManagePrefix, _cboManageBranch,
            _btnPublish, _btnTrack, _btnUpdate, _btnFinish, _chkKeep, _chkNoFetch, lblHint
        ]);
        Controls.Add(_grpManage);
    }

    private void BuildResultGroup()
    {
        _grpResult = new GroupBox
        {
            Text   = "Result of git flow command run",
            Bounds = new Rectangle(8, 380, 638, 390),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        _txtResult = new TextBox
        {
            Multiline  = true,
            ReadOnly   = true,
            ScrollBars = ScrollBars.Both,
            WordWrap   = false,
            BackColor  = SystemColors.Window,
            Bounds     = new Rectangle(10, 22, 618, 358),
            Anchor     = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Font       = new Font("Consolas", 9f)
        };
        _grpResult.Controls.Add(_txtResult);
        Controls.Add(_grpResult);
    }

    // ── Data ────────────────────────────────────────────────────────────────

    private void InitData()
    {
        _lblHead.Text = "HEAD:  " + _svc.GetHeadRef();

        _cboBasedOn.Items.Clear();
        _cboBasedOn.Items.Add("develop");
        foreach (var b in _svc.GetLocalBranches())
            if (!_cboBasedOn.Items.Contains(b.FullName))
                _cboBasedOn.Items.Add(b.FullName);
        _cboBasedOn.SelectedIndex = 0; // "develop" is the default base
        _cboBasedOn.Enabled = _chkBasedOn.Checked;

        // Detect git-flow type of the currently checked-out branch so the Manage
        // panel opens already pointing at it (matching what the user is on).
        string current = _svc.GetCurrentBranch();
        int matchIdx = -1;
        string matchName = string.Empty;
        for (int i = 0; i < BranchHierarchyService.GitFlowTypes.Length; i++)
        {
            string prefix = _svc.GetGitFlowPrefix(BranchHierarchyService.GitFlowTypes[i]);
            if (prefix.Length > 0 && current.StartsWith(prefix, StringComparison.Ordinal))
            {
                matchIdx  = i;
                matchName = current[prefix.Length..];
                break;
            }
        }

        _cboStartType.SelectedIndex  = 0; // triggers prefix update
        _cboManageType.SelectedIndex = matchIdx >= 0 ? matchIdx : 0; // triggers branch reload

        if (matchName.Length > 0)
        {
            int branchIdx = _cboManageBranch.Items.IndexOf(matchName);
            if (branchIdx >= 0)
                _cboManageBranch.SelectedIndex = branchIdx;
            else
                _cboManageBranch.Text = matchName;
        }
    }

    private void ReloadManageBranches()
    {
        if (_cboManageType.SelectedIndex < 0) return;

        string prefix = _svc.GetGitFlowPrefix(_cboManageType.Text);
        _lblManagePrefix.Text = prefix;

        var names = new List<string>();
        foreach (var name in _svc.GetGitFlowBranches(prefix))
            if (!names.Contains(name)) names.Add(name);

        // Also list remote branches of this type (with prefix stripped) so Track can
        // pick up a branch that exists only on the remote.
        foreach (var rb in _svc.GetRemoteBranches())
        {
            int slash = rb.FullName.IndexOf('/');
            if (slash < 0) continue;
            string afterRemote = rb.FullName[(slash + 1)..];
            if (!afterRemote.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string stripped = afterRemote[prefix.Length..];
            if (stripped.Length > 0 && !names.Contains(stripped)) names.Add(stripped);
        }

        _cboManageBranch.Items.Clear();
        foreach (var n in names) _cboManageBranch.Items.Add(n);
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

    private void DoTrack()
    {
        string type = _cboManageType.Text;
        string name = Clean(_cboManageBranch.Text);
        if (name.Length == 0) return;
        RunFlow($"flow {type} track \"{name}\"");
    }

    private void DoUpdate()
    {
        string type = _cboManageType.Text;
        string name = Clean(_cboManageBranch.Text);
        if (name.Length == 0) return;
        RunFlow($"flow {type} update \"{name}\"");
    }

    private void DoFinish()
    {
        string type = _cboManageType.Text;
        string name = Clean(_cboManageBranch.Text);
        if (name.Length == 0) return;

        string flags = string.Empty;
        if (_chkKeep.Checked)    flags += "-k ";
        if (_chkNoFetch.Checked) flags += "--no-fetch ";
        RunFlow($"flow {type} finish {flags}\"{name}\"");
    }

    private void RunFlow(string args)
    {
        string output;
        int code;
        Cursor = Cursors.WaitCursor;
        try
        {
            (output, code) = _svc.RunGitFlow(args);
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

        if (code != 0)
            ShowFlowError(output);
    }

    /// <summary>
    /// Shows a MessageBox after a failed git flow command. When the output points to a missing
    /// base/production branch (the common git-flow-next finish failure), it adds guidance.
    /// </summary>
    private static void ShowFlowError(string output)
    {
        bool missingBase =
            output.Contains("couldn't find remote ref", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("start point branch", StringComparison.OrdinalIgnoreCase);

        string msg = missingBase
            ? "O git flow não encontrou a branch base/produção (ex.: 'main' ou 'develop').\n\n" +
              "Verifique se ela existe localmente:\n" +
              "    git branch --list main master develop\n\n" +
              "E a configuração do git flow:\n" +
              "    git config gitflow.branch.main\n" +
              "    git config gitflow.branch.develop\n\n" +
              "Crie a branch que falta ou ajuste a config. Se a falha for ao buscar do remoto, " +
              "marque \"No fetch (--no-fetch)\" e tente novamente."
            : "O comando git flow falhou. Veja os detalhes na janela de resultado.";

        MessageBox.Show(msg, "GitFlow — falha", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            "git flow organiza o trabalho em branches de tipo feature, release, hotfix, " +
            "bugfix e support.\n\n" +
            "• start   — cria a branch a partir da base (develop por padrão)\n" +
            "• publish — envia a branch para o remoto\n" +
            "• track   — cria uma branch local que rastreia a remota\n" +
            "• update  — traz mudanças da branch pai para a branch\n" +
            "• finish  — mescla de volta e remove a branch\n\n" +
            "Requer a extensão git-flow instalada (git-flow-next).",
            "About GitFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>Removes double quotes to keep command arguments safe.</summary>
    private static string Clean(string s) => s.Trim().Replace("\"", "");
}
