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
    private readonly TextBox onlineCatalogUrl = new() { Dock = DockStyle.Fill, PlaceholderText = "https://..." };
    private readonly Button onlineRefreshButton = new() { Text = "刷新", AutoSize = true };
    private readonly Label onlineStatus = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly FlowLayoutPanel onlinePets = new()
    {
        AutoScroll = true,
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        Padding = new Padding(0, 8, 0, 0),
        WrapContents = false,
    };
    private readonly Dictionary<string, OnlinePetCard> onlineCards = new(StringComparer.OrdinalIgnoreCase);
    private bool loading;

    public SettingsForm()
    {
        Text = "白荆科技宿舍走廊 - 设置";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 560);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(780, 620);

        renderPicker.Items.AddRange(["平滑", "像素化"]);
        idlePicker.Items.AddRange(["关闭", "低频", "正常", "高频"]);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
        var generalPage = new TabPage("常规") { Padding = new Padding(12) };
        var onlinePage = new TabPage("在线宠物") { Padding = new Padding(12) };
        tabs.TabPages.Add(generalPage);
        tabs.TabPages.Add(onlinePage);
        generalPage.Controls.Add(BuildGeneralPage());
        onlinePage.Controls.Add(BuildOnlinePage());
        Controls.Add(tabs);

        onlinePets.ClientSizeChanged += (_, _) => ResizeOnlineCards();
        onlineRefreshButton.Click += (_, _) => OnlineRefreshRequested?.Invoke(this, onlineCatalogUrl.Text.Trim());
        onlineCatalogUrl.Leave += (_, _) => RaiseSettingsChanged();

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
    public event EventHandler<string>? OnlineRefreshRequested;
    public event EventHandler<string>? OnlineInstallRequested;
    public event EventHandler<string>? OnlineDeleteRequested;

    private TableLayoutPanel BuildGeneralPage()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 13,
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
        return table;
    }

    private TableLayoutPanel BuildOnlinePage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var address = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
        address.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        address.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        address.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        address.Controls.Add(new Label { Text = "目录地址", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 8) }, 0, 0);
        address.Controls.Add(onlineCatalogUrl, 1, 0);
        address.Controls.Add(onlineRefreshButton, 2, 0);
        layout.Controls.Add(address, 0, 0);
        layout.Controls.Add(onlineStatus, 0, 1);
        layout.Controls.Add(onlinePets, 0, 2);
        return layout;
    }

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
            onlineCatalogUrl.Text = settings.OnlineCatalogUrl ?? string.Empty;
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
        settings.OnlineCatalogUrl = string.IsNullOrWhiteSpace(onlineCatalogUrl.Text) ? null : onlineCatalogUrl.Text.Trim();
    }

    public void SetOnlineLoading(string message = "正在刷新在线目录...")
    {
        onlineRefreshButton.Enabled = false;
        onlineStatus.ForeColor = SystemColors.GrayText;
        onlineStatus.Text = message;
    }

    public void SetOnlineError(string message)
    {
        onlineRefreshButton.Enabled = true;
        onlineStatus.ForeColor = Color.FromArgb(175, 45, 45);
        onlineStatus.Text = message;
    }

    public void SetOnlineMessage(string message)
    {
        onlineRefreshButton.Enabled = true;
        onlineStatus.ForeColor = SystemColors.GrayText;
        onlineStatus.Text = message;
    }

    public void SetOnlinePets(IEnumerable<OnlinePetViewModel> pets)
    {
        foreach (OnlinePetCard card in onlineCards.Values)
        {
            card.Dispose();
        }

        onlineCards.Clear();
        onlinePets.Controls.Clear();
        foreach (OnlinePetViewModel pet in pets)
        {
            var card = new OnlinePetCard(pet);
            card.InstallRequested += (_, id) => OnlineInstallRequested?.Invoke(this, id);
            card.DeleteRequested += (_, id) => OnlineDeleteRequested?.Invoke(this, id);
            onlineCards.Add(pet.Item.Id, card);
            onlinePets.Controls.Add(card);
        }

        onlineRefreshButton.Enabled = true;
        onlineStatus.ForeColor = SystemColors.GrayText;
        onlineStatus.Text = onlineCards.Count == 0 ? "目录中暂无宠物。" : $"已加载 {onlineCards.Count} 个在线宠物。";
        ResizeOnlineCards();
    }

    public void SetOnlinePetState(OnlinePetViewModel pet)
    {
        if (onlineCards.TryGetValue(pet.Item.Id, out OnlinePetCard? card))
        {
            card.ApplyViewModel(pet);
        }
    }

    public void SetOnlinePreview(string petId, Image image)
    {
        if (onlineCards.TryGetValue(petId, out OnlinePetCard? card))
        {
            card.SetPreview(image);
        }
        else
        {
            image.Dispose();
        }
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

    private void ResizeOnlineCards()
    {
        int width = Math.Max(420, onlinePets.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        foreach (OnlinePetCard card in onlineCards.Values)
        {
            card.Width = width;
        }
    }
}
