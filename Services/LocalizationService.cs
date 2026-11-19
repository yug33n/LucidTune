using System;
using System.Collections.Generic;

namespace LucidTune.Services
{
    public static class LocalizationService
    {
        public static string CurrentLanguage { get; private set; } = "ru";
        public static event Action? LanguageChanged;

        public static bool IsRussian => CurrentLanguage == "ru";
        public static bool IsEnglish => CurrentLanguage == "en";

        public static void SetLanguage(string lang)
        {
            var normalized = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";
            if (CurrentLanguage != normalized)
            {
                CurrentLanguage = normalized;
                LanguageChanged?.Invoke();
            }
        }

        public static string T(string key) => Get(key);

        public static string Get(string key)
        {
            if (IsEnglish && StringsEn.TryGetValue(key, out var enVal)) return enVal;
            if (StringsRu.TryGetValue(key, out var ruVal)) return ruVal;
            return key;
        }

        private static readonly Dictionary<string, string> StringsRu = new(StringComparer.OrdinalIgnoreCase)
        {
            // Common
            ["AppName"] = "LucidTune",
            ["Save"] = "Сохранить",
            ["Cancel"] = "Отмена",
            ["Close"] = "Закрыть",
            ["Exit"] = "Выход",
            ["Hide"] = "Скрыть",
            ["Show"] = "Показать",
            ["Settings"] = "Настройки",
            ["WidgetSettings"] = "Настройки виджета",
            ["Updates"] = "Обновления",
            ["CheckUpdates"] = "Проверить обновления",
            ["Volume"] = "Громкость",
            ["VolumeWindows"] = "Громкость (Windows)",

            // Playback & Track
            ["NoActiveTrack"] = "Нет активного трека",
            ["NoTrack"] = "Нет трека",
            ["PlayMusic"] = "Запустите музыку",
            ["UnknownArtist"] = "Неизвестный исполнитель",
            ["PrevTrack"] = "Предыдущий",
            ["PlayPause"] = "Воспроизведение / Пауза",
            ["NextTrack"] = "Следующий",

            // Settings Header
            ["SettingsTitle"] = "Настройки LucidTune",
            ["SettingsSubtitle"] = "Персонализация виджета, горячие клавиши и интеграции",
            ["StatusDesktopActive"] = "Оверлей рабочего стола активен",
            ["StatusTaskbarActive"] = "Виджет в панели задач активен",
            ["StatusReadyToCapture"] = "Готов к перехвату медиа (Spotify, браузеры, Telegram)",
            ["OverlayActive"] = "Оверлей активен",
            ["OverlayActiveDesc"] = "Оверлей отображается на экране и готов к воспроизведению.",
            ["OverlayHidden"] = "Оверлей скрыт",
            ["OverlayHiddenDesc"] = "Оверлей скрыт с экрана. Нажмите «Показать» или горячую клавишу.",

            // Language
            ["LanguageTitle"] = "Язык интерфейса",
            ["LanguageDesc"] = "Переключение языка приложения (Русский / English).",

            // Theme & Appearance
            ["ThemeTitle"] = "Тема оформления",
            ["ThemeDesc"] = "Переключение между тёмным и светлым стилем.",
            ["ThemeDark"] = "Тёмная",
            ["ThemeLight"] = "Светлая",
            ["AccentTitle"] = "Основной цвет",
            ["AccentDesc"] = "Цветовой акцент для кнопок, ползунков и бейджей.",

            ["AdaptiveColorTitle"] = "Адаптивный цвет под обложку",
            ["AdaptiveColorDesc"] = "Автоматически подбирает цвет акцента под цветовую гамму текущей обложки трека.",
            ["AdaptiveShimmerTitle"] = "Перелив фона в цветах обложки",
            ["AdaptiveShimmerDesc"] = "Фон оверлея плавно переливается живым градиентом в оттенках текущей обложки.",

            ["OpacityTitle"] = "Прозрачность виджета",
            ["OpacityDesc"] = "Настройка видимости оверлея поверх других окон.",

            ["CompactModeTitle"] = "Компактный виджет",
            ["CompactModeDesc"] = "Скрывает изображение альбома, делая оверлей меньше и компактнее.",

            ["ShowVolumeTitle"] = "Показывать регулировку громкости",
            ["ShowVolumeDesc"] = "Отображает иконку и полоску управления громкостью внизу виджета.",

            ["AudioSpectrumTitle"] = "Анимированный аудио-спектр",
            ["AudioSpectrumDesc"] = "Показывает пульсирующие полоски эквалайзера в полном режиме оверлея на экране.",

            // Taskbar Dock
            ["TaskbarModeTitle"] = "Виджет в панели задач (Taskbar Dock)",
            ["TaskbarModeDesc"] = "Переносит музыкальный плеер в панель задач Windows вместо оверлея на рабочем столе.",
            ["TaskbarPositionTitle"] = "Расположение в панели задач",
            ["TaskbarPositionDesc"] = "Справа или слева в панели задач.",
            ["TaskbarPosRight"] = "Справа",
            ["TaskbarPosLeft"] = "Слева",
            ["TaskbarAutoHideVideoTitle"] = "Скрывать при полноэкранном видео (16:9, 21:9)",
            ["TaskbarAutoHideVideoDesc"] = "Автоматически скрывает мини-плеер на широких экранах, чтобы не перекрывать кадр видео.",
            ["TaskbarVideoBehaviorTitle"] = "При полноэкранном видео",
            ["TaskbarVideoBehaviorDesc"] = "Поведение виджета в таскбаре при открытии видео во весь экран.",
            ["TaskbarVideoBehaviorSnap"] = "Смещать в угол экрана",
            ["TaskbarVideoBehaviorHide"] = "Скрывать виджет",
            ["TaskbarVideoBehaviorNone"] = "Не изменять позицию",

            // Snapping & Capture
            ["SnapToEdgesTitle"] = "Прилипание к краям экрана",
            ["SnapToEdgesDesc"] = "Автоматически выравнивает виджет вдоль границ монитора при перемещении.",
            ["ExcludeFromCaptureTitle"] = "Скрывать при захвате экрана (OBS / Discord)",
            ["ExcludeFromCaptureDesc"] = "Оверлей не отображается на стримах и видеозаписях при захвате рабочего стола.",

            // System & Game
            ["AutoHideTitle"] = "Скрывать в полноэкранных играх",
            ["AutoHideDesc"] = "Автоматически скрывает виджет при запуске полноэкранных игр и видео.",
            ["AutoStartTitle"] = "Запускать вместе с Windows",
            ["AutoStartDesc"] = "Автоматический запуск LucidTune при старте операционной системы.",
            ["ShowSettingsOnStartupTitle"] = "Показывать настройки при запуске",
            ["ShowSettingsOnStartupDesc"] = "Открывать окно настроек каждый раз при обычном запуске программы.",

            // Discord RPC
            ["DiscordSectionTitle"] = "Discord Rich Presence",
            ["DiscordEnableTitle"] = "Отображать статус в Discord",
            ["DiscordEnableDesc"] = "Показывать название песни, исполнителя, обложку и кнопку перехода на сайт приложения.",
            ["DiscordIgnoreExternalMediaTitle"] = "Игнорировать сторонние медиа (YouTube, Twitch, Telegram)",
            ["DiscordIgnoreExternalMediaDesc"] = "Не транслировать в статус Discord видеоролики из браузера и голосовые сообщения Telegram.",
            ["DiscordIgnoreBrowserVideosTitle"] = "Игнорировать видео в браузере (YouTube, Twitch)",
            ["DiscordIgnoreBrowserVideosDesc"] = "Не транслировать видеоролики из YouTube и Twitch как музыку в статус Discord.",
            ["DiscordIgnoreTelegramVoiceTitle"] = "Игнорировать голосовые сообщения Telegram",
            ["DiscordIgnoreTelegramVoiceDesc"] = "Не транслировать в статус Discord голосовые сообщения и видеозаметки из Telegram.",

            // Hotkeys
            ["HotkeysSectionTitle"] = "Горячие клавиши",
            ["HotkeyToggle"] = "Скрыть / показать виджет",
            ["HotkeyMediaTitle"] = "Горячие клавиши управления музыкой",
            ["HotkeyPlayPause"] = "Пауза / продолжить",
            ["HotkeyNext"] = "Следующий трек",
            ["HotkeyPrev"] = "Предыдущий трек",

            // Tray & Context
            ["TraySwitchToDesktop"] = "Переключить на рабочий стол",
            ["TraySwitchToTaskbar"] = "Виджет в панели задач",
            ["TrayAutoStart"] = "Автозагрузка",
            ["TrayExit"] = "Выход"
        };

        private static readonly Dictionary<string, string> StringsEn = new(StringComparer.OrdinalIgnoreCase)
        {
            // Common
            ["AppName"] = "LucidTune",
            ["Save"] = "Save",
            ["Cancel"] = "Cancel",
            ["Close"] = "Close",
            ["Exit"] = "Exit",
            ["Hide"] = "Hide",
            ["Show"] = "Show",
            ["Settings"] = "Settings",
            ["WidgetSettings"] = "Widget Settings",
            ["Updates"] = "Updates",
            ["CheckUpdates"] = "Check for Updates",
            ["Volume"] = "Volume",
            ["VolumeWindows"] = "Volume (Windows)",

            // Playback & Track
            ["NoActiveTrack"] = "No active track",
            ["NoTrack"] = "No track",
            ["PlayMusic"] = "Play some music",
            ["UnknownArtist"] = "Unknown Artist",
            ["PrevTrack"] = "Previous",
            ["PlayPause"] = "Play / Pause",
            ["NextTrack"] = "Next",

            // Settings Header
            ["SettingsTitle"] = "LucidTune Settings",
            ["SettingsSubtitle"] = "Widget customization, hotkeys and integrations",
            ["StatusDesktopActive"] = "Desktop overlay is active",
            ["StatusTaskbarActive"] = "Taskbar dock is active",
            ["StatusReadyToCapture"] = "Ready to capture media (Spotify, browsers, Telegram)",
            ["OverlayActive"] = "Overlay is active",
            ["OverlayActiveDesc"] = "Overlay is visible on screen and ready to capture media.",
            ["OverlayHidden"] = "Overlay is hidden",
            ["OverlayHiddenDesc"] = "Overlay is hidden. Click 'Show' or use the hotkey to restore.",

            // Language
            ["LanguageTitle"] = "Interface Language",
            ["LanguageDesc"] = "Switch application language (Russian / English).",

            // Theme & Appearance
            ["ThemeTitle"] = "Appearance Theme",
            ["ThemeDesc"] = "Toggle between Dark and Light appearance style.",
            ["ThemeDark"] = "Dark",
            ["ThemeLight"] = "Light",
            ["AccentTitle"] = "Accent Color",
            ["AccentDesc"] = "Color accent for buttons, progress bars and badges.",

            ["AdaptiveColorTitle"] = "Adaptive Cover Color",
            ["AdaptiveColorDesc"] = "Automatically extracts the primary accent color from current track artwork.",
            ["AdaptiveShimmerTitle"] = "Ambient Cover Shimmer",
            ["AdaptiveShimmerDesc"] = "Overlay background subtly animates with an ambient gradient in album colors.",

            ["OpacityTitle"] = "Widget Opacity",
            ["OpacityDesc"] = "Adjust overlay visibility over other desktop windows.",

            ["CompactModeTitle"] = "Compact Widget",
            ["CompactModeDesc"] = "Hides album artwork to make the overlay slimmer and smaller.",

            ["ShowVolumeTitle"] = "Show Volume Control",
            ["ShowVolumeDesc"] = "Displays volume icon and slider at the bottom of the widget.",

            ["AudioSpectrumTitle"] = "Animated Audio Spectrum",
            ["AudioSpectrumDesc"] = "Displays pulsing equalizer spectrum bars in the full desktop overlay.",

            // Taskbar Dock
            ["TaskbarModeTitle"] = "Taskbar Dock Widget",
            ["TaskbarModeDesc"] = "Docks the media player into the Windows taskbar instead of the desktop overlay.",
            ["TaskbarPositionTitle"] = "Taskbar Widget Position",
            ["TaskbarPositionDesc"] = "Select placement on the taskbar (Right or Left).",
            ["TaskbarPosRight"] = "Right",
            ["TaskbarPosLeft"] = "Left",
            ["TaskbarAutoHideVideoTitle"] = "Auto-hide on fullscreen video (16:9, 21:9)",
            ["TaskbarAutoHideVideoDesc"] = "Automatically hides taskbar widget on widescreen monitors to avoid overlapping the video.",
            ["TaskbarVideoBehaviorTitle"] = "On Fullscreen Video",
            ["TaskbarVideoBehaviorDesc"] = "Taskbar widget behavior when a video is played in fullscreen.",
            ["TaskbarVideoBehaviorSnap"] = "Move to screen corner",
            ["TaskbarVideoBehaviorHide"] = "Hide widget",
            ["TaskbarVideoBehaviorNone"] = "Keep position",

            // Snapping & Capture
            ["SnapToEdgesTitle"] = "Snap to Screen Edges",
            ["SnapToEdgesDesc"] = "Automatically snaps the widget along display boundaries when dragged.",
            ["ExcludeFromCaptureTitle"] = "Hide from Screen Capture (OBS / Discord)",
            ["ExcludeFromCaptureDesc"] = "Overlay will be invisible in screen recordings and stream captures.",

            // System & Game
            ["AutoHideTitle"] = "Auto-hide in Fullscreen Games",
            ["AutoHideDesc"] = "Automatically hides widget when playing fullscreen games or videos.",
            ["AutoStartTitle"] = "Start with Windows",
            ["AutoStartDesc"] = "Automatically start LucidTune when logging into Windows.",
            ["ShowSettingsOnStartupTitle"] = "Open Settings on Startup",
            ["ShowSettingsOnStartupDesc"] = "Show settings window whenever application is launched directly.",

            // Discord RPC
            ["DiscordSectionTitle"] = "Discord Rich Presence",
            ["DiscordEnableTitle"] = "Enable Discord Rich Presence",
            ["DiscordEnableDesc"] = "Show current track title, artist, artwork and application button on your Discord profile.",
            ["DiscordIgnoreExternalMediaTitle"] = "Ignore external media (YouTube, Twitch, Telegram)",
            ["DiscordIgnoreExternalMediaDesc"] = "Prevent web browser videos and Telegram voice notes from showing in Discord status.",
            ["DiscordIgnoreBrowserVideosTitle"] = "Ignore Browser Videos (YouTube, Twitch)",
            ["DiscordIgnoreBrowserVideosDesc"] = "Prevent web videos on YouTube and Twitch from broadcasting as songs in Discord.",
            ["DiscordIgnoreTelegramVoiceTitle"] = "Ignore Telegram Voice Messages",
            ["DiscordIgnoreTelegramVoiceDesc"] = "Prevent voice notes and video messages in Telegram from showing in Discord status.",

            // Hotkeys
            ["HotkeysSectionTitle"] = "Global Hotkeys",
            ["HotkeyToggle"] = "Show / Hide Widget",
            ["HotkeyMediaTitle"] = "Media Control Hotkeys",
            ["HotkeyPlayPause"] = "Play / Pause",
            ["HotkeyNext"] = "Next Track",
            ["HotkeyPrev"] = "Previous Track",

            // Tray & Context
            ["TraySwitchToDesktop"] = "Switch to Desktop Widget",
            ["TraySwitchToTaskbar"] = "Taskbar Dock Widget",
            ["TrayAutoStart"] = "Start with Windows",
            ["TrayExit"] = "Exit"
        };
    }
}
