using SeedDormitoryCorridor.Configuration;

namespace SeedDormitoryCorridor.App.Settings;

public sealed record PetListItem(string Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed class SettingsForm : Form
{
    private readonly ComboBox petPicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly NumericUpDown scalePicker = new() { Minimum = 25, Maximum = 400, Increment = 5, DecimalPlaces = 0, Width = 90 };
    private readonly ComboBox renderPicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly NumericUpDown alphaPicker = new() { Minimum = 0, Maximum = 255, Width = 90 };
    private readonly ComboBox idlePicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly CheckBox topMost = new() { Text = "总在最前", AutoSize = true };
    private readonly CheckBox clickThrough = new() { Text = "鼠标完全穿透", AutoSize = true };
    private readonly CheckBox startup = new() { Text = "开机启动", AutoSize = true };
    private readonly CheckBox showOnStartup = new() { Text = "启动时显示宠物", AutoSize = true };
    private bool loading;

    public SettingsForm()
    {
        Text = "白荆科技宿舍走廊 - 设置";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(540, 510);

        renderPicker.Items.AddRange(["平滑", "像素化"]);
        idlePicker.Items.AddRange(["关闭", "低频", "正常", "高频"]);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 13,
            AutoSize = true,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, "当前宠物", petPicker);
        AddRow(table, "缩放比例 (%)", scalePicker);
        AddRow(table, "渲染模式", renderPicker);
        AddRow(table, "Alpha 命中阈值", alphaPicker);
        AddRow(table, "特殊 Idle", idlePicker);
        AddRow(table, string.Empty, topMost);
        AddRow(table, string.Empty, clickThrough);
        AddRow(table, string.Empty, startup);
        AddRow(table, string.Empty, showOnStartup);

        var assetButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        assetButtons.Controls.Add(MakeButton("导入宠物", (_, _) => ImportRequested?.Invoke(this, EventArgs.Empty)));
        assetButtons.Controls.Add(MakeButton("删除当前宠物", (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty)));
        AddRow(table, "宠物管理", assetButtons);

        var folderButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        folderButtons.Controls.Add(MakeButton("打开宠物目录", (_, _) => OpenPetsRequested?.Invoke(this, EventArgs.Empty)));
        folderButtons.Controls.Add(MakeButton("打开日志目录", (_, _) => OpenLogsRequested?.Invoke(this, EventArgs.Empty)));
        AddRow(table, "目录", folderButtons);

        var actionButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        actionButtons.Controls.Add(MakeButton("恢复默认设置", (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty)));
        actionButtons.Controls.Add(MakeButton("退出应用", (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));
        AddRow(table, string.Empty, actionButtons);
        Controls.Add(table);

        petPicker.SelectedIndexChanged += (_, _) =>
        {
            if (!loading && petPicker.SelectedItem is PetListItem pet)
            {
                PetChanged?.Invoke(this, pet.Id);
            }
        };
        scalePicker.ValueChanged += (_, _) => RaiseSettingsChanged();
        renderPicker.SelectedIndexChanged += (_, _) => RaiseSettingsChanged();
        alphaPicker.ValueChanged += (_, _) => RaiseSettingsChanged();
        idlePicker.SelectedIndexChanged += (_, _) => RaiseSettingsChanged();
        topMost.CheckedChanged += (_, _) => RaiseSettingsChanged();
        clickThrough.CheckedChanged += (_, _) => RaiseSettingsChanged();
        startup.CheckedChanged += (_, _) => RaiseSettingsChanged();
        showOnStartup.CheckedChanged += (_, _) => RaiseSettingsChanged();
    }

    public event EventHandler<string>? PetChanged;
    public event EventHandler? SettingsChanged;
    public event EventHandler? ImportRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? OpenPetsRequested;
    public event EventHandler? OpenLogsRequested;
    public event EventHandler? ResetRequested;
    public event EventHandler? ExitRequested;

    public void LoadValues(AppSettings settings, IEnumerable<PetListItem> pets)
    {
        loading = true;
        try
        {
            petPicker.Items.Clear();
            petPicker.Items.AddRange(pets.Cast<object>().ToArray());
            PetListItem? selected = petPicker.Items.Cast<PetListItem>().FirstOrDefault(pet =>
                string.Equals(pet.Id, settings.CurrentPetId, StringComparison.OrdinalIgnoreCase));
            petPicker.SelectedItem = selected ?? (petPicker.Items.Count > 0 ? petPicker.Items[0] : null);
            scalePicker.Value = Math.Clamp((decimal)settings.Scale * 100, scalePicker.Minimum, scalePicker.Maximum);
            renderPicker.SelectedIndex = settings.RenderMode == RenderMode.Pixelated ? 1 : 0;
            alphaPicker.Value = settings.AlphaThreshold;
            idlePicker.SelectedIndex = settings.IdleFrequency.ToLowerInvariant() switch
            {
                "off" => 0,
                "low" => 1,
                "high" => 3,
                _ => 2,
            };
            topMost.Checked = settings.TopMost;
            clickThrough.Checked = settings.ClickThrough;
            startup.Checked = settings.StartWithWindows;
            showOnStartup.Checked = settings.ShowOnStartup;
        }
        finally
        {
            loading = false;
        }
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.Scale = (float)(scalePicker.Value / 100);
        settings.RenderMode = renderPicker.SelectedIndex == 1 ? RenderMode.Pixelated : RenderMode.Smooth;
        settings.AlphaThreshold = (byte)alphaPicker.Value;
        settings.IdleFrequency = idlePicker.SelectedIndex switch
        {
            0 => "off",
            1 => "low",
            3 => "high",
            _ => "normal",
        };
        settings.TopMost = topMost.Checked;
        settings.ClickThrough = clickThrough.Checked;
        settings.StartWithWindows = startup.Checked;
        settings.ShowOnStartup = showOnStartup.Checked;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        int row = table.Controls.Count / 2;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 9, 3, 9) }, 0, row);
        control.Anchor = AnchorStyles.Left;
        control.Margin = new Padding(3, 5, 3, 5);
        table.Controls.Add(control, 1, row);
    }

    private static Button MakeButton(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += click;
        return button;
    }

    private void RaiseSettingsChanged()
    {
        if (!loading)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
