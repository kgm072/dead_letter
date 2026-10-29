using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DeadLetter.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string GameSceneName = "SampleScene";
        private const string VideoFileName = "Dead_Letter_Title_1080p.mp4";
        private const string VolumePreference = "dead_letter.master_volume";
        private const string MusicVolumePreference = "dead_letter.music_volume";
        private const string SfxVolumePreference = "dead_letter.sfx_volume";
        public const string MouseSensitivityPreference = "dead_letter.mouse_sensitivity";

        private static readonly Color Ink = new Color(0.025f, 0.02f, 0.025f, 0.94f);
        private static readonly Color Paper = new Color(0.87f, 0.84f, 0.76f, 1f);
        private static readonly Color Blood = new Color(0.62f, 0.035f, 0.045f, 1f);

        private Font menuFont;
        private CanvasGroup curtain;
        private GameObject mainPanel;
        private GameObject modalLayer;
        private GameObject optionsPanel;
        private GameObject exitPanel;
        private AudioSource videoAudio;
        private VideoPlayer videoPlayer;
        private RenderTexture videoTexture;
        private bool transitionStarted;
        private float masterVolume;
        private float musicVolume;
        private float sfxVolume;
        private float mouseSensitivity;
        private bool fullscreen;
        private Resolution[] resolutions;
        private int resolutionIndex;
        private Text resolutionValue;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            menuFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            masterVolume = PlayerPrefs.GetFloat(VolumePreference, 0.8f);
            musicVolume = PlayerPrefs.GetFloat(MusicVolumePreference, 0.65f);
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumePreference, 0.75f);
            mouseSensitivity = PlayerPrefs.GetFloat(MouseSensitivityPreference, 0.5f);
            fullscreen = Screen.fullScreen;
            PrepareResolutions();
            CreateCameraAndVideo();
            CreateEventSystem();
            CreateInterface();
            PreviewAudio();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || transitionStarted)
                return;

            if (exitPanel != null && exitPanel.activeSelf)
                HideExitConfirmation();
            else if (optionsPanel != null && optionsPanel.activeSelf)
                HideOptions();
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.errorReceived -= OnVideoError;
                videoPlayer.prepareCompleted -= OnVideoPrepared;
                videoPlayer.targetTexture = null;
            }

            if (videoTexture != null)
            {
                videoTexture.Release();
                Destroy(videoTexture);
            }
        }

        private void CreateCameraAndVideo()
        {
            GameObject cameraObject = new GameObject("Main Menu Camera");
            cameraObject.transform.SetParent(transform, false);
            Camera menuCamera = cameraObject.AddComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = Color.black;
            menuCamera.cullingMask = 0;
            cameraObject.AddComponent<AudioListener>();

            videoAudio = cameraObject.AddComponent<AudioSource>();
            videoAudio.playOnAwake = false;
            videoAudio.loop = true;

            videoPlayer = cameraObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.isLooping = true;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = Path.Combine(Application.streamingAssetsPath, VideoFileName);
            videoTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
            {
                name = "Main Menu Video"
            };
            videoTexture.Create();
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoTexture;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, videoAudio);
            videoPlayer.errorReceived += OnVideoError;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.Prepare();
        }

        private static void CreateEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private void CreateInterface()
        {
            GameObject canvasObject = new GameObject("Main Menu Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject contentObject = new GameObject("16:9 Menu Content");
            contentObject.transform.SetParent(canvasObject.transform, false);
            RectTransform content = contentObject.AddComponent<RectTransform>();
            Stretch(content);
            AspectRatioFitter contentRatio = contentObject.AddComponent<AspectRatioFitter>();
            contentRatio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            contentRatio.aspectRatio = 16f / 9f;

            GameObject videoObject = new GameObject("Animated Title Video");
            videoObject.transform.SetParent(content, false);
            RawImage videoImage = videoObject.AddComponent<RawImage>();
            videoImage.texture = videoTexture;
            videoImage.color = Color.white;
            videoImage.raycastTarget = false;
            Stretch(videoImage.rectTransform);

            mainPanel = CreatePanel(content, "Video Menu Hit Areas", Vector2.zero, Vector2.one);
            CreateMenuButton(mainPanel.transform, "START", new Vector2(0.055f, 0.405f), new Vector2(0.235f, 0.49f), StartGame);
            CreateMenuButton(mainPanel.transform, "OPTIONS", new Vector2(0.055f, 0.305f), new Vector2(0.235f, 0.39f), ShowOptions);
            CreateMenuButton(mainPanel.transform, "EXIT", new Vector2(0.055f, 0.205f), new Vector2(0.235f, 0.29f), ShowExitConfirmation);

            CreateModalInterface(content);

            GameObject curtainObject = new GameObject("Transition Curtain");
            curtainObject.transform.SetParent(canvasObject.transform, false);
            Image curtainImage = curtainObject.AddComponent<Image>();
            curtainImage.color = Color.black;
            Stretch(curtainImage.rectTransform);
            curtain = curtainObject.AddComponent<CanvasGroup>();
            curtain.alpha = 0f;
            curtain.blocksRaycasts = false;
        }

        private void CreateModalInterface(RectTransform content)
        {
            modalLayer = CreatePanel(content, "Modal Layer", Vector2.zero, Vector2.one);
            Image dimmer = modalLayer.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.76f);

            optionsPanel = CreateFramedPanel(modalLayer.transform, "Options Window",
                new Vector2(0.19f, 0.13f), new Vector2(0.81f, 0.87f));
            CreateText(optionsPanel.transform, "Options Header", "OPTIONS", 38, FontStyle.Bold,
                new Vector2(0.055f, 0.855f), new Vector2(0.55f, 0.965f), TextAnchor.MiddleLeft, Paper);
            CreateCloseButton(optionsPanel.transform, HideOptions);
            CreateRule(optionsPanel.transform, "Header Rule", 0.84f);
            CreateRule(optionsPanel.transform, "Footer Rule", 0.17f);
            CreateRule(optionsPanel.transform, "Column Rule", 0.5f, true);

            Color muted = new Color(0.72f, 0.69f, 0.63f, 1f);
            CreateText(optionsPanel.transform, "Audio Section", "AUDIO", 17, FontStyle.Bold,
                new Vector2(0.06f, 0.75f), new Vector2(0.46f, 0.82f), TextAnchor.MiddleLeft, muted);
            CreateText(optionsPanel.transform, "Display Section", "DISPLAY / CONTROL", 17, FontStyle.Bold,
                new Vector2(0.54f, 0.75f), new Vector2(0.94f, 0.82f), TextAnchor.MiddleLeft, muted);

            CreateSettingsSlider(optionsPanel.transform, "MASTER VOLUME", 0.63f, 0.72f, masterVolume,
                value => { masterVolume = value; PreviewAudio(); });
            CreateSettingsSlider(optionsPanel.transform, "MUSIC VOLUME", 0.49f, 0.58f, musicVolume,
                value => { musicVolume = value; PreviewAudio(); });
            CreateSettingsSlider(optionsPanel.transform, "SFX VOLUME", 0.35f, 0.44f, sfxVolume,
                value => sfxVolume = value);

            CreateFullscreenControl(optionsPanel.transform);
            CreateResolutionControl(optionsPanel.transform);
            CreateMouseSensitivityControl(optionsPanel.transform);
            CreateDialogButton(optionsPanel.transform, "APPLY", new Vector2(0.32f, 0.055f), new Vector2(0.48f, 0.13f), ApplyOptions);
            CreateDialogButton(optionsPanel.transform, "BACK", new Vector2(0.56f, 0.055f), new Vector2(0.72f, 0.13f), HideOptions);

            exitPanel = CreateFramedPanel(modalLayer.transform, "Exit Window",
                new Vector2(0.29f, 0.29f), new Vector2(0.71f, 0.71f));
            CreateText(exitPanel.transform, "Exit Header", "EXIT", 38, FontStyle.Bold,
                new Vector2(0.075f, 0.76f), new Vector2(0.55f, 0.94f), TextAnchor.MiddleLeft, Paper);
            CreateCloseButton(exitPanel.transform, HideExitConfirmation);
            CreateRule(exitPanel.transform, "Header Rule", 0.74f);
            CreateText(exitPanel.transform, "Exit Question", "ARE YOU SURE YOU WANT TO EXIT?", 23, FontStyle.Bold,
                new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.64f), TextAnchor.MiddleCenter, Paper);
            CreateText(exitPanel.transform, "Exit Warning", "THE GAME WILL CLOSE.", 16, FontStyle.Normal,
                new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.49f), TextAnchor.MiddleCenter, muted);
            CreateDialogButton(exitPanel.transform, "YES", new Vector2(0.17f, 0.13f), new Vector2(0.45f, 0.27f), ExitGame);
            CreateDialogButton(exitPanel.transform, "NO", new Vector2(0.55f, 0.13f), new Vector2(0.83f, 0.27f), HideExitConfirmation, true);

            optionsPanel.SetActive(false);
            exitPanel.SetActive(false);
            modalLayer.SetActive(false);
        }

        private GameObject CreateFramedPanel(Transform parent, string name, Vector2 min, Vector2 max)
        {
            GameObject panel = CreatePanel(parent, name, min, max);
            Image background = panel.AddComponent<Image>();
            background.color = Ink;
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.72f, 0.7f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject inner = CreatePanel(panel.transform, "Inner Border", new Vector2(0.012f, 0.018f), new Vector2(0.988f, 0.982f));
            Image innerImage = inner.AddComponent<Image>();
            innerImage.color = Color.clear;
            Outline innerOutline = inner.AddComponent<Outline>();
            innerOutline.effectColor = new Color(0.28f, 0.28f, 0.28f, 0.9f);
            innerOutline.effectDistance = new Vector2(1f, -1f);
            innerImage.raycastTarget = false;
            return panel;
        }

        private void CreateCloseButton(Transform parent, UnityEngine.Events.UnityAction action)
        {
            CreateDialogButton(parent, "[X]", new Vector2(0.89f, 0.86f), new Vector2(0.96f, 0.96f), action, false, 17);
        }

        private static void CreateRule(Transform parent, string name, float position, bool vertical = false)
        {
            Image line = CreateImage(parent, name, new Color(0.34f, 0.34f, 0.33f, 0.85f));
            if (vertical)
                SetAnchors(line.rectTransform, new Vector2(position, 0.21f), new Vector2(position, 0.79f), new Vector2(-1f, 0f), new Vector2(1f, 0f));
            else
                SetAnchors(line.rectTransform, new Vector2(0.055f, position), new Vector2(0.945f, position), new Vector2(0f, -1f), new Vector2(0f, 1f));
        }

        private void CreateSettingsSlider(Transform parent, string label, float yMin, float yMax, float initialValue,
            UnityEngine.Events.UnityAction<float> changed)
        {
            CreateText(parent, label + " Label", label, 17, FontStyle.Bold,
                new Vector2(0.06f, yMin), new Vector2(0.25f, yMax), TextAnchor.MiddleLeft, Paper);

            GameObject sliderObject = new GameObject(label + " Slider");
            sliderObject.transform.SetParent(parent, false);
            Slider slider = sliderObject.AddComponent<Slider>();
            SetAnchors(sliderObject.GetComponent<RectTransform>(), new Vector2(0.25f, yMin + 0.025f), new Vector2(0.42f, yMax - 0.025f), Vector2.zero, Vector2.zero);
            Image background = CreateImage(sliderObject.transform, "Track", new Color(0.31f, 0.3f, 0.29f, 1f));
            Stretch(background.rectTransform, 0f, 0f, 10f, 10f);
            Image fill = CreateImage(sliderObject.transform, "Fill", Blood);
            Stretch(fill.rectTransform, 0f, 0f, 10f, 10f);
            Image handle = CreateImage(sliderObject.transform, "Handle", Paper);
            handle.rectTransform.sizeDelta = new Vector2(13f, 28f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initialValue;

            Text valueText = CreateText(parent, label + " Value", Mathf.RoundToInt(initialValue * 100f).ToString(), 16, FontStyle.Bold,
                new Vector2(0.425f, yMin), new Vector2(0.47f, yMax), TextAnchor.MiddleRight, Paper);
            slider.onValueChanged.AddListener(value =>
            {
                valueText.text = Mathf.RoundToInt(value * 100f).ToString();
                changed(value);
            });
        }

        private void CreateFullscreenControl(Transform parent)
        {
            CreateText(parent, "Fullscreen Label", "FULLSCREEN", 17, FontStyle.Bold,
                new Vector2(0.54f, 0.63f), new Vector2(0.73f, 0.72f), TextAnchor.MiddleLeft, Paper);
            Button button = CreateDialogButton(parent, "ON", new Vector2(0.79f, 0.645f), new Vector2(0.93f, 0.705f), null);
            Text value = button.GetComponentInChildren<Text>();
            value.text = fullscreen ? "ON" : "OFF";
            button.onClick.AddListener(() =>
            {
                fullscreen = !fullscreen;
                value.text = fullscreen ? "ON" : "OFF";
            });
        }

        private void CreateResolutionControl(Transform parent)
        {
            CreateText(parent, "Resolution Label", "RESOLUTION", 17, FontStyle.Bold,
                new Vector2(0.54f, 0.49f), new Vector2(0.73f, 0.58f), TextAnchor.MiddleLeft, Paper);
            CreateDialogButton(parent, "<", new Vector2(0.76f, 0.505f), new Vector2(0.79f, 0.565f), () => ChangeResolution(-1), false, 18);
            GameObject valueBox = CreatePanel(parent, "Resolution Value", new Vector2(0.79f, 0.505f), new Vector2(0.91f, 0.565f));
            Image box = valueBox.AddComponent<Image>();
            box.color = new Color(1f, 1f, 1f, 0.035f);
            Outline outline = valueBox.AddComponent<Outline>();
            outline.effectColor = new Color(0.38f, 0.38f, 0.37f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
            resolutionValue = CreateText(valueBox.transform, "Label", GetResolutionLabel(), 16, FontStyle.Bold,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, Paper);
            CreateDialogButton(parent, ">", new Vector2(0.91f, 0.505f), new Vector2(0.94f, 0.565f), () => ChangeResolution(1), false, 18);
        }

        private void CreateMouseSensitivityControl(Transform parent)
        {
            CreateText(parent, "Mouse Label", "MOUSE SENSITIVITY", 17, FontStyle.Bold,
                new Vector2(0.54f, 0.34f), new Vector2(0.75f, 0.44f), TextAnchor.MiddleLeft, Paper);
            GameObject sliderObject = new GameObject("Mouse Sensitivity Slider");
            sliderObject.transform.SetParent(parent, false);
            Slider slider = sliderObject.AddComponent<Slider>();
            SetAnchors(sliderObject.GetComponent<RectTransform>(), new Vector2(0.76f, 0.365f), new Vector2(0.91f, 0.415f), Vector2.zero, Vector2.zero);
            Image track = CreateImage(sliderObject.transform, "Track", new Color(0.31f, 0.3f, 0.29f, 1f));
            Stretch(track.rectTransform, 0f, 0f, 10f, 10f);
            Image fill = CreateImage(sliderObject.transform, "Fill", Blood);
            Stretch(fill.rectTransform, 0f, 0f, 10f, 10f);
            Image handle = CreateImage(sliderObject.transform, "Handle", Paper);
            handle.rectTransform.sizeDelta = new Vector2(13f, 28f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0.1f;
            slider.maxValue = 1f;
            slider.value = mouseSensitivity;
            Text valueText = CreateText(parent, "Mouse Value", Mathf.RoundToInt(mouseSensitivity * 10f).ToString(), 16, FontStyle.Bold,
                new Vector2(0.915f, 0.34f), new Vector2(0.95f, 0.44f), TextAnchor.MiddleRight, Paper);
            slider.onValueChanged.AddListener(value =>
            {
                mouseSensitivity = value;
                valueText.text = Mathf.RoundToInt(value * 10f).ToString();
            });
        }

        private Button CreateDialogButton(Transform parent, string label, Vector2 min, Vector2 max,
            UnityEngine.Events.UnityAction action, bool selected = false, int fontSize = 19)
        {
            GameObject buttonObject = CreatePanel(parent, label + " Button", min, max);
            Image background = buttonObject.AddComponent<Image>();
            background.color = selected ? new Color(0.24f, 0.025f, 0.03f, 0.6f) : new Color(1f, 1f, 1f, 0.025f);
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = selected ? Blood : new Color(0.4f, 0.4f, 0.39f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = background.color;
            colors.highlightedColor = new Color(0.62f, 0.035f, 0.045f, 0.45f);
            colors.pressedColor = new Color(0.62f, 0.035f, 0.045f, 0.75f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            if (action != null)
                button.onClick.AddListener(action);
            CreateText(buttonObject.transform, "Label", label, fontSize, FontStyle.Bold,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, Paper);
            return button;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 min, Vector2 max)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            SetAnchors(rect, min, max, Vector2.zero, Vector2.zero);
            return panel;
        }

        private static void CreateMenuButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
            UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);
            Image background = buttonObject.AddComponent<Image>();
            background.color = Color.clear;
            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.clear;
            colors.highlightedColor = new Color(0.62f, 0.035f, 0.045f, 0.32f);
            colors.pressedColor = new Color(0.62f, 0.035f, 0.045f, 0.58f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetAnchors(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        }

        private void CreateVolumeSlider(Transform parent)
        {
            GameObject sliderObject = new GameObject("Master Volume");
            sliderObject.transform.SetParent(parent, false);
            Slider slider = sliderObject.AddComponent<Slider>();
            RectTransform rect = sliderObject.GetComponent<RectTransform>();
            SetAnchors(rect, new Vector2(0f, 0.53f), new Vector2(1f, 0.61f), Vector2.zero, Vector2.zero);

            Image background = CreateImage(sliderObject.transform, "Background", new Color(0.32f, 0.3f, 0.28f, 1f));
            Stretch(background.rectTransform, 0f, 0f, 0f, 0f);
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            Stretch(fillAreaRect, 0f, 0f, 0f, 0f);
            Image fill = CreateImage(fillArea.transform, "Fill", Blood);
            Stretch(fill.rectTransform);
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            Stretch(handleAreaRect, 8f, 8f, 0f, 0f);
            Image handle = CreateImage(handleArea.transform, "Handle", Paper);
            RectTransform handleRect = handle.rectTransform;
            handleRect.sizeDelta = new Vector2(16f, 34f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = PlayerPrefs.GetFloat(VolumePreference, 0.8f);
            slider.onValueChanged.AddListener(ApplySavedVolume);
        }

        private void CreateFullscreenToggle(Transform parent)
        {
            GameObject toggleObject = new GameObject("Fullscreen Toggle");
            toggleObject.transform.SetParent(parent, false);
            Toggle toggle = toggleObject.AddComponent<Toggle>();
            RectTransform rect = toggleObject.GetComponent<RectTransform>();
            SetAnchors(rect, new Vector2(0f, 0.34f), new Vector2(1f, 0.48f), Vector2.zero, Vector2.zero);

            Image box = CreateImage(toggleObject.transform, "Box", new Color(0.18f, 0.17f, 0.16f, 1f));
            SetAnchors(box.rectTransform, new Vector2(0f, 0.15f), new Vector2(0.1f, 0.85f), Vector2.zero, Vector2.zero);
            Image check = CreateImage(box.transform, "Checkmark", Blood);
            Stretch(check.rectTransform, 5f, 5f, 5f, 5f);
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = Screen.fullScreen;
            toggle.onValueChanged.AddListener(value => Screen.fullScreen = value);
            CreateText(toggleObject.transform, "Label", "FULLSCREEN", 18, FontStyle.Bold,
                new Vector2(0.15f, 0f), new Vector2(1f, 1f), TextAnchor.MiddleLeft, new Color(0.72f, 0.69f, 0.63f));
        }

        private void CreateBackButton(Transform parent)
        {
            GameObject buttonObject = new GameObject("Back");
            buttonObject.transform.SetParent(parent, false);
            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.05f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(HideOptions);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetAnchors(rect, new Vector2(0f, 0f), new Vector2(1f, 0.22f), Vector2.zero, Vector2.zero);
            CreateText(buttonObject.transform, "Label", "<  BACK", 22, FontStyle.Bold,
                new Vector2(0.05f, 0f), new Vector2(0.95f, 1f), TextAnchor.MiddleLeft, Paper);
        }

        private Text CreateText(Transform parent, string name, string value, int size, FontStyle style,
            Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = menuFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            SetAnchors(text.rectTransform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one, new Vector2(left, bottom), new Vector2(-right, -top));
        }

        private void PrepareResolutions()
        {
            resolutions = Screen.resolutions;
            if (resolutions == null || resolutions.Length == 0)
            {
                resolutions = new[] { new Resolution() };
                resolutionIndex = 0;
                return;
            }

            resolutionIndex = resolutions.Length - 1;
            for (int i = 0; i < resolutions.Length; i++)
            {
                if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                    resolutionIndex = i;
            }
        }

        private string GetResolutionLabel()
        {
            Resolution resolution = resolutions[resolutionIndex];
            int width = resolution.width > 0 ? resolution.width : Screen.width;
            int height = resolution.height > 0 ? resolution.height : Screen.height;
            return $"{width} x {height}";
        }

        private void ChangeResolution(int direction)
        {
            resolutionIndex = (resolutionIndex + direction + resolutions.Length) % resolutions.Length;
            if (resolutionValue != null)
                resolutionValue.text = GetResolutionLabel();
        }

        private void PreviewAudio()
        {
            AudioListener.volume = Mathf.Clamp01(masterVolume);
            if (videoAudio != null)
                videoAudio.volume = Mathf.Clamp01(masterVolume * musicVolume);
        }

        private void ApplyOptions()
        {
            Resolution resolution = resolutions[resolutionIndex];
            int width = resolution.width > 0 ? resolution.width : Screen.width;
            int height = resolution.height > 0 ? resolution.height : Screen.height;
            Screen.SetResolution(width, height, fullscreen);
            PlayerPrefs.SetFloat(VolumePreference, masterVolume);
            PlayerPrefs.SetFloat(MusicVolumePreference, musicVolume);
            PlayerPrefs.SetFloat(SfxVolumePreference, sfxVolume);
            PlayerPrefs.SetFloat(MouseSensitivityPreference, mouseSensitivity);
            PlayerPrefs.Save();
            PreviewAudio();
        }

        private void ApplySavedVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            PreviewAudio();
            PlayerPrefs.SetFloat(VolumePreference, masterVolume);
        }

        private void ShowOptions()
        {
            mainPanel.SetActive(false);
            modalLayer.SetActive(true);
            exitPanel.SetActive(false);
            optionsPanel.SetActive(true);
        }

        private void HideOptions()
        {
            optionsPanel.SetActive(false);
            modalLayer.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void ShowExitConfirmation()
        {
            mainPanel.SetActive(false);
            modalLayer.SetActive(true);
            optionsPanel.SetActive(false);
            exitPanel.SetActive(true);
        }

        private void HideExitConfirmation()
        {
            exitPanel.SetActive(false);
            modalLayer.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void StartGame()
        {
            if (!transitionStarted)
                StartCoroutine(LoadGameAfterFade());
        }

        private IEnumerator LoadGameAfterFade()
        {
            transitionStarted = true;
            curtain.blocksRaycasts = true;
            float elapsed = 0f;
            const float duration = 0.45f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                curtain.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            SceneManager.LoadScene(GameSceneName);
        }

        private static void ExitGame()
        {
            Debug.Log("Exit requested from the DEAD LETTER main menu.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void OnVideoError(VideoPlayer player, string message)
        {
            Debug.LogError($"Main menu video could not be played: {message}");
        }

        private static void OnVideoPrepared(VideoPlayer player)
        {
            player.Play();
        }
    }
}
