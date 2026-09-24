using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalAppsHub.Models;
using PersonalAppsHub.Services;
using Forms = System.Windows.Forms;

namespace PersonalAppsHub;

public partial class MainWindow : Window
{
    private const string TipeeeUrl = "https://fr.tipeee.com/flexhub-applications-by-flexron/";
    private const string OpenAiApiKeysUrl = "https://platform.openai.com/api-keys";
    private const string GeminiApiKeysUrl = "https://aistudio.google.com/app/apikey";
    private const string DeepLApiKeysUrl = "https://www.deepl.com/your-account/keys";
    private const string GoogleTranslateApiKeysUrl = "https://console.cloud.google.com/apis/credentials";
    private const string LibreTranslateApiKeysUrl = "https://portal.libretranslate.com";
    private const string GeminiQuotasUrl = "https://aistudio.google.com/usage";
    private const string GoogleTranslateQuotasUrl = "https://console.cloud.google.com/iam-admin/quotas";
    private readonly SettingsService _settingsService = new();
    private readonly CorrectionService _correctionService = new();
    private readonly TranslationService _translationService = new();
    private readonly ResponseGenerationService _responseGenerationService = new();
    private readonly WiktionaryService _wiktionaryService = new();
    private readonly NvidiaProfileService _nvidiaProfileService = new();
    private readonly XmpMonitorService _xmpMonitorService = new();
    private readonly SystemMonitoringService _systemMonitoringService = new();
    private readonly NetworkMonitoringService _networkMonitoringService = new();
    private readonly NetworkHistoryService _networkHistoryService = new();
    private readonly GameSessionService _gameSessionService = new();
    private readonly GameProcessMonitoringService _gameProcessMonitoringService = new();
    private readonly GameSessionHistoryService _gameSessionHistoryService = new();
    private readonly GamePerformanceService _gamePerformanceService = new();
    private readonly TemporaryFileCleanupService _temporaryFileCleanupService = new();
    private readonly DuplicateFileService _duplicateFileService = new();
    private readonly StorageHealthService _storageHealthService = new();
    private readonly StartupAuditService _startupAuditService = new();
    private readonly UpdateService _updateService = new();
    private readonly ApiQuotaService _apiQuotaService = new();
    private readonly HotkeyService _hotkeyService = new(9471);
    private readonly HotkeyService _translatorHotkeyService = new(9472);
    private readonly HotkeyService _responseGeneratorHotkeyService = new(9473);
    private readonly HotkeyService _actionWheelHotkeyService = new(9474);
    private readonly KeyboardLayoutMonitorService _keyboardLayoutMonitorService = new();
    private readonly GameChatKeyboardService _gameChatKeyboardService = new();
    private readonly DispatcherTimer _reminderTimer = new();
    private readonly DispatcherTimer _xmpTimer = new();
    private readonly DispatcherTimer _monitoringTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _ramHistory = new();
    private readonly Queue<double> _gpuHistory = new();
    private readonly Queue<DateTime> _monitoringTimestamps = new();
    private readonly Forms.NotifyIcon _tray;
    private HubSettings _settings;
    private bool _exit;
    private bool _loadingSettings;
    private bool _xmpCheckRunning;
    private bool _initialXmpCheckDone;
    private bool _actionWheelOpen;
    private XmpAlertWindow? _xmpAlertWindow;
    private MonitoringAlertWindow? _monitoringAlertWindow;
    private Action? _balloonClickAction;
    private UpdateCheckResult? _availableUpdate;
    private bool _updateInstallRunning;
    private double _sidebarScrollTarget;
    private bool _sidebarScrollAnimating;
    private bool _monitoringRefreshRunning;
    private bool _networkMonitoringRunning;
    private bool _gameSessionRefreshRunning;
    private bool _automaticTemporaryCleanupRunning;
    private bool _duplicateScanRunning;
    private CancellationTokenSource? _duplicateScanCancellation;
    private IReadOnlyList<DuplicateFileCandidate> _duplicateResults = Array.Empty<DuplicateFileCandidate>();
    private string? _duplicateScanRoot;
    private DateTime _lastAutomaticNetworkMeasureUtc = DateTime.MinValue;
    private DateTime _lastGameSessionRefreshUtc = DateTime.MinValue;
    private int _networkAnomalySamples;
    private DateTime _lastNetworkAlertUtc = DateTime.MinValue;
    private readonly Queue<double> _networkPingHistory = new();
    private readonly Queue<double> _networkJitterHistory = new();
    private readonly Queue<double> _networkLossHistory = new();
    private readonly Queue<DateTime> _networkHistoryTimestamps = new();
    private readonly HashSet<int> _alertedGameSessionProcessIds = new();
    private IReadOnlyList<WeeklyGameSummary> _currentGameSummaries = Array.Empty<WeeklyGameSummary>();
    private bool _textToolsExpanded;
    private bool _monitoringGamesExpanded;
    private bool _maintenanceExpanded;
    private bool _automationExpanded;
    private int _highCpuSamples;
    private int _highRamSamples;
    private int _highGpuTemperatureSamples;
    private DateTime _lastCpuAlertUtc = DateTime.MinValue;
    private DateTime _lastRamAlertUtc = DateTime.MinValue;
    private DateTime _lastGpuTemperatureAlertUtc = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        LoadNetworkHistory();
        ApplyAppearance();
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-logo.png");
        _tray = new Forms.NotifyIcon { Text = "FlexHub", Visible = true };
        try
        {
            var logo = new BitmapImage();
            logo.BeginInit();
            logo.CacheOption = BitmapCacheOption.OnLoad;
            logo.UriSource = new Uri(logoPath, UriKind.Absolute);
            logo.EndInit();
            logo.Freeze();
            HeaderLogo.Source = logo;
            Icon = logo;
            using var bitmap = new Bitmap(logoPath);
            var handle = bitmap.GetHicon();
            _tray.Icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone();
            DestroyIcon(handle);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Logo indisponible : {ex.Message}");
            _tray.Icon = System.Drawing.SystemIcons.Application;
        }
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Ouvrir le Hub", null, (_, _) => Dispatcher.Invoke(ShowHub));
        menu.Items.Add("Quitter", null, (_, _) => Dispatcher.Invoke(ExitHub));
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowHub);
        _tray.BalloonTipClicked += (_, _) => Dispatcher.Invoke(() => _balloonClickAction?.Invoke());

        _loadingSettings = true;
        LoadSettings();
        _loadingSettings = false;
        ReminderNav.Click += (_, _) => ShowPage("reminder");
        TextToolsToggle.Click += (_, _) => ToggleTextToolsSection();
        MonitoringGamesToggle.Click += (_, _) => ToggleNavigationSection(MonitoringGamesPanel, MonitoringGamesChevron, ref _monitoringGamesExpanded);
        MaintenanceToggle.Click += (_, _) => ToggleNavigationSection(MaintenancePanel, MaintenanceChevron, ref _maintenanceExpanded);
        AutomationToggle.Click += (_, _) => ToggleNavigationSection(AutomationPanel, AutomationChevron, ref _automationExpanded);
        MonitoringNav.Click += async (_, _) => { ShowPage("monitoring"); await RefreshMonitoringAsync(); };
        GameSessionsNav.Click += async (_, _) => { ShowPage("gameSessions"); await RefreshGameSessionsAsync(true); };
        NetworkMonitoringNav.Click += async (_, _) => { ShowPage("networkMonitoring"); RefreshNetworkApplications(); await RefreshNetworkMonitoringAsync(); };
        CleanupNav.Click += (_, _) => ShowPage("cleanup");
        DuplicateFilesNav.Click += (_, _) => ShowPage("duplicateFiles");
        StorageHealthNav.Click += async (_, _) => { ShowPage("storageHealth"); await RefreshStorageHealthAsync(); };
        StartupAuditNav.Click += async (_, _) => { ShowPage("startupAudit"); await RefreshStartupAuditAsync(); };
        CorrectorNav.Click += (_, _) => ShowPage("corrector");
        ReformulateNav.Click += (_, _) => ShowPage("reformulate");
        SimplifyNav.Click += (_, _) => ShowPage("simplify");
        ConversationSummaryNav.Click += (_, _) => ShowPage("conversationSummary");
        WordDefinitionNav.Click += (_, _) => ShowPage("wordDefinition");
        TranslatorNav.Click += (_, _) => ShowPage("translator");
        ResponseGeneratorNav.Click += (_, _) => ShowPage("responseGenerator");
        ActionWheelNav.Click += (_, _) => ShowPage("actionWheel");
        NvidiaNav.Click += (_, _) => ShowPage("nvidia");
        XmpNav.Click += (_, _) => ShowPage("xmp");
        KeyboardLayoutNav.Click += (_, _) => ShowPage("keyboardLayout");
        TipeeeButton.Click += (_, _) => OpenTipeeePage();
        GeneralSettingsButton.Click += (_, _) => ShowPage("general");
        HideHubButton.Click += (_, _) => Hide();
        QuitHubButton.Click += (_, _) => ExitHub();
        SaveReminder.Click += (_, _) => SaveReminderSettings();
        TestReminder.Click += (_, _) => TestReminderNow();
        SaveCorrector.Click += (_, _) => SaveCorrectorSettings();
        SaveTranslator.Click += (_, _) => SaveTranslatorSettings();
        SaveResponseGenerator.Click += (_, _) => SaveResponseGeneratorSettings();
        SaveReformulator.Click += (_, _) => SaveReformulatorSettings();
        SaveSimplifier.Click += (_, _) => SaveSimplifierSettings();
        SaveConversationSummary.Click += (_, _) => SaveConversationSummarySettings();
        SaveWordDefinition.Click += (_, _) => SaveWordDefinitionSettings();
        OpenReformulateAiSettings.Click += (_, _) => ShowPage("responseGenerator");
        OpenSimplifyAiSettings.Click += (_, _) => ShowPage("responseGenerator");
        OpenReformulateWheelSettings.Click += (_, _) => ShowPage("actionWheel");
        OpenSimplifyWheelSettings.Click += (_, _) => ShowPage("actionWheel");
        OpenConversationSummaryAiSettings.Click += (_, _) => ShowPage("responseGenerator");
        OpenWordDefinitionAiSettings.Click += (_, _) => ShowPage("responseGenerator");
        SaveActionWheel.Click += (_, _) => SaveActionWheelSettings();
        ReminderEnabled.Checked += (_, _) => ApplyEnabledStates();
        ReminderEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        CorrectorEnabled.Checked += (_, _) => ApplyEnabledStates();
        CorrectorEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        TranslatorEnabled.Checked += (_, _) => ApplyEnabledStates();
        TranslatorEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        ResponseGeneratorEnabled.Checked += (_, _) => ApplyEnabledStates();
        ResponseGeneratorEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        ReformulatorEnabled.Checked += (_, _) => ApplyEnabledStates();
        ReformulatorEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        SimplifierEnabled.Checked += (_, _) => ApplyEnabledStates();
        SimplifierEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        ConversationSummarizerEnabled.Checked += (_, _) => ApplyEnabledStates();
        ConversationSummarizerEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        WordDefinitionEnabled.Checked += (_, _) => ApplyEnabledStates();
        WordDefinitionEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        ActionWheelEnabled.Checked += (_, _) => ApplyEnabledStates();
        ActionWheelEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        NvidiaEnabled.Checked += (_, _) => ApplyEnabledStates();
        NvidiaEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        XmpEnabled.Checked += (_, _) => ApplyEnabledStates();
        XmpEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        KeyboardLayoutEnabled.Checked += (_, _) => ApplyEnabledStates();
        KeyboardLayoutEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        MonitoringModuleEnabled.Checked += (_, _) => ApplyEnabledStates();
        MonitoringModuleEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        GameSessionsModuleEnabled.Checked += (_, _) => ApplyEnabledStates();
        GameSessionsModuleEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        NetworkMonitoringModuleEnabled.Checked += (_, _) => ApplyEnabledStates();
        NetworkMonitoringModuleEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        TemporaryCleanupModuleEnabled.Checked += (_, _) => ApplyEnabledStates();
        TemporaryCleanupModuleEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        DuplicateFilesModuleEnabled.Checked += (_, _) => ApplyEnabledStates();
        DuplicateFilesModuleEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        StorageHealthModuleEnabled.Checked += (_, _) => ApplyEnabledStates();
        StorageHealthModuleEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        StartupAuditModuleEnabled.Checked += (_, _) => ApplyEnabledStates();
        StartupAuditModuleEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        TestXmp.Click += async (_, _) => await CheckXmpAsync(true);
        RefreshMonitoring.Click += async (_, _) => await RefreshMonitoringAsync();
        ConfigureMonitoringAlerts.Click += (_, _) => OpenMonitoringAlertSettings();
        OpenStressTest.Click += (_, _) => new StressTestWindow { Owner = this }.ShowDialog();
        RefreshNetworkMonitoring.Click += async (_, _) => await RefreshNetworkMonitoringAsync();
        RefreshNetworkApplicationsButton.Click += (_, _) => RefreshNetworkApplications();
        AnalyzeNetworkApplication.Click += async (_, _) => await AnalyzeNetworkApplicationAsync();
        ClearNetworkHistory.Click += (_, _) => ClearNetworkHistoryNow();
        SaveNetworkTargets.Click += (_, _) => SaveNetworkMonitoringTargets();
        SaveGameSessionAlert.Click += (_, _) => SaveGameSessionAlertSettings();
        SaveGamePerformanceMode.Click += (_, _) => SaveGamePerformanceModeSettings();
        GameSessionPeriodBox.SelectionChanged += (_, _) => RefreshGameSessionSummary();
        ClearGameSessionHistory.Click += (_, _) => ClearGameSessionHistory_OnClick();
        ExportGameSessionHistory.Click += (_, _) => ExportGameSessionHistory_OnClick();
        SaveNetworkJitterAlert.Click += (_, _) => SaveNetworkJitterAlertSettings();
        ScanTemporaryFiles.Click += async (_, _) => await ScanTemporaryFilesAsync();
        SelectAllCleanupFiles.Click += (_, _) => CleanupFilesList.SelectAll();
        DeleteSelectedTemporaryFiles.Click += async (_, _) => await DeleteSelectedTemporaryFilesAsync();
        SaveAutomaticTemporaryCleanup.Click += async (_, _) => await SaveAutomaticTemporaryCleanupAsync();
        ChooseDuplicateFolder.Click += (_, _) => ChooseDuplicateFolderNow();
        ScanDuplicateFiles.Click += async (_, _) => await ScanDuplicateFilesAsync();
        CancelDuplicateScan.Click += (_, _) => _duplicateScanCancellation?.Cancel();
        DuplicateFilesList.SelectionChanged += (_, _) =>
            OpenDuplicateFileLocation.IsEnabled = DuplicateFilesList.SelectedItem is DuplicateFileCandidate;
        OpenDuplicateFileLocation.Click += (_, _) => OpenSelectedDuplicateLocation();
        AutoSelectDuplicateFiles.Click += (_, _) => AutoSelectDuplicateFilesNow();
        DeleteSelectedDuplicateFiles.Click += async (_, _) => await DeleteSelectedDuplicateFilesAsync();
        CleanupFilesList.SelectionChanged += (_, _) =>
            DeleteSelectedTemporaryFiles.IsEnabled = CleanupFilesList.SelectedItems.Count > 0;
        RefreshStorageHealth.Click += async (_, _) => await RefreshStorageHealthAsync();
        RefreshStartupAudit.Click += async (_, _) => await RefreshStartupAuditAsync();
        StartupEntriesList.SelectionChanged += (_, _) => UpdateStartupAuditButtons();
        DisableStartupEntry.Click += async (_, _) => await ChangeStartupEntryStateAsync(false);
        EnableStartupEntry.Click += async (_, _) => await ChangeStartupEntryStateAsync(true);
        SaveXmp.Click += (_, _) => SaveXmpSettings();
        SaveKeyboardLayout.Click += (_, _) => SaveKeyboardLayoutSettings();
        TestKeyboardLayout.Click += (_, _) => TestKeyboardLayoutNow();
        OpenDiagnosticLog.Click += (_, _) => OpenDiagnosticLogNow();
        SaveGeneralSettings.Click += (_, _) => SaveGeneralSettingsNow();
        OpenOpenAiApiPage.Click += (_, _) => OpenExternalPage(OpenAiApiKeysUrl, "OpenAI");
        OpenGeminiApiPage.Click += (_, _) => OpenExternalPage(GeminiApiKeysUrl, "Google Gemini");
        OpenDeepLApiPage.Click += (_, _) => OpenExternalPage(DeepLApiKeysUrl, "DeepL");
        OpenGoogleTranslateApiPage.Click += (_, _) => OpenExternalPage(GoogleTranslateApiKeysUrl, "Google Traduction");
        OpenLibreTranslateApiPage.Click += (_, _) => OpenExternalPage(LibreTranslateApiKeysUrl, "LibreTranslate");
        OpenGeminiQuotaPage.Click += (_, _) => OpenExternalPage(GeminiQuotasUrl, "quotas Google Gemini");
        OpenGoogleTranslateQuotaPage.Click += (_, _) => OpenExternalPage(GoogleTranslateQuotasUrl, "quotas Google Traduction");
        RefreshApiQuotas.Click += async (_, _) => await RefreshApiQuotaStatusesAsync();
        DeleteAllPersonalData.Click += (_, _) => DeleteAllPersonalDataNow();
        CheckUpdates.Click += async (_, _) => await CheckForUpdatesAsync();
        DownloadUpdate.Click += async (_, _) => await InstallAvailableUpdateAsync();
        OpenPatchNotes.Click += (_, _) => OpenSelectedPatchNotes();
        StartWithWindowsToggle.Checked += (_, _) => UpdateStartupLabel();
        StartWithWindowsToggle.Unchecked += (_, _) => UpdateStartupLabel();
        OptimizeNvidia.Click += async (_, _) => await OptimizeNvidiaAsync();
        RestoreNvidia.Click += async (_, _) => await RestoreNvidiaAsync();
        SaveNvidiaProfile.Click += async (_, _) => await SaveNvidiaProfileAsync();
        ApplySelectedNvidiaProfile.Click += async (_, _) => await ApplySelectedNvidiaProfileAsync();
        DeleteSelectedNvidiaProfile.Click += (_, _) => DeleteSelectedNvidiaProfileNow();
        NvidiaProfilesBox.SelectionChanged += (_, _) => UpdateNvidiaProfileButtons();
        TestApi.Click += async (_, _) => await TestApiAsync();
        TestLanguageTool.Click += async (_, _) => await TestLanguageToolAsync();
        ForgetApi.Click += (_, _) =>
        {
            var provider = CurrentProvider();
            SecretStore.Delete(provider);
            ApiKeyBox.Clear();
            CorrectorStatus.Text = $"Clé {provider} supprimée.";
        };
        TestTranslationApi.Click += async (_, _) => await TestTranslationApiAsync();
        ForgetTranslationApi.Click += (_, _) =>
        {
            var provider = CurrentTranslationProvider();
            SecretStore.Delete(provider);
            TranslationApiKeyBox.Clear();
            TranslatorStatus.Text = $"Clé {provider} supprimée.";
        };
        _hotkeyService.Pressed += async (_, _) =>
        {
            AppLog.Write("Raccourci global reçu.");
            await RunCorrectionAsync();
        };
        _translatorHotkeyService.Pressed += async (_, _) => await RunTranslationAsync();
        _responseGeneratorHotkeyService.Pressed += async (_, _) => await RunResponseGenerationAsync();
        _actionWheelHotkeyService.Pressed += async (_, _) => await ShowActionWheelAsync();
        _gameChatKeyboardService.StateChanged += (_, active) => Dispatcher.BeginInvoke(() =>
            KeyboardLayoutStatus.Text = active
                ? "Mode chat en jeu : AZERTY actif. Entrée, Échap ou un clic restaure le QWERTY."
                : "Mode chat terminé : QWERTY restauré dans le jeu.");
        _reminderTimer.Tick += (_, _) => ShowReminder();
        _xmpTimer.Tick += async (_, _) => await CheckXmpAsync(false);
        _monitoringTimer.Tick += async (_, _) =>
        {
            await RefreshMonitoringAsync();
            if (_settings.NetworkMonitoringModuleEnabled && NetworkMonitoringPage.Visibility == Visibility.Visible &&
                DateTime.UtcNow - _lastAutomaticNetworkMeasureUtc >= TimeSpan.FromSeconds(15))
            {
                _lastAutomaticNetworkMeasureUtc = DateTime.UtcNow;
                await RefreshNetworkMonitoringAsync();
            }
            await RefreshGameSessionsAsync();
            await RunAutomaticTemporaryCleanupAsync();
        };
        SourceInitialized += (_, _) => { RegisterHotkey(); RegisterTranslatorHotkey(); RegisterResponseGeneratorHotkey(); RegisterActionWheelHotkey(); };
        ContentRendered += async (_, _) =>
        {
            if (_initialXmpCheckDone) return;
            _initialXmpCheckDone = true;
            ShowMemorySetupIfNeeded();
            ShowGeminiSetupIfNeeded();
            if (_settings.XmpMonitorEnabled) await CheckXmpAsync(false);
            await RunAutomaticTemporaryCleanupAsync();
            await CheckForUpdatesAtStartupAsync();
        };
        Closing += OnClosing;
        ConfigureReminderTimer();
        ConfigureXmpTimer();
        _monitoringTimer.Start();
        ConfigureKeyboardLayoutMonitor();
        UpdateNavigationState();
    }

    private void LoadSettings()
    {
        if (_settings.XmpCheckIntervalMinutes == 30)
        {
            _settings.XmpCheckIntervalMinutes = 360;
            _settingsService.Save(_settings);
        }
        ReminderEnabled.IsChecked = _settings.ReminderEnabled;
        ReminderInterval.Text = _settings.ReminderIntervalMinutes.ToString();
        ReminderDuration.Text = _settings.ReminderDisplaySeconds.ToString();
        ReminderUrl.Text = _settings.ReminderUrl;
        UpdateLastReminderText();
        CorrectorEnabled.IsChecked = _settings.CorrectorEnabled;
        HotkeyBox.Text = _settings.Hotkey.Replace("+", " + ");
        var provider = _settings.UseOpenAi && _settings.CorrectionProvider == "Local" ? "OpenAI" : _settings.CorrectionProvider;
        EngineBox.SelectedIndex = provider switch { "OpenAI" => 1, "LanguageTool" => 2, "Gemini" => 3, _ => 0 };
        PreviewCheck.IsChecked = _settings.PreviewBeforeReplace;
        FallbackCheck.IsChecked = _settings.FallbackToLocal;
        LanguageToolUrlBox.Text = _settings.LanguageToolUrl;
        UpdateProviderPanel();
        TranslatorEnabled.IsChecked = _settings.TranslatorEnabled;
        TranslatorHotkeyBox.Text = _settings.TranslatorHotkey.Replace("+", " + ");
        TranslationEngineBox.SelectedIndex = _settings.TranslatorProvider switch { "GoogleTranslate" => 1, "Gemini" => 2, "LibreTranslate" => 3, "MyMemory" => 4, _ => 0 };
        var languages = new[]
        {
            new LanguageChoice("Détection automatique", "auto"), new LanguageChoice("Français", "FR"),
            new LanguageChoice("Anglais", "EN"), new LanguageChoice("Allemand", "DE"),
            new LanguageChoice("Espagnol", "ES"), new LanguageChoice("Italien", "IT"),
            new LanguageChoice("Portugais", "PT"), new LanguageChoice("Néerlandais", "NL"),
            new LanguageChoice("Polonais", "PL"), new LanguageChoice("Japonais", "JA")
        };
        SourceLanguageBox.ItemsSource = languages;
        TargetLanguageBox.ItemsSource = languages.Skip(1).ToArray();
        SourceLanguageBox.SelectedValue = _settings.TranslationSourceLanguage;
        TargetLanguageBox.SelectedValue = _settings.TranslationTargetLanguage;
        RefreshActionWheelPreview();
        if (SourceLanguageBox.SelectedIndex < 0) SourceLanguageBox.SelectedIndex = 0;
        if (TargetLanguageBox.SelectedIndex < 0) TargetLanguageBox.SelectedValue = "EN";
        TranslationPreviewCheck.IsChecked = _settings.TranslationPreviewBeforeReplace;
        DeepLFreeApiCheck.IsChecked = _settings.DeepLUseFreeApi;
        UpdateTranslationProviderPanel();
        ResponseGeneratorEnabled.IsChecked = _settings.ResponseGeneratorEnabled;
        ResponseGeneratorHotkeyBox.Text = _settings.ResponseGeneratorHotkey.Replace("+", " + ");
        ResponseGeneratorEngineBox.SelectedIndex = _settings.ResponseGeneratorProvider == "OpenAI" ? 1 : 0;
        ResponseGeneratorToneBox.SelectedIndex = _settings.ResponseGeneratorTone switch { "Professionnel" => 1, "Amical" => 2, "Concis" => 3, _ => 0 };
        ResponseGeneratorInstructionBox.Text = _settings.ResponseGeneratorInstruction;
        ResponseGeneratorPreviewCheck.IsChecked = _settings.ResponseGeneratorPreview;
        ReformulatorEnabled.IsChecked = _settings.ReformulatorEnabled;
        ReformulationStyleBox.SelectedIndex = _settings.ReformulationStyle switch
        {
            "Message décontracté" => 1, "LinkedIn enthousiaste" => 2, "LinkedIn cringe" => 3,
            "Discours médiéval" => 4, "Très formel" => 5, "Amical" => 6,
            "Direct et concis" => 7, "Diplomatique" => 8, "Humoristique" => 9,
            "Commercial convaincant" => 10, "Académique" => 11, "Poétique" => 12,
            "Fantasy" => 13, "Discours motivant" => 14, "Vieux français" => 15,
            "Français très complexe" => 16, "Scientifique" => 17, "Méchant" => 18,
            "Insultant" => 19, "Insultant mais discret" => 20, "Méchant de film" => 21, _ => 0
        };
        SimplifierEnabled.IsChecked = _settings.SimplifierEnabled;
        SimplificationLevelBox.SelectedIndex = _settings.SimplificationLevel switch
        {
            "Ultra simplifié" => 0, "Beaucoup simplifié" => 1, _ => 2
        };
        ConversationSummarizerEnabled.IsChecked = _settings.ConversationSummarizerEnabled;
        ConversationSummaryLengthBox.SelectedIndex = _settings.ConversationSummaryLength switch
        {
            "Très court - 3 lignes" => 0, "Moyen - 10 lignes" => 2, "Détaillé" => 3, _ => 1
        };
        ConversationSummaryPreserveNamesCheck.IsChecked = _settings.ConversationSummaryPreserveNames;
        ConversationSummaryIncludeActionsCheck.IsChecked = _settings.ConversationSummaryIncludeActions;
        WordDefinitionEnabled.IsChecked = _settings.WordDefinitionEnabled;
        WordDefinitionDetailBox.SelectedIndex = _settings.WordDefinitionDetail switch
        {
            "Très simple" => 0, "Détaillé" => 2, "Expert" => 3, _ => 1
        };
        WordDefinitionIncludeExamplesCheck.IsChecked = _settings.WordDefinitionIncludeExamples;
        UpdateResponseGeneratorModel();
        RefreshTransformationPages();
        ActionWheelEnabled.IsChecked = _settings.ActionWheelEnabled;
        ActionWheelHotkeyBox.Text = _settings.ActionWheelHotkey.Replace("+", " + ");
        NvidiaEnabled.IsChecked = _settings.NvidiaOptimizerEnabled;
        MonitoringModuleEnabled.IsChecked = _settings.MonitoringEnabled;
        UpdateNvidiaPage();
        XmpEnabled.IsChecked = _settings.XmpMonitorEnabled;
        MemoryTypeBox.SelectedIndex = _settings.MemoryType == "DDR5" ? 1 : 0;
        XmpCustomSpeedCheck.IsChecked = _settings.XmpUseCustomSpeed;
        XmpExpectedSpeedBox.Text = _settings.XmpExpectedSpeed.ToString();
        UpdateXmpSpeedControls();
        XmpIntervalBox.Text = _settings.XmpCheckIntervalMinutes.ToString();
        KeyboardLayoutEnabled.IsChecked = _settings.AutoFrenchKeyboardInDialogs;
        GameChatShortcutBox.Text = _settings.GameChatKeyboardShortcut.Replace("+", " + ");
        NetworkTargetsBox.Text = _settings.NetworkMonitoringTargets;
        GameSessionAlertHoursBox.Text = _settings.GameSessionAlertHours.ToString();
        GameSessionAlertEnabled.IsChecked = _settings.GameSessionAlertEnabled;
        AutomaticGameHighPriorityEnabled.IsChecked = _settings.AutomaticGameHighPriorityEnabled;
        NetworkJitterAlertEnabled.IsChecked = _settings.NetworkJitterAlertEnabled;
        NetworkJitterAlertMsBox.Text = _settings.NetworkJitterAlertMs.ToString();
        GameSessionsModuleEnabled.IsChecked = _settings.GameSessionsModuleEnabled;
        NetworkMonitoringModuleEnabled.IsChecked = _settings.NetworkMonitoringModuleEnabled;
        TemporaryCleanupModuleEnabled.IsChecked = _settings.TemporaryCleanupModuleEnabled;
        DuplicateFilesModuleEnabled.IsChecked = _settings.DuplicateFilesModuleEnabled;
        StorageHealthModuleEnabled.IsChecked = _settings.StorageHealthModuleEnabled;
        StartupAuditModuleEnabled.IsChecked = _settings.StartupAuditModuleEnabled;
        AutomaticTemporaryCleanupEnabled.IsChecked = _settings.AutomaticTemporaryCleanupEnabled;
        UpdateXmpLastCheck();
        StartWithWindowsToggle.IsChecked = _settings.StartWithWindows;
        FontSizeBox.SelectedIndex = _settings.FontSizePreference switch { "Petit" => 0, "Grand" => 2, _ => 1 };
        ThemeBox.SelectedIndex = _settings.ThemePreference == "Clair" ? 1 : 0;
        AiConsentCheck.IsChecked = _settings.AiPrivacyConsentAccepted;
        CurrentVersionText.Text = $"FlexHub {UpdateService.CurrentVersion}";
        var changelogPath = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        var versionNotes = ChangelogService.Load(changelogPath);
        VersionHistoryBox.ItemsSource = versionNotes;
        VersionHistoryBox.SelectedItem = versionNotes.FirstOrDefault(note => note.Version == UpdateService.CurrentVersion) ?? versionNotes.FirstOrDefault();
        OpenPatchNotes.IsEnabled = VersionHistoryBox.SelectedItem != null;
        UpdateStartupLabel();
        RefreshGeneralApiKeyBoxes();
        RefreshApiQuotaStatuses();
    }

    private void ShowPage(string page)
    {
        ReminderPage.Visibility = page == "reminder" ? Visibility.Visible : Visibility.Collapsed;
        CorrectorPage.Visibility = page == "corrector" ? Visibility.Visible : Visibility.Collapsed;
        ReformulatePage.Visibility = page == "reformulate" ? Visibility.Visible : Visibility.Collapsed;
        SimplifyPage.Visibility = page == "simplify" ? Visibility.Visible : Visibility.Collapsed;
        ConversationSummaryPage.Visibility = page == "conversationSummary" ? Visibility.Visible : Visibility.Collapsed;
        WordDefinitionPage.Visibility = page == "wordDefinition" ? Visibility.Visible : Visibility.Collapsed;
        TranslatorPage.Visibility = page == "translator" ? Visibility.Visible : Visibility.Collapsed;
        ResponseGeneratorPage.Visibility = page == "responseGenerator" ? Visibility.Visible : Visibility.Collapsed;
        ActionWheelPage.Visibility = page == "actionWheel" ? Visibility.Visible : Visibility.Collapsed;
        NvidiaPage.Visibility = page == "nvidia" ? Visibility.Visible : Visibility.Collapsed;
        MonitoringPage.Visibility = page == "monitoring" ? Visibility.Visible : Visibility.Collapsed;
        GameSessionsPage.Visibility = page == "gameSessions" ? Visibility.Visible : Visibility.Collapsed;
        NetworkMonitoringPage.Visibility = page == "networkMonitoring" ? Visibility.Visible : Visibility.Collapsed;
        CleanupPage.Visibility = page == "cleanup" ? Visibility.Visible : Visibility.Collapsed;
        DuplicateFilesPage.Visibility = page == "duplicateFiles" ? Visibility.Visible : Visibility.Collapsed;
        StorageHealthPage.Visibility = page == "storageHealth" ? Visibility.Visible : Visibility.Collapsed;
        StartupAuditPage.Visibility = page == "startupAudit" ? Visibility.Visible : Visibility.Collapsed;
        XmpPage.Visibility = page == "xmp" ? Visibility.Visible : Visibility.Collapsed;
        KeyboardLayoutPage.Visibility = page == "keyboardLayout" ? Visibility.Visible : Visibility.Collapsed;
        GeneralSettingsPage.Visibility = page == "general" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SidebarScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (SidebarScrollViewer.ScrollableHeight <= 0) return;
        e.Handled = true;
        var startingOffset = _sidebarScrollAnimating ? _sidebarScrollTarget : SidebarScrollViewer.VerticalOffset;
        _sidebarScrollTarget = Math.Clamp(
            startingOffset - e.Delta * 0.42,
            0,
            SidebarScrollViewer.ScrollableHeight);
        if (_sidebarScrollAnimating) return;
        _sidebarScrollAnimating = true;
        CompositionTarget.Rendering += AnimateSidebarScroll;
    }

    private void AnimateSidebarScroll(object? sender, EventArgs e)
    {
        var difference = _sidebarScrollTarget - SidebarScrollViewer.VerticalOffset;
        if (Math.Abs(difference) < 0.5)
        {
            SidebarScrollViewer.ScrollToVerticalOffset(_sidebarScrollTarget);
            CompositionTarget.Rendering -= AnimateSidebarScroll;
            _sidebarScrollAnimating = false;
            return;
        }
        SidebarScrollViewer.ScrollToVerticalOffset(SidebarScrollViewer.VerticalOffset + difference * 0.22);
    }

    private void SaveGeneralSettingsNow()
    {
        _settings.StartWithWindows = StartWithWindowsToggle.IsChecked == true;
        _settings.FontSizePreference = (FontSizeBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Moyen";
        _settings.ThemePreference = (ThemeBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Sombre";
        _settings.AiPrivacyConsentAccepted = AiConsentCheck.IsChecked == true;
        try
        {
            SaveApiKeyFromSettings("OpenAI", GeneralOpenAiKeyBox);
            SaveApiKeyFromSettings("Gemini", GeneralGeminiKeyBox);
            SaveApiKeyFromSettings("DeepL", GeneralDeepLKeyBox);
            SaveApiKeyFromSettings("GoogleTranslate", GeneralGoogleTranslateKeyBox);
            SaveApiKeyFromSettings("LibreTranslate", GeneralLibreTranslateKeyBox);
            StartupService.SetEnabled(_settings.StartWithWindows);
            _settingsService.Save(_settings);
            ApplyAppearance();
            RefreshGeneralApiKeyBoxes();
            UpdateProviderPanel();
            UpdateTranslationProviderPanel();
            UpdateResponseGeneratorModel();
            AppLog.Write($"PARAMÈTRES GÉNÉRAUX ENREGISTRÉS | DémarrageWindows={_settings.StartWithWindows}; " +
                         $"Thème={_settings.ThemePreference}; Police={_settings.FontSizePreference}; ConsentementIA={_settings.AiPrivacyConsentAccepted}");
            GeneralSettingsStatus.Text = "Paramètres généraux et clés API enregistrés.";
        }
        catch (Exception ex)
        {
            GeneralSettingsStatus.Text = $"Impossible de modifier le démarrage automatique : {ex.Message}";
        }
    }

    private void ConfigureKeyboardLayoutMonitor()
    {
        if (_settings.AutoFrenchKeyboardInDialogs) _keyboardLayoutMonitorService.Start();
        else _keyboardLayoutMonitorService.Stop();
        if (!_gameChatKeyboardService.Configure(_settings.AutoFrenchKeyboardInDialogs, _settings.GameChatKeyboardShortcut))
            AppLog.Write("Raccourci du chat en jeu : installation du hook clavier impossible.");
    }

    private void SaveKeyboardLayoutSettings()
    {
        _settings.AutoFrenchKeyboardInDialogs = KeyboardLayoutEnabled.IsChecked == true;
        _settings.MonitoringEnabled = MonitoringModuleEnabled.IsChecked == true;
        _settings.GameChatKeyboardShortcut = GameChatShortcutBox.Text.Replace(" ", "");
        _settingsService.Save(_settings);
        LogModuleStates();
        ConfigureKeyboardLayoutMonitor();
        UpdateNavigationState();
        KeyboardLayoutStatus.Text = _settings.AutoFrenchKeyboardInDialogs
            ? "Module activé. Les zones de saisie ouvertes en QWERTY utiliseront temporairement l’AZERTY."
            : "Module désactivé.";
    }

    private void TestKeyboardLayoutNow()
    {
        if (KeyboardLayoutEnabled.IsChecked != true)
        {
            KeyboardLayoutStatus.Text = "Activez d’abord le module pour effectuer le test.";
            return;
        }

        KeyboardLayoutStatus.Text = "Passez en clavier anglais dans le dialogue, puis testez les touches A et Q.";
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Test FlexHub — le clavier doit être en Français (AZERTY)",
            CheckFileExists = false,
            Multiselect = false
        };
        dialog.ShowDialog(this);
        KeyboardLayoutStatus.Text = "Dialogue fermé : la disposition utilisée avant le test doit être restaurée.";
    }

    private void OpenDiagnosticLogNow()
    {
        AppLog.Write($"OUVERTURE DU JOURNAL | {AppLog.MemorySnapshot()}");
        try
        {
            Process.Start(new ProcessStartInfo { FileName = AppLog.PathName, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            KeyboardLayoutStatus.Text = $"Impossible d’ouvrir le journal : {ex.Message}";
        }
    }

    private void LogModuleStates() =>
        AppLog.Write($"ÉTAT DES MODULES | Rappel={_settings.ReminderEnabled}; Correcteur={_settings.CorrectorEnabled}; " +
                     $"Traducteur={_settings.TranslatorEnabled}; Réponses={_settings.ResponseGeneratorEnabled}; " +
                     $"Roue={_settings.ActionWheelEnabled}; NVIDIA={_settings.NvidiaOptimizerEnabled}; " +
                     $"XMP={_settings.XmpMonitorEnabled}; Clavier={_settings.AutoFrenchKeyboardInDialogs}");

    private void GameChatShortcutBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var shortcut = ReadShortcut(e);
        if (shortcut == null)
        {
            KeyboardLayoutStatus.Text = "Choisissez une touche autre que Ctrl, Alt, Maj ou Windows seule.";
            return;
        }
        GameChatShortcutBox.Text = shortcut.Replace("+", " + ");
    }

    private static void SaveApiKeyFromSettings(string provider, System.Windows.Controls.PasswordBox box)
    {
        var value = box.Password.Trim();
        if (string.IsNullOrWhiteSpace(value)) SecretStore.Delete(provider);
        else if (!value.StartsWith('•')) SecretStore.Save(provider, value);
    }

    private void RefreshGeneralApiKeyBoxes()
    {
        SetStoredKeyState(GeneralOpenAiKeyBox, "OpenAI");
        SetStoredKeyState(GeneralGeminiKeyBox, "Gemini");
        SetStoredKeyState(GeneralDeepLKeyBox, "DeepL");
        SetStoredKeyState(GeneralGoogleTranslateKeyBox, "GoogleTranslate");
        SetStoredKeyState(GeneralLibreTranslateKeyBox, "LibreTranslate");
    }

    private static void SetStoredKeyState(System.Windows.Controls.PasswordBox box, string provider) =>
        box.Password = SecretStore.HasKey(provider) ? "••••••••••••••••" : "";

    private void RefreshApiQuotaStatuses()
    {
        OpenAiQuotaStatus.Text = ApiQuotaTracker.OpenAiStatus;
        DeepLQuotaStatus.Text = SecretStore.HasKey("DeepL") ? "Cliquer sur Actualiser" : "Clé API manquante";
    }

    private async Task RefreshApiQuotaStatusesAsync()
    {
        RefreshApiQuotas.IsEnabled = false;
        OpenAiQuotaStatus.Text = ApiQuotaTracker.OpenAiStatus;
        var deepLKey = SecretStore.Load("DeepL");
        DeepLQuotaStatus.Text = string.IsNullOrWhiteSpace(deepLKey)
            ? "Clé API manquante"
            : "Actualisation…";
        try
        {
            if (!string.IsNullOrWhiteSpace(deepLKey))
                DeepLQuotaStatus.Text = await _apiQuotaService.GetDeepLStatusAsync(deepLKey, _settings.DeepLUseFreeApi);
        }
        catch (Exception ex)
        {
            DeepLQuotaStatus.Text = "Quota indisponible";
            AppLog.Write($"Lecture du quota DeepL impossible : {ex.Message}");
        }
        finally { RefreshApiQuotas.IsEnabled = true; }
    }

    private void DeleteAllPersonalDataNow()
    {
        var firstConfirmation = System.Windows.MessageBox.Show(
            this,
            "Cette action supprimera tous les réglages, toutes les clés API, les journaux et les profils NVIDIA que vous avez sauvegardés.\n\nLes profils d’optimisation fournis avec l’application ne seront pas supprimés.",
            "Effacer toutes les données personnelles",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (firstConfirmation != MessageBoxResult.Yes) return;

        var finalConfirmation = System.Windows.MessageBox.Show(
            this,
            "Dernière confirmation : cette suppression est irréversible et le Hub va se fermer. Continuer ?",
            "Confirmation définitive",
            MessageBoxButton.YesNo,
            MessageBoxImage.Stop,
            MessageBoxResult.No);
        if (finalConfirmation != MessageBoxResult.Yes) return;

        try
        {
            StartupService.SetEnabled(false);
            _hotkeyService.Unregister();
            _translatorHotkeyService.Unregister();
            _responseGeneratorHotkeyService.Unregister();
            _actionWheelHotkeyService.Unregister();
            _reminderTimer.Stop();
            _xmpTimer.Stop();
            _settingsService.DeleteAllPersonalData();
            System.Windows.MessageBox.Show(this, "Toutes les données personnelles ont été supprimées. Le Hub va maintenant se fermer.", "Suppression terminée", MessageBoxButton.OK, MessageBoxImage.Information);
            ExitHub();
        }
        catch (Exception ex)
        {
            GeneralSettingsStatus.Text = $"La suppression a échoué : {ex.Message}";
        }
    }

    private void UpdateStartupLabel()
    {
        if (StartWithWindowsLabel != null)
            StartWithWindowsLabel.Text = StartWithWindowsToggle.IsChecked == true ? "Oui" : "Non";
    }

    private void ContentScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not System.Windows.Controls.ScrollViewer viewer || viewer.ScrollableHeight <= 0) return;
        var target = Math.Clamp(viewer.VerticalOffset - e.Delta, 0, viewer.ScrollableHeight);
        if (Math.Abs(target - viewer.VerticalOffset) < 0.5) return;
        viewer.ScrollToVerticalOffset(target);
        e.Handled = true;
    }

    private void ApplyAppearance()
    {
        var large = _settings.FontSizePreference == "Grand";
        var small = _settings.FontSizePreference == "Petit";
        System.Windows.Application.Current.Resources["BaseFontSize"] = large ? 16d : small ? 12d : 14d;
        System.Windows.Application.Current.Resources["ControlFontSize"] = large ? 16d : small ? 12d : 14d;

        var light = _settings.ThemePreference == "Clair";
        SetThemeBrush("BackgroundBrush", light ? "#FFF4F1ED" : "#FF0E0F0F");
        SetThemeBrush("PanelBrush", light ? "#FFFFFFFF" : "#FF1A1918");
        SetThemeBrush("PanelLightBrush", light ? "#FFF0EAE4" : "#FF24211F");
        SetThemeBrush("HeaderBrush", light ? "#FFE9E1D8" : "#FF151515");
        SetThemeBrush("SidebarBrush", light ? "#FFEEE8E2" : "#FF141414");
        SetThemeBrush("FieldBrush", light ? "#FFFFFFFF" : "#FF111212");
        SetThemeBrush("ComboBrush", light ? "#FFF4EFEA" : "#FF2A2420");
        SetThemeBrush("ComboPopupBrush", light ? "#FFFFFFFF" : "#FF211D1A");
        SetThemeBrush("ButtonBrush", light ? "#FFE7DED5" : "#FF28231F");
        SetThemeBrush("HoverBrush", light ? "#FFD8C8B9" : "#FF3B2D23");
        SetThemeBrush("BorderBrush", light ? "#FFBDAE9F" : "#FF554D47");
        SetThemeBrush("SeparatorBrush", light ? "#FFCABDB1" : "#FF4A4039");
        SetThemeBrush("TextBrush", light ? "#FF241F1B" : "#FFF7F3EF");
        SetThemeBrush("MutedBrush", light ? "#FF665E58" : "#FFBDB7B2");
        SetThemeBrush("DisabledTextBrush", light ? "#FF8A817A" : "#FF8E8883");
        SetThemeBrush("ToggleOffBrush", light ? "#FFD5CBC2" : "#FF49433E");
        SetThemeBrush("ToggleThumbBrush", light ? "#FFFFFFFF" : "#FFFFDFC0");
    }

    private static void SetThemeBrush(string resourceKey, string color)
    {
        var parsedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
        if (System.Windows.Application.Current.Resources[resourceKey] is System.Windows.Media.SolidColorBrush brush && !brush.IsFrozen)
            brush.Color = parsedColor;
        else
            System.Windows.Application.Current.Resources[resourceKey] = new System.Windows.Media.SolidColorBrush(parsedColor);
    }

    private void UpdateNavigationState()
    {
        if (ReminderNav == null || CorrectorNav == null || ReformulateNav == null || SimplifyNav == null || ConversationSummaryNav == null || WordDefinitionNav == null || TranslatorNav == null || ResponseGeneratorNav == null || ActionWheelNav == null || MonitoringNav == null || GameSessionsNav == null || NetworkMonitoringNav == null || CleanupNav == null || DuplicateFilesNav == null || StorageHealthNav == null || StartupAuditNav == null || NvidiaNav == null || XmpNav == null || KeyboardLayoutNav == null) return;
        PlaceNavigationButton(ReminderNav, ReminderEnabled.IsChecked == true);
        PlaceNavigationButton(CorrectorNav, CorrectorEnabled.IsChecked == true);
        PlaceNavigationButton(ReformulateNav, ReformulatorEnabled.IsChecked == true);
        PlaceNavigationButton(SimplifyNav, SimplifierEnabled.IsChecked == true);
        PlaceNavigationButton(ConversationSummaryNav, ConversationSummarizerEnabled.IsChecked == true);
        PlaceNavigationButton(WordDefinitionNav, WordDefinitionEnabled.IsChecked == true);
        PlaceNavigationButton(TranslatorNav, TranslatorEnabled.IsChecked == true);
        PlaceNavigationButton(ResponseGeneratorNav, ResponseGeneratorEnabled.IsChecked == true);
        PlaceNavigationButton(ActionWheelNav, ActionWheelEnabled.IsChecked == true);
        PlaceNavigationButton(MonitoringNav, _settings.MonitoringEnabled);
        PlaceNavigationButton(GameSessionsNav, _settings.GameSessionsModuleEnabled);
        PlaceNavigationButton(NetworkMonitoringNav, _settings.NetworkMonitoringModuleEnabled);
        PlaceNavigationButton(CleanupNav, _settings.TemporaryCleanupModuleEnabled);
        PlaceNavigationButton(DuplicateFilesNav, _settings.DuplicateFilesModuleEnabled);
        PlaceNavigationButton(StorageHealthNav, _settings.StorageHealthModuleEnabled);
        PlaceNavigationButton(StartupAuditNav, _settings.StartupAuditModuleEnabled);
        PlaceNavigationButton(NvidiaNav, NvidiaEnabled.IsChecked == true);
        PlaceNavigationButton(XmpNav, XmpEnabled.IsChecked == true);
        PlaceNavigationButton(KeyboardLayoutNav, KeyboardLayoutEnabled.IsChecked == true);
        DisabledAppsSection.Visibility = DisabledAppsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        TextToolsSection.Visibility = Visibility.Visible;
        MonitoringGamesSection.Visibility = Visibility.Visible;
        MaintenanceSection.Visibility = Visibility.Visible;
        AutomationSection.Visibility = Visibility.Visible;
        UpdateCategoryCount(TextToolsModuleCount, CorrectorNav, ReformulateNav, SimplifyNav,
            ConversationSummaryNav, WordDefinitionNav, TranslatorNav, ResponseGeneratorNav);
        UpdateCategoryCount(MonitoringGamesModuleCount, MonitoringNav, GameSessionsNav,
            NetworkMonitoringNav, NvidiaNav, XmpNav);
        UpdateCategoryCount(MaintenanceModuleCount, CleanupNav, DuplicateFilesNav, StorageHealthNav, StartupAuditNav);
        UpdateCategoryCount(AutomationModuleCount, ReminderNav, ActionWheelNav, KeyboardLayoutNav);
    }

    private void PlaceNavigationButton(System.Windows.Controls.Button button, bool enabled)
    {
        var target = enabled ? NavigationPanelFor(button) : DisabledAppsPanel;
        if (button.Parent is System.Windows.Controls.Panel current && current != target)
        {
            current.Children.Remove(button);
            var rank = NavigationRank(button);
            var index = target.Children.Cast<System.Windows.UIElement>()
                .TakeWhile(child => child is not System.Windows.Controls.Button other || NavigationRank(other) < rank)
                .Count();
            target.Children.Insert(index, button);
        }
        button.Margin = new Thickness(0, 0, 0, 9);
        ApplyNavigationState(button, enabled);
    }

    private bool IsTextTool(System.Windows.Controls.Button button) =>
        button == CorrectorNav || button == ReformulateNav || button == SimplifyNav ||
        button == ConversationSummaryNav || button == WordDefinitionNav ||
        button == TranslatorNav || button == ResponseGeneratorNav;

    private System.Windows.Controls.Panel NavigationPanelFor(System.Windows.Controls.Button button)
    {
        if (IsTextTool(button)) return TextToolsPanel;
        if (button == MonitoringNav || button == GameSessionsNav || button == NetworkMonitoringNav ||
            button == NvidiaNav || button == XmpNav) return MonitoringGamesPanel;
        if (button == CleanupNav || button == DuplicateFilesNav || button == StorageHealthNav ||
            button == StartupAuditNav) return MaintenancePanel;
        return AutomationPanel;
    }

    private void ToggleTextToolsSection()
    {
        _textToolsExpanded = !_textToolsExpanded;
        TextToolsPanel.Visibility = _textToolsExpanded ? Visibility.Visible : Visibility.Collapsed;
        TextToolsChevron.Text = _textToolsExpanded ? "⌄" : "›";
    }

    private static void ToggleNavigationSection(System.Windows.Controls.StackPanel panel,
        System.Windows.Controls.TextBlock chevron, ref bool expanded)
    {
        expanded = !expanded;
        panel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        chevron.Text = expanded ? "⌄" : "›";
    }

    private void UpdateCategoryCount(System.Windows.Controls.TextBlock label,
        params System.Windows.Controls.Button[] buttons)
    {
        var disabled = buttons.Count(button => button.Parent == DisabledAppsPanel);
        var active = buttons.Length - disabled;
        label.Text = disabled == 0
            ? $"{active} module{(active > 1 ? "s" : "")}"
            : $"{active} actif{(active > 1 ? "s" : "")} · {disabled} désactivé{(disabled > 1 ? "s" : "")}";
    }

    private int NavigationRank(System.Windows.Controls.Button button) =>
        button == ReminderNav ? 0 : button == CorrectorNav ? 1 : button == ReformulateNav ? 2 : button == SimplifyNav ? 3 : button == ConversationSummaryNav ? 4 : button == WordDefinitionNav ? 5 : button == TranslatorNav ? 6 : button == ResponseGeneratorNav ? 7 : button == ActionWheelNav ? 8 : button == MonitoringNav ? 9 : button == GameSessionsNav ? 10 : button == NetworkMonitoringNav ? 11 : button == CleanupNav ? 12 : button == DuplicateFilesNav ? 13 : button == StorageHealthNav ? 14 : button == StartupAuditNav ? 15 : button == NvidiaNav ? 16 : button == XmpNav ? 17 : 18;

    private void ApplyEnabledStates()
    {
        _settings.ReminderEnabled = ReminderEnabled.IsChecked == true;
        _settings.CorrectorEnabled = CorrectorEnabled.IsChecked == true;
        _settings.TranslatorEnabled = TranslatorEnabled.IsChecked == true;
        _settings.ResponseGeneratorEnabled = ResponseGeneratorEnabled.IsChecked == true;
        _settings.ReformulatorEnabled = ReformulatorEnabled.IsChecked == true;
        _settings.SimplifierEnabled = SimplifierEnabled.IsChecked == true;
        _settings.ConversationSummarizerEnabled = ConversationSummarizerEnabled.IsChecked == true;
        _settings.WordDefinitionEnabled = WordDefinitionEnabled.IsChecked == true;
        _settings.ActionWheelEnabled = ActionWheelEnabled.IsChecked == true;
        _settings.NvidiaOptimizerEnabled = NvidiaEnabled.IsChecked == true;
        _settings.XmpMonitorEnabled = XmpEnabled.IsChecked == true;
        _settings.AutoFrenchKeyboardInDialogs = KeyboardLayoutEnabled.IsChecked == true;
        _settings.GameSessionsModuleEnabled = GameSessionsModuleEnabled.IsChecked == true;
        _settings.NetworkMonitoringModuleEnabled = NetworkMonitoringModuleEnabled.IsChecked == true;
        _settings.TemporaryCleanupModuleEnabled = TemporaryCleanupModuleEnabled.IsChecked == true;
        _settings.DuplicateFilesModuleEnabled = DuplicateFilesModuleEnabled.IsChecked == true;
        _settings.StorageHealthModuleEnabled = StorageHealthModuleEnabled.IsChecked == true;
        _settings.StartupAuditModuleEnabled = StartupAuditModuleEnabled.IsChecked == true;
        _settingsService.Save(_settings);
        LogModuleStates();
        ConfigureReminderTimer();
        ConfigureXmpTimer();
        ConfigureKeyboardLayoutMonitor();
        RegisterHotkey();
        RegisterTranslatorHotkey();
        RegisterResponseGeneratorHotkey();
        RegisterActionWheelHotkey();
        UpdateNavigationState();
        RefreshActionWheelPreview();
        UpdateNvidiaPage();
    }

    private void UpdateLastReminderText()
    {
        LastReminderText.Text = _settings.LastReminderUtc.HasValue
            ? $"Dernier rappel : {_settings.LastReminderUtc.Value.ToLocalTime():dd/MM/yyyy à HH:mm:ss}"
            : "Dernier rappel : jamais";
    }

    private static void ApplyNavigationState(System.Windows.Controls.Button button, bool enabled)
    {
        button.Opacity = enabled ? 1.0 : 0.42;
        button.SetResourceReference(
            System.Windows.Controls.Control.ForegroundProperty,
            enabled ? "TextBrush" : "DisabledTextBrush");
        button.ToolTip = enabled ? null : "Application désactivée — cliquez pour modifier ses réglages";
    }

    private void SaveReminderSettings()
    {
        if (!int.TryParse(ReminderInterval.Text, out var interval) || interval < 1) { ReminderStatus.Text = "L’intervalle doit être supérieur à zéro."; return; }
        if (!int.TryParse(ReminderDuration.Text, out var duration) || duration < 1 || duration > 300) { ReminderStatus.Text = "La durée doit être comprise entre 1 et 300 secondes."; return; }
        if (!IsHttpUrl(ReminderUrl.Text)) { ReminderStatus.Text = "L’adresse doit commencer par https:// ou http://."; return; }
        _settings.ReminderEnabled = ReminderEnabled.IsChecked == true;
        _settings.ReminderIntervalMinutes = interval;
        _settings.ReminderDisplaySeconds = duration;
        _settings.ReminderUrl = ReminderUrl.Text.Trim();
        _settingsService.Save(_settings); ConfigureReminderTimer();
        ReminderStatus.Text = "Configuration enregistrée.";
    }

    private void SaveCorrectorSettings()
    {
        var provider = CurrentProvider();
        _settings.CorrectorEnabled = CorrectorEnabled.IsChecked == true;
        _settings.CorrectionProvider = provider;
        _settings.UseOpenAi = provider == "OpenAI";
        _settings.PreviewBeforeReplace = PreviewCheck.IsChecked == true;
        _settings.FallbackToLocal = FallbackCheck.IsChecked == true;
        _settings.Hotkey = HotkeyBox.Text.Replace(" ", "");
        if (provider == "OpenAI") _settings.OpenAiModel = string.IsNullOrWhiteSpace(ModelBox.Text) ? "gpt-4o-mini" : ModelBox.Text.Trim();
        if (provider == "Gemini") _settings.GeminiModel = string.IsNullOrWhiteSpace(ModelBox.Text) ? "gemini-3.5-flash-lite" : ModelBox.Text.Trim();
        if (provider == "LanguageTool")
        {
            if (!IsHttpUrl(LanguageToolUrlBox.Text)) { CorrectorStatus.Text = "L’adresse LanguageTool doit utiliser HTTP ou HTTPS."; return; }
            _settings.LanguageToolUrl = LanguageToolUrlBox.Text.Trim();
        }
        if (provider is "OpenAI" or "Gemini" && !string.IsNullOrWhiteSpace(ApiKeyBox.Password) && !ApiKeyBox.Password.StartsWith('•'))
            SecretStore.Save(provider, ApiKeyBox.Password.Trim());
        _settingsService.Save(_settings);
        CorrectorStatus.Text = RegisterHotkey() ? "Configuration enregistrée." : "Ce raccourci est déjà utilisé par une autre application.";
        if (provider is "OpenAI" or "Gemini") ApiKeyBox.Password = SecretStore.HasKey(provider) ? "••••••••••••••••" : "";
    }

    private void SaveTranslatorSettings()
    {
        var provider = CurrentTranslationProvider();
        _settings.TranslatorEnabled = TranslatorEnabled.IsChecked == true;
        _settings.TranslatorProvider = provider;
        _settings.TranslatorHotkey = TranslatorHotkeyBox.Text.Replace(" ", "");
        _settings.TranslationSourceLanguage = SourceLanguageBox.SelectedValue?.ToString() ?? "auto";
        _settings.TranslationTargetLanguage = TargetLanguageBox.SelectedValue?.ToString() ?? "EN";
        _settings.TranslationPreviewBeforeReplace = TranslationPreviewCheck.IsChecked == true;
        _settings.DeepLUseFreeApi = DeepLFreeApiCheck.IsChecked == true;
        if (provider == "Gemini") _settings.TranslatorGeminiModel = string.IsNullOrWhiteSpace(TranslationOptionBox.Text) ? "gemini-3.5-flash-lite" : TranslationOptionBox.Text.Trim();
        if (provider == "LibreTranslate")
        {
            if (!IsHttpUrl(TranslationOptionBox.Text)) { TranslatorStatus.Text = "L’adresse LibreTranslate doit utiliser HTTP ou HTTPS."; return; }
            _settings.LibreTranslateUrl = TranslationOptionBox.Text.Trim();
        }
        if (provider == "MyMemory") _settings.MyMemoryEmail = TranslationOptionBox.Text.Trim();
        if (provider != "MyMemory" && !string.IsNullOrWhiteSpace(TranslationApiKeyBox.Password) && !TranslationApiKeyBox.Password.StartsWith('•'))
            SecretStore.Save(provider, TranslationApiKeyBox.Password.Trim());
        _settingsService.Save(_settings);
        RefreshActionWheelPreview();
        TranslatorStatus.Text = RegisterTranslatorHotkey()
            ? "Configuration enregistrée."
            : "Ce raccourci est déjà utilisé par une autre application.";
        TranslationApiKeyBox.Password = SecretStore.HasKey(provider) ? "••••••••••••••••" : "";
    }

    private void SaveResponseGeneratorSettings()
    {
        var provider = CurrentResponseGeneratorProvider();
        _settings.ResponseGeneratorEnabled = ResponseGeneratorEnabled.IsChecked == true;
        _settings.ResponseGeneratorProvider = provider;
        _settings.ResponseGeneratorHotkey = ResponseGeneratorHotkeyBox.Text.Replace(" ", "");
        _settings.ResponseGeneratorTone = (ResponseGeneratorToneBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Naturel";
        _settings.ResponseGeneratorInstruction = ResponseGeneratorInstructionBox.Text.Trim();
        _settings.ResponseGeneratorPreview = ResponseGeneratorPreviewCheck.IsChecked == true;
        if (provider == "OpenAI") _settings.ResponseGeneratorOpenAiModel = ResponseGeneratorModelBox.Text.Trim();
        else _settings.ResponseGeneratorGeminiModel = ResponseGeneratorModelBox.Text.Trim();
        _settingsService.Save(_settings);
        RefreshTransformationPages();
        ResponseGeneratorStatus.Text = RegisterResponseGeneratorHotkey()
            ? $"Configuration enregistrée. Clé {provider} : {(SecretStore.HasKey(provider) ? "disponible" : "manquante")}."
            : "Ce raccourci est déjà utilisé par une autre application.";
        UpdateNavigationState();
    }

    private void SaveReformulatorSettings()
    {
        _settings.ReformulatorEnabled = ReformulatorEnabled.IsChecked == true;
        _settings.ReformulationStyle = (ReformulationStyleBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()
            ?? "Email professionnel";
        _settingsService.Save(_settings);
        RefreshTransformationPages();
        RefreshActionWheelPreview();
        UpdateNavigationState();
    }

    private void SaveSimplifierSettings()
    {
        _settings.SimplifierEnabled = SimplifierEnabled.IsChecked == true;
        _settings.SimplificationLevel = (SimplificationLevelBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()
            ?? "Simplifié";
        _settingsService.Save(_settings);
        RefreshTransformationPages();
        RefreshActionWheelPreview();
        UpdateNavigationState();
    }

    private void SaveConversationSummarySettings()
    {
        _settings.ConversationSummarizerEnabled = ConversationSummarizerEnabled.IsChecked == true;
        _settings.ConversationSummaryLength = (ConversationSummaryLengthBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()
            ?? "Court - 5 lignes";
        _settings.ConversationSummaryPreserveNames = ConversationSummaryPreserveNamesCheck.IsChecked == true;
        _settings.ConversationSummaryIncludeActions = ConversationSummaryIncludeActionsCheck.IsChecked == true;
        _settingsService.Save(_settings);
        RefreshTransformationPages();
        RefreshActionWheelPreview();
        UpdateNavigationState();
        ConversationSummaryStatus.Text = "Configuration enregistrée.";
    }

    private void SaveWordDefinitionSettings()
    {
        _settings.WordDefinitionEnabled = WordDefinitionEnabled.IsChecked == true;
        _settings.WordDefinitionDetail = (WordDefinitionDetailBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()
            ?? "Simple";
        _settings.WordDefinitionIncludeExamples = WordDefinitionIncludeExamplesCheck.IsChecked == true;
        _settingsService.Save(_settings);
        RefreshTransformationPages();
        RefreshActionWheelPreview();
        UpdateNavigationState();
        WordDefinitionStatus.Text = "Configuration enregistrée.";
    }

    private void SaveActionWheelSettings()
    {
        _settings.ActionWheelEnabled = ActionWheelEnabled.IsChecked == true;
        _settings.ActionWheelHotkey = ActionWheelHotkeyBox.Text.Replace(" ", "");
        _settingsService.Save(_settings);
        ActionWheelStatus.Text = RegisterActionWheelHotkey()
            ? "Configuration enregistrée. Appuyez sur le raccourci pour afficher la roue."
            : "Ce raccourci est déjà utilisé par une autre application.";
        UpdateNavigationState();
    }

    private bool RegisterHotkey()
    {
        if (!_settings.CorrectorEnabled) { _hotkeyService.Unregister(); return true; }
        var registered = _hotkeyService.Register(new WindowInteropHelper(this).Handle, _settings.Hotkey);
        AppLog.Write($"Enregistrement du raccourci {_settings.Hotkey}: {registered}.");
        return registered;
    }

    private bool RegisterTranslatorHotkey()
    {
        if (!_settings.TranslatorEnabled) { _translatorHotkeyService.Unregister(); return true; }
        var registered = _translatorHotkeyService.Register(new WindowInteropHelper(this).Handle, _settings.TranslatorHotkey);
        AppLog.Write($"Enregistrement du raccourci traducteur {_settings.TranslatorHotkey}: {registered}.");
        return registered;
    }

    private bool RegisterResponseGeneratorHotkey()
    {
        if (!_settings.ResponseGeneratorEnabled) { _responseGeneratorHotkeyService.Unregister(); return true; }
        var registered = _responseGeneratorHotkeyService.Register(new WindowInteropHelper(this).Handle, _settings.ResponseGeneratorHotkey);
        AppLog.Write($"Enregistrement du raccourci générateur {_settings.ResponseGeneratorHotkey}: {registered}.");
        return registered;
    }

    private bool RegisterActionWheelHotkey()
    {
        if (!_settings.ActionWheelEnabled) { _actionWheelHotkeyService.Unregister(); return true; }
        var registered = _actionWheelHotkeyService.Register(new WindowInteropHelper(this).Handle, _settings.ActionWheelHotkey);
        AppLog.Write($"Enregistrement du raccourci roue d’actions {_settings.ActionWheelHotkey}: {registered}.");
        return registered;
    }

    private async Task ShowActionWheelAsync()
    {
        if (_actionWheelOpen || !_settings.ActionWheelEnabled) return;
        _actionWheelOpen = true;
        try
        {
            var sourceLanguage = DisplayLanguage(_settings.TranslationSourceLanguage);
            var targetLanguage = DisplayLanguage(_settings.TranslationTargetLanguage);
            var wheel = new ActionWheelWindow(
                sourceLanguage,
                targetLanguage,
                _settings.CorrectorEnabled,
                _settings.TranslatorEnabled,
                _settings.ResponseGeneratorEnabled,
                _settings.ReformulatorEnabled,
                _settings.SimplifierEnabled,
                _settings.ConversationSummarizerEnabled,
                _settings.WordDefinitionEnabled);
            var action = await wheel.ShowAndWaitAsync();
            switch (action)
            {
                case WheelAction.Correct when _settings.CorrectorEnabled:
                    await RunCorrectionAsync();
                    break;
                case WheelAction.TranslateForward when _settings.TranslatorEnabled:
                    await RunTranslationAsync();
                    break;
                case WheelAction.TranslateReverse when _settings.TranslatorEnabled:
                    await RunTranslationAsync(reverseLanguages: true);
                    break;
                case WheelAction.Respond when _settings.ResponseGeneratorEnabled:
                    await RunResponseGenerationAsync();
                    break;
                case WheelAction.Reformulate when _settings.ResponseGeneratorEnabled && _settings.ReformulatorEnabled:
                    await RunTextTransformationAsync(TextTransformationMode.Reformulate);
                    break;
                case WheelAction.Simplify when _settings.ResponseGeneratorEnabled && _settings.SimplifierEnabled:
                    await RunTextTransformationAsync(TextTransformationMode.Simplify);
                    break;
                case WheelAction.SummarizeConversation when _settings.ResponseGeneratorEnabled && _settings.ConversationSummarizerEnabled:
                    await RunTextTransformationAsync(TextTransformationMode.SummarizeConversation);
                    break;
                case WheelAction.DefineWord when _settings.ResponseGeneratorEnabled && _settings.WordDefinitionEnabled:
                    await RunTextTransformationAsync(TextTransformationMode.DefineWord);
                    break;
                case WheelAction.Correct:
                    ShowVisibleMessage("Roue d’actions", "Le Correcteur universel est désactivé.");
                    break;
                case WheelAction.TranslateForward:
                case WheelAction.TranslateReverse:
                    ShowVisibleMessage("Roue d’actions", "Le Traducteur universel est désactivé.");
                    break;
                case WheelAction.Respond:
                    ShowVisibleMessage("Roue d’actions", "Le Générateur de réponse est désactivé.");
                    break;
                case WheelAction.Reformulate:
                    ShowVisibleMessage("Roue d’actions", _settings.ResponseGeneratorEnabled
                        ? "Le module Reformuler est désactivé. Activez-le depuis son onglet."
                        : "Activez le Générateur de réponse pour utiliser les transformations de texte.");
                    break;
                case WheelAction.Simplify:
                    ShowVisibleMessage("Roue d’actions", _settings.ResponseGeneratorEnabled
                        ? "Le module Simplifier est désactivé. Activez-le depuis son onglet."
                        : "Activez le Générateur de réponse pour utiliser les transformations de texte.");
                    break;
                case WheelAction.SummarizeConversation:
                    ShowVisibleMessage("Roue d’actions", _settings.ResponseGeneratorEnabled
                        ? "Le module Résumer une conversation est désactivé. Activez-le depuis son onglet."
                        : "Activez le Générateur de réponse pour utiliser le résumé de conversation.");
                    break;
                case WheelAction.DefineWord:
                    ShowVisibleMessage("Roue d’actions", _settings.ResponseGeneratorEnabled
                        ? "Le module Définition d’un mot est désactivé. Activez-le depuis son onglet."
                        : "Activez le Générateur de réponse pour utiliser la définition d’un mot.");
                    break;
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"ERREUR ROUE D’ACTIONS: {ex}");
            ShowVisibleMessage("Roue d’actions", ex.Message);
        }
        finally { _actionWheelOpen = false; }
    }

    private async Task RunResponseGenerationAsync()
    {
        if (!_settings.ResponseGeneratorEnabled) return;
        if (!EnsureOnlineServicesConsent()) return;
        System.Windows.IDataObject? previousClipboard = null;
        try
        {
            var capture = await ClipboardService.CopySelectionAsync();
            previousClipboard = capture.Previous;
            if (string.IsNullOrWhiteSpace(capture.Text))
            {
                ClipboardService.Restore(capture.Previous);
                ShowVisibleMessage("Générateur de réponse", "Aucun texte n’a été récupéré. Sélectionnez le message, relâchez les touches, puis utilisez de nouveau le raccourci.");
                return;
            }

            var provider = _settings.ResponseGeneratorProvider;
            var key = SecretStore.Load(provider);
            if (string.IsNullOrWhiteSpace(key))
            {
                ClipboardService.Restore(capture.Previous);
                ShowVisibleMessage("Générateur de réponse", $"Ajoutez d’abord une clé API {provider} dans le module Correcteur universel.");
                return;
            }

            var generated = provider == "OpenAI"
                ? await _responseGenerationService.GenerateOpenAiAsync(capture.Text, key, _settings.ResponseGeneratorOpenAiModel, _settings.ResponseGeneratorTone, _settings.ResponseGeneratorInstruction)
                : await _responseGenerationService.GenerateGeminiAsync(capture.Text, key, _settings.ResponseGeneratorGeminiModel, _settings.ResponseGeneratorTone, _settings.ResponseGeneratorInstruction);

            if (_settings.ResponseGeneratorPreview)
            {
                var preview = new ResponsePreviewWindow(generated);
                if (preview.ShowDialog() != true) { ClipboardService.Restore(capture.Previous); return; }
                generated = preview.Result;
            }
            await ClipboardService.ReplaceAsync(generated, capture.Window, capture.Previous);
            ShowTrayMessage("Générateur de réponse", "La réponse générée a remplacé le texte sélectionné.");
        }
        catch (TaskCanceledException)
        {
            ClipboardService.Restore(previousClipboard);
            ShowVisibleMessage("Erreur de génération", "Le service d’IA n’a pas répondu dans le délai de 20 secondes.");
        }
        catch (Exception ex)
        {
            ClipboardService.Restore(previousClipboard);
            AppLog.Write($"ERREUR GÉNÉRATION DE RÉPONSE: {ex}");
            ShowVisibleMessage("Erreur de génération", ex.Message);
        }
    }

    private async Task RunTextTransformationAsync(TextTransformationMode mode)
    {
        if (!_settings.ResponseGeneratorEnabled) return;
        if (mode != TextTransformationMode.DefineWord && !EnsureOnlineServicesConsent()) return;
        System.Windows.IDataObject? previousClipboard = null;
        var actionName = mode switch
        {
            TextTransformationMode.Reformulate => "Reformuler",
            TextTransformationMode.Simplify => "Simplifier",
            TextTransformationMode.SummarizeConversation => "Résumer une conversation",
            TextTransformationMode.DefineWord => "Définir un mot",
            _ => "Transformer"
        };
        try
        {
            var capture = await ClipboardService.CopySelectionAsync();
            previousClipboard = capture.Previous;
            if (string.IsNullOrWhiteSpace(capture.Text))
            {
                ClipboardService.Restore(capture.Previous);
                ShowVisibleMessage(actionName, "Aucun texte n’a été récupéré. Sélectionnez le texte, relâchez les touches, puis ouvrez de nouveau la roue.");
                return;
            }

            if (mode == TextTransformationMode.DefineWord)
            {
                try
                {
                    AppLog.Write($"DICTIONNAIRE | Recherche Wiktionnaire pour {capture.Text.Length} caractère(s).");
                    var dictionaryResult = await _wiktionaryService.FindAsync(capture.Text);
                    if (dictionaryResult != null)
                    {
                        AppLog.Write($"DICTIONNAIRE | Définition Wiktionnaire trouvée pour « {dictionaryResult.Word} ».");
                        var preview = new ResponsePreviewWindow(
                            dictionaryResult.DisplayText + $"{Environment.NewLine}{Environment.NewLine}Source : {dictionaryResult.SourceUrl}",
                            $"Définition de « {dictionaryResult.Word} »",
                            "Définition fournie par le Wiktionnaire. Le texte sélectionné ne sera pas remplacé.",
                            "Fermer");
                        preview.ShowDialog();
                        ClipboardService.Restore(capture.Previous);
                        return;
                    }
                    AppLog.Write("DICTIONNAIRE | Aucune définition exploitable, passage à Gemini.");
                }
                catch (Exception dictionaryError)
                {
                    AppLog.Write($"DICTIONNAIRE | Wiktionnaire indisponible, passage à Gemini : {dictionaryError.Message}");
                }

                if (!EnsureOnlineServicesConsent())
                {
                    ClipboardService.Restore(capture.Previous);
                    return;
                }
            }

            var provider = mode == TextTransformationMode.DefineWord ? "Gemini" : _settings.ResponseGeneratorProvider;
            var key = SecretStore.Load(provider);
            if (string.IsNullOrWhiteSpace(key))
            {
                ClipboardService.Restore(capture.Previous);
                ShowVisibleMessage(actionName, $"Ajoutez d’abord une clé API {provider} dans les paramètres généraux.");
                return;
            }

            var transformationOption = mode switch
            {
                TextTransformationMode.Reformulate => _settings.ReformulationStyle,
                TextTransformationMode.Simplify => _settings.SimplificationLevel,
                TextTransformationMode.SummarizeConversation => BuildConversationSummaryOption(),
                TextTransformationMode.DefineWord => BuildWordDefinitionOption(),
                _ => ""
            };
            AppLog.Write($"TRANSFORMATION {actionName.ToUpperInvariant()} | Démarrage avec {provider}; option={transformationOption}; caractères={capture.Text.Length}.");
            ShowTrayMessage(actionName, $"Génération en cours avec {provider}…");
            var transformed = provider == "OpenAI"
                ? await _responseGenerationService.TransformOpenAiAsync(capture.Text, key, _settings.ResponseGeneratorOpenAiModel, mode, transformationOption)
                : await _responseGenerationService.TransformGeminiAsync(capture.Text, key, _settings.ResponseGeneratorGeminiModel, mode, transformationOption);

            if (mode == TextTransformationMode.DefineWord)
            {
                var definitionPreview = new ResponsePreviewWindow(
                    transformed,
                    "Définition proposée par Gemini",
                    "Le Wiktionnaire n’avait pas de résultat exploitable. Le texte sélectionné ne sera pas remplacé.",
                    "Fermer");
                definitionPreview.ShowDialog();
                ClipboardService.Restore(capture.Previous);
                AppLog.Write($"DICTIONNAIRE | Définition de secours Gemini affichée. Longueur : {transformed.Length}.");
                return;
            }

            if (_settings.ResponseGeneratorPreview)
            {
                var preview = new ResponsePreviewWindow(
                    transformed,
                    $"Texte à {actionName.ToLowerInvariant()}",
                    "Vous pouvez modifier le résultat avant de remplacer le texte sélectionné.");
                if (preview.ShowDialog() != true)
                {
                    ClipboardService.Restore(capture.Previous);
                    return;
                }
                transformed = preview.Result;
            }

            await ClipboardService.ReplaceAsync(transformed, capture.Window, capture.Previous);
            ShowTrayMessage(actionName, "Le texte sélectionné a été remplacé.");
            AppLog.Write($"Transformation {actionName} terminée. Longueur du résultat : {transformed.Length}.");
        }
        catch (TaskCanceledException)
        {
            ClipboardService.Restore(previousClipboard);
            AppLog.Write($"ERREUR {actionName.ToUpperInvariant()}: aucun modèle Gemini n’a répondu après les tentatives d’une minute.");
            ShowVisibleMessage($"Erreur - {actionName}", "Gemini n’a répondu avec aucun des modèles essayés. Chaque tentative a disposé d’une minute.");
        }
        catch (Exception ex)
        {
            ClipboardService.Restore(previousClipboard);
            AppLog.Write($"ERREUR {actionName.ToUpperInvariant()}: {ex}");
            ShowVisibleMessage($"Erreur - {actionName}", ex.Message);
        }
    }

    private string BuildConversationSummaryOption()
    {
        var length = _settings.ConversationSummaryLength switch
        {
            "Très court - 3 lignes" => "Produis un résumé de 3 lignes maximum.",
            "Moyen - 10 lignes" => "Produis un résumé structuré d’environ 10 lignes.",
            "Détaillé" => "Produis un résumé détaillé et structuré sans répétitions inutiles.",
            _ => "Produis un résumé de 5 lignes maximum."
        };
        var names = _settings.ConversationSummaryPreserveNames
            ? "Conserve les noms des participants lorsqu’ils sont utiles pour comprendre qui dit ou décide quoi."
            : "Ne mentionne pas les noms des participants sauf si cela est indispensable.";
        var actions = _settings.ConversationSummaryIncludeActions
            ? "Termine par des rubriques séparées Décisions et Tâches à faire lorsqu’il y en a."
            : "N’ajoute pas de rubrique séparée pour les décisions ou les tâches.";
        return $"{length} {names} {actions}";
    }

    private string BuildWordDefinitionOption()
    {
        var detail = _settings.WordDefinitionDetail switch
        {
            "Très simple" => "Utilise une seule phrase et des mots compréhensibles par un enfant.",
            "Détaillé" => "Donne le sens principal, les nuances importantes et la catégorie grammaticale.",
            "Expert" => "Fournis une définition précise avec terminologie spécialisée, nuances et étymologie utile.",
            _ => "Donne une définition courte et facile à comprendre."
        };
        var example = _settings.WordDefinitionIncludeExamples
            ? "Ajoute ensuite un exemple d’utilisation naturel et clairement séparé."
            : "N’ajoute pas d’exemple.";
        return $"{detail} {example}";
    }

    private async Task RunTranslationAsync(bool reverseLanguages = false)
    {
        if (!_settings.TranslatorEnabled) return;
        if (!EnsureOnlineServicesConsent()) return;
        if (reverseLanguages && _settings.TranslationSourceLanguage == "auto")
        {
            ShowVisibleMessage("Traducteur universel", "Choisissez une langue source précise dans les paramètres pour utiliser la traduction inverse.");
            return;
        }
        var sourceLanguage = reverseLanguages ? _settings.TranslationTargetLanguage : _settings.TranslationSourceLanguage;
        var targetLanguage = reverseLanguages ? _settings.TranslationSourceLanguage : _settings.TranslationTargetLanguage;
        System.Windows.IDataObject? previousClipboard = null;
        try
        {
            var capture = await ClipboardService.CopySelectionAsync();
            previousClipboard = capture.Previous;
            if (string.IsNullOrWhiteSpace(capture.Text))
            {
                ClipboardService.Restore(capture.Previous);
                ShowVisibleMessage("Traducteur universel", "Aucun texte n’a été récupéré. Sélectionnez le texte, relâchez les touches, puis utilisez de nouveau le raccourci.");
                return;
            }

            var provider = _settings.TranslatorProvider;
            var key = SecretStore.Load(provider);
            if (provider is "DeepL" or "GoogleTranslate" or "Gemini" && string.IsNullOrWhiteSpace(key))
            {
                ClipboardService.Restore(capture.Previous);
                ShowVisibleMessage("Traducteur universel", $"Ajoutez une clé API {DisplayTranslationProvider(provider)} dans le Hub.");
                return;
            }

            var translated = provider switch
            {
                "GoogleTranslate" => await _translationService.TranslateGoogleAsync(capture.Text, key!, targetLanguage, sourceLanguage),
                "Gemini" => await _translationService.TranslateGeminiAsync(capture.Text, key!, _settings.TranslatorGeminiModel, targetLanguage, sourceLanguage),
                "LibreTranslate" => await _translationService.TranslateLibreAsync(capture.Text, key, _settings.LibreTranslateUrl, targetLanguage, sourceLanguage),
                "MyMemory" => await _translationService.TranslateMyMemoryAsync(capture.Text, targetLanguage, sourceLanguage, _settings.MyMemoryEmail),
                _ => await _translationService.TranslateDeepLAsync(capture.Text, key!, targetLanguage, sourceLanguage, _settings.DeepLUseFreeApi)
            };

            if (_settings.TranslationPreviewBeforeReplace)
            {
                var preview = new TranslationPreviewWindow(translated);
                if (preview.ShowDialog() != true) { ClipboardService.Restore(capture.Previous); return; }
                translated = preview.Result;
            }
            await ClipboardService.ReplaceAsync(translated, capture.Window, capture.Previous);
            ShowTrayMessage("Traducteur", "Le texte sélectionné a été traduit et remplacé.");
        }
        catch (TaskCanceledException)
        {
            ClipboardService.Restore(previousClipboard);
            ShowVisibleMessage("Erreur de traduction", "Le service de traduction n’a pas répondu dans le délai de 20 secondes.");
        }
        catch (Exception ex)
        {
            ClipboardService.Restore(previousClipboard);
            AppLog.Write($"ERREUR TRADUCTION: {ex}");
            ShowVisibleMessage("Erreur de traduction", ex.Message);
        }
    }

    private async Task RunCorrectionAsync()
    {
        if (!_settings.CorrectorEnabled) return;
        System.Windows.IDataObject? previousClipboard = null;
        try
        {
            var capture = await ClipboardService.CopySelectionAsync();
            previousClipboard = capture.Previous;
            AppLog.Write($"Capture terminée. Caractères récupérés: {capture.Text.Length}. Moteur: {_settings.CorrectionProvider}.");
            if (string.IsNullOrWhiteSpace(capture.Text))
            {
                ClipboardService.Restore(capture.Previous);
                AppLog.Write("Aucun texte détecté dans la sélection.");
                ShowVisibleMessage("Correcteur universel", "Aucun texte n’a été récupéré. Sélectionnez le texte, relâchez les touches, puis utilisez de nouveau le raccourci.");
                return;
            }
            string corrected;
            var provider = _settings.CorrectionProvider;
            if (provider is "OpenAI" or "Gemini")
            {
                if (!EnsureOnlineServicesConsent()) { ClipboardService.Restore(capture.Previous); return; }
                var key = SecretStore.Load(provider);
                if (string.IsNullOrWhiteSpace(key)) { ClipboardService.Restore(capture.Previous); ShowTrayMessage("Correcteur", $"Ajoutez une clé API {provider} dans le Hub."); return; }
                try
                {
                    corrected = provider == "OpenAI"
                        ? await _correctionService.CorrectOpenAiAsync(capture.Text, key, _settings.OpenAiModel, _settings.Language)
                        : await _correctionService.CorrectGeminiAsync(capture.Text, key, _settings.GeminiModel, _settings.Language);
                }
                catch (Exception ex) when (_settings.FallbackToLocal)
                {
                    AppLog.Write($"{provider} indisponible, basculement local : {ex.Message}");
                    ShowTrayMessage("Correcteur", $"{provider} indisponible : correction locale utilisée.");
                    corrected = _correctionService.CorrectLocal(capture.Text, _settings.Language);
                }
            }
            else if (provider == "LanguageTool")
            {
                try { corrected = await _correctionService.CorrectLanguageToolAsync(capture.Text, _settings.LanguageToolUrl, _settings.Language); }
                catch when (_settings.FallbackToLocal) { corrected = _correctionService.CorrectLocal(capture.Text, _settings.Language); }
            }
            else corrected = _correctionService.CorrectLocal(capture.Text, _settings.Language);

            AppLog.Write($"Correction terminée. Texte modifié: {corrected != capture.Text}. Longueur du résultat: {corrected.Length}.");
            if (_settings.PreviewBeforeReplace)
            {
                var preview = new CorrectionPreviewWindow(corrected);
                if (preview.ShowDialog() != true) { ClipboardService.Restore(capture.Previous); return; }
                corrected = preview.Result;
            }
            await ClipboardService.ReplaceAsync(corrected, capture.Window, capture.Previous);
            ShowTrayMessage("Correcteur", "Le texte sélectionné a été remplacé.");
        }
        catch (Exception ex)
        {
            ClipboardService.Restore(previousClipboard);
            AppLog.Write($"ERREUR: {ex}");
            ShowVisibleMessage("Erreur de correction", ex.Message);
        }
    }

    private async Task TestApiAsync()
    {
        try
        {
            var provider = CurrentProvider();
            if (provider is not ("OpenAI" or "Gemini")) return;
            var key = ApiKeyBox.Password.StartsWith('•') ? SecretStore.Load(provider) : ApiKeyBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(key)) { CorrectorStatus.Text = "Saisissez une clé API."; return; }
            TestApi.IsEnabled = false; CorrectorStatus.Text = "Connexion en cours…";
            var model = string.IsNullOrWhiteSpace(ModelBox.Text) ? (provider == "OpenAI" ? "gpt-4o-mini" : "gemini-3.5-flash-lite") : ModelBox.Text.Trim();
            var result = provider == "OpenAI"
                ? await _correctionService.CorrectOpenAiAsync("je sui disponible demain", key, model, "français")
                : await _correctionService.CorrectGeminiAsync("je sui disponible demain", key, model, "français");
            if (!ApiKeyBox.Password.StartsWith('•')) SecretStore.Save(provider, key);
            _settings.CorrectionProvider = provider;
            _settings.UseOpenAi = provider == "OpenAI";
            if (provider == "OpenAI") _settings.OpenAiModel = model; else _settings.GeminiModel = model;
            _settingsService.Save(_settings);
            ApiKeyBox.Password = "••••••••••••••••";
            CorrectorStatus.Text = $"Connexion réussie : {result}";
        }
        catch (Exception ex) { CorrectorStatus.Text = ex.Message; }
        finally { TestApi.IsEnabled = true; }
    }

    private void HotkeyBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var shortcut = ReadShortcut(e);
        if (shortcut == null) { CorrectorStatus.Text = "Choisissez une touche autre que Ctrl, Alt, Maj ou Windows seule."; return; }
        HotkeyBox.Text = shortcut.Replace("+", " + ");
    }

    private void TranslatorHotkeyBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var shortcut = ReadShortcut(e);
        if (shortcut == null) { TranslatorStatus.Text = "Choisissez une touche autre que Ctrl, Alt, Maj ou Windows seule."; return; }
        TranslatorHotkeyBox.Text = shortcut.Replace("+", " + ");
    }

    private void ResponseGeneratorHotkeyBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var shortcut = ReadShortcut(e);
        if (shortcut == null) { ResponseGeneratorStatus.Text = "Choisissez une touche autre que Ctrl, Alt, Maj ou Windows seule."; return; }
        ResponseGeneratorHotkeyBox.Text = shortcut.Replace("+", " + ");
    }

    private void ActionWheelHotkeyBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var shortcut = ReadShortcut(e);
        if (shortcut == null) { ActionWheelStatus.Text = "Choisissez une touche autre que Ctrl, Alt, Maj ou Windows seule."; return; }
        ActionWheelHotkeyBox.Text = shortcut.Replace("+", " + ");
    }

    private static string? ReadShortcut(System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return null;
        var parts = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private void EngineBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateProviderPanel();
        if (!_loadingSettings && _settings != null)
        {
            var provider = CurrentProvider();
            _settings.CorrectionProvider = provider;
            _settings.UseOpenAi = provider == "OpenAI";
            _settingsService.Save(_settings);
            CorrectorStatus.Text = $"Moteur {provider} activé.";
        }
    }

    private string CurrentProvider() => EngineBox.SelectedIndex switch { 1 => "OpenAI", 2 => "LanguageTool", 3 => "Gemini", _ => "Local" };

    private string CurrentTranslationProvider() => TranslationEngineBox.SelectedIndex switch { 1 => "GoogleTranslate", 2 => "Gemini", 3 => "LibreTranslate", 4 => "MyMemory", _ => "DeepL" };
    private string CurrentResponseGeneratorProvider() => ResponseGeneratorEngineBox.SelectedIndex == 1 ? "OpenAI" : "Gemini";

    private void ResponseGeneratorEngineBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateResponseGeneratorModel();
    }

    private void UpdateResponseGeneratorModel()
    {
        if (ResponseGeneratorModelBox == null || ResponseGeneratorEngineBox == null) return;
        ResponseGeneratorModelBox.Text = CurrentResponseGeneratorProvider() == "OpenAI"
            ? _settings.ResponseGeneratorOpenAiModel
            : _settings.ResponseGeneratorGeminiModel;
        if (ResponseGeneratorStatus != null)
        {
            var provider = CurrentResponseGeneratorProvider();
            ResponseGeneratorStatus.Text = SecretStore.HasKey(provider)
                ? $"Clé {provider} disponible."
                : $"Clé {provider} manquante : ajoutez-la dans le Correcteur universel.";
        }
    }

    private void RefreshTransformationPages()
    {
        if (ReformulateConfigurationText == null || SimplifyConfigurationText == null || ConversationSummaryConfigurationText == null || WordDefinitionConfigurationText == null) return;
        var provider = _settings.ResponseGeneratorProvider == "OpenAI" ? "OpenAI" : "Google Gemini";
        var keyState = SecretStore.HasKey(_settings.ResponseGeneratorProvider) ? "clé disponible" : "clé manquante";
        var previewState = _settings.ResponseGeneratorPreview ? "aperçu activé" : "aperçu désactivé";
        ReformulateConfigurationText.Text = $"{provider} • {_settings.ReformulationStyle} • {previewState} • {keyState}";
        SimplifyConfigurationText.Text = $"{provider} • {_settings.SimplificationLevel} • {previewState} • {keyState}";
        ConversationSummaryConfigurationText.Text = $"{provider} • {_settings.ConversationSummaryLength} • {previewState} • {keyState}";
        var geminiKeyState = SecretStore.HasKey("Gemini") ? "secours Gemini disponible" : "clé Gemini manquante pour le secours";
        WordDefinitionConfigurationText.Text = $"Wiktionnaire en priorité • niveau {_settings.WordDefinitionDetail} • {geminiKeyState}";
    }
    private static string DisplayTranslationProvider(string provider) => provider switch { "GoogleTranslate" => "Google Traduction", "Gemini" => "Google Gemini", "LibreTranslate" => "LibreTranslate", "MyMemory" => "MyMemory (Translated)", _ => "DeepL" };
    private static string DisplayLanguage(string code) => code switch
    {
        "auto" => "Auto", "FR" => "Français", "EN" => "Anglais", "DE" => "Allemand",
        "ES" => "Espagnol", "IT" => "Italien", "PT" => "Portugais", "NL" => "Néerlandais",
        "PL" => "Polonais", "JA" => "Japonais", _ => code
    };

    private void RefreshActionWheelPreview()
    {
        if (PreviewTranslationForwardText == null || PreviewTranslationReverseText == null) return;
        var source = DisplayLanguage(_settings.TranslationSourceLanguage);
        var target = DisplayLanguage(_settings.TranslationTargetLanguage);
        PreviewTranslationForwardText.Text = $"{source} > {target}";
        PreviewTranslationReverseText.Text = $"{target} > {source}";

        PreviewCorrectorZone.Visibility = _settings.CorrectorEnabled ? Visibility.Visible : Visibility.Collapsed;
        PreviewReformulateZone.Visibility = _settings.ResponseGeneratorEnabled && _settings.ReformulatorEnabled ? Visibility.Visible : Visibility.Collapsed;
        PreviewSimplifyZone.Visibility = _settings.ResponseGeneratorEnabled && _settings.SimplifierEnabled ? Visibility.Visible : Visibility.Collapsed;
        PreviewTranslationForwardZone.Visibility = _settings.TranslatorEnabled ? Visibility.Visible : Visibility.Collapsed;
        PreviewTranslationReverseZone.Visibility = _settings.TranslatorEnabled ? Visibility.Visible : Visibility.Collapsed;
        PreviewResponseZone.Visibility = _settings.ResponseGeneratorEnabled ? Visibility.Visible : Visibility.Collapsed;
        PreviewSummarizeZone.Visibility = _settings.ResponseGeneratorEnabled && _settings.ConversationSummarizerEnabled ? Visibility.Visible : Visibility.Collapsed;
        PreviewDefineWordZone.Visibility = _settings.ResponseGeneratorEnabled && _settings.WordDefinitionEnabled ? Visibility.Visible : Visibility.Collapsed;

        System.Windows.FrameworkElement[] previewZones =
        [
            PreviewCorrectorZone, PreviewReformulateZone, PreviewSimplifyZone,
            PreviewTranslationForwardZone, PreviewTranslationReverseZone, PreviewResponseZone,
            PreviewSummarizeZone, PreviewDefineWordZone
        ];
        var visibleZones = previewZones.Where(zone => zone.Visibility == Visibility.Visible).ToArray();
        PreviewActionCount.Text = visibleZones.Length == 1
            ? "Aperçu · 1 action active"
            : $"Aperçu · {visibleZones.Length} actions actives";
        for (var index = 0; index < visibleZones.Length; index++)
        {
            var angle = -Math.PI / 2 + index * 2 * Math.PI / visibleZones.Length;
            var centerX = 325 + 190 * Math.Cos(angle);
            var centerY = 305 + 190 * Math.Sin(angle);
            System.Windows.Controls.Canvas.SetLeft(visibleZones[index], centerX - visibleZones[index].Width / 2);
            System.Windows.Controls.Canvas.SetTop(visibleZones[index], centerY - visibleZones[index].Height / 2);
        }
    }

    private void TranslationEngineBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateTranslationProviderPanel();
        if (!_loadingSettings && _settings != null)
        {
            _settings.TranslatorProvider = CurrentTranslationProvider();
            _settingsService.Save(_settings);
        }
    }

    private void UpdateTranslationProviderPanel()
    {
        if (TranslationApiKeyBox == null) return;
        var provider = CurrentTranslationProvider();
        var display = DisplayTranslationProvider(provider);
        TranslationApiKeyLabel.Text = $"Clé API {display}";
        TranslationPrivacyText.Text = $"La clé {display} est gérée depuis les paramètres généraux. Le texte sélectionné est transmis uniquement lorsque ce module est utilisé.";
        DeepLFreeApiCheck.Visibility = provider == "DeepL" ? Visibility.Visible : Visibility.Collapsed;
        TranslationApiKeyLabel.Visibility = Visibility.Collapsed;
        TranslationApiKeyBox.Visibility = Visibility.Collapsed;
        TranslationOptionPanel.Visibility = provider is "Gemini" or "LibreTranslate" or "MyMemory" ? Visibility.Visible : Visibility.Collapsed;
        if (provider == "Gemini") { TranslationOptionLabel.Text = "Modèle Gemini"; TranslationOptionBox.Text = _settings.TranslatorGeminiModel; }
        else if (provider == "LibreTranslate") { TranslationOptionLabel.Text = "Adresse du serveur LibreTranslate"; TranslationOptionBox.Text = _settings.LibreTranslateUrl; }
        else if (provider == "MyMemory") { TranslationOptionLabel.Text = "Courriel (facultatif, pour le quota)"; TranslationOptionBox.Text = _settings.MyMemoryEmail; }
        TranslationApiKeyBox.Password = SecretStore.HasKey(provider) ? "••••••••••••••••" : "";
    }

    private async Task TestTranslationApiAsync()
    {
        try
        {
            var provider = CurrentTranslationProvider();
            var key = TranslationApiKeyBox.Password.StartsWith('•') ? SecretStore.Load(provider) : TranslationApiKeyBox.Password.Trim();
            if (provider is "DeepL" or "GoogleTranslate" or "Gemini" && string.IsNullOrWhiteSpace(key)) { TranslatorStatus.Text = "Saisissez une clé API."; return; }
            TestTranslationApi.IsEnabled = false;
            TranslatorStatus.Text = "Connexion en cours…";
            var result = provider switch
            {
                "GoogleTranslate" => await _translationService.TranslateGoogleAsync("Bonjour le monde", key!, "EN", "FR"),
                "Gemini" => await _translationService.TranslateGeminiAsync("Bonjour le monde", key!, string.IsNullOrWhiteSpace(TranslationOptionBox.Text) ? _settings.TranslatorGeminiModel : TranslationOptionBox.Text.Trim(), "EN", "FR"),
                "LibreTranslate" => await _translationService.TranslateLibreAsync("Bonjour le monde", key, TranslationOptionBox.Text.Trim(), "EN", "FR"),
                "MyMemory" => await _translationService.TranslateMyMemoryAsync("Bonjour le monde", "EN", "FR", TranslationOptionBox.Text.Trim()),
                _ => await _translationService.TranslateDeepLAsync("Bonjour le monde", key!, "EN", "FR", DeepLFreeApiCheck.IsChecked == true)
            };
            if (provider != "MyMemory" && !TranslationApiKeyBox.Password.StartsWith('•') && !string.IsNullOrWhiteSpace(key)) SecretStore.Save(provider, key);
            TranslatorStatus.Text = $"Connexion réussie : {result}";
            if (provider != "MyMemory") TranslationApiKeyBox.Password = "••••••••••••••••";
        }
        catch (TaskCanceledException) { TranslatorStatus.Text = "Le service n’a pas répondu dans le délai de 20 secondes."; }
        catch (Exception ex) { TranslatorStatus.Text = ex.Message; }
        finally { TestTranslationApi.IsEnabled = true; }
    }

    private void UpdateProviderPanel()
    {
        if (ApiPanel == null || LanguageToolPanel == null) return;
        var provider = CurrentProvider();
        ApiPanel.Visibility = provider is "OpenAI" or "Gemini" ? Visibility.Visible : Visibility.Collapsed;
        LanguageToolPanel.Visibility = provider == "LanguageTool" ? Visibility.Visible : Visibility.Collapsed;
        if (provider is "OpenAI" or "Gemini")
        {
            ApiKeyLabel.Text = $"Clé API {provider}";
            ApiPrivacyText.Text = $"La clé {provider} est gérée depuis les paramètres généraux. Le texte sélectionné est transmis uniquement quand ce mode est actif.";
            ModelBox.Text = provider == "OpenAI" ? _settings.OpenAiModel : _settings.GeminiModel;
            ApiKeyBox.Password = SecretStore.HasKey(provider) ? "••••••••••••••••" : "";
        }
    }

    private async Task TestLanguageToolAsync()
    {
        try
        {
            TestLanguageTool.IsEnabled = false; CorrectorStatus.Text = "Connexion au serveur local…";
            var result = await _correctionService.CorrectLanguageToolAsync("je sui disponible demain", LanguageToolUrlBox.Text.Trim(), "fr");
            CorrectorStatus.Text = $"LanguageTool fonctionne : {result}";
        }
        catch (Exception ex) { CorrectorStatus.Text = ex.Message; }
        finally { TestLanguageTool.IsEnabled = true; }
    }

    private void UpdateNvidiaPage()
    {
        if (NvidiaProfileState == null) return;
        NvidiaHardwareInfo.Text = _nvidiaProfileService.GetHardwareInformation();
        if (!_nvidiaProfileService.FilesAvailable)
        {
            NvidiaProfileState.Text = "Les fichiers du profil NVIDIA sont incomplets.";
            OptimizeNvidia.IsEnabled = false;
            RestoreNvidia.IsEnabled = false;
        }
        else if (!_nvidiaProfileService.CanOptimize)
        {
            NvidiaProfileState.Text = "Carte non reconnue : aucun profil automatique disponible. Séries compatibles : GeForce RTX 2000, 3000, 4000 et 5000.";
            OptimizeNvidia.IsEnabled = false;
            RestoreNvidia.IsEnabled = _nvidiaProfileService.CanRestore;
        }
        else
        {
            NvidiaProfileState.Text = $"Profil {_nvidiaProfileService.DetectedGpuGroup} généré automatiquement. La version du pilote est informative et ne bloque pas l’application.";
            OptimizeNvidia.IsEnabled = NvidiaEnabled.IsChecked == true;
            RestoreNvidia.IsEnabled = _nvidiaProfileService.CanRestore;
        }
        RefreshNvidiaProfiles();
        NvidiaLastAction.Text = _settings.LastNvidiaOptimizationUtc.HasValue
            ? $"Dernière optimisation : {_settings.LastNvidiaOptimizationUtc.Value.ToLocalTime():dd/MM/yyyy à HH:mm:ss}"
            : "Dernière optimisation : jamais";
    }

    private async Task SaveNvidiaProfileAsync()
    {
        try
        {
            SaveNvidiaProfile.IsEnabled = false;
            NvidiaProfileState.Text = "Extraction du profil global NVIDIA en cours…";
            var snapshot = await _nvidiaProfileService.SaveCurrentProfileAsync();
            NvidiaProfileState.Text = $"Profil sauvegardé : {snapshot.SettingCount} paramètres.";
            NvidiaLastAction.Text = snapshot.Path;
            ShowTrayMessage("Profil NVIDIA", "Le profil global actuel a été sauvegardé.");
            RefreshNvidiaProfiles(snapshot.Path);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            NvidiaProfileState.Text = "La sauvegarde a été annulée dans la fenêtre UAC.";
        }
        catch (Exception ex)
        {
            AppLog.Write($"ERREUR SAUVEGARDE NVIDIA: {ex}");
            ShowVisibleMessage("Erreur NVIDIA", ex.Message);
        }
        finally { SaveNvidiaProfile.IsEnabled = true; }
    }

    private async Task OptimizeNvidiaAsync()
    {
        if (System.Windows.MessageBox.Show(this, "Appliquer le profil d’optimisation NVIDIA global ?", "Confirmation NVIDIA", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunNvidiaActionAsync(async () => { await _nvidiaProfileService.BackupThenOptimizeAsync(); }, true, "Optimisation FlexHub", "Profil d’optimisation NVIDIA appliqué. L’état précédent a été sauvegardé.");
    }

    private async Task RestoreNvidiaAsync()
    {
        if (!_nvidiaProfileService.CanRestore) { ShowVisibleMessage("Restauration NVIDIA", "Aucune sauvegarde antérieure à une optimisation n’est disponible."); return; }
        if (System.Windows.MessageBox.Show(this, "Restaurer l’état NVIDIA enregistré juste avant la dernière optimisation ?", "Restauration NVIDIA", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunNvidiaActionAsync(_nvidiaProfileService.RestorePreviousAsync, false, "Profil restauré", "État NVIDIA antérieur à l’optimisation restauré.");
    }

    private void RefreshNvidiaProfiles(string? selectPath = null)
    {
        if (NvidiaProfilesBox == null) return;
        var selectedPath = selectPath ?? (NvidiaProfilesBox.SelectedItem as NvidiaSavedProfile)?.Path;
        var profiles = _nvidiaProfileService.GetSavedProfiles();
        NvidiaProfilesBox.ItemsSource = profiles;
        NvidiaProfilesBox.SelectedItem = profiles.FirstOrDefault(profile => string.Equals(profile.Path, selectedPath, StringComparison.OrdinalIgnoreCase)) ?? profiles.FirstOrDefault();
        UpdateNvidiaProfileButtons();
    }

    private void UpdateNvidiaProfileButtons()
    {
        if (ApplySelectedNvidiaProfile == null) return;
        var profile = NvidiaProfilesBox.SelectedItem as NvidiaSavedProfile;
        ApplySelectedNvidiaProfile.IsEnabled = profile != null && NvidiaEnabled.IsChecked == true;
        DeleteSelectedNvidiaProfile.IsEnabled = profile is { IsOptimization: false };
    }

    private async Task ApplySelectedNvidiaProfileAsync()
    {
        if (NvidiaProfilesBox.SelectedItem is not NvidiaSavedProfile profile) return;
        if (System.Windows.MessageBox.Show(this, $"Appliquer le profil « {profile.Name} » ?", "Profil NVIDIA", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunNvidiaActionAsync(async () => { await _nvidiaProfileService.BackupThenApplyAsync(profile); }, false, profile.Name, $"Profil « {profile.Name} » appliqué. L’état précédent a été sauvegardé.");
    }

    private void DeleteSelectedNvidiaProfileNow()
    {
        if (NvidiaProfilesBox.SelectedItem is not NvidiaSavedProfile profile) return;
        if (profile.IsOptimization) { ShowVisibleMessage("Profil NVIDIA", "Le profil d’optimisation ne peut pas être supprimé."); return; }
        if (System.Windows.MessageBox.Show(this, $"Supprimer définitivement la sauvegarde « {profile.Name} » ?", "Suppression du profil", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { _nvidiaProfileService.DeleteProfile(profile); NvidiaProfileState.Text = "Sauvegarde supprimée."; RefreshNvidiaProfiles(); }
        catch (Exception ex) { ShowVisibleMessage("Erreur NVIDIA", ex.Message); }
    }

    private async Task RunNvidiaActionAsync(Func<Task> action, bool optimization, string activeProfileName, string successMessage)
    {
        try
        {
            OptimizeNvidia.IsEnabled = RestoreNvidia.IsEnabled = false;
            NvidiaProfileState.Text = "Traitement du profil NVIDIA en cours…";
            await action();
            if (optimization) _settings.LastNvidiaOptimizationUtc = DateTime.UtcNow;
            _settings.ActiveNvidiaProfileName = activeProfileName;
            _settingsService.Save(_settings);
            NvidiaProfileState.Text = successMessage;
            ShowTrayMessage("Optimisation NVIDIA", successMessage);
        }
        catch (Exception ex)
        {
            AppLog.Write($"ERREUR NVIDIA: {ex}");
            var message = ex is System.ComponentModel.Win32Exception { NativeErrorCode: 1223 }
                ? "L’opération NVIDIA a été annulée dans la fenêtre de contrôle de compte utilisateur."
                : ex.Message;
            ShowVisibleMessage("Erreur NVIDIA", message);
        }
        finally { UpdateNvidiaPage(); }
    }

    private void SaveXmpSettings()
    {
        var memoryType = (MemoryTypeBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "DDR4";
        var custom = XmpCustomSpeedCheck.IsChecked == true;
        var expected = memoryType == "DDR5" ? 5800 : 3200;
        if (custom && (!int.TryParse(XmpExpectedSpeedBox.Text, out expected) || expected < 800 || expected > 20000))
        {
            XmpStatus.Text = "La valeur personnalisée doit être comprise entre 800 et 20 000 MT/s.";
            return;
        }
        if (!int.TryParse(XmpIntervalBox.Text, out var interval) || interval < 1 || interval > 10080)
        {
            XmpStatus.Text = "L’intervalle doit être compris entre 1 minute et 7 jours.";
            return;
        }
        _settings.XmpMonitorEnabled = XmpEnabled.IsChecked == true;
        _settings.MemorySetupCompleted = true;
        _settings.MemoryType = memoryType;
        _settings.XmpUseCustomSpeed = custom;
        _settings.XmpExpectedSpeed = expected;
        _settings.XmpCheckIntervalMinutes = interval;
        _settingsService.Save(_settings);
        ConfigureXmpTimer();
        UpdateNavigationState();
        XmpStatus.Text = "Configuration XMP enregistrée.";
    }

    private void ConfigureXmpTimer()
    {
        _xmpTimer.Stop();
        if (_settings.XmpMonitorEnabled)
        {
            _xmpTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, _settings.XmpCheckIntervalMinutes));
            _xmpTimer.Start();
        }
    }

    private async Task CheckXmpAsync(bool manual)
    {
        if (_xmpCheckRunning || (!manual && !_settings.XmpMonitorEnabled)) return;
        try
        {
            _xmpCheckRunning = true;
            TestXmp.IsEnabled = false;
            XmpStatus.Text = "Vérification de la fréquence mémoire…";
            var (minimum, maximum) = GetExpectedMemoryRange();
            var result = await _xmpMonitorService.CheckAsync(minimum, maximum);
            _settings.LastXmpCheckUtc = DateTime.UtcNow;
            _settingsService.Save(_settings);
            XmpStatus.Text = result.Message;
            XmpStatus.Foreground = new System.Windows.Media.SolidColorBrush(result.IsProbablyActive
                ? System.Windows.Media.Color.FromRgb(124, 228, 194)
                : System.Windows.Media.Color.FromRgb(240, 174, 114));
            UpdateXmpLastCheck();
            if (result.IsAvailable && !result.IsProbablyActive)
                ShowPersistentXmpAlert(result.MinimumSpeed, _settings.XmpExpectedSpeed);
        }
        catch (TaskCanceledException) { XmpStatus.Text = "La vérification XMP a dépassé le délai autorisé."; }
        catch (Exception ex)
        {
            AppLog.Write($"ERREUR XMP: {ex}");
            XmpStatus.Text = $"Vérification impossible : {ex.Message}";
            if (manual) ShowVisibleMessage("Surveillance XMP", XmpStatus.Text);
        }
        finally { _xmpCheckRunning = false; TestXmp.IsEnabled = true; }
    }

    private void UpdateXmpLastCheck()
    {
        XmpLastCheck.Text = _settings.LastXmpCheckUtc.HasValue
            ? $"Dernière vérification : {_settings.LastXmpCheckUtc.Value.ToLocalTime():dd/MM/yyyy à HH:mm:ss}"
            : "Dernière vérification : jamais";
    }

    private void ShowPersistentXmpAlert(int detectedSpeed, int expectedSpeed)
    {
        if (_xmpAlertWindow != null)
        {
            _xmpAlertWindow.Close();
            _xmpAlertWindow = null;
        }
        _xmpAlertWindow = new XmpAlertWindow(detectedSpeed, expectedSpeed);
        _xmpAlertWindow.Closed += (_, _) => _xmpAlertWindow = null;
        _xmpAlertWindow.Show();
    }

    private (int Minimum, int Maximum) GetExpectedMemoryRange()
    {
        if (_settings.XmpUseCustomSpeed) return (_settings.XmpExpectedSpeed, _settings.XmpExpectedSpeed);
        return _settings.MemoryType == "DDR5" ? (5800, 7400) : (3200, 3600);
    }

    private void ShowMemorySetupIfNeeded()
    {
        if (_settings.MemorySetupCompleted) return;
        var dialog = new MemorySetupWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _settings.MemorySetupCompleted = true;
        _settings.MemoryType = dialog.MemoryType;
        _settings.XmpMonitorEnabled = dialog.MonitoringEnabled;
        _settings.XmpUseCustomSpeed = dialog.UseCustomSpeed;
        _settings.XmpExpectedSpeed = dialog.CustomSpeed;
        _settingsService.Save(_settings);
        XmpEnabled.IsChecked = _settings.XmpMonitorEnabled;
        MemoryTypeBox.SelectedIndex = _settings.MemoryType == "DDR5" ? 1 : 0;
        XmpCustomSpeedCheck.IsChecked = _settings.XmpUseCustomSpeed;
        XmpExpectedSpeedBox.Text = _settings.XmpExpectedSpeed.ToString();
        UpdateXmpSpeedControls();
        ConfigureXmpTimer();
    }

    private void ShowGeminiSetupIfNeeded()
    {
        if (_settings.GeminiSetupCompleted) return;
        if (SecretStore.HasKey("Gemini"))
        {
            _settings.GeminiSetupCompleted = true;
            _settingsService.Save(_settings);
            return;
        }
        var dialog = new GeminiSetupWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        SecretStore.Save("Gemini", dialog.ApiKey);
        _settings.GeminiSetupCompleted = true;
        _settingsService.Save(_settings);
        RefreshGeneralApiKeyBoxes();
        UpdateProviderPanel();
        UpdateTranslationProviderPanel();
        UpdateResponseGeneratorModel();
    }

    private void MemoryTypeBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateXmpSpeedControls();
    private void XmpCustomSpeedChanged(object sender, RoutedEventArgs e) => UpdateXmpSpeedControls();

    private void UpdateXmpSpeedControls()
    {
        if (XmpExpectedSpeedBox == null || XmpCustomSpeedCheck == null || MemoryTypeBox == null) return;
        var custom = XmpCustomSpeedCheck.IsChecked == true;
        XmpExpectedSpeedBox.IsEnabled = custom;
        if (!custom)
        {
            var ddr5 = MemoryTypeBox.SelectedIndex == 1;
            XmpExpectedSpeedBox.Text = ddr5 ? "5800 à 7400" : "3200 à 3600";
        }
        else if (!int.TryParse(XmpExpectedSpeedBox.Text, out _))
        {
            XmpExpectedSpeedBox.Text = MemoryTypeBox.SelectedIndex == 1 ? "5800" : "3200";
        }
    }

    private bool EnsureOnlineServicesConsent()
    {
        if (_settings.AiPrivacyConsentAccepted) return true;
        ShowVisibleMessage("Confidentialité", "Avant d’utiliser un service en ligne, acceptez dans les paramètres généraux la transmission du texte sélectionné au fournisseur choisi.");
        return false;
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            CheckUpdates.IsEnabled = false;
            DownloadUpdate.Visibility = Visibility.Collapsed;
            UpdateStatus.Text = "Recherche d’une nouvelle version…";
            _settings.LastUpdateCheckUtc = DateTime.UtcNow;
            _settingsService.Save(_settings);
            _availableUpdate = await _updateService.CheckAsync();
            if (_availableUpdate.IsNewer)
            {
                await InstallAvailableUpdateAsync();
            }
            else UpdateStatus.Text = $"Vous utilisez la dernière version ({UpdateService.CurrentVersion}).";
        }
        catch (TaskCanceledException) { UpdateStatus.Text = "La vérification a dépassé le délai autorisé."; }
        catch (Exception ex) { UpdateStatus.Text = $"Vérification impossible : {ex.Message}"; }
        finally { CheckUpdates.IsEnabled = true; }
    }

    private async Task CheckForUpdatesAtStartupAsync()
    {
        try
        {
            _availableUpdate = await _updateService.CheckAsync();
            if (!_availableUpdate.IsNewer) return;
            await InstallAvailableUpdateAsync();
        }
        catch (Exception ex)
        {
            AppLog.Write($"Vérification automatique des mises à jour impossible : {ex.Message}");
        }
    }

    private async Task InstallAvailableUpdateAsync()
    {
        if (_updateInstallRunning || _availableUpdate is not { IsNewer: true } update) return;
        _updateInstallRunning = true;
        CheckUpdates.IsEnabled = false;
        DownloadUpdate.IsEnabled = false;
        DownloadUpdate.Visibility = Visibility.Collapsed;
        try
        {
            UpdateStatus.Text = $"Téléchargement et vérification de FlexHub {update.LatestVersion}…";
            AppLog.Write($"MISE À JOUR AUTOMATIQUE | Téléchargement de {update.LatestVersion}");
            await _updateService.DownloadAndStartInstallerAsync(update);
            AppLog.Write($"MISE À JOUR AUTOMATIQUE | Installeur {update.LatestVersion} vérifié et lancé");
            ExitHub();
        }
        catch (Exception ex)
        {
            AppLog.WriteException("ÉCHEC DE LA MISE À JOUR AUTOMATIQUE", ex);
            UpdateStatus.Text = $"Installation automatique impossible : {ex.Message}";
            DownloadUpdate.Visibility = Visibility.Visible;
            DownloadUpdate.IsEnabled = true;
            _updateInstallRunning = false;
            CheckUpdates.IsEnabled = true;
        }
    }

    private void OpenSelectedPatchNotes()
    {
        if (VersionHistoryBox.SelectedItem is not VersionNote note)
        {
            ShowVisibleMessage("Notes de version", "Aucune note n’est disponible pour cette version.");
            return;
        }
        new PatchNotesWindow(note) { Owner = this }.ShowDialog();
    }

    private void ConfigureReminderTimer()
    {
        _reminderTimer.Stop();
        if (!_settings.ReminderEnabled)
        {
            return;
        }

        var reminderInterval = TimeSpan.FromMinutes(Math.Max(1, _settings.ReminderIntervalMinutes));
        var remainingTime = _settings.LastReminderUtc.HasValue
            ? _settings.LastReminderUtc.Value.Add(reminderInterval) - DateTime.UtcNow
            : reminderInterval;

        // DispatcherTimer n'accepte pas une durée nulle. Si le rappel est déjà
        // en retard au démarrage, il est déclenché presque immédiatement.
        _reminderTimer.Interval = remainingTime > TimeSpan.Zero
            ? remainingTime
            : TimeSpan.FromSeconds(1);
        _reminderTimer.Start();
    }

    private void SaveNetworkMonitoringTargets()
    {
        var targets = NetworkTargetsBox.Text.Trim();
        if (targets.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length == 0)
        {
            NetworkMonitoringStatus.Text = "Ajoutez au moins une cible valide.";
            return;
        }

        _settings.NetworkMonitoringTargets = targets;
        _settingsService.Save(_settings);
        NetworkMonitoringStatus.Text = "Cibles enregistrées.";
    }

    private void SaveGameSessionAlertSettings()
    {
        if (!int.TryParse(GameSessionAlertHoursBox.Text.Trim(), out var hours) || hours is < 1 or > 24)
        {
            ActiveGameSessionSummary.Text = "Choisissez une durée de 1 à 24 heures.";
            return;
        }
        _settings.GameSessionAlertHours = hours;
        _settings.GameSessionAlertEnabled = GameSessionAlertEnabled.IsChecked == true;
        _settingsService.Save(_settings);
        _alertedGameSessionProcessIds.Clear();
        ActiveGameSessionSummary.Text = _settings.GameSessionAlertEnabled
            ? $"Alerte activée après {hours} h."
            : "Alerte de pause désactivée.";
    }

    private void SaveGamePerformanceModeSettings()
    {
        var enable = AutomaticGameHighPriorityEnabled.IsChecked == true;
        if (enable && !_settings.AutomaticGameHighPriorityEnabled)
        {
            var activeGames = _gameSessionService.DetectActiveSessions();
            var preview = activeGames.Count == 0
                ? "Aucun jeu n’est actif actuellement. Le mode s’appliquera au prochain jeu détecté."
                : "Jeux concernés actuellement :\n• " + string.Join("\n• ", activeGames.Select(game => game.Name));
            var confirmation = System.Windows.MessageBox.Show(this,
                "FlexHub appliquera la priorité CPU Haute aux jeux détectés. La priorité Temps réel n’est jamais utilisée.\n\n" +
                preview + "\n\nLa priorité d’origine sera restaurée à la désactivation ou à la fermeture de FlexHub.",
                "Activer le mode performance", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
            {
                AutomaticGameHighPriorityEnabled.IsChecked = false;
                return;
            }
        }
        _settings.AutomaticGameHighPriorityEnabled = enable;
        _settingsService.Save(_settings);
        var result = _gamePerformanceService.Update(_gameSessionService.DetectActiveSessions(), enable);
        GamePerformanceStatus.Text = result.Message;
    }

    private void SaveNetworkJitterAlertSettings()
    {
        if (!int.TryParse(NetworkJitterAlertMsBox.Text.Trim(), out var threshold) || threshold is < 5 or > 200)
        {
            NetworkDiagnosticText.Text = "Le seuil de jitter doit être compris entre 5 et 200 ms.";
            return;
        }
        _settings.NetworkJitterAlertEnabled = NetworkJitterAlertEnabled.IsChecked == true;
        _settings.NetworkJitterAlertMs = threshold;
        _settingsService.Save(_settings);
        _networkAnomalySamples = 0;
        NetworkDiagnosticText.Text = _settings.NetworkJitterAlertEnabled
            ? $"Alerte jitter activée à partir de {threshold} ms."
            : "Alerte jitter désactivée.";
    }

    private async Task RefreshGameSessionsAsync(bool force = false)
    {
        if (!_settings.GameSessionsModuleEnabled)
        {
            _gamePerformanceService.Update([], false);
            return;
        }
        if (_gameSessionRefreshRunning || (!force && DateTime.UtcNow - _lastGameSessionRefreshUtc < TimeSpan.FromSeconds(15))) return;
        _gameSessionRefreshRunning = true;
        try
        {
            var detectedSessions = await Task.Run(_gameSessionService.DetectActiveSessions);
            var processMetrics = await Task.Run(() => _gameProcessMonitoringService.Capture(detectedSessions));
            var sessions = detectedSessions.Select(session => processMetrics.TryGetValue(session.ProcessId, out var metrics)
                ? session with
                {
                    ProcessCpuPercent = metrics.CpuPercent,
                    ProcessRamMb = metrics.RamMb,
                    ProcessGpuPercent = metrics.GpuPercent,
                    ProcessVramMb = metrics.VramMb
                }
                : session).ToArray();
            _lastGameSessionRefreshUtc = DateTime.UtcNow;
            ActiveGameSessionsList.ItemsSource = sessions;
            _gameSessionHistoryService.Update(sessions, _settings.ActiveNvidiaProfileName);
            _gameSessionHistoryService.RecordProcessMetrics(processMetrics.Values);
            GameSessionHistoryList.ItemsSource = _gameSessionHistoryService.GetRecentReports();
            var performance = _gamePerformanceService.Update(sessions, _settings.AutomaticGameHighPriorityEnabled);
            GamePerformanceStatus.Text = performance.Message;
            RefreshGameSessionSummary();
            ActiveGameSessionEmpty.Visibility = sessions.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ActiveGameSessionSummary.Text = sessions.Length == 0
                ? "Aucune session de jeu détectée."
                : sessions.Length == 1 ? "1 jeu actif" : $"{sessions.Length} jeux actifs";
            var activeIds = sessions.Select(session => session.ProcessId).ToHashSet();
            _alertedGameSessionProcessIds.RemoveWhere(processId => !activeIds.Contains(processId));
            if (!_settings.GameSessionAlertEnabled) return;
            var threshold = TimeSpan.FromHours(_settings.GameSessionAlertHours);
            foreach (var session in sessions.Where(session => session.Duration >= threshold &&
                         !_alertedGameSessionProcessIds.Contains(session.ProcessId)))
            {
                _alertedGameSessionProcessIds.Add(session.ProcessId);
                ShowGameSessionAlert("Pause conseillée",
                    $"Vous jouez à {session.Name} depuis {session.DurationText}. Pensez à faire une pause.");
            }
        }
        finally
        {
            _gameSessionRefreshRunning = false;
        }
    }

    private void RefreshGameSessionSummary()
    {
        if (WeeklyGameSummaryList is null || GameSessionPeriodBox is null) return;
        var now = DateTime.Now;
        DateTime? start = GameSessionPeriodBox.SelectedIndex switch
        {
            0 => now.AddDays(-7),
            1 => now.AddDays(-30),
            2 => new DateTime(now.Year, 1, 1),
            _ => null
        };
        _currentGameSummaries = _gameSessionHistoryService.GetSummary(start);
        WeeklyGameSummaryList.ItemsSource = _currentGameSummaries;
        DrawGameTimeChart();
    }

    private void GameTimeChart_OnSizeChanged(object sender, SizeChangedEventArgs e) => DrawGameTimeChart();

    private void DrawGameTimeChart()
    {
        if (GameTimeChart is null || GameTimeChart.ActualWidth < 100 || GameTimeChart.ActualHeight < 80) return;
        GameTimeChart.Children.Clear();
        const double left = 42;
        const double bottom = 32;
        const double top = 10;
        var width = GameTimeChart.ActualWidth - left - 8;
        var height = GameTimeChart.ActualHeight - top - bottom;
        var summaries = _currentGameSummaries.Take(8).ToArray();
        var maximumHours = Math.Max(1, Math.Ceiling(summaries.Select(item => item.TotalDuration.TotalHours).DefaultIfEmpty(0).Max()));

        for (var step = 0; step <= 4; step++)
        {
            var y = top + height - height * step / 4d;
            var value = maximumHours * step / 4d;
            GameTimeChart.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = left, X2 = left + width, Y1 = y, Y2 = y,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 210, 205, 200)), StrokeThickness = 1
            });
            var scale = new System.Windows.Controls.TextBlock
            {
                Text = $"{value:0.#} h", FontSize = 10,
                Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush")
            };
            System.Windows.Controls.Canvas.SetLeft(scale, 2);
            System.Windows.Controls.Canvas.SetTop(scale, y - 7);
            GameTimeChart.Children.Add(scale);
        }

        if (summaries.Length == 0) return;
        var slot = width / summaries.Length;
        var barWidth = Math.Min(70, slot * 0.58);
        for (var index = 0; index < summaries.Length; index++)
        {
            var item = summaries[index];
            var barHeight = Math.Max(3, item.TotalDuration.TotalHours / maximumHours * height);
            var x = left + index * slot + (slot - barWidth) / 2;
            var rectangle = new System.Windows.Shapes.Rectangle
            {
                Width = barWidth, Height = barHeight, RadiusX = 4, RadiusY = 4,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 162, 76)),
                ToolTip = $"{item.GameName} : {item.TotalDurationText}"
            };
            System.Windows.Controls.Canvas.SetLeft(rectangle, x);
            System.Windows.Controls.Canvas.SetTop(rectangle, top + height - barHeight);
            GameTimeChart.Children.Add(rectangle);
            var label = new System.Windows.Controls.TextBlock
            {
                Text = item.GameName.Length > 12 ? item.GameName[..11] + "…" : item.GameName,
                FontSize = 10, Width = slot, TextAlignment = TextAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush")
            };
            System.Windows.Controls.Canvas.SetLeft(label, left + index * slot);
            System.Windows.Controls.Canvas.SetTop(label, top + height + 7);
            GameTimeChart.Children.Add(label);
        }
    }

    private void ClearGameSessionHistory_OnClick()
    {
        var confirmation = System.Windows.MessageBox.Show(this,
            "Effacer tout l’historique des sessions de jeu ? Cette action est irréversible.",
            "Effacer l’historique", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;
        _gameSessionHistoryService.Clear();
        GameSessionHistoryList.ItemsSource = Array.Empty<GameSessionReport>();
        WeeklyGameSummaryList.ItemsSource = Array.Empty<WeeklyGameSummary>();
        _currentGameSummaries = Array.Empty<WeeklyGameSummary>();
        DrawGameTimeChart();
        ActiveGameSessionSummary.Text = "Historique effacé.";
        AppLog.Write("Historique des sessions de jeu effacé par l’utilisateur.");
    }

    private void SetReferenceSession_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { DataContext: GameSessionReport report }) return;
        if (!_gameSessionHistoryService.SetReference(report))
        {
            ActiveGameSessionSummary.Text = "Terminez la session avant de l’utiliser comme référence.";
            return;
        }
        GameSessionHistoryList.ItemsSource = null;
        GameSessionHistoryList.ItemsSource = _gameSessionHistoryService.GetRecentReports();
        ActiveGameSessionSummary.Text = $"Référence enregistrée pour {report.GameName}.";
    }

    private void ExportGameSessionHistory_OnClick()
    {
        var reports = _gameSessionHistoryService.GetAllReports();
        if (reports.Count == 0)
        {
            ActiveGameSessionSummary.Text = "Aucune session à exporter.";
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exporter les sessions de jeu",
            Filter = "Fichier CSV (*.csv)|*.csv",
            FileName = $"sessions-jeu-{DateTime.Now:yyyy-MM-dd}.csv",
            DefaultExt = ".csv",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true) return;
        static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var lines = new List<string>
        {
            "Jeu;Reference;Profil_NVIDIA;Debut;Fin;Duree_minutes;PC_CPU_max_pct;PC_RAM_max_pct;PC_GPU_moy_pct;PC_GPU_max_pct;GPU_max_C;Jeu_CPU_max_pct;Jeu_RAM_max_Mo;Jeu_GPU_moy_pct;Jeu_GPU_max_pct;Jeu_VRAM_max_Mo"
        };
        foreach (var report in reports)
        {
            var end = report.IsActive ? DateTime.Now : report.LastSeenAt;
            var duration = Math.Max(0, (end - report.StartedAt).TotalMinutes);
            var gpuAverage = report.GpuUsageSampleCount > 0 ? report.GpuUsageTotal / report.GpuUsageSampleCount : (double?)null;
            lines.Add(string.Join(';', Csv(report.GameName), report.IsReference ? "oui" : "non", Csv(report.NvidiaProfileName), report.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                end.ToString("yyyy-MM-dd HH:mm:ss"), duration.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                report.PeakCpuUsagePercent?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                report.PeakRamUsagePercent?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                gpuAverage?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                report.PeakGpuUsagePercent?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                report.PeakGpuTemperatureC?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                report.PeakProcessCpuPercent?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                report.PeakProcessRamMb?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                report.ProcessGpuUsageSampleCount > 0 ? (report.ProcessGpuUsageTotal / report.ProcessGpuUsageSampleCount).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "",
                report.PeakProcessGpuPercent?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                report.PeakProcessVramMb?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? ""));
        }
        try
        {
            File.WriteAllLines(dialog.FileName, lines, new System.Text.UTF8Encoding(true));
            ActiveGameSessionSummary.Text = $"Historique exporté : {Path.GetFileName(dialog.FileName)}";
            AppLog.Write($"Historique des sessions exporté vers {dialog.FileName}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ActiveGameSessionSummary.Text = $"Export impossible : {ex.Message}";
            AppLog.Write($"Export des sessions impossible : {ex.Message}");
        }
    }

    private async Task RefreshNetworkMonitoringAsync()
    {
        if (_networkMonitoringRunning) return;
        _networkMonitoringRunning = true;
        RefreshNetworkMonitoring.IsEnabled = false;
        NetworkMonitoringStatus.Text = "Mesure en arrière-plan (4 paquets par cible)…";
        try
        {
            var targets = NetworkTargetsBox.Text.Split(';',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var results = await _networkMonitoringService.MeasureAsync(targets);
            NetworkResultsList.ItemsSource = results;
            NetworkMonitoringStatus.Text = results.Count == 0
                ? "Aucune cible utilisable. Vérifiez la passerelle ou les adresses saisies."
                : $"Dernière mesure à {DateTime.Now:HH:mm:ss} · {results.Count} cible(s).";
            UpdateNetworkDiagnosis(results);
            RecordNetworkHistory(results);
        }
        catch (Exception ex)
        {
            NetworkMonitoringStatus.Text = $"Mesure réseau impossible : {ex.Message}";
            AppLog.Write($"Monitoring réseau indisponible : {ex.Message}");
        }
        finally
        {
            RefreshNetworkMonitoring.IsEnabled = true;
            _networkMonitoringRunning = false;
        }
    }

    private void RefreshNetworkApplications()
    {
        var previousProcessId = (NetworkApplicationBox.SelectedItem as NetworkApplication)?.ProcessId;
        var applications = _networkMonitoringService.GetActiveApplications();
        NetworkApplicationBox.ItemsSource = applications;
        NetworkApplicationBox.SelectedItem = applications.FirstOrDefault(item => item.ProcessId == previousProcessId);
        if (NetworkApplicationBox.SelectedItem is null && applications.Count > 0)
            NetworkApplicationBox.SelectedIndex = 0;
        AnalyzeNetworkApplication.IsEnabled = applications.Count > 0;
        NetworkApplicationStatus.Text = applications.Count == 0
            ? "Aucune application avec du trafic réseau actif."
            : $"{applications.Count} application(s) détectée(s), sans élévation administrateur.";
    }

    private async Task AnalyzeNetworkApplicationAsync()
    {
        if (NetworkApplicationBox.SelectedItem is not NetworkApplication application) return;
        AnalyzeNetworkApplication.IsEnabled = false;
        NetworkApplicationStatus.Text = $"Analyse de {application.DisplayName}…";
        try
        {
            var results = await _networkMonitoringService.MeasureApplicationAsync(application.ProcessId);
            NetworkResultsList.ItemsSource = results;
            NetworkApplicationStatus.Text = results.Count == 0
                ? "Aucun serveur TCP public mesurable pour cette application. Réessayez pendant son utilisation."
                : $"Analyse terminée à {DateTime.Now:HH:mm:ss} · {results.Count} serveur(s).";
            var measurable = results.Where(result => result.StatusOverride is null).ToArray();
            NetworkDiagnosticText.Text = results.Count == 0
                ? "Diagnostic application : aucune destination publique mesurable pendant cet instantané."
                : results.All(result => result.StatusOverride is not null)
                    ? "Diagnostic application : trafic UDP détecté, mais Windows n’expose pas sa destination distante."
                    : measurable.Any(result => result.SuccessfulSamples == 0 || result.PacketLossPercent > 0 ||
                                               result.JitterMs > 30 || result.AveragePingMs > 120)
                        ? "Diagnostic application : au moins une destination présente une latence, un jitter ou des pertes élevés."
                        : "Diagnostic application : les destinations mesurables répondent normalement.";
            RecordNetworkHistory(results);
        }
        catch (InvalidOperationException ex)
        {
            NetworkApplicationStatus.Text = ex.Message;
            RefreshNetworkApplications();
        }
        finally
        {
            AnalyzeNetworkApplication.IsEnabled = NetworkApplicationBox.Items.Count > 0;
        }
    }

    private void UpdateNetworkDiagnosis(IReadOnlyList<NetworkTargetMetrics> results)
    {
        var gateway = results.FirstOrDefault(result => result.Name.StartsWith("Box /", StringComparison.OrdinalIgnoreCase));
        var internet = results.FirstOrDefault(result => result.Name.Equals("Internet", StringComparison.OrdinalIgnoreCase));
        var game = results.FirstOrDefault(result => result.Name.StartsWith("Jeu ·", StringComparison.OrdinalIgnoreCase));

        var gatewayUnstable = gateway is not null && (gateway.SuccessfulSamples == 0 ||
            gateway.PacketLossPercent > 0 || gateway.JitterMs > 10 || gateway.AveragePingMs > 15);
        var internetUnstable = internet is not null && (internet.SuccessfulSamples == 0 ||
            internet.PacketLossPercent > 0 || internet.JitterMs > 30 || internet.AveragePingMs > 120);
        var gameUnstable = game is { StatusOverride: null } && (game.SuccessfulSamples == 0 ||
            game.PacketLossPercent > 0 || game.JitterMs > 30 || game.AveragePingMs > 120);

        NetworkDiagnosticText.Text = gateway is null || internet is null
            ? "Diagnostic incomplet : ajoutez « passerelle » et « Internet=1.1.1.1 » aux cibles pour séparer un problème local d’un problème extérieur."
            : gatewayUnstable
                ? "Diagnostic local : problème probable entre ce PC et la box (Wi-Fi, câble, carte réseau ou routeur)."
                : internetUnstable
                    ? "Diagnostic Internet : la box répond correctement, mais la connexion extérieure est instable ou indisponible."
                    : gameUnstable
                        ? "Diagnostic serveur distant : le réseau local et Internet fonctionnent, mais le trajet vers le serveur du jeu est instable ou refuse le ping."
                        : game is { StatusOverride: not null }
                            ? "Diagnostic partiel : réseau local et Internet stables ; le serveur UDP du jeu ne peut pas être mesuré directement."
                            : "Diagnostic stable : réseau local et accès Internet fonctionnent normalement.";

        var measurable = results.Where(result => result.StatusOverride is null && result.SuccessfulSamples > 0).ToArray();
        var anomaly = measurable.Any(result => result.PacketLossPercent >= 5 ||
            (_settings.NetworkJitterAlertEnabled && result.JitterMs >= _settings.NetworkJitterAlertMs) ||
            result.AveragePingMs >= 120);
        _networkAnomalySamples = anomaly ? _networkAnomalySamples + 1 : 0;
        if (_networkAnomalySamples < 3 || DateTime.UtcNow - _lastNetworkAlertUtc < TimeSpan.FromMinutes(15)) return;

        _lastNetworkAlertUtc = DateTime.UtcNow;
        _networkAnomalySamples = 0;
        var worst = measurable.OrderByDescending(result => result.PacketLossPercent)
            .ThenByDescending(result => result.JitterMs).ThenByDescending(result => result.AveragePingMs).First();
        ShowNetworkAlert("Alerte réseau",
            $"Instabilité sur {worst.Name} : {worst.PingText}, jitter {worst.JitterText}, pertes {worst.LossText}.");
    }

    private void RecordNetworkHistory(IReadOnlyList<NetworkTargetMetrics> results)
    {
        var source = results.FirstOrDefault(result => result.Name.StartsWith("Jeu ·", StringComparison.OrdinalIgnoreCase) && result.SuccessfulSamples > 0)
            ?? results.FirstOrDefault(result => result.Name.Equals("Internet", StringComparison.OrdinalIgnoreCase) && result.SuccessfulSamples > 0)
            ?? results.FirstOrDefault(result => result.StatusOverride is null && result.SuccessfulSamples > 0);
        if (source is null) return;
        EnqueueNetworkValue(_networkPingHistory, Math.Min(200, source.AveragePingMs));
        EnqueueNetworkValue(_networkJitterHistory, Math.Min(200, source.JitterMs));
        EnqueueNetworkValue(_networkLossHistory, Math.Min(100, source.PacketLossPercent));
        _networkHistoryTimestamps.Enqueue(DateTime.Now);
        while (_networkHistoryTimestamps.Count > 60) _networkHistoryTimestamps.Dequeue();
        _networkHistoryService.Add(new NetworkHistorySample(DateTime.Now, source.Name,
            source.AveragePingMs, source.JitterMs, source.PacketLossPercent));
        DrawNetworkHistory();
    }

    private void LoadNetworkHistory()
    {
        foreach (var sample in _networkHistoryService.GetRecent())
        {
            _networkPingHistory.Enqueue(Math.Min(200, sample.PingMs));
            _networkJitterHistory.Enqueue(Math.Min(200, sample.JitterMs));
            _networkLossHistory.Enqueue(Math.Min(100, sample.PacketLossPercent));
            _networkHistoryTimestamps.Enqueue(sample.RecordedAt);
        }
    }

    private void ClearNetworkHistoryNow()
    {
        var confirmation = System.Windows.MessageBox.Show(this,
            "Effacer les 60 dernières mesures réseau ?", "Effacer l’historique",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;
        _networkHistoryService.Clear();
        _networkPingHistory.Clear();
        _networkJitterHistory.Clear();
        _networkLossHistory.Clear();
        _networkHistoryTimestamps.Clear();
        DrawNetworkHistory();
        NetworkMonitoringStatus.Text = "Historique réseau effacé.";
    }

    private static void EnqueueNetworkValue(Queue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > 60) queue.Dequeue();
    }

    private void NetworkHistoryChart_OnSizeChanged(object sender, SizeChangedEventArgs e) => DrawNetworkHistory();

    private void NetworkHistoryChart_OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var ping = _networkPingHistory.ToArray();
        var jitter = _networkJitterHistory.ToArray();
        var loss = _networkLossHistory.ToArray();
        var times = _networkHistoryTimestamps.ToArray();
        if (ping.Length == 0 || NetworkHistoryChart.ActualWidth <= 42) return;

        const double left = 42;
        var plotWidth = NetworkHistoryChart.ActualWidth - left;
        var position = e.GetPosition(NetworkHistoryChart);
        var ratio = Math.Clamp((position.X - left) / plotWidth, 0, 1);
        var index = ping.Length == 1 ? 0 : (int)Math.Round(ratio * (ping.Length - 1));
        var x = left + (ping.Length == 1 ? 0 : index * plotWidth / (ping.Length - 1));

        NetworkHoverLine.X1 = x;
        NetworkHoverLine.X2 = x;
        NetworkHoverLine.Y1 = 0;
        NetworkHoverLine.Y2 = Math.Max(0, NetworkHistoryChart.ActualHeight - 18);
        NetworkHoverText.Text = $"{times[index]:HH:mm:ss}\nPing  {ping[index]:0.0} ms\nJitter  {jitter[index]:0.0} ms\nPertes  {loss[index]:0.0} %";
        NetworkHoverLine.Visibility = Visibility.Visible;
        NetworkHoverTooltip.Visibility = Visibility.Visible;
        NetworkHoverTooltip.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var tooltipWidth = NetworkHoverTooltip.DesiredSize.Width;
        var tooltipLeft = x + 10;
        if (tooltipLeft + tooltipWidth > NetworkHistoryChart.ActualWidth)
            tooltipLeft = x - tooltipWidth - 10;
        System.Windows.Controls.Canvas.SetLeft(NetworkHoverTooltip, Math.Max(left, tooltipLeft));
        System.Windows.Controls.Canvas.SetTop(NetworkHoverTooltip, 8);
    }

    private void NetworkHistoryChart_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        NetworkHoverLine.Visibility = Visibility.Collapsed;
        NetworkHoverTooltip.Visibility = Visibility.Collapsed;
    }

    private void DrawNetworkHistory()
    {
        if (NetworkHistoryChart.ActualWidth <= 0 || NetworkHistoryChart.ActualHeight <= 0) return;
        var maximumValue = _networkPingHistory.Concat(_networkJitterHistory).Concat(_networkLossHistory)
            .DefaultIfEmpty(0).Max();
        var scaleMaximum = Math.Max(25, Math.Ceiling(maximumValue / 25d) * 25);
        DrawNetworkScale(scaleMaximum);
        DrawNetworkSeries(NetworkPingLine, _networkPingHistory, scaleMaximum);
        DrawNetworkSeries(NetworkJitterLine, _networkJitterHistory, scaleMaximum);
        DrawNetworkSeries(NetworkLossLine, _networkLossHistory, scaleMaximum);
    }

    private void DrawNetworkScale(double scaleMaximum)
    {
        foreach (var element in NetworkHistoryChart.Children.OfType<FrameworkElement>()
                     .Where(element => Equals(element.Tag, "NetworkScale")).ToArray())
            NetworkHistoryChart.Children.Remove(element);

        const double left = 42;
        const double bottom = 18;
        var plotHeight = Math.Max(1, NetworkHistoryChart.ActualHeight - bottom);
        for (var step = 0; step <= 4; step++)
        {
            var value = scaleMaximum * step / 4d;
            var y = plotHeight - plotHeight * step / 4d;
            var gridLine = new System.Windows.Shapes.Line
            {
                X1 = left, X2 = NetworkHistoryChart.ActualWidth, Y1 = y, Y2 = y,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(55, 190, 185, 180)),
                StrokeThickness = 1, Tag = "NetworkScale"
            };
            var label = new System.Windows.Controls.TextBlock
            {
                Text = $"{value:0}", FontSize = 10, Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"),
                Tag = "NetworkScale"
            };
            System.Windows.Controls.Canvas.SetLeft(label, 2);
            System.Windows.Controls.Canvas.SetTop(label, Math.Clamp(y - 7, 0, plotHeight));
            NetworkHistoryChart.Children.Add(gridLine);
            NetworkHistoryChart.Children.Add(label);
            System.Windows.Controls.Panel.SetZIndex(gridLine, 0);
        }
    }

    private void DrawNetworkSeries(System.Windows.Shapes.Polyline line, Queue<double> values, double scaleMaximum)
    {
        line.Points.Clear();
        var samples = values.ToArray();
        if (samples.Length == 0) return;
        const double left = 42;
        const double bottom = 18;
        var width = Math.Max(1, NetworkHistoryChart.ActualWidth - left);
        var height = Math.Max(1, NetworkHistoryChart.ActualHeight - bottom);
        for (var index = 0; index < samples.Length; index++)
        {
            var x = left + (samples.Length == 1 ? 0 : index * width / (samples.Length - 1));
            var y = height - Math.Clamp(samples[index], 0, scaleMaximum) / scaleMaximum * height;
            line.Points.Add(new System.Windows.Point(x, y));
        }
    }

    private void ShowNetworkAlert(string title, string message)
    {
        _monitoringAlertWindow?.Close();
        _monitoringAlertWindow = new MonitoringAlertWindow(title, message);
        _monitoringAlertWindow.OpenMonitoringRequested += (_, _) =>
        {
            ShowHub();
            ShowPage("networkMonitoring");
        };
        _monitoringAlertWindow.Closed += (_, _) => _monitoringAlertWindow = null;
        _monitoringAlertWindow.Show();
    }

    private void ShowGameSessionAlert(string title, string message)
    {
        _monitoringAlertWindow?.Close();
        _monitoringAlertWindow = new MonitoringAlertWindow(title, message);
        _monitoringAlertWindow.OpenMonitoringRequested += (_, _) =>
        {
            ShowHub();
            ShowPage("gameSessions");
        };
        _monitoringAlertWindow.Closed += (_, _) => _monitoringAlertWindow = null;
        _monitoringAlertWindow.Show();
    }

    private async Task RefreshMonitoringAsync()
    {
        if (!_settings.MonitoringEnabled) return;
        if (_monitoringRefreshRunning) return;
        _monitoringRefreshRunning = true;
        RefreshMonitoring.IsEnabled = false;
        try
        {
            var metrics = await _systemMonitoringService.CaptureAsync();
            _gameSessionHistoryService.RecordPerformanceMetrics(metrics.CpuPercent, metrics.RamPercent,
                metrics.GpuPercent, metrics.GpuTemperatureC);
            MonitorCpuText.Text = $"{metrics.CpuPercent:0}%";
            MonitorCpuDetail.Text = metrics.TopCpuProcessText is null ? "Mesure du processus…" : "Top : " + metrics.TopCpuProcessText;
            MonitorRamText.Text = $"{metrics.RamPercent:0}%";
            MonitorRamDetail.Text = $"{metrics.RamUsedGb:0.0} / {metrics.RamTotalGb:0.0} Go";
            MonitorRamProcessDetail.Text = metrics.TopRamProcessText is null ? string.Empty : "Top : " + metrics.TopRamProcessText;
            MonitorGpuText.Text = metrics.GpuPercent.HasValue
                ? $"{metrics.GpuPercent:0}%  ·  {metrics.GpuTemperatureC:0} °C"
                : "Indisponible";
            MonitorVramText.Text = metrics.VramPercent.HasValue ? $"{metrics.VramPercent:0}%" : "Indisponible";
            MonitorVramDetail.Text = metrics.VramUsedGb.HasValue
                ? $"{metrics.VramUsedGb:0.0} / {metrics.VramTotalGb:0.0} Go"
                : "Capteurs GPU compatibles non détectés";
            MonitoringStatus.Text = $"Dernière mesure à {metrics.CapturedAt:HH:mm:ss}";
            AddHistory(_cpuHistory, metrics.CpuPercent);
            AddHistory(_ramHistory, metrics.RamPercent);
            AddHistory(_gpuHistory, metrics.GpuPercent ?? 0);
            _monitoringTimestamps.Enqueue(metrics.CapturedAt);
            while (_monitoringTimestamps.Count > 60) _monitoringTimestamps.Dequeue();
            DrawMonitoringChart();
            CheckMonitoringAlerts(metrics);
        }
        catch (Exception ex)
        {
            MonitoringStatus.Text = $"Mesure impossible : {ex.Message}";
            AppLog.Write($"Monitoring PC indisponible : {ex.Message}");
        }
        finally
        {
            RefreshMonitoring.IsEnabled = true;
            _monitoringRefreshRunning = false;
        }
    }

    private static void AddHistory(Queue<double> history, double value)
    {
        history.Enqueue(Math.Clamp(value, 0, 100));
        while (history.Count > 60) history.Dequeue();
    }

    private void OpenMonitoringAlertSettings()
    {
        var dialog = new MonitoringSettingsWindow(
            _settings.MonitoringAlertsEnabled,
            _settings.MonitoringCpuAlertPercent,
            _settings.MonitoringRamAlertPercent,
            _settings.MonitoringGpuTemperatureAlertC) { Owner = this };
        dialog.TestRequested += (_, _) => ShowMonitoringAlert(
            "Test de l’alerte",
            "Les alertes du monitoring fonctionnent. Cette notification est un test.",
            "Alerte de test envoyée à " + DateTime.Now.ToString("HH:mm:ss"));
        if (dialog.ShowDialog() != true) return;

        _settings.MonitoringAlertsEnabled = dialog.AlertsAreEnabled;
        _settings.MonitoringCpuAlertPercent = dialog.CpuAlertPercent;
        _settings.MonitoringRamAlertPercent = dialog.RamAlertPercent;
        _settings.MonitoringGpuTemperatureAlertC = dialog.GpuTemperatureAlertC;
        _settingsService.Save(_settings);
        ResetMonitoringAlertCounters();
        MonitoringAlertStatus.Text = _settings.MonitoringAlertsEnabled
            ? "Alertes enregistrées. Trois mesures consécutives sont nécessaires avant une notification."
            : "Alertes désactivées.";
    }

    public static bool TryReadThreshold(string value, int minimum, int maximum, out int threshold) =>
        int.TryParse(value.Trim(), out threshold) && threshold >= minimum && threshold <= maximum;

    private void CheckMonitoringAlerts(SystemMetrics metrics)
    {
        if (!_settings.MonitoringAlertsEnabled)
        {
            ResetMonitoringAlertCounters();
            return;
        }

        _highCpuSamples = metrics.CpuPercent >= _settings.MonitoringCpuAlertPercent ? _highCpuSamples + 1 : 0;
        _highRamSamples = metrics.RamPercent >= _settings.MonitoringRamAlertPercent ? _highRamSamples + 1 : 0;
        _highGpuTemperatureSamples = metrics.GpuTemperatureC >= _settings.MonitoringGpuTemperatureAlertC
            ? _highGpuTemperatureSamples + 1 : 0;

        var pending = Math.Max(Math.Max(_highCpuSamples, _highRamSamples), _highGpuTemperatureSamples);
        if (pending is > 0 and < 3)
            MonitoringAlertStatus.Text = $"Seuil dépassé : confirmation en cours ({pending}/3 mesures).";
        else if (pending == 0)
            MonitoringAlertStatus.Text = "Aucune alerte récente.";

        if (_highCpuSamples >= 3 && CanShowMonitoringAlert(_lastCpuAlertUtc))
        {
            _lastCpuAlertUtc = DateTime.UtcNow;
            ShowMonitoringAlert("Alerte CPU",
                $"Utilisation CPU élevée : {metrics.CpuPercent:0}% (seuil {_settings.MonitoringCpuAlertPercent}%).\nProcessus principal : {metrics.TopCpuProcessText ?? "indéterminé"}.",
                $"CPU élevé : {metrics.CpuPercent:0}% à {DateTime.Now:HH:mm:ss}");
            AppLog.Write($"ALERTE MONITORING | CPU={metrics.CpuPercent:0}%");
        }
        if (_highRamSamples >= 3 && CanShowMonitoringAlert(_lastRamAlertUtc))
        {
            _lastRamAlertUtc = DateTime.UtcNow;
            ShowMonitoringAlert("Alerte RAM",
                $"Utilisation RAM élevée : {metrics.RamPercent:0}% (seuil {_settings.MonitoringRamAlertPercent}%).\nProcessus principal : {metrics.TopRamProcessText ?? "indéterminé"}.",
                $"RAM élevée : {metrics.RamPercent:0}% à {DateTime.Now:HH:mm:ss}");
            AppLog.Write($"ALERTE MONITORING | RAM={metrics.RamPercent:0}%");
        }
        if (_highGpuTemperatureSamples >= 3 && CanShowMonitoringAlert(_lastGpuTemperatureAlertUtc))
        {
            _lastGpuTemperatureAlertUtc = DateTime.UtcNow;
            ShowMonitoringAlert("Alerte température GPU",
                $"Le GPU atteint {metrics.GpuTemperatureC:0} °C (seuil {_settings.MonitoringGpuTemperatureAlertC} °C).",
                $"Température GPU élevée : {metrics.GpuTemperatureC:0} °C à {DateTime.Now:HH:mm:ss}");
            AppLog.Write($"ALERTE MONITORING | Température GPU={metrics.GpuTemperatureC:0}°C");
        }
    }

    private void ShowMonitoringAlert(string title, string message, string visibleStatus)
    {
        MonitoringAlertStatus.Text = "⚠ " + visibleStatus;
        MonitoringAlertStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 179, 107));
        _monitoringAlertWindow?.Close();
        _monitoringAlertWindow = new MonitoringAlertWindow(title, message);
        _monitoringAlertWindow.OpenMonitoringRequested += (_, _) =>
        {
            ShowHub();
            ShowPage("monitoring");
        };
        _monitoringAlertWindow.Closed += (_, _) => _monitoringAlertWindow = null;
        _monitoringAlertWindow.Show();
    }

    private static bool CanShowMonitoringAlert(DateTime lastAlertUtc) =>
        DateTime.UtcNow - lastAlertUtc >= TimeSpan.FromMinutes(15);

    private void ResetMonitoringAlertCounters()
    {
        _highCpuSamples = 0;
        _highRamSamples = 0;
        _highGpuTemperatureSamples = 0;
    }

    private void MonitoringChart_OnSizeChanged(object sender, SizeChangedEventArgs e) => DrawMonitoringChart();

    private void DrawMonitoringChart()
    {
        if (MonitoringChart.ActualWidth <= 0 || MonitoringChart.ActualHeight <= 0) return;
        DrawMonitoringScale();
        CpuHistoryLine.Points = CreateHistoryPoints(_cpuHistory);
        RamHistoryLine.Points = CreateHistoryPoints(_ramHistory);
        GpuHistoryLine.Points = CreateHistoryPoints(_gpuHistory);
    }

    private PointCollection CreateHistoryPoints(IEnumerable<double> values)
    {
        var samples = values.ToArray();
        var points = new PointCollection(samples.Length);
        const double left = 38;
        const double top = 4;
        const double bottom = 18;
        var plotWidth = Math.Max(1, MonitoringChart.ActualWidth - left - 4);
        var plotHeight = Math.Max(1, MonitoringChart.ActualHeight - top - bottom);
        for (var index = 0; index < samples.Length; index++)
        {
            var x = samples.Length <= 1 ? left : left + index * plotWidth / (samples.Length - 1);
            var y = top + plotHeight * (1 - samples[index] / 100d);
            points.Add(new System.Windows.Point(x, y));
        }
        return points;
    }

    private void DrawMonitoringScale()
    {
        foreach (var element in MonitoringChart.Children.OfType<FrameworkElement>()
                     .Where(element => Equals(element.Tag, "monitoring-scale")).ToArray())
            MonitoringChart.Children.Remove(element);

        const double left = 38;
        const double top = 4;
        const double bottom = 18;
        var plotHeight = Math.Max(1, MonitoringChart.ActualHeight - top - bottom);
        foreach (var value in new[] { 100, 75, 50, 25, 0 })
        {
            var y = top + plotHeight * (1 - value / 100d);
            var line = new System.Windows.Shapes.Line
            {
                X1 = left, X2 = MonitoringChart.ActualWidth - 4, Y1 = y, Y2 = y,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(48, 255, 255, 255)),
                StrokeThickness = 1, Tag = "monitoring-scale"
            };
            System.Windows.Controls.Panel.SetZIndex(line, 0);
            MonitoringChart.Children.Add(line);
            var label = new System.Windows.Controls.TextBlock
            {
                Text = $"{value}%", FontSize = 10, Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"),
                Tag = "monitoring-scale"
            };
            System.Windows.Controls.Canvas.SetLeft(label, 2);
            System.Windows.Controls.Canvas.SetTop(label, Math.Clamp(y - 7, 0, Math.Max(0, MonitoringChart.ActualHeight - 15)));
            System.Windows.Controls.Panel.SetZIndex(label, 1);
            MonitoringChart.Children.Add(label);
        }
    }

    private void MonitoringChart_OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var cpu = _cpuHistory.ToArray();
        var ram = _ramHistory.ToArray();
        var gpu = _gpuHistory.ToArray();
        var times = _monitoringTimestamps.ToArray();
        var count = new[] { cpu.Length, ram.Length, gpu.Length, times.Length }.Min();
        if (count == 0) return;

        const double left = 38;
        var plotWidth = Math.Max(1, MonitoringChart.ActualWidth - left - 4);
        var position = e.GetPosition(MonitoringChart);
        var ratio = Math.Clamp((position.X - left) / plotWidth, 0, 1);
        var index = count <= 1 ? 0 : (int)Math.Round(ratio * (count - 1));
        var x = count <= 1 ? left : left + index * plotWidth / (count - 1);

        MonitoringHoverLine.X1 = x;
        MonitoringHoverLine.X2 = x;
        MonitoringHoverLine.Y1 = 4;
        MonitoringHoverLine.Y2 = Math.Max(4, MonitoringChart.ActualHeight - 18);
        MonitoringHoverText.Inlines.Clear();
        MonitoringHoverText.Text = $"{times[index]:HH:mm:ss}\nCPU  {cpu[index]:0}%   RAM  {ram[index]:0}%   GPU  {gpu[index]:0}%";
        MonitoringHoverLine.Visibility = Visibility.Visible;
        MonitoringHoverTooltip.Visibility = Visibility.Visible;
        MonitoringHoverTooltip.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var tooltipWidth = MonitoringHoverTooltip.DesiredSize.Width;
        System.Windows.Controls.Canvas.SetLeft(MonitoringHoverTooltip, Math.Clamp(x + 9, left, Math.Max(left, MonitoringChart.ActualWidth - tooltipWidth - 4)));
        System.Windows.Controls.Canvas.SetTop(MonitoringHoverTooltip, 8);
    }

    private void MonitoringChart_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        MonitoringHoverLine.Visibility = Visibility.Collapsed;
        MonitoringHoverTooltip.Visibility = Visibility.Collapsed;
    }

    private void ChooseDuplicateFolderNow()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choisissez le dossier dans lequel rechercher les fichiers en double.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
        DuplicateFolderBox.Text = dialog.SelectedPath;
        ScanDuplicateFiles.IsEnabled = Directory.Exists(dialog.SelectedPath);
        DuplicateFilesStatus.Text = "Dossier sélectionné. L’analyse compare uniquement les fichiers de même taille.";
    }

    private async Task ScanDuplicateFilesAsync()
    {
        var folder = DuplicateFolderBox.Text.Trim();
        if (_duplicateScanRunning || !Directory.Exists(folder)) return;
        _duplicateScanRunning = true;
        _duplicateScanCancellation?.Dispose();
        _duplicateScanCancellation = new CancellationTokenSource();
        ChooseDuplicateFolder.IsEnabled = false;
        ScanDuplicateFiles.IsEnabled = false;
        CancelDuplicateScan.IsEnabled = true;
        DuplicateFilesList.ItemsSource = null;
        _duplicateResults = Array.Empty<DuplicateFileCandidate>();
        _duplicateScanRoot = null;
        AutoSelectDuplicateFiles.IsEnabled = false;
        DeleteSelectedDuplicateFiles.IsEnabled = false;
        DuplicateProgressPanel.Visibility = Visibility.Visible;
        DuplicateProgressBar.IsIndeterminate = true;
        DuplicateProgressBar.Value = 0;
        DuplicateProgressText.Text = "Inventaire des fichiers…";
        DuplicateProgressPercent.Text = "";
        DuplicateFilesStatus.Text = "Analyse en lecture seule en cours… Les dossiers volumineux peuvent demander plusieurs minutes.";
        try
        {
            var progress = new Progress<DuplicateScanProgress>(value =>
            {
                DuplicateProgressBar.IsIndeterminate = value.IsIndeterminate;
                DuplicateProgressText.Text = value.IsIndeterminate
                    ? $"{value.Phase} · {value.ProcessedFiles} fichier(s) trouvé(s)"
                    : $"{value.Phase} · {value.ProcessedFiles}/{value.TotalFiles}";
                if (!value.IsIndeterminate)
                {
                    var percent = value.TotalFiles == 0 ? 100 : value.ProcessedFiles * 100d / value.TotalFiles;
                    DuplicateProgressBar.Value = percent;
                    DuplicateProgressPercent.Text = $"{percent:0}%";
                }
            });
            var result = await _duplicateFileService.ScanAsync(folder, progress, _duplicateScanCancellation.Token);
            _duplicateResults = result.Files;
            _duplicateScanRoot = folder;
            DuplicateFilesList.ItemsSource = result.Files;
            AutoSelectDuplicateFiles.IsEnabled = result.Files.Count > 0;
            DeleteSelectedDuplicateFiles.IsEnabled = result.Files.Count > 0;
            DuplicateProgressBar.IsIndeterminate = false;
            DuplicateProgressBar.Value = 100;
            DuplicateProgressText.Text = "Analyse terminée";
            DuplicateProgressPercent.Text = "100%";
            DuplicateFilesStatus.Text = result.Files.Count == 0
                ? "Aucun fichier en double trouvé."
                : $"{result.GroupCount} groupe(s) · {result.Files.Count} fichiers · jusqu’à {TemporaryFileCleanupService.FormatSize(result.RecoverableBytes)} récupérables après validation" +
                  (result.SkippedFiles > 0 ? $" · {result.SkippedFiles} fichier(s) inaccessible(s) ignoré(s)." : ".");
            AppLog.Write($"DOUBLONS | Dossier={folder}; Groupes={result.GroupCount}; Fichiers={result.Files.Count}; Ignorés={result.SkippedFiles}");
        }
        catch (OperationCanceledException)
        {
            DuplicateProgressPanel.Visibility = Visibility.Collapsed;
            DuplicateFilesStatus.Text = "Analyse annulée. Aucun fichier n’a été modifié.";
            AppLog.Write($"Analyse des doublons annulée : {folder}");
        }
        catch (Exception ex)
        {
            DuplicateFilesStatus.Text = $"Analyse impossible : {ex.Message}";
            AppLog.Write($"Analyse des doublons impossible : {ex.Message}");
        }
        finally
        {
            _duplicateScanRunning = false;
            CancelDuplicateScan.IsEnabled = false;
            ChooseDuplicateFolder.IsEnabled = true;
            ScanDuplicateFiles.IsEnabled = Directory.Exists(folder);
            _duplicateScanCancellation?.Dispose();
            _duplicateScanCancellation = null;
        }
    }

    private void DuplicateFilesList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        OpenSelectedDuplicateLocation();

    private void OpenSelectedDuplicateLocation()
    {
        if (DuplicateFilesList.SelectedItem is not DuplicateFileCandidate candidate) return;
        if (!File.Exists(candidate.Path))
        {
            DuplicateFilesStatus.Text = "Ce fichier n’existe plus à cet emplacement.";
            return;
        }
        try
        {
            var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add("/select,");
            startInfo.ArgumentList.Add(candidate.Path);
            Process.Start(startInfo);
            DuplicateFilesStatus.Text = $"Emplacement ouvert : {candidate.Name}";
        }
        catch (Exception ex)
        {
            DuplicateFilesStatus.Text = $"Impossible d’ouvrir l’emplacement : {ex.Message}";
        }
    }

    private void AutoSelectDuplicateFilesNow()
    {
        foreach (var group in _duplicateResults.GroupBy(file => file.GroupNumber))
        {
            var keep = group.OrderBy(file => file.LastWriteTime).ThenBy(file => file.Path.Length).First();
            foreach (var file in group) file.IsSelected = file != keep;
        }
        var selected = _duplicateResults.Count(file => file.IsSelected);
        DuplicateFilesStatus.Text = $"{selected} doublon(s) sélectionné(s). Le fichier le plus ancien de chaque groupe est conservé.";
    }

    private async Task DeleteSelectedDuplicateFilesAsync()
    {
        if (string.IsNullOrWhiteSpace(_duplicateScanRoot)) return;
        var selected = _duplicateResults.Where(file => file.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            DuplicateFilesStatus.Text = "Cochez au moins un fichier à supprimer.";
            return;
        }
        var unsafeGroup = _duplicateResults.GroupBy(file => file.GroupNumber)
            .FirstOrDefault(group => group.All(file => file.IsSelected));
        if (unsafeGroup is not null)
        {
            DuplicateFilesStatus.Text = $"Sélection refusée : conservez au moins un fichier dans le groupe {unsafeGroup.Key}.";
            return;
        }
        var total = selected.Sum(file => file.SizeBytes);
        var confirmation = System.Windows.MessageBox.Show(this,
            $"Déplacer {selected.Length} fichier(s) en double ({TemporaryFileCleanupService.FormatSize(total)}) vers la Corbeille ?\n\nUn exemplaire non coché sera conservé dans chaque groupe.",
            "Confirmer la suppression des doublons", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes) return;

        DeleteSelectedDuplicateFiles.IsEnabled = false;
        AutoSelectDuplicateFiles.IsEnabled = false;
        DuplicateFilesStatus.Text = "Déplacement vers la Corbeille en cours…";
        var result = await Task.Run(() => _duplicateFileService.MoveToRecycleBin(selected, _duplicateScanRoot));
        var deletedPaths = selected.Where(file => !File.Exists(file.Path)).Select(file => file.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _duplicateResults = _duplicateResults.Where(file => !deletedPaths.Contains(file.Path)).ToArray();
        DuplicateFilesList.ItemsSource = _duplicateResults;
        AutoSelectDuplicateFiles.IsEnabled = _duplicateResults.Count > 1;
        DeleteSelectedDuplicateFiles.IsEnabled = _duplicateResults.Count > 1;
        DuplicateFilesStatus.Text = $"{result.DeletedFiles} fichier(s) déplacé(s) vers la Corbeille · {TemporaryFileCleanupService.FormatSize(result.RecoveredBytes)}" +
            (result.FailedFiles > 0 ? $" · {result.FailedFiles} échec(s)." : ".");
        AppLog.Write($"DOUBLONS SUPPRESSION | Corbeille={result.DeletedFiles}; Taille={result.RecoveredBytes}; Échecs={result.FailedFiles}");
    }

    private async Task ScanTemporaryFilesAsync()
    {
        ScanTemporaryFiles.IsEnabled = false;
        DeleteSelectedTemporaryFiles.IsEnabled = false;
        SelectAllCleanupFiles.IsEnabled = false;
        CleanupFilesList.ItemsSource = null;
        CleanupSummary.Text = "Analyse du dossier temporaire en cours…";
        CleanupStatus.Text = "";
        try
        {
            var result = await _temporaryFileCleanupService.ScanAsync();
            CleanupFilesList.ItemsSource = result.Files;
            CleanupSummary.Text = result.Files.Count == 0
                ? "Aucun fichier temporaire ancien de plus de 24 heures n’a été trouvé."
                : $"{result.Files.Count} fichier(s) · {TemporaryFileCleanupService.FormatSize(result.TotalSizeBytes)} récupérables";
            CleanupStatus.Text = result.SkippedFiles > 0
                ? $"{result.SkippedFiles} élément(s) inaccessible(s) ont été ignorés."
                : "Analyse terminée. Sélectionnez uniquement les fichiers à supprimer.";
            SelectAllCleanupFiles.IsEnabled = result.Files.Count > 0;
        }
        catch (Exception ex)
        {
            CleanupSummary.Text = "Analyse impossible.";
            CleanupStatus.Text = ex.Message;
            AppLog.Write($"Analyse des fichiers temporaires impossible : {ex.Message}");
        }
        finally { ScanTemporaryFiles.IsEnabled = true; }
    }

    private async Task SaveAutomaticTemporaryCleanupAsync()
    {
        var enable = AutomaticTemporaryCleanupEnabled.IsChecked == true;
        if (enable && !_settings.AutomaticTemporaryCleanupEnabled)
        {
            var confirmation = System.Windows.MessageBox.Show(this,
                "Activer le nettoyage automatique quotidien ?\n\nFlexHub supprimera sans nouvelle confirmation les fichiers du dossier temporaire Windows inutilisés depuis plus de 3 jours. Les fichiers verrouillés seront ignorés et les caches d’applications ne seront pas concernés.",
                "Activer le nettoyage automatique", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes)
            {
                AutomaticTemporaryCleanupEnabled.IsChecked = false;
                return;
            }
        }
        _settings.AutomaticTemporaryCleanupEnabled = enable;
        if (_settings.AutomaticTemporaryCleanupEnabled)
            _settings.LastAutomaticTemporaryCleanupUtc = null;
        _settingsService.Save(_settings);
        CleanupStatus.Text = _settings.AutomaticTemporaryCleanupEnabled
            ? "Nettoyage automatique activé. Premier contrôle en cours…"
            : "Nettoyage automatique désactivé.";
        if (_settings.AutomaticTemporaryCleanupEnabled)
            await RunAutomaticTemporaryCleanupAsync(true);
    }

    private async Task RunAutomaticTemporaryCleanupAsync(bool force = false)
    {
        if (!_settings.TemporaryCleanupModuleEnabled || !_settings.AutomaticTemporaryCleanupEnabled || _automaticTemporaryCleanupRunning) return;
        if (!force && _settings.LastAutomaticTemporaryCleanupUtc.HasValue &&
            DateTime.UtcNow - _settings.LastAutomaticTemporaryCleanupUtc.Value < TimeSpan.FromDays(1)) return;

        _automaticTemporaryCleanupRunning = true;
        try
        {
            var scan = await _temporaryFileCleanupService.ScanWindowsTemporaryAsync(TimeSpan.FromDays(3));
            var result = await Task.Run(() => _temporaryFileCleanupService.Delete(scan.Files));
            _settings.LastAutomaticTemporaryCleanupUtc = DateTime.UtcNow;
            _settingsService.Save(_settings);
            AppLog.Write($"NETTOYAGE AUTOMATIQUE | Trouvés={scan.Files.Count}; Supprimés={result.DeletedFiles}; Récupéré={result.RecoveredBytes}; Échecs={result.FailedFiles}");
            CleanupStatus.Text = result.DeletedFiles == 0
                ? "Nettoyage automatique : aucun fichier de plus de 3 jours à supprimer."
                : $"Nettoyage automatique : {result.DeletedFiles} fichier(s), {TemporaryFileCleanupService.FormatSize(result.RecoveredBytes)} récupérés" +
                  (result.FailedFiles > 0 ? $" · {result.FailedFiles} ignoré(s)." : ".");
        }
        catch (Exception ex)
        {
            AppLog.Write($"Nettoyage automatique impossible : {ex.Message}");
            CleanupStatus.Text = $"Nettoyage automatique impossible : {ex.Message}";
        }
        finally { _automaticTemporaryCleanupRunning = false; }
    }

    private async Task DeleteSelectedTemporaryFilesAsync()
    {
        var selected = CleanupFilesList.SelectedItems.Cast<TemporaryFileCandidate>().ToArray();
        if (selected.Length == 0) return;
        var total = selected.Sum(file => file.SizeBytes);
        var confirmation = System.Windows.MessageBox.Show(this,
            $"Supprimer définitivement {selected.Length} fichier(s) temporaire(s) représentant {TemporaryFileCleanupService.FormatSize(total)} ?\n\nLes fichiers utilisés par une application seront ignorés.",
            "Confirmer le nettoyage", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes) return;

        ScanTemporaryFiles.IsEnabled = false;
        DeleteSelectedTemporaryFiles.IsEnabled = false;
        CleanupStatus.Text = "Suppression en cours…";
        var result = await Task.Run(() => _temporaryFileCleanupService.Delete(selected));
        AppLog.Write($"NETTOYAGE TEMPORAIRE | Supprimés={result.DeletedFiles}; Récupéré={result.RecoveredBytes}; Échecs={result.FailedFiles}");
        var resultMessage = $"{result.DeletedFiles} fichier(s) supprimé(s), {TemporaryFileCleanupService.FormatSize(result.RecoveredBytes)} récupérés" +
                            (result.FailedFiles > 0 ? $" · {result.FailedFiles} fichier(s) ignoré(s)." : ".");
        await ScanTemporaryFilesAsync();
        CleanupStatus.Text = resultMessage;
    }

    private async Task RefreshStorageHealthAsync()
    {
        RefreshStorageHealth.IsEnabled = false;
        StorageHealthStatus.Text = "Lecture des informations de stockage…";
        try
        {
            var snapshot = await _storageHealthService.ReadAsync();
            StorageVolumesList.ItemsSource = snapshot.Volumes;
            PhysicalDisksList.ItemsSource = snapshot.PhysicalDisks;
            StorageUnavailableText.Visibility = snapshot.PhysicalDisks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StorageHealthStatus.Text = $"Actualisé à {DateTime.Now:HH:mm:ss} · {snapshot.Volumes.Count} volume(s), {snapshot.PhysicalDisks.Count} disque(s) physique(s).";
        }
        catch (Exception ex)
        {
            StorageHealthStatus.Text = $"Lecture impossible : {ex.Message}";
            AppLog.Write($"Lecture du stockage impossible : {ex.Message}");
        }
        finally { RefreshStorageHealth.IsEnabled = true; }
    }

    private async Task RefreshStartupAuditAsync()
    {
        RefreshStartupAudit.IsEnabled = false;
        DisableStartupEntry.IsEnabled = false;
        EnableStartupEntry.IsEnabled = false;
        StartupAuditStatus.Text = "Lecture des emplacements de démarrage…";
        try
        {
            var result = await _startupAuditService.ScanAsync();
            StartupEntriesList.ItemsSource = result.Entries;
            var disabled = result.Entries.Count(entry => entry.Status == "Désactivé");
            StartupAuditStatus.Text = result.Entries.Count == 0
                ? "Aucun programme de démarrage trouvé."
                : $"{result.Entries.Count} programme(s), dont {disabled} désactivé(s)" +
                  (result.InaccessibleSources > 0 ? $" · {result.InaccessibleSources} source(s) inaccessible(s)." : ".");
        }
        catch (Exception ex)
        {
            StartupAuditStatus.Text = $"Analyse impossible : {ex.Message}";
            AppLog.Write($"Audit du démarrage impossible : {ex.Message}");
        }
        finally { RefreshStartupAudit.IsEnabled = true; }
    }

    private void UpdateStartupAuditButtons()
    {
        var selected = StartupEntriesList.SelectedItem as StartupEntry;
        DisableStartupEntry.IsEnabled = selected?.IsEnabled == true;
        EnableStartupEntry.IsEnabled = selected is { IsEnabled: false } && selected.Status == "Désactivé";
    }

    private async Task ChangeStartupEntryStateAsync(bool enabled)
    {
        if (StartupEntriesList.SelectedItem is not StartupEntry entry) return;
        var action = enabled ? "réactiver" : "désactiver";
        var consequence = enabled
            ? "Cette application pourra de nouveau se lancer automatiquement avec Windows."
            : "L’application restera installée et pourra toujours être lancée manuellement.";
        var confirmation = System.Windows.MessageBox.Show(this,
            $"Voulez-vous {action} « {entry.Name} » au démarrage ?\n\n{entry.Reason}\n\n{consequence}",
            "Modifier le démarrage", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes) return;

        DisableStartupEntry.IsEnabled = false;
        EnableStartupEntry.IsEnabled = false;
        StartupAuditStatus.Text = $"Modification de {entry.Name}…";
        try
        {
            await _startupAuditService.SetEnabledAsync(entry, enabled);
            AppLog.Write($"AUDIT DÉMARRAGE | {entry.Name}; État={(enabled ? "activé" : "désactivé")}; Source={entry.Source}");
            await RefreshStartupAuditAsync();
            StartupAuditStatus.Text = $"{entry.Name} a été {(enabled ? "réactivé" : "désactivé")}. Modification réversible depuis cette page.";
        }
        catch (UnauthorizedAccessException)
        {
            StartupAuditStatus.Text = "Windows refuse cette modification sans droits administrateur.";
        }
        catch (Exception ex)
        {
            StartupAuditStatus.Text = $"Modification impossible : {ex.Message}";
            AppLog.Write($"Modification du démarrage impossible pour {entry.Name} : {ex.Message}");
        }
        finally { UpdateStartupAuditButtons(); }
    }

    private void TestReminderNow()
    {
        var duration = int.TryParse(ReminderDuration.Text, out var seconds) && seconds is >= 1 and <= 300
            ? seconds
            : _settings.ReminderDisplaySeconds;
        ShowTrayMessage("Test du rappel Top-Serveurs", "La notification fonctionne correctement. Cliquez ici pour ouvrir la page.", duration * 1000, OpenReminderUrl);
        ReminderStatus.Text = "Notification de test lancée.";
    }
    private void ShowReminder()
    {
        _settings.LastReminderUtc = DateTime.UtcNow;
        _settingsService.Save(_settings);
        UpdateLastReminderText();
        ShowTrayMessage("Rappel Top-Serveurs", "Vous pouvez maintenant effectuer votre vote manuel.", _settings.ReminderDisplaySeconds * 1000, OpenReminderUrl);
        ConfigureReminderTimer();
    }
    private void ShowTrayMessage(string title, string text, int timeout = 5000, Action? clickAction = null) { _balloonClickAction = clickAction; _tray.BalloonTipTitle = title; _tray.BalloonTipText = text; _tray.ShowBalloonTip(timeout); }
    private void OpenTipeeePage() { try { Process.Start(new ProcessStartInfo(TipeeeUrl) { UseShellExecute = true }); } catch (Exception ex) { AppLog.Write($"Ouverture de Tipeee impossible : {ex.Message}"); } }
    private static void OpenExternalPage(string url, string provider)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { AppLog.Write($"Ouverture de la page API {provider} impossible : {ex.Message}"); }
    }
    private void OpenReminderUrl() { try { Process.Start(new ProcessStartInfo(_settings.ReminderUrl) { UseShellExecute = true }); } catch { } }
    private static bool IsHttpUrl(string value) => Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    private static void ShowVisibleMessage(string title, string message)
    {
        var owner = new Window { Width = 1, Height = 1, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Topmost = true, Opacity = 0 };
        owner.Show();
        System.Windows.MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        owner.Close();
    }
    private void ShowHub() { Show(); WindowState = WindowState.Normal; Activate(); }
    internal void ShowFromSecondaryLaunch()
    {
        ShowHub();
        Topmost = true;
        Topmost = false;
        Focus();
    }
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { if (!_exit) { e.Cancel = true; Hide(); } }
    private void ExitHub() { _exit = true; _gamePerformanceService.Dispose(); _systemMonitoringService.Dispose(); _gameChatKeyboardService.Dispose(); _keyboardLayoutMonitorService.Dispose(); _hotkeyService.Dispose(); _translatorHotkeyService.Dispose(); _responseGeneratorHotkeyService.Dispose(); _actionWheelHotkeyService.Dispose(); _reminderTimer.Stop(); _xmpTimer.Stop(); _monitoringTimer.Stop(); _tray.Visible = false; _tray.Dispose(); Close(); System.Windows.Application.Current.Shutdown(); }

    private sealed record LanguageChoice(string Name, string Code)
    {
        public override string ToString() => Name;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr handle);
}
