namespace PersonalAppsHub.Models;

public sealed class HubSettings
{
    public int SettingsSchemaVersion { get; set; } = 2;
    public bool StartWithWindows { get; set; } = true;
    public string FontSizePreference { get; set; } = "Moyen";
    public string ThemePreference { get; set; } = "Sombre";
    public bool ReminderEnabled { get; set; } = true;
    public int ReminderIntervalMinutes { get; set; } = 130;
    public int ReminderDisplaySeconds { get; set; } = 20;
    public string ReminderUrl { get; set; } = "https://top-serveurs.net/project-zomboid/fr-nextgen-france-project-zomboid-nouveau-serveur";
    public DateTime? LastReminderUtc { get; set; }
    public bool CorrectorEnabled { get; set; } = true;
    public bool UseOpenAi { get; set; }
    public string CorrectionProvider { get; set; } = "Local";
    public bool PreviewBeforeReplace { get; set; } = true;
    public bool FallbackToLocal { get; set; } = true;
    public string Hotkey { get; set; } = "Ctrl+Alt+F8";
    public string Language { get; set; } = "fr-FR";
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string GeminiModel { get; set; } = "gemini-3.5-flash-lite";
    public string LanguageToolUrl { get; set; } = "http://localhost:8081";
    public bool TranslatorEnabled { get; set; } = true;
    public string TranslatorProvider { get; set; } = "DeepL";
    public string TranslatorHotkey { get; set; } = "Ctrl+Alt+F9";
    public string TranslationSourceLanguage { get; set; } = "auto";
    public string TranslationTargetLanguage { get; set; } = "EN";
    public bool TranslationPreviewBeforeReplace { get; set; } = true;
    public bool DeepLUseFreeApi { get; set; } = true;
    public string TranslatorGeminiModel { get; set; } = "gemini-3.5-flash-lite";
    public string LibreTranslateUrl { get; set; } = "https://libretranslate.com";
    public string MyMemoryEmail { get; set; } = "";
    public bool ResponseGeneratorEnabled { get; set; } = true;
    public string ResponseGeneratorProvider { get; set; } = "Gemini";
    public string ResponseGeneratorHotkey { get; set; } = "Ctrl+Alt+F10";
    public string ResponseGeneratorTone { get; set; } = "Naturel";
    public string ResponseGeneratorInstruction { get; set; } = "Réponds dans la langue du message sélectionné.";
    public string ResponseGeneratorOpenAiModel { get; set; } = "gpt-4o-mini";
    public string ResponseGeneratorGeminiModel { get; set; } = "gemini-3.5-flash-lite";
    public bool ResponseGeneratorPreview { get; set; } = true;
    public bool ActionWheelEnabled { get; set; } = true;
    public string ActionWheelHotkey { get; set; } = "Ctrl+Alt+Space";
    public bool NvidiaOptimizerEnabled { get; set; } = true;
    public DateTime? LastNvidiaOptimizationUtc { get; set; }
    public bool XmpMonitorEnabled { get; set; }
    public int XmpCheckIntervalMinutes { get; set; } = 360;
    public bool MemorySetupCompleted { get; set; }
    public string MemoryType { get; set; } = "";
    public bool XmpUseCustomSpeed { get; set; }
    public int XmpExpectedSpeed { get; set; } = 3200;
    public DateTime? LastXmpCheckUtc { get; set; }
    public bool AiPrivacyConsentAccepted { get; set; }
    public string GitHubRepository { get; set; } = "";
    public DateTime? LastUpdateCheckUtc { get; set; }
}
