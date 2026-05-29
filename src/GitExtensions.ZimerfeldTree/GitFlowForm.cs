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

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GitExtensions", "ZimerfeldTree.gitflowsettings.json");

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

    // ── Bottom close button ──
    private Button _btnClose = null!;

    public GitFlowForm(BranchHierarchyService svc)
    {
        _svc = svc;

        Text            = "ZimerfeldTree - GitFlow";
        Size            = new Size(662, 824);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.Manual;   // caller controls position (side-by-side)
        Font            = new Font("Segoe UI", 9f);
        Icon            = TreeOfLifeIcon.ForForm();

        BuildHeader();
        BuildStartGroup();
        BuildManageGroup();
        BuildResultGroup();
        BuildCloseButton();

        CancelButton = _btnClose;

        SetTabOrder();
        Load += (_, _) => { InitData(); ApplySettings(); };
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
        // "Start branch" is now the GroupBox title — no separate lblType inside.
        _grpStart = new GroupBox
        {
            Text   = "Start branch",
            Bounds = new Rectangle(8, 36, 638, 120),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Row 1 — type selector (col label x=12, col input x=108)
        var lblType = new Label
        {
            Text      = "Type:",
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds    = new Rectangle(12, 24, 90, 22)
        };
        _cboStartType = new ComboBox
        {
            Bounds        = new Rectangle(108, 22, 180, 24),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cboStartType.Items.AddRange([.. BranchHierarchyService.GitFlowTypes]);
        _cboStartType.SelectedIndexChanged += (_, _) =>
            _lblStartPrefix.Text = _svc.GetGitFlowPrefix(_cboStartType.Text);

        // Row 2 — expected name (prefix label + text input + Start! button)
        var lblName = new Label
        {
            Text      = "Expected name:",
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds    = new Rectangle(12, 54, 90, 22)
        };
        _lblStartPrefix = new Label
        {
            TextAlign = ContentAlignment.MiddleRight,
            Bounds    = new Rectangle(108, 54, 60, 22)
        };
        _txtStartName = new TextBox
        {
            Bounds = new Rectangle(172, 54, 356, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _btnStart = new Button
        {
            Text   = "Start!",
            Bounds = new Rectangle(534, 52, 90, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnStart.Click += (_, _) => DoStart();

        // Row 3 — optional base branch
        _chkBasedOn = new CheckBox
        {
            Text   = "based on:",
            Bounds = new Rectangle(108, 84, 90, 22)
        };
        _chkBasedOn.CheckedChanged += (_, _) => _cboBasedOn.Enabled = _chkBasedOn.Checked;

        _cboBasedOn = new ComboBox
        {
            Bounds        = new Rectangle(202, 82, 322, 24),
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
            Bounds = new Rectangle(8, 164, 638, 192),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Row 1 — type selector (aligned with grpStart: label x=12, input x=108)
        var lblType = new Label
        {
            Text      = "Type:",
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds    = new Rectangle(12, 24, 90, 22)
        };
        _cboManageType = new ComboBox
        {
            Bounds        = new Rectangle(108, 22, 180, 24),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cboManageType.Items.AddRange([.. BranchHierarchyService.GitFlowTypes]);
        _cboManageType.SelectedIndexChanged += (_, _) => ReloadManageBranches();

        // Row 2 — branch selector (same column positions as grpStart name row)
        var lblBranch = new Label
        {
            Text      = "Branch:",
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds    = new Rectangle(12, 54, 90, 22)
        };
        _lblManagePrefix = new Label
        {
            Text      = "/",
            TextAlign = ContentAlignment.MiddleRight,
            Bounds    = new Rectangle(108, 54, 60, 22)
        };
        _cboManageBranch = new ComboBox
        {
            Bounds        = new Rectangle(172, 52, 452, 24),
            DropDownStyle = ComboBoxStyle.DropDown,
            Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // ── Buttons row: 4 × 140 px, gap 18 px, left margin 12 px ──────────────
        // (12)[Publish 140](18)[Track 140](18)[Update 140](18)[Finish 140](12) = 638 ✓
        _btnPublish = new Button { Text = "Publish", Bounds = new Rectangle( 12, 84, 140, 26) };
        _btnPublish.Click += (_, _) => DoPublish();

        _btnTrack = new Button { Text = "Track",   Bounds = new Rectangle(170, 84, 140, 26) };
        _btnTrack.Click += (_, _) => DoTrack();

        _btnUpdate = new Button { Text = "Update",  Bounds = new Rectangle(328, 84, 140, 26) };
        _btnUpdate.Click += (_, _) => DoUpdate();

        _btnFinish = new Button { Text = "Finish",  Bounds = new Rectangle(486, 84, 140, 26) };
        _btnFinish.Click += (_, _) => DoFinish();

        // ── Checkboxes stacked below the Finish button ─────────────────────────
        _chkKeep = new CheckBox
        {
            Text    = "Keep branch after finish",
            Bounds  = new Rectangle(444, 114, 182, 20),
            Checked = true  // default: keep branch; overridden by saved settings on Load
        };
        _chkKeep.CheckedChanged += (_, _) => SaveSettings(_chkKeep.Checked, _chkNoFetch.Checked);

        _chkNoFetch = new CheckBox
        {
            Text   = "No fetch (--no-fetch)",
            Bounds = new Rectangle(444, 136, 182, 20)
        };
        _chkNoFetch.CheckedChanged += (_, _) => SaveSettings(_chkKeep.Checked, _chkNoFetch.Checked);

        // ── Hint label ─────────────────────────────────────────────────────────
        var lblHint = new Label
        {
            Text      = "Track: cria branch local da remota   •   Update: traz mudanças da branch pai",
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = SystemColors.GrayText,
            Bounds    = new Rectangle(12, 162, 614, 18)
        };

        _grpManage.Controls.AddRange(
        [
            lblType, _cboManageType, lblBranch, _lblManagePrefix, _cboManageBranch,
            _btnPublish, _btnTrack, _btnUpdate, _btnFinish, _chkKeep, _chkNoFetch, lblHint
        ]);
        Controls.Add(_grpManage);
    }

    private void BuildResultGroup()
    {
        // Height reduced by 48 px to leave room for the Fechar button below.
        _grpResult = new GroupBox
        {
            Text   = "Result of git flow command run",
            Bounds = new Rectangle(8, 364, 638, 362),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _txtResult = new TextBox
        {
            Multiline  = true,
            ReadOnly   = true,
            ScrollBars = ScrollBars.Both,
            WordWrap   = false,
            BackColor  = SystemColors.Window,
            Bounds     = new Rectangle(10, 22, 618, 310),
            Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font       = new Font("Consolas", 9f)
        };
        _grpResult.Controls.Add(_txtResult);
        Controls.Add(_grpResult);
    }

    private void BuildCloseButton()
    {
        _btnClose = new Button
        {
            Text         = "Fechar",
            Width        = 90,
            Height       = 28,
            Bounds       = new Rectangle(286, 736, 90, 28),
            Anchor       = AnchorStyles.Bottom,
            DialogResult = DialogResult.Cancel
        };
        _btnClose.Click += (_, _) => Close();
        Controls.Add(_btnClose);
    }

    // ── Tab order ───────────────────────────────────────────────────────────

    private void SetTabOrder()
    {
        // Form-level: top→bottom visually, right→left within rows
        _lnkAbout  .TabIndex = 0;
        _grpStart  .TabIndex = 1;
        _grpManage .TabIndex = 2;
        _grpResult .TabIndex = 3;
        _btnClose  .TabIndex = 4;

        // grpStart — top→bottom, right→left
        _cboStartType.TabIndex = 0;
        _btnStart    .TabIndex = 1;   // row 2, rightmost
        _txtStartName.TabIndex = 2;
        _cboBasedOn  .TabIndex = 3;   // row 3, right
        _chkBasedOn  .TabIndex = 4;

        // grpManage — top→bottom, right→left
        _cboManageType  .TabIndex = 0;
        _cboManageBranch.TabIndex = 1;
        _btnFinish      .TabIndex = 2;  // row 3, rightmost
        _btnUpdate      .TabIndex = 3;
        _btnTrack       .TabIndex = 4;
        _btnPublish     .TabIndex = 5;
        _chkKeep        .TabIndex = 6;
        _chkNoFetch     .TabIndex = 7;

        // grpResult
        _txtResult.TabIndex = 0;
    }

    // ── Settings persistence (checkboxes) ───────────────────────────────────

    private void ApplySettings()
    {
        var (keepBranch, noFetch) = LoadSettings();
        _chkKeep   .Checked = keepBranch;
        _chkNoFetch.Checked = noFetch;
    }

    private static (bool keepBranch, bool noFetch) LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath)) return (true, false);
            string json = File.ReadAllText(SettingsFilePath);
            bool keep    = json.Contains("\"keepBranchAfterFinish\":true");
            bool noFetch = json.Contains("\"noFetch\":true");
            return (keep, noFetch);
        }
        catch { return (true, false); }
    }

    private static void SaveSettings(bool keepBranch, bool noFetch)
    {
        try
        {
            string dir = Path.GetDirectoryName(SettingsFilePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsFilePath,
                $"{{\"keepBranchAfterFinish\":{(keepBranch ? "true" : "false")}," +
                $"\"noFetch\":{(noFetch ? "true" : "false")}}}");
        }
        catch { }
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
        bool ok = RunFlow($"flow {type} finish {flags}\"{name}\"");

        // After a successful "release" finish: push master, push develop,
        // and (if both succeed) checkout develop.
        if (!ok) return;
        if (!string.Equals(type, "release", StringComparison.OrdinalIgnoreCase)) return;

        string master  = _svc.GetGitFlowBranchName("master");
        string develop = _svc.GetGitFlowBranchName("develop");
        string remote  = _svc.GetDefaultRemote();
        if (remote.Length == 0)
        {
            MessageBox.Show(
                "Release finalizada localmente, mas nenhum remoto configurado para push.",
                "GitFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!RunFlow($"push {remote} {master}",  append: true)) return;
        if (!RunFlow($"push {remote} {develop}", append: true)) return;
        RunFlow($"checkout {develop}", append: true);
    }

    /// <summary>
    /// Runs <c>git {args}</c>, shows the result in the textbox and returns true on exit code 0.
    /// When <paramref name="append"/> is true, the new output block is appended to the existing
    /// result text (so multi-step flows like release-finish + push + checkout stay visible).
    /// </summary>
    private bool RunFlow(string args, bool append = false)
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
            string block = $"command - git {args}\r\n\r\n{body}";
            _txtResult.Text = append && _txtResult.Text.Length > 0
                ? _txtResult.Text + "\r\n\r\n" + block
                : block;
        }
        finally
        {
            Cursor = Cursors.Default;
        }

        _lblHead.Text = "HEAD:  " + _svc.GetHeadRef();
        ReloadManageBranches();

        if (code != 0)
            ShowFlowError(output);

        return code == 0;
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
            "Botões:\n" +
            "  Publish — envia a branch para o remoto (git flow <tipo> publish).\n" +
            "  Track   — cria branch local rastreando a branch remota de mesmo nome.\n" +
            "  Update  — traz commits da branch pai (ex.: develop) para a branch atual.\n" +
            "  Finish  — mescla de volta e exclui a branch (git flow <tipo> finish).\n\n" +
            "Checkboxes do Finish:\n" +
            "  Keep branch after finish — mantém a branch após a mesclagem (flag -k).\n" +
            "  No fetch (--no-fetch)   — não busca no remoto antes de finalizar.\n\n" +
            "Requer git-flow-next instalado.",
            "About GitFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>Removes double quotes to keep command arguments safe.</summary>
    private static string Clean(string s) => s.Trim().Replace("\"", "");
}
