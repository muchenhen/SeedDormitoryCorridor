namespace SeedDormitoryCorridor.App.Settings;

internal sealed class OnlinePetCard : UserControl
{
    private readonly PictureBox preview = new()
    {
        BackColor = Color.FromArgb(245, 245, 245),
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.Zoom,
    };
    private readonly Label name = new() { AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold) };
    private readonly Label status = new() { AutoSize = true };
    private readonly Label description = new() { AutoEllipsis = true, Dock = DockStyle.Fill };
    private readonly Label metadata = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly Button installButton = new() { AutoSize = true, MinimumSize = new Size(84, 30) };
    private readonly Button deleteButton = new() { AutoSize = true, MinimumSize = new Size(84, 30), Text = "删除" };
    private OnlinePetViewModel viewModel;

    public OnlinePetCard(OnlinePetViewModel viewModel)
    {
        this.viewModel = viewModel;
        Height = 146;
        Margin = new Padding(0, 0, 0, 8);
        BorderStyle = BorderStyle.FixedSingle;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 3,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.Controls.Add(preview, 0, 0);

        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10, 0, 10, 0) };
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.Controls.Add(name, 0, 0);
        details.Controls.Add(description, 0, 1);
        details.Controls.Add(metadata, 0, 2);
        details.Controls.Add(status, 0, 3);
        layout.Controls.Add(details, 1, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(5, 0, 0, 0),
        };
        actions.Controls.Add(installButton);
        actions.Controls.Add(deleteButton);
        layout.Controls.Add(actions, 2, 0);
        Controls.Add(layout);

        installButton.Click += (_, _) => InstallRequested?.Invoke(this, viewModel.Item.Id);
        deleteButton.Click += (_, _) => DeleteRequested?.Invoke(this, viewModel.Item.Id);
        ApplyViewModel(viewModel);
    }

    public event EventHandler<string>? InstallRequested;

    public event EventHandler<string>? DeleteRequested;

    public string PetId => viewModel.Item.Id;

    public void ApplyViewModel(OnlinePetViewModel value)
    {
        viewModel = value;
        name.Text = value.Item.DisplayName;
        description.Text = value.Item.Description;
        string author = string.IsNullOrWhiteSpace(value.Item.Author) ? "未知作者" : value.Item.Author;
        metadata.Text = $"{author}  |  v{value.Item.Version}  |  {FormatSize(value.Item.PackageSize)}";
        status.Text = value.Status switch
        {
            OnlinePetUiStatus.NotInstalled => "未安装",
            OnlinePetUiStatus.Downloading => "正在下载并校验...",
            OnlinePetUiStatus.Installed => "已安装",
            OnlinePetUiStatus.Incompatible => value.ErrorMessage ?? $"不兼容（需要客户端 {value.Item.MinimumClientVersion}）",
            OnlinePetUiStatus.Failed => $"失败：{value.ErrorMessage ?? "未知错误"}",
            _ => value.Status.ToString(),
        };
        status.ForeColor = value.Status switch
        {
            OnlinePetUiStatus.Installed => Color.FromArgb(20, 110, 60),
            OnlinePetUiStatus.Incompatible or OnlinePetUiStatus.Failed => Color.FromArgb(175, 45, 45),
            _ => SystemColors.ControlText,
        };
        installButton.Text = value.Status == OnlinePetUiStatus.Installed ? "重新安装" : "安装";
        installButton.Enabled = value.Status is OnlinePetUiStatus.NotInstalled or OnlinePetUiStatus.Installed or OnlinePetUiStatus.Failed;
        deleteButton.Enabled = value.IsInstalled && value.Status != OnlinePetUiStatus.Downloading;
    }

    public void SetPreview(Image image)
    {
        Image? previous = preview.Image;
        preview.Image = image;
        previous?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            preview.Image?.Dispose();
            name.Font?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string FormatSize(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        return bytes >= megabyte
            ? $"{bytes / megabyte:0.##} MB"
            : $"{bytes / 1024d:0.##} KB";
    }
}
