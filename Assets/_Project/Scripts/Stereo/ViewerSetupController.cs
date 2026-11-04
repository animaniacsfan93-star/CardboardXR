using System.Globalization;
using CardboardXR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace CardboardXR.Stereo
{
    // Lets the user pick their viewer: by scanning the QR code printed on it, like the Cardboard apps'
    // "switch viewer" flow, or, for a viewer with no code, by tuning it by hand (ViewerTuningController)
    // against a test pattern seen through its lenses. Either way the viewer is kept as a named profile
    // (ViewerProfileStore), to switch between, and the one last chosen starts the app.
    // A button at the top right, where Cardboard apps put their settings gear, opens a menu. Scanning
    // needs the phone out of the viewer and pauses the stereo view, switching the AR session and camera
    // on for it and then putting them back as they were: off, unless ArPoseSource is tracking with them.
    // Tuning is done with the phone in the viewer, so the phone's UI hides until it's done.
    public class ViewerSetupController : MonoBehaviour
    {
        const string k_ScanPrompt = "Point the camera at the QR code on your viewer.";
        const float k_RetryDelaySeconds = 2.5f;
        const float k_ToastSeconds = 3f;
        // How long the menu stays open without a choice, in seconds.
        const float k_MenuSeconds = 6f;
        // Reference pixels per dp: the canvas is 1080 tall, about 412 dp on a phone held sideways.
        const float k_DpOnScreen = 2.6f;
        // And for the profiles panel, whose list has to fit beside the phone's top and bottom.
        const float k_ProfileScale = 1.8f;
        // The profiles panel's list: how wide a row is, and how many fit.
        const float k_ProfileWidth = 640f;
        const int k_MaxProfileRows = 6;

        [SerializeField] StereoRig m_Rig;
        [SerializeField] ARSession m_Session;
        [SerializeField] ARCameraManager m_CameraManager;
        [SerializeField] ViewerQrScanner m_Scanner;
        [Tooltip("Tunes a viewer with no QR code by hand, in the viewer, against a test pattern.")]
        [SerializeField] ViewerTuningController m_Tuning;

        // The corner's settings button, and the menu it opens.
        GameObject m_SettingsButton;
        GameObject m_Menu;
        float m_MenuCloseTime;
        M3Button m_View;
        bool m_ShownMono;
        GameObject m_ScanPanel;
        RawImage m_Preview;
        AspectRatioFitter m_PreviewFitter;
        TMP_Text m_Status;
        GameObject m_ProfilesPanel;
        RectTransform m_ProfileList;
        TMP_Text m_ProfileStatus;
        TMP_Text m_Toast;
        GameObject m_Snackbar;
        float m_ToastHideTime;
        bool m_Resolving;
        bool m_SessionWasEnabled;
        bool m_CameraWasEnabled;

        void Awake()
        {
            EnsureEventSystem();
            BuildUi();
            ShowScanPanel(false);
            ShowProfiles(false);
            LoadViewer();
        }

        void OnEnable()
        {
            m_Scanner.CodeFound += OnCodeFound;
            if (m_Tuning != null)
                m_Tuning.Finished += OnTuningFinished;
        }

        void OnDisable()
        {
            m_Scanner.CodeFound -= OnCodeFound;
            if (m_Tuning != null)
                m_Tuning.Finished -= OnTuningFinished;
        }

        void Update()
        {
            if (m_ScanPanel.activeSelf && m_Scanner.Preview != null)
            {
                m_Preview.texture = m_Scanner.Preview;
                m_PreviewFitter.aspectRatio = m_Scanner.Preview.width / (float)m_Scanner.Preview.height;
            }

            if (M3Fade.IsShown(m_Snackbar) && Time.unscaledTime > m_ToastHideTime)
                M3Fade.Hide(m_Snackbar);
            if (M3Fade.IsShown(m_Menu) && Time.unscaledTime > m_MenuCloseTime)
                M3Fade.Hide(m_Menu);

            // The rig reads the "mono" launch option after this UI is built, and the view can change
            // from elsewhere, so the label follows it rather than only the button's own presses.
            if (m_ShownMono != m_Rig.IsMono)
                UpdateViewLabel();
        }

        // The viewer last chosen, the code an older build saved, or the Cardboard v1 this app falls
        // back to. ViewerProfileStore moves an older build's code into the list.
        void LoadViewer()
        {
            var profile = ViewerProfileStore.Active;
            if (profile != null)
            {
                m_Rig.Viewer = profile.viewer;
                Debug.Log($"[Viewer] Using saved viewer {profile.name}");
                return;
            }

            m_Rig.Viewer = ViewerParams.CardboardV1;
        }

        void SetScanning(bool scanning)
        {
            if (scanning == m_ScanPanel.activeSelf)
                return;
            if (scanning)
            {
                m_SessionWasEnabled = m_Session.enabled;
                m_CameraWasEnabled = m_CameraManager.enabled;
                ShowProfiles(false);
            }

            ShowScanPanel(scanning);
            m_Session.enabled = scanning || m_SessionWasEnabled;
            m_CameraManager.enabled = scanning || m_CameraWasEnabled;
            m_Scanner.enabled = scanning;
        }

        void ToggleView()
        {
            M3Fade.Hide(m_Menu);
            m_Rig.ToggleMono();
            UpdateViewLabel();
        }

        void ToggleMenu()
        {
            M3Fade.SetShown(m_Menu, !M3Fade.IsShown(m_Menu));
            m_MenuCloseTime = Time.unscaledTime + k_MenuSeconds;
        }

        void ChooseOpenSettings()
        {
            M3Fade.Hide(m_Menu);
            SettingsPanelHub.Open();
        }

        void ChooseSetViewer()
        {
            M3Fade.Hide(m_Menu);
            SetScanning(true);
        }

        // Tunes the viewer in use, or a manual one if none is saved yet: with the phone back in the
        // viewer, against the test pattern.
        void ChooseTuneViewer()
        {
            M3Fade.Hide(m_Menu);
            var active = ViewerProfileStore.Active;
            BeginTuning(active != null ? active.viewer : ViewerParams.ManualDefault,
                active != null ? active.name : null);
        }

        void ChooseViewerProfiles()
        {
            M3Fade.Hide(m_Menu);
            ShowProfiles(true);
        }

        void BeginTuning(ViewerParams start, string name)
        {
            if (m_Tuning == null)
            {
                Debug.LogWarning("[Viewer] No viewer tuning in this scene: rebuild it to tune by hand");
                return;
            }
            ShowProfiles(false);
            ShowScanPanel(false);
            // The gear would be seen through the lenses while the pattern is being tuned against.
            m_SettingsButton.SetActive(false);
            m_Tuning.Begin(start, name);
        }

        void OnTuningFinished(bool saved)
        {
            m_SettingsButton.SetActive(true);
            if (!saved)
                return;
            var active = ViewerProfileStore.Active;
            ShowToast(active != null ? $"Viewer: {active.name}" : "Viewer saved");
        }

        // The button says which view it would switch to, not which one it's in.
        void UpdateViewLabel()
        {
            m_ShownMono = m_Rig.IsMono;
            if (m_View != null)
            {
                m_View.Text = m_ShownMono ? "Viewer view" : "Phone view";
                m_View.Icon = m_ShownMono ? "view_agenda" : "smartphone";
            }
        }

        void ShowScanPanel(bool show)
        {
            m_ScanPanel.SetActive(show);
            m_SettingsButton.SetActive(!show);
            M3Fade.Hide(m_Menu);
            m_Rig.enabled = !show;
            m_Status.text = k_ScanPrompt;
        }

        // The saved viewers, to switch between or delete, and the way to tune another.
        void ShowProfiles(bool show)
        {
            if (m_ProfilesPanel == null || show == m_ProfilesPanel.activeSelf)
                return;
            m_ProfilesPanel.SetActive(show);
            m_SettingsButton.SetActive(!show);
            M3Fade.Hide(m_Menu);
            if (show)
                RebuildProfiles();
        }

        // A row per saved viewer: picking it makes it the one in use, and its bin removes it.
        void RebuildProfiles()
        {
            for (var i = m_ProfileList.childCount - 1; i >= 0; i--)
            {
                var child = m_ProfileList.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            var profiles = ViewerProfileStore.Profiles;
            m_ProfileStatus.text = profiles.Count == 0
                ? "No viewers saved yet. Tune one by hand, or scan a code."
                : "Look at a viewer to switch to it";
            var active = ViewerProfileStore.ActiveName;
            var shown = Mathf.Min(profiles.Count, k_MaxProfileRows);
            for (var i = 0; i < shown; i++)
            {
                var profile = profiles[i];
                var y = -(M3.k_ListItemHeight / 2f + i * (M3.k_ListItemHeight + 8f));
                // A pinch narrower than the list, leaving room for the bin at the row's end.
                var rowWidth = k_ProfileWidth - 68f;
                var (item, supporting) = M3.ListItem($"Viewer {profile.name}", m_ProfileList, "visibility",
                    new Vector2(-28f, y), rowWidth, () => Activate(profile));
                item.Text = profile.name == active ? $"{profile.name}  (in use)" : profile.name;
                item.Selected = profile.name == active;
                supporting.text = Summary(profile.viewer);
                M3.IconButton($"Delete {profile.name}", (RectTransform)item.transform, "delete",
                    M3Button.Variant.Text, new Vector2(k_ProfileWidth / 2f - 28f, 0f),
                    () => DeleteProfile(profile.name), 40f);
            }
            if (profiles.Count > shown)
                m_ProfileStatus.text += $"  (+{profiles.Count - shown} more)";
        }

        void Activate(ViewerProfile profile)
        {
            if (profile == null)
                return;
            ViewerProfileStore.SetActive(profile.name);
            m_Rig.Viewer = profile.viewer;
            ShowProfiles(false);
            ShowToast($"Viewer: {profile.name}");
            Debug.Log($"[Viewer] Switched to {profile.name}");
        }

        void DeleteProfile(string name)
        {
            ViewerProfileStore.Remove(name);
            var active = ViewerProfileStore.Active;
            m_Rig.Viewer = active != null ? active.viewer : ViewerParams.CardboardV1;
            Debug.Log($"[Viewer] Removed {name}");
            RebuildProfiles();
        }

        // A viewer's numbers in a line, to tell one saved viewer from another.
        static string Summary(ViewerParams viewer)
        {
            var fov = viewer.leftEyeFieldOfViewAngles != null && viewer.leftEyeFieldOfViewAngles.Length > 0
                ? viewer.leftEyeFieldOfViewAngles[0] : 0f;
            var k = viewer.distortionCoefficients;
            var k1 = k != null && k.Length > 0 ? k[0] : 0f;
            var k2 = k != null && k.Length > 1 ? k[1] : 0f;
            return string.Format(CultureInfo.InvariantCulture,
                "{0:F1} mm lenses, {1:F1} mm to the lens, fov {2:F0}, k {3:F2}/{4:F2}",
                viewer.interLensDistance * 1000f, viewer.screenToLensDistance * 1000f, fov, k1, k2);
        }

        async void OnCodeFound(string text)
        {
            if (m_Resolving)
                return;

            m_Resolving = true;
            m_Scanner.enabled = false;
            m_Status.text = "Reading viewer...";
            Debug.Log($"[Viewer] Scanned: {text}");

            var (status, url) = await ViewerUrlResolver.ResolveAsync(text);
            m_Resolving = false;
            // Cancelled while the link was being followed.
            if (!m_ScanPanel.activeSelf)
                return;

            if (status == ViewerUrlResolver.Status.Ok && ViewerParamsCodec.TryDecodeUrl(url, out var viewer))
            {
                var profile = ViewerProfileStore.Save($"{viewer.vendor} {viewer.model}".Trim(), viewer);
                m_Rig.Viewer = viewer;
                SetScanning(false);
                ShowToast($"Viewer: {profile.name}");
                Debug.Log($"[Viewer] Switched to {profile.name} from {url}");
                return;
            }

            m_Status.text = status == ViewerUrlResolver.Status.ConnectionError
                ? "Couldn't follow the code's link. Check the connection and try again."
                : "That doesn't look like a Cardboard viewer code. Trying again...";
            await Awaitable.WaitForSecondsAsync(k_RetryDelaySeconds);
            if (m_ScanPanel.activeSelf)
            {
                m_Status.text = k_ScanPrompt;
                m_Scanner.enabled = true;
            }
        }

        void UseCardboardV1()
        {
            var profile = ViewerProfileStore.Save("Cardboard v1", ViewerParams.CardboardV1);
            m_Rig.Viewer = ViewerParams.CardboardV1;
            SetScanning(false);
            ShowToast($"Viewer: {profile.name}");
        }

        void ShowToast(string message)
        {
            m_Toast.text = message;
            M3Fade.Show(m_Snackbar);
            m_ToastHideTime = Time.unscaledTime + k_ToastSeconds;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // The phone-screen UI, for use with the phone in hand: Material tonal buttons at the top right,
        // the QR scanner's screen, the saved viewers, and a snackbar for messages. Laid out in dp inside
        // a frame scaled to the screen (k_DpOnScreen reference pixels each), so it's sized as Material
        // sizes phone UI.
        void BuildUi()
        {
            var canvasObject = new GameObject("Viewer Setup UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasObject.transform;

            // One small settings button in the corner, clear of the alignment marker at the bottom
            // center and covering as little of the view as it can: with the phone in a viewer it's
            // all that's left on screen. It opens a menu below it with the viewer's setup (a QR code or
            // by hand), the saved viewers, and the switch between the viewer's two eye views and one
            // view filling the screen. All are here rather than on the gaze panel because they're
            // pressed with the phone in hand, out of the viewer, where a world-space panel can't be
            // reached.
            var corner = Frame("Corner", root, new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(40f, 40f));
            m_SettingsButton = M3.IconButton("Settings", corner, "settings", M3Button.Variant.Tonal, Vector2.zero, ToggleMenu, 40f).gameObject;
            var menuFrame = Frame("Menu Frame", root, new Vector2(1f, 1f), new Vector2(-12f, -60f), new Vector2(200f, 308f));
            var menu = M3.Surface("Menu", menuFrame, Vector2.zero, new Vector2(200f, 308f), M3Theme.SurfaceContainer, M3Theme.k_ShapeSmall);
            M3.Button("Set viewer", menu.rectTransform, "Set viewer", M3Button.Variant.Text, new Vector2(0f, 120f), ChooseSetViewer,
                "qr_code_scanner", 184f);
            M3.Button("Tune viewer", menu.rectTransform, "Tune viewer", M3Button.Variant.Text, new Vector2(0f, 60f), ChooseTuneViewer,
                "tune", 184f);
            M3.Button("Viewer profiles", menu.rectTransform, "Viewer profiles", M3Button.Variant.Text, new Vector2(0f, 0f),
                ChooseViewerProfiles, "list", 184f);
            m_View = M3.Button("View", menu.rectTransform, "Phone view", M3Button.Variant.Text, new Vector2(0f, -60f), ToggleView,
                "smartphone", 184f);
            // The settings panel is summoned by looking at a palm and pinching; this is for when there's
            // no hand to do it with.
            M3.Button("Open settings", menu.rectTransform, "Open settings", M3Button.Variant.Text, new Vector2(0f, -120f),
                ChooseOpenSettings, "settings", 184f);
            m_Menu = menuFrame.gameObject;
            m_Menu.AddComponent<M3Fade>();
            m_Menu.SetActive(false);
            UpdateViewLabel();

            m_ScanPanel = new GameObject("Scan Panel", typeof(Image));
            m_ScanPanel.transform.SetParent(root, false);
            Stretch((RectTransform)m_ScanPanel.transform);
            m_ScanPanel.GetComponent<Image>().color = M3Theme.Surface;

            var previewObject = new GameObject("Camera Preview", typeof(RawImage), typeof(AspectRatioFitter));
            previewObject.transform.SetParent(m_ScanPanel.transform, false);
            Stretch((RectTransform)previewObject.transform);
            m_Preview = previewObject.GetComponent<RawImage>();
            m_Preview.raycastTarget = false;
            m_PreviewFitter = previewObject.GetComponent<AspectRatioFitter>();
            m_PreviewFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            var scanRect = (RectTransform)m_ScanPanel.transform;
            var scanTop = Frame("Top", scanRect, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(600f, 64f));
            var statusBar = M3.Surface("Status Bar", scanTop, Vector2.zero, new Vector2(600f, 64f), M3Theme.SurfaceContainerHigh,
                M3Theme.k_ShapeLarge);
            m_Status = M3.Text("Status", statusBar.rectTransform, "", M3Theme.Type.BodyLarge, Vector2.zero, new Vector2(568f, 56f));
            var actions = Frame("Actions", scanRect, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(420f, 56f));
            M3.Button("Cancel", actions, "Cancel", M3Button.Variant.Tonal, new Vector2(-110f, 0f), () => SetScanning(false), "close", 180f);
            M3.Button("Use Cardboard v1", actions, "Use Cardboard v1", M3Button.Variant.Filled, new Vector2(100f, 0f), UseCardboardV1,
                null, 210f);

            // The saved viewers, over everything, since the phone is in hand for it.
            m_ProfilesPanel = new GameObject("Profiles Panel", typeof(Image));
            m_ProfilesPanel.transform.SetParent(root, false);
            Stretch((RectTransform)m_ProfilesPanel.transform);
            m_ProfilesPanel.GetComponent<Image>().color = M3Theme.Surface;
            var profilesRect = (RectTransform)m_ProfilesPanel.transform;
            // Laid out smaller than the rest of the phone UI: the list is what has to fit on a phone
            // held sideways, and this leaves room for six saved viewers and the controls under them.
            var profilesTop = Frame("Top", profilesRect, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(640f, 64f),
                k_ProfileScale);
            var profilesBar = M3.Surface("Status Bar", profilesTop, Vector2.zero, new Vector2(640f, 64f),
                M3Theme.SurfaceContainerHigh, M3Theme.k_ShapeLarge);
            m_ProfileStatus = M3.Text("Status", profilesBar.rectTransform, "", M3Theme.Type.BodyLarge, Vector2.zero,
                new Vector2(608f, 56f));
            m_ProfileList = Frame("List", profilesRect, new Vector2(0.5f, 1f), new Vector2(0f, -96f),
                new Vector2(k_ProfileWidth, k_MaxProfileRows * M3.k_ListItemHeight + (k_MaxProfileRows - 1) * 8f),
                k_ProfileScale);
            var profileActions = Frame("Actions", profilesRect, new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                new Vector2(420f, 56f), k_ProfileScale);
            M3.Button("Tune viewer", profileActions, "Tune viewer", M3Button.Variant.Filled, new Vector2(-110f, 0f),
                ChooseTuneViewer, "tune", 190f);
            M3.Button("Close", profileActions, "Close", M3Button.Variant.Tonal, new Vector2(110f, 0f),
                () => ShowProfiles(false), "close", 120f);

            // A Material snackbar: inverse colors, low on the screen.
            var bottom = Frame("Snackbar Frame", root, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(560f, 56f));
            var snackbar = M3.Surface("Snackbar", bottom, Vector2.zero, new Vector2(560f, 56f), M3Theme.InverseSurface, 4f);
            m_Toast = M3.Text("Message", snackbar.rectTransform, "", M3Theme.Type.BodyMedium, Vector2.zero, new Vector2(528f, 48f),
                M3Theme.InverseOnSurface, TextAlignmentOptions.Left);
            m_Snackbar = snackbar.gameObject;
            m_Snackbar.AddComponent<M3Fade>();
            m_Snackbar.SetActive(false);
        }

        // A frame laid out in dp, anchored and pivoted at a point of its parent, its own size in dp.
        // The scale is reference pixels per dp: the default fills the screen, the profiles panel uses
        // less so its list fits.
        static RectTransform Frame(string name, RectTransform parent, Vector2 anchor, Vector2 offsetDp, Vector2 sizeDp,
            float scale = k_DpOnScreen)
        {
            var frame = new GameObject(name, typeof(RectTransform));
            frame.transform.SetParent(parent, false);
            var rect = (RectTransform)frame.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = sizeDp;
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = offsetDp * scale;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
