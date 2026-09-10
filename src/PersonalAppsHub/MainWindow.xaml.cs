using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
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
    private readonly NvidiaProfileService _nvidiaProfileService = new();
    private readonly XmpMonitorService _xmpMonitorService = new();
    private readonly UpdateService _updateService = new();
    private readonly ApiQuotaService _apiQuotaService = new();
    private readonly HotkeyService _hotkeyService = new(9471);
    private readonly HotkeyService _translatorHotkeyService = new(9472);
    private readonly HotkeyService _responseGeneratorHotkeyService = new(9473);
    private readonly HotkeyService _actionWheelHotkeyService = new(9474);
    private readonly DispatcherTimer _reminderTimer = new();
    private readonly DispatcherTimer _xmpTimer = new();
    private readonly Forms.NotifyIcon _tray;
    private HubSettings _settings;
    private bool _exit;
    private bool _loadingSettings;
    private bool _xmpCheckRunning;
    private bool _initialXmpCheckDone;
    private bool _actionWheelOpen;
    private XmpAlertWindow? _xmpAlertWindow;
    private Action? _balloonClickAction;
    private UpdateCheckResult? _availableUpdate;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
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
        CorrectorNav.Click += (_, _) => ShowPage("corrector");
        TranslatorNav.Click += (_, _) => ShowPage("translator");
        ResponseGeneratorNav.Click += (_, _) => ShowPage("responseGenerator");
        ActionWheelNav.Click += (_, _) => ShowPage("actionWheel");
        NvidiaNav.Click += (_, _) => ShowPage("nvidia");
        XmpNav.Click += (_, _) => ShowPage("xmp");
        TipeeeButton.Click += (_, _) => OpenTipeeePage();
        GeneralSettingsButton.Click += (_, _) => ShowPage("general");
        HideHubButton.Click += (_, _) => Hide();
        QuitHubButton.Click += (_, _) => ExitHub();
        SaveReminder.Click += (_, _) => SaveReminderSettings();
        TestReminder.Click += (_, _) => TestReminderNow();
        SaveCorrector.Click += (_, _) => SaveCorrectorSettings();
        SaveTranslator.Click += (_, _) => SaveTranslatorSettings();
        SaveResponseGenerator.Click += (_, _) => SaveResponseGeneratorSettings();
        SaveActionWheel.Click += (_, _) => SaveActionWheelSettings();
        ReminderEnabled.Checked += (_, _) => ApplyEnabledStates();
        ReminderEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        CorrectorEnabled.Checked += (_, _) => ApplyEnabledStates();
        CorrectorEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        TranslatorEnabled.Checked += (_, _) => ApplyEnabledStates();
        TranslatorEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        ResponseGeneratorEnabled.Checked += (_, _) => ApplyEnabledStates();
        ResponseGeneratorEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        ActionWheelEnabled.Checked += (_, _) => ApplyEnabledStates();
        ActionWheelEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        NvidiaEnabled.Checked += (_, _) => ApplyEnabledStates();
        NvidiaEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        XmpEnabled.Checked += (_, _) => ApplyEnabledStates();
        XmpEnabled.Unchecked += (_, _) => ApplyEnabledStates();
        TestXmp.Click += async (_, _) => await CheckXmpAsync(true);
        SaveXmp.Click += (_, _) => SaveXmpSettings();
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
        DownloadUpdate.Click += (_, _) => { if (_availableUpdate != null) UpdateService.OpenRelease(_availableUpdate); };
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
        _reminderTimer.Tick += (_, _) => ShowReminder();
        _xmpTimer.Tick += async (_, _) => await CheckXmpAsync(false);
        SourceInitialized += (_, _) => { RegisterHotkey(); RegisterTranslatorHotkey(); RegisterResponseGeneratorHotkey(); RegisterActionWheelHotkey(); };
        ContentRendered += async (_, _) =>
        {
            if (_initialXmpCheckDone) return;
            _initialXmpCheckDone = true;
            ShowMemorySetupIfNeeded();
            if (_settings.XmpMonitorEnabled) await CheckXmpAsync(false);
            await CheckForUpdatesAtStartupAsync();
        };
        Closing += OnClosing;
        ConfigureReminderTimer();
        ConfigureXmpTimer();
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
        UpdateResponseGeneratorModel();
        ActionWheelEnabled.IsChecked = _settings.ActionWheelEnabled;
        ActionWheelHotkeyBox.Text = _settings.ActionWheelHotkey.Replace("+", " + ");
        NvidiaEnabled.IsChecked = _settings.NvidiaOptimizerEnabled;
        UpdateNvidiaPage();
        XmpEnabled.IsChecked = _settings.XmpMonitorEnabled;
        MemoryTypeBox.SelectedIndex = _settings.MemoryType == "DDR5" ? 1 : 0;
        XmpCustomSpeedCheck.IsChecked = _settings.XmpUseCustomSpeed;
        XmpExpectedSpeedBox.Text = _settings.XmpExpectedSpeed.ToString();
        UpdateXmpSpeedControls();
        XmpIntervalBox.Text = _settings.XmpCheckIntervalMinutes.ToString();
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
        TranslatorPage.Visibility = page == "translator" ? Visibility.Visible : Visibility.Collapsed;
        ResponseGeneratorPage.Visibility = page == "responseGenerator" ? Visibility.Visible : Visibility.Collapsed;
        ActionWheelPage.Visibility = page == "actionWheel" ? Visibility.Visible : Visibility.Collapsed;
        NvidiaPage.Visibility = page == "nvidia" ? Visibility.Visible : Visibility.Collapsed;
        XmpPage.Visibility = page == "xmp" ? Visibility.Visible : Visibility.Collapsed;
        GeneralSettingsPage.Visibility = page == "general" ? Visibility.Visible : Visibility.Collapsed;
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
            GeneralSettingsStatus.Text = "Paramètres généraux et clés API enregistrés.";
        }
        catch (Exception ex)
        {
            GeneralSettingsStatus.Text = $"Impossible de modifier le démarrage automatique : {ex.Message}";
        }
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

    private void GeneralSettingsScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        GeneralSettingsScrollViewer.ScrollToVerticalOffset(GeneralSettingsScrollViewer.VerticalOffset - e.Delta);
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
        if (ReminderNav == null || CorrectorNav == null || TranslatorNav == null || ResponseGeneratorNav == null || ActionWheelNav == null || NvidiaNav == null || XmpNav == null) return;
        PlaceNavigationButton(ReminderNav, ReminderEnabled.IsChecked == true);
        PlaceNavigationButton(CorrectorNav, CorrectorEnabled.IsChecked == true);
        PlaceNavigationButton(TranslatorNav, TranslatorEnabled.IsChecked == true);
        PlaceNavigationButton(ResponseGeneratorNav, ResponseGeneratorEnabled.IsChecked == true);
        PlaceNavigationButton(ActionWheelNav, ActionWheelEnabled.IsChecked == true);
        PlaceNavigationButton(NvidiaNav, NvidiaEnabled.IsChecked == true);
        PlaceNavigationButton(XmpNav, XmpEnabled.IsChecked == true);
        DisabledAppsSection.Visibility = DisabledAppsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PlaceNavigationButton(System.Windows.Controls.Button button, bool enabled)
    {
        var target = enabled ? ActiveAppsPanel : DisabledAppsPanel;
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

    private int NavigationRank(System.Windows.Controls.Button button) =>
        button == ReminderNav ? 0 : button == CorrectorNav ? 1 : button == TranslatorNav ? 2 : button == ResponseGeneratorNav ? 3 : button == ActionWheelNav ? 4 : button == NvidiaNav ? 5 : 6;

    private void ApplyEnabledStates()
    {
        _settings.ReminderEnabled = ReminderEnabled.IsChecked == true;
        _settings.CorrectorEnabled = CorrectorEnabled.IsChecked == true;
        _settings.TranslatorEnabled = TranslatorEnabled.IsChecked == true;
        _settings.ResponseGeneratorEnabled = ResponseGeneratorEnabled.IsChecked == true;
        _settings.ActionWheelEnabled = ActionWheelEnabled.IsChecked == true;
        _settings.NvidiaOptimizerEnabled = NvidiaEnabled.IsChecked == true;
        _settings.XmpMonitorEnabled = XmpEnabled.IsChecked == true;
        _settingsService.Save(_settings);
        ConfigureReminderTimer();
        ConfigureXmpTimer();
        RegisterHotkey();
        RegisterTranslatorHotkey();
        RegisterResponseGeneratorHotkey();
        RegisterActionWheelHotkey();
        UpdateNavigationState();
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
        ResponseGeneratorStatus.Text = RegisterResponseGeneratorHotkey()
            ? $"Configuration enregistrée. Clé {provider} : {(SecretStore.HasKey(provider) ? "disponible" : "manquante")}."
            : "Ce raccourci est déjà utilisé par une autre application.";
        UpdateNavigationState();
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
            var wheel = new ActionWheelWindow(sourceLanguage, targetLanguage);
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
            NvidiaProfileState.Text = "Carte non reconnue : aucun profil automatique disponible. Séries compatibles : RTX 5000, 4000 et 3000.";
            OptimizeNvidia.IsEnabled = false;
            RestoreNvidia.IsEnabled = _nvidiaProfileService.CanRestore;
        }
        else
        {
            NvidiaProfileState.Text = $"Profil {_nvidiaProfileService.DetectedGpuGroup} sélectionné automatiquement.";
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
        await RunNvidiaActionAsync(async () => { await _nvidiaProfileService.BackupThenOptimizeAsync(); }, true, "Profil d’optimisation NVIDIA appliqué. L’état précédent a été sauvegardé.");
    }

    private async Task RestoreNvidiaAsync()
    {
        if (!_nvidiaProfileService.CanRestore) { ShowVisibleMessage("Restauration NVIDIA", "Aucune sauvegarde antérieure à une optimisation n’est disponible."); return; }
        if (System.Windows.MessageBox.Show(this, "Restaurer l’état NVIDIA enregistré juste avant la dernière optimisation ?", "Restauration NVIDIA", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunNvidiaActionAsync(_nvidiaProfileService.RestorePreviousAsync, false, "État NVIDIA antérieur à l’optimisation restauré.");
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
        await RunNvidiaActionAsync(async () => { await _nvidiaProfileService.BackupThenApplyAsync(profile); }, false, $"Profil « {profile.Name} » appliqué. L’état précédent a été sauvegardé.");
    }

    private void DeleteSelectedNvidiaProfileNow()
    {
        if (NvidiaProfilesBox.SelectedItem is not NvidiaSavedProfile profile) return;
        if (profile.IsOptimization) { ShowVisibleMessage("Profil NVIDIA", "Le profil d’optimisation ne peut pas être supprimé."); return; }
        if (System.Windows.MessageBox.Show(this, $"Supprimer définitivement la sauvegarde « {profile.Name} » ?", "Suppression du profil", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { _nvidiaProfileService.DeleteProfile(profile); NvidiaProfileState.Text = "Sauvegarde supprimée."; RefreshNvidiaProfiles(); }
        catch (Exception ex) { ShowVisibleMessage("Erreur NVIDIA", ex.Message); }
    }

    private async Task RunNvidiaActionAsync(Func<Task> action, bool optimization, string successMessage)
    {
        try
        {
            OptimizeNvidia.IsEnabled = RestoreNvidia.IsEnabled = false;
            NvidiaProfileState.Text = "Traitement du profil NVIDIA en cours…";
            await action();
            if (optimization) _settings.LastNvidiaOptimizationUtc = DateTime.UtcNow;
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
                UpdateStatus.Text = $"Version {_availableUpdate.LatestVersion} disponible.";
                DownloadUpdate.Visibility = Visibility.Visible;
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

            var choice = System.Windows.MessageBox.Show(
                this,
                $"Une nouvelle version de FlexHub est disponible : {_availableUpdate.LatestVersion}.\n\nVoulez-vous la télécharger et l’installer maintenant ?",
                "Mise à jour de FlexHub",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (choice != MessageBoxResult.Yes) return;

            UpdateStatus.Text = $"Téléchargement de FlexHub {_availableUpdate.LatestVersion}…";
            await _updateService.DownloadAndStartInstallerAsync(_availableUpdate);
            ExitHub();
        }
        catch (Exception ex)
        {
            AppLog.Write($"Vérification automatique des mises à jour impossible : {ex.Message}");
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
    private void ExitHub() { _exit = true; _hotkeyService.Dispose(); _translatorHotkeyService.Dispose(); _responseGeneratorHotkeyService.Dispose(); _actionWheelHotkeyService.Dispose(); _reminderTimer.Stop(); _xmpTimer.Stop(); _tray.Visible = false; _tray.Dispose(); Close(); System.Windows.Application.Current.Shutdown(); }

    private sealed record LanguageChoice(string Name, string Code)
    {
        public override string ToString() => Name;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr handle);
}
