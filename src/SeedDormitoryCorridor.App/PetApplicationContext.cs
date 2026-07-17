using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using SeedDormitoryCorridor.App.Logging;
using SeedDormitoryCorridor.App.Settings;
using SeedDormitoryCorridor.App.SingleInstance;
using SeedDormitoryCorridor.Assets;
using SeedDormitoryCorridor.Configuration;
using SeedDormitoryCorridor.Platform.Windows;
using SeedDormitoryCorridor.Rendering;
using SeedDormitoryCorridor.Runtime;

namespace SeedDormitoryCorridor.App;

public sealed class PetApplicationContext : ApplicationContext
{
    private const string DefaultPetId = "builtin-su-xiao";
    private const string RecoveryPetId = "builtin-seed";
    private readonly AppPaths paths;
    private readonly AppLogger logger;
    private readonly SingleInstanceCoordinator instance;
    private readonly SettingsStore settingsStore;
    private readonly PetPackageLoader packageLoader = new();
    private readonly PetInstaller installer;
    private readonly RuntimeClock clock = new();
    private readonly System.Windows.Forms.Timer animationTimer = new();
    private readonly Control dispatcher = new();
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem visibilityMenu = new();
    private readonly ToolStripMenuItem pauseMenu = new();
    private readonly ToolStripMenuItem petMenu = new("切换宠物");
    private readonly ToolStripMenuItem animationMenu = new("播放动画");
    private readonly ToolStripMenuItem clickThroughMenu = new("鼠标穿透");
    private readonly ToolStripMenuItem topMostMenu = new("总在最前");
    private readonly ToolStripMenuItem startupMenu = new("开机启动");
    private readonly IdleScheduler idleScheduler = new(
    [
        new IdleCandidate("waving", 3, 120_000),
        new IdleCandidate("jumping", 2, 120_000),
        new IdleCandidate("waiting", 3, 180_000),
        new IdleCandidate("review", 2, 180_000),
    ]);
    private AppSettings appSettings;
    private PetPackage? currentPackage;
    private LayeredWindowRenderer? renderer;
    private LayeredPetWindow? petWindow;
    private AnimationPlayer? player;
    private SettingsForm? settingsForm;
    private bool exiting;

    public PetApplicationContext(AppPaths paths, AppLogger logger, SingleInstanceCoordinator instance)
    {
        this.paths = paths;
        this.logger = logger;
        this.instance = instance;
        settingsStore = new SettingsStore(paths.SettingsFile);
        appSettings = settingsStore.Load(out string? recoveryMessage);
        if (recoveryMessage is not null)
        {
            logger.Error(recoveryMessage);
        }

        installer = new PetInstaller(paths.PetsDirectory, paths.StagingDirectory, packageLoader);
        dispatcher.CreateControl();
        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "白荆科技宿舍走廊",
            Visible = true,
        };
        trayIcon.ContextMenuStrip = BuildTrayMenu();
        trayIcon.DoubleClick += (_, _) => ShowPet();
        animationTimer.Tick += (_, _) => OnAnimationTick();
        SystemEvents.SessionEnding += OnSessionEnding;
        instance.StartServer(command => dispatcher.BeginInvoke(() => HandleCommand(command)));

        idleScheduler.Frequency = ParseIdleFrequency(appSettings.IdleFrequency);
        LoadInitialPet();
        RefreshMenus();
        if (appSettings.ShowOnStartup && appSettings.PetVisible)
        {
            ShowPet();
        }
        else
        {
            HidePet();
        }

        if (appSettings.AnimationPaused)
        {
            PauseAnimation();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.SessionEnding -= OnSessionEnding;
            animationTimer.Stop();
            animationTimer.Dispose();
            settingsForm?.Dispose();
            petWindow?.Dispose();
            currentPackage?.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            dispatcher.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        visibilityMenu.Click += (_, _) =>
        {
            if (petWindow?.Visible == true)
            {
                HidePet();
            }
            else
            {
                ShowPet();
            }
        };
        pauseMenu.Click += (_, _) =>
        {
            if (player?.IsPaused == true)
            {
                ResumeAnimation();
            }
            else
            {
                PauseAnimation();
            }
        };
        clickThroughMenu.Click += (_, _) => SetClickThrough(!appSettings.ClickThrough);
        topMostMenu.Click += (_, _) => SetTopMost(!appSettings.TopMost);
        startupMenu.Click += (_, _) => SetStartup(!appSettings.StartWithWindows);
        menu.Items.AddRange(
        [
            visibilityMenu,
            pauseMenu,
            new ToolStripSeparator(),
            petMenu,
            animationMenu,
            clickThroughMenu,
            topMostMenu,
            new ToolStripSeparator(),
            new ToolStripMenuItem("设置", null, (_, _) => ShowSettings()),
            new ToolStripMenuItem("导入宠物", null, (_, _) => ImportFromDialog()),
            new ToolStripMenuItem("打开宠物目录", null, (_, _) => OpenDirectory(paths.PetsDirectory)),
            startupMenu,
            new ToolStripSeparator(),
            new ToolStripMenuItem("退出", null, (_, _) => ExitApplication()),
        ]);
        return menu;
    }

    private void LoadInitialPet()
    {
        string preferred = string.IsNullOrWhiteSpace(appSettings.CurrentPetId) ||
            string.Equals(appSettings.CurrentPetId, RecoveryPetId, StringComparison.OrdinalIgnoreCase)
                ? DefaultPetId
                : appSettings.CurrentPetId;
        bool loaded = TrySwitchPet(preferred, showError: false);
        if (!loaded && !string.Equals(preferred, DefaultPetId, StringComparison.OrdinalIgnoreCase))
        {
            loaded = TrySwitchPet(DefaultPetId, showError: false);
        }

        if (!loaded)
        {
            loaded = TrySwitchPet(RecoveryPetId, showError: false);
        }

        if (!loaded)
        {
            throw new InvalidOperationException("内置安全宠物无法加载，请重新安装应用。 ");
        }
    }

    private bool TrySwitchPet(string petId, bool showError = true)
    {
        string? root = ResolvePetPath(petId);
        if (root is null)
        {
            if (showError)
            {
                MessageBox.Show($"找不到宠物 '{petId}'。", "切换失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return false;
        }

        PetPackage? newPackage = null;
        LayeredWindowRenderer? newRenderer = null;
        LayeredPetWindow? newWindow = null;
        try
        {
            newPackage = packageLoader.Load(root);
            newRenderer = new LayeredWindowRenderer
            {
                ScalingMode = ResolveRenderMode(newPackage.Manifest),
            };
            newRenderer.LoadSpriteSheet(newPackage.SpriteSheet, newPackage.Atlas);
            newRenderer.Resize(ResolveScale(newPackage.Manifest), DpiScale.Default);
            newWindow = new LayeredPetWindow(newRenderer)
            {
                AlphaThreshold = ResolveAlphaThreshold(newPackage.Manifest),
                FullClickThrough = appSettings.ClickThrough,
            };
            LayeredWindowRenderer preparedRenderer = newRenderer;
            newRenderer = null;
            newWindow.SetTopMost(appSettings.TopMost);

            Point desired = petWindow?.Location ?? (appSettings.X == int.MinValue
                ? MonitorHelper.DefaultPosition(newWindow.Size)
                : new Point(appSettings.X, appSettings.Y));
            newWindow.Location = MonitorHelper.EnsurePartiallyVisible(desired, newWindow.Size, appSettings.MonitorDeviceName);
            WireWindowEvents(newWindow);

            long now = clock.ElapsedMilliseconds;
            var newPlayer = new AnimationPlayer(newPackage.Atlas.Animations, "idle", now);
            string onShow = newPackage.Manifest.DesktopPet?.Behavior?.OnShow ?? "waving";
            if (newPackage.Atlas.Animations.TryGet(onShow, out _))
            {
                newPlayer.Play(onShow, now, force: true);
            }

            AnimationFrameState state = newPlayer.State;
            newWindow.ApplyFrame(new SpriteRenderFrame(state.Column, state.Row));

            LayeredPetWindow? oldWindow = petWindow;
            PetPackage? oldPackage = currentPackage;
            bool shouldShow = oldWindow?.Visible ?? appSettings.PetVisible;
            petWindow = newWindow;
            currentPackage = newPackage;
            renderer = preparedRenderer;
            player = newPlayer;
            newWindow = null;
            newPackage = null;
            appSettings.CurrentPetId = petId;
            appSettings.Scale = ResolveScale(currentPackage.Manifest);
            SaveSettings();

            if (shouldShow)
            {
                petWindow.Show();
                ScheduleNextFrame();
            }

            oldWindow?.Hide();
            oldWindow?.Dispose();
            oldPackage?.Dispose();
            idleScheduler.Reset(now);
            logger.Info($"Loaded pet id={petId}.");
            RefreshMenus();
            settingsForm?.LoadValues(appSettings, GetPetItems());
            return true;
        }
        catch (Exception exception) when (exception is PetValidationException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.Error($"Failed to load pet id={petId}.", exception);
            newWindow?.Dispose();
            newRenderer?.Dispose();
            newPackage?.Dispose();
            if (showError)
            {
                MessageBox.Show($"无法加载宠物，当前宠物将继续运行：\n{exception.Message}", "切换失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return false;
        }
    }

    private void WireWindowEvents(LayeredPetWindow window)
    {
        window.PetContextMenuRequested += (_, args) => trayIcon.ContextMenuStrip?.Show(args.ScreenLocation);
        window.PetSingleClick += (_, _) => PlayInteraction(currentPackage?.Manifest.DesktopPet?.Behavior?.OnSingleClick ?? "jumping", 10);
        window.PetDoubleClick += (_, _) => PlayInteraction(currentPackage?.Manifest.DesktopPet?.Behavior?.OnDoubleClick ?? "waving", 10);
        window.PetDragDirectionChanged += (_, args) =>
            PlayInteraction(args.Left
                ? currentPackage?.Manifest.DesktopPet?.Behavior?.OnDragLeft ?? "running-left"
                : currentPackage?.Manifest.DesktopPet?.Behavior?.OnDragRight ?? "running-right", 20, restart: false);
        window.PetDragEnded += (_, _) =>
        {
            player?.RestoreDefault(clock.ElapsedMilliseconds);
            idleScheduler.Reset(clock.ElapsedMilliseconds);
            RenderCurrentFrame();
            ScheduleNextFrame();
            SavePosition();
        };
        window.LocationChanged += (_, _) => SavePosition();
        window.DpiScaleChanged += (_, _) => ApplyRendererSettings();
    }

    private void PlayInteraction(string animationName, int priority, bool restart = true)
    {
        if (player is null || currentPackage is null || !currentPackage.Atlas.Animations.TryGet(animationName, out _))
        {
            return;
        }

        long now = clock.ElapsedMilliseconds;
        if (player.Play(animationName, now, priority, restart: restart))
        {
            idleScheduler.Reset(now);
            RenderCurrentFrame();
            ScheduleNextFrame();
        }
    }

    private void OnAnimationTick()
    {
        animationTimer.Stop();
        if (player is null || petWindow?.Visible != true || player.IsPaused)
        {
            return;
        }

        long now = clock.ElapsedMilliseconds;
        if (player.Update(now))
        {
            RenderCurrentFrame();
        }

        string? specialIdle = idleScheduler.TrySchedule(now, interactionBlocked: player.ActivePriority >= 10);
        if (specialIdle is not null)
        {
            player.Play(specialIdle, now, force: true);
            RenderCurrentFrame();
        }

        ScheduleNextFrame();
    }

    private void RenderCurrentFrame()
    {
        if (player is null || petWindow is null)
        {
            return;
        }

        AnimationFrameState state = player.State;
        petWindow.ApplyFrame(new SpriteRenderFrame(state.Column, state.Row));
    }

    private void ScheduleNextFrame()
    {
        if (player is null || petWindow?.Visible != true || player.IsPaused)
        {
            animationTimer.Stop();
            return;
        }

        animationTimer.Interval = Math.Clamp(player.GetRemainingFrameTimeMs(clock.ElapsedMilliseconds), 1, 60_000);
        animationTimer.Start();
    }

    private void ShowPet()
    {
        if (petWindow is null)
        {
            return;
        }

        petWindow.Location = MonitorHelper.EnsurePartiallyVisible(petWindow.Location, petWindow.Size, appSettings.MonitorDeviceName);
        petWindow.Show();
        appSettings.PetVisible = true;
        if (!player!.IsPaused)
        {
            PlayInteraction(currentPackage?.Manifest.DesktopPet?.Behavior?.OnShow ?? "waving", 10);
        }

        SaveSettings();
        RefreshMenus();
    }

    private void HidePet()
    {
        animationTimer.Stop();
        petWindow?.Hide();
        appSettings.PetVisible = false;
        SaveSettings();
        RefreshMenus();
    }

    private void PauseAnimation()
    {
        player?.Pause(clock.ElapsedMilliseconds);
        animationTimer.Stop();
        appSettings.AnimationPaused = true;
        SaveSettings();
        RefreshMenus();
    }

    private void ResumeAnimation()
    {
        player?.Resume(clock.ElapsedMilliseconds);
        appSettings.AnimationPaused = false;
        ScheduleNextFrame();
        SaveSettings();
        RefreshMenus();
    }

    private void SetClickThrough(bool value)
    {
        appSettings.ClickThrough = value;
        if (petWindow is not null)
        {
            petWindow.FullClickThrough = value;
        }

        SaveSettings();
        RefreshMenus();
    }

    private void SetTopMost(bool value)
    {
        appSettings.TopMost = value;
        petWindow?.SetTopMost(value);
        SaveSettings();
        RefreshMenus();
    }

    private void SetStartup(bool value)
    {
        try
        {
            StartupRegistration.SetEnabled(value, Application.ExecutablePath);
            appSettings.StartWithWindows = value;
            SaveSettings();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            logger.Error("Failed to change startup registration.", exception);
            MessageBox.Show(exception.Message, "开机启动设置失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        RefreshMenus();
    }

    private void ShowSettings()
    {
        if (settingsForm is null || settingsForm.IsDisposed)
        {
            settingsForm = new SettingsForm();
            settingsForm.FormClosed += (_, _) => settingsForm = null;
            settingsForm.PetChanged += (_, id) => TrySwitchPet(id);
            settingsForm.SettingsChanged += (_, _) => ApplySettingsFromForm();
            settingsForm.ImportRequested += (_, _) => ImportFromDialog();
            settingsForm.DeleteRequested += (_, _) => DeleteCurrentPet();
            settingsForm.OpenPetsRequested += (_, _) => OpenDirectory(paths.PetsDirectory);
            settingsForm.OpenLogsRequested += (_, _) => OpenDirectory(paths.LogsDirectory);
            settingsForm.ResetRequested += (_, _) => ResetSettings();
            settingsForm.ExitRequested += (_, _) => ExitApplication();
        }

        settingsForm.LoadValues(appSettings, GetPetItems());
        settingsForm.Show();
        settingsForm.Activate();
    }

    private void ApplySettingsFromForm()
    {
        settingsForm?.ApplyTo(appSettings);
        idleScheduler.Frequency = ParseIdleFrequency(appSettings.IdleFrequency);
        ApplyRendererSettings();
        SetTopMost(appSettings.TopMost);
        SetClickThrough(appSettings.ClickThrough);
        SetStartup(appSettings.StartWithWindows);
    }

    private void ApplyRendererSettings()
    {
        if (renderer is null || petWindow is null)
        {
            return;
        }

        renderer.ScalingMode = appSettings.RenderMode == RenderMode.Pixelated
            ? SpriteScalingMode.Pixelated
            : SpriteScalingMode.Smooth;
        renderer.Resize(appSettings.Scale, DpiHelper.GetScale(petWindow));
        petWindow.AlphaThreshold = appSettings.AlphaThreshold;
        RenderCurrentFrame();
        petWindow.Location = MonitorHelper.EnsurePartiallyVisible(petWindow.Location, petWindow.Size);
        SaveSettings();
    }

    private void ImportFromDialog()
    {
        DialogResult type = MessageBox.Show("选择“是”导入 ZIP，选择“否”导入目录。", "导入宠物",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        string? source = null;
        if (type == DialogResult.Yes)
        {
            using var dialog = new OpenFileDialog { Filter = "宠物 ZIP (*.zip)|*.zip", CheckFileExists = true };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                source = dialog.FileName;
            }
        }
        else if (type == DialogResult.No)
        {
            using var dialog = new FolderBrowserDialog { Description = "选择包含 pet.json 的宠物目录" };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                source = dialog.SelectedPath;
            }
        }

        if (source is not null)
        {
            ImportPet(source);
        }
    }

    private void ImportPet(string source)
    {
        try
        {
            PetInstallResult result;
            try
            {
                result = installer.Install(source, ExistingPetPolicy.Cancel);
            }
            catch (IOException exception) when (exception.Message.Contains("已存在", StringComparison.Ordinal))
            {
                if (MessageBox.Show("同 ID 宠物已存在，是否安全替换？", "替换宠物",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                {
                    return;
                }

                result = installer.Install(source, ExistingPetPolicy.Replace);
            }

            logger.Info($"Imported pet id={result.PetId}.");
            TrySwitchPet(result.PetId);
            RefreshMenus();
            settingsForm?.LoadValues(appSettings, GetPetItems());
        }
        catch (PetValidationException exception)
        {
            logger.Error("Pet import validation failed.", exception);
            string issues = string.Join(Environment.NewLine, exception.Validation.Issues.Select(issue => $"• [{issue.Code}] {issue.Message}"));
            MessageBox.Show(issues, "宠物包校验失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger.Error("Pet import failed.", exception);
            MessageBox.Show(exception.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteCurrentPet()
    {
        string id = appSettings.CurrentPetId ?? DefaultPetId;
        if (IsBuiltInPet(id))
        {
            MessageBox.Show("内置安全宠物不能删除。", "删除宠物", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show($"确定删除当前宠物 '{currentPackage?.Manifest.DisplayName}'？将先切换到内置宠物。", "删除宠物",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
        {
            return;
        }

        if (TrySwitchPet(DefaultPetId))
        {
            installer.Delete(id);
            RefreshMenus();
            settingsForm?.LoadValues(appSettings, GetPetItems());
        }
    }

    private void ResetSettings()
    {
        string? current = appSettings.CurrentPetId;
        appSettings = new AppSettings { CurrentPetId = current };
        idleScheduler.Frequency = IdleFrequency.Normal;
        ApplyRendererSettings();
        SetTopMost(true);
        SetClickThrough(false);
        SetStartup(false);
        settingsForm?.LoadValues(appSettings, GetPetItems());
    }

    private void RefreshMenus()
    {
        visibilityMenu.Text = petWindow?.Visible == true ? "隐藏宠物" : "显示宠物";
        pauseMenu.Text = player?.IsPaused == true ? "恢复动画" : "暂停动画";
        clickThroughMenu.Checked = appSettings.ClickThrough;
        topMostMenu.Checked = appSettings.TopMost;
        startupMenu.Checked = appSettings.StartWithWindows;

        petMenu.DropDownItems.Clear();
        foreach (PetListItem pet in GetPetItems())
        {
            var item = new ToolStripMenuItem(pet.DisplayName)
            {
                Checked = string.Equals(pet.Id, appSettings.CurrentPetId, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += (_, _) => TrySwitchPet(pet.Id);
            petMenu.DropDownItems.Add(item);
        }

        animationMenu.DropDownItems.Clear();
        IEnumerable<AnimationDefinition> animations = currentPackage is null
            ? Enumerable.Empty<AnimationDefinition>()
            : currentPackage.Atlas.Animations.All.OrderBy(item => item.Row);
        foreach (AnimationDefinition animation in animations)
        {
            var item = new ToolStripMenuItem(animation.Name);
            item.Click += (_, _) => PlayInteraction(animation.Name, animation.Priority, restart: true);
            animationMenu.DropDownItems.Add(item);
        }
    }

    private List<PetListItem> GetPetItems()
    {
        var items = new List<PetListItem> { new(DefaultPetId, "苏筱（内置）") };
        foreach ((string id, string path) in installer.ListInstalled())
        {
            string displayName = id;
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "pet.json")));
                if (document.RootElement.TryGetProperty("displayName", out JsonElement name) && name.ValueKind == JsonValueKind.String)
                {
                    displayName = name.GetString() ?? id;
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                logger.Error($"Failed to read installed pet metadata id={id}.", exception);
            }

            items.Add(new PetListItem(id, displayName));
        }

        return items;
    }

    private string? ResolvePetPath(string id)
    {
        string? builtInDirectory = id.ToLowerInvariant() switch
        {
            DefaultPetId => "builtin-su-xiao",
            RecoveryPetId => "builtin-seed",
            _ => null,
        };
        if (builtInDirectory is not null)
        {
            string root = Path.Combine(AppContext.BaseDirectory, "assets", builtInDirectory);
            return Directory.Exists(root) ? root : null;
        }

        return installer.ListInstalled().FirstOrDefault(pet => string.Equals(pet.Id, id, StringComparison.OrdinalIgnoreCase)).Path;
    }

    private static bool IsBuiltInPet(string id) =>
        string.Equals(id, DefaultPetId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, RecoveryPetId, StringComparison.OrdinalIgnoreCase);

    private float ResolveScale(PetManifest manifest) => appSettings.PetOverrides.TryGetValue(manifest.Id!, out PetOverrides? overrides) && overrides.Scale.HasValue
        ? overrides.Scale.Value
        : appSettings.Scale == 1f ? manifest.DesktopPet?.DefaultScale ?? 1f : appSettings.Scale;

    private SpriteScalingMode ResolveRenderMode(PetManifest manifest)
    {
        if (appSettings.PetOverrides.TryGetValue(manifest.Id!, out PetOverrides? overrides) && overrides.RenderMode.HasValue)
        {
            return overrides.RenderMode.Value == RenderMode.Pixelated ? SpriteScalingMode.Pixelated : SpriteScalingMode.Smooth;
        }

        return string.Equals(manifest.DesktopPet?.RenderMode, "pixelated", StringComparison.OrdinalIgnoreCase) || appSettings.RenderMode == RenderMode.Pixelated
            ? SpriteScalingMode.Pixelated
            : SpriteScalingMode.Smooth;
    }

    private byte ResolveAlphaThreshold(PetManifest manifest) => appSettings.PetOverrides.TryGetValue(manifest.Id!, out PetOverrides? overrides) && overrides.AlphaThreshold.HasValue
        ? overrides.AlphaThreshold.Value
        : appSettings.AlphaThreshold == 16 ? (byte)(manifest.DesktopPet?.AlphaThreshold ?? 16) : appSettings.AlphaThreshold;

    private static IdleFrequency ParseIdleFrequency(string value) => value.ToLowerInvariant() switch
    {
        "off" => IdleFrequency.Off,
        "low" => IdleFrequency.Low,
        "high" => IdleFrequency.High,
        _ => IdleFrequency.Normal,
    };

    private void SavePosition()
    {
        if (petWindow is null)
        {
            return;
        }

        appSettings.X = petWindow.Left;
        appSettings.Y = petWindow.Top;
        appSettings.MonitorDeviceName = Screen.FromControl(petWindow).DeviceName;
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            settingsStore.Save(appSettings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Error("Failed to save settings.", exception);
        }
    }

    private void HandleCommand(string command)
    {
        if (command == "show")
        {
            ShowPet();
        }
        else if (command == "hide")
        {
            HidePet();
        }
        else if (command == "settings")
        {
            ShowSettings();
        }
        else if (command.StartsWith("import ", StringComparison.Ordinal))
        {
            ImportPet(command[7..]);
        }
    }

    private static void OpenDirectory(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        logger.Info($"Windows session ending reason={e.Reason}.");
        SavePosition();
        SaveSettings();
    }

    private void ExitApplication()
    {
        if (exiting)
        {
            return;
        }

        exiting = true;
        SavePosition();
        SaveSettings();
        trayIcon.Visible = false;
        ExitThread();
    }
}
