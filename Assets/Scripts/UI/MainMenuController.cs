using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
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

        private static readonly Color Ink = new Color(0.025f, 0.02f, 0.025f, 0.94f);
        private static readonly Color Paper = new Color(0.87f, 0.84f, 0.76f, 1f);
        private static readonly Color Blood = new Color(0.62f, 0.035f, 0.045f, 1f);

        private Font menuFont;
        private CanvasGroup curtain;
        private GameObject mainPanel;
        private GameObject optionsPanel;
        private AudioSource videoAudio;
        private VideoPlayer videoPlayer;
        private RenderTexture videoTexture;
        private bool transitionStarted;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            menuFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateCameraAndVideo();
            CreateEventSystem();
            CreateInterface();
            ApplySavedVolume(PlayerPrefs.GetFloat(VolumePreference, 0.8f));
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
            CreateMenuButton(mainPanel.transform, "EXIT", new Vector2(0.055f, 0.205f), new Vector2(0.235f, 0.29f), ExitGame);

            optionsPanel = CreatePanel(content, "Options", new Vector2(0.045f, 0.13f), new Vector2(0.35f, 0.56f));
            Image optionsBackground = optionsPanel.AddComponent<Image>();
            optionsBackground.color = Ink;
            CreateText(optionsPanel.transform, "Options Header", "OPTIONS", 34, FontStyle.Bold,
                new Vector2(0f, 0.82f), new Vector2(1f, 1f), TextAnchor.MiddleLeft, Paper);
            CreateText(optionsPanel.transform, "Volume Label", "MASTER VOLUME", 18, FontStyle.Bold,
                new Vector2(0f, 0.61f), new Vector2(1f, 0.75f), TextAnchor.MiddleLeft, new Color(0.72f, 0.69f, 0.63f));
            CreateVolumeSlider(optionsPanel.transform);
            CreateFullscreenToggle(optionsPanel.transform);
            CreateBackButton(optionsPanel.transform);
            optionsPanel.SetActive(false);

            GameObject curtainObject = new GameObject("Transition Curtain");
            curtainObject.transform.SetParent(canvasObject.transform, false);
            Image curtainImage = curtainObject.AddComponent<Image>();
            curtainImage.color = Color.black;
            Stretch(curtainImage.rectTransform);
            curtain = curtainObject.AddComponent<CanvasGroup>();
            curtain.alpha = 0f;
            curtain.blocksRaycasts = false;
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

        private void ApplySavedVolume(float value)
        {
            float clamped = Mathf.Clamp01(value);
            AudioListener.volume = clamped;
            if (videoAudio != null)
                videoAudio.volume = clamped;
            PlayerPrefs.SetFloat(VolumePreference, clamped);
        }

        private void ShowOptions()
        {
            mainPanel.SetActive(false);
            optionsPanel.SetActive(true);
        }

        private void HideOptions()
        {
            optionsPanel.SetActive(false);
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
