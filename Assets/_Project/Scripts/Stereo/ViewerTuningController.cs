using System;
using CardboardXR.Interaction;
using CardboardXR.UI;
using TMPro;
using UnityEngine;

namespace CardboardXR.Stereo
{
    // Sets up a viewer that has no QR code to scan: a test pattern hangs in front of the eyes, seen
    // through the viewer's lenses, and a panel under it moves the numbers one at a time, looked at and
    // tapped (a screen tap, or the viewer's own button, clicks what the gaze is on). The right lens
    // separation, screen distance, distortion and eye offsets make the pattern's lines straight and
    // evenly spaced, with the cross in the middle of each lens; anything off bows them, stretches
    // them, or puts the cross off centre. Save keeps the numbers as a named profile
    // (ViewerProfileStore); Cancel puts the viewer that was in use before back.
    // The panel is a GazeCanvas, and the rig's viewer changes live as a value moves (StereoRig
    // .ApplyViewer), so the pattern answers a press at once. Put it on the Viewer Setup object, which
    // ViewerSetupController starts and hears the end of. Logs [Viewer].
    public class ViewerTuningController : MonoBehaviour
    {
        // The pattern, in meters in front of the eyes and across: 3.4 m at 1.6 m covers the 46
        // degrees to each side that a phone viewer's lens can take in.
        const float k_PatternDistance = 1.6f;
        const float k_PatternSize = 3.4f;
        // The panel, in dp, and where it hangs: near enough to be the nearest thing the gaze meets
        // (the settings panel is at 1.25 m), low enough to leave the pattern's middle clear.
        const float k_PanelDistance = 1.1f;
        const float k_PanelPitch = 16f;
        const float k_PanelWidth = 480f;
        const float k_PanelHeight = 276f;
        const float k_HintY = 104f;
        const float k_SelectorY = 52f;
        const float k_ValueY = -14f;
        const float k_ActionsY = -82f;

        [SerializeField] StereoRig m_Rig;
        [Tooltip("The pattern's unlit material, made by the builder; it gets the pattern texture at runtime.")]
        [SerializeField] Material m_PatternMaterial;

        GameObject m_Pattern;
        GameObject m_Panel;
        Material m_Material;
        Texture2D m_Texture;
        TMP_Text m_FieldLabel;
        TMP_Text m_Value;
        ViewerTuning m_Tuning;
        ViewerTuning.Field m_Field = ViewerTuning.Field.InterLensDistance;
        ViewerParams m_SavedViewer;
        bool m_WasMono;
        bool m_Built;
        string m_Name = "";

        // Tuning started (true) and ended, with whether it saved (true) or was cancelled.
        public event Action<bool> Finished;

        public bool IsOpen { get; private set; }

        // Starts tuning from this viewer, under this name: a name left empty gets a new one. The view
        // goes through the viewer's lenses, whatever view the app was in, since that's the only way to
        // see the pattern.
        public void Begin(ViewerParams start, string name)
        {
            if (m_Rig == null || IsOpen)
                return;

            BuildUi();
            m_SavedViewer = m_Rig.Viewer;
            m_WasMono = m_Rig.IsMono;
            m_Name = string.IsNullOrWhiteSpace(name) ? ViewerProfileStore.NewName() : name.Trim();
            m_Tuning = new ViewerTuning(start ?? ViewerParams.ManualDefault);
            m_Field = ViewerTuning.Field.InterLensDistance;
            IsOpen = true;
            m_Rig.SetMono(false);
            Apply();
            UpdateLabels();
            if (m_Pattern != null)
                M3Fade.Show(m_Pattern);
            M3Fade.Show(m_Panel);
            var viewer = MakeViewer();
            Debug.Log($"[Viewer] Tuning {m_Name}: {viewer.interLensDistance * 1000f:F1} mm lenses, " +
                      $"{viewer.screenToLensDistance * 1000f:F1} mm to the screen, " +
                      $"fov {viewer.leftEyeFieldOfViewAngles[0]:F0}, k1 {viewer.distortionCoefficients[0]:F2}, " +
                      $"k2 {viewer.distortionCoefficients[1]:F2}");
        }

        // The next number of the set: the lens separation, the screen distance, the field of view, the
        // two distortion coefficients, then each eye's offsets.
        void Page(int direction)
        {
            var next = ((int)m_Field + direction + ViewerTuning.FieldCount) % ViewerTuning.FieldCount;
            m_Field = (ViewerTuning.Field)next;
            UpdateLabels();
        }

        // One press: half a millimeter, a degree or a hundredth of a coefficient, or the same back.
        void Nudge(int steps)
        {
            if (m_Tuning == null)
                return;
            m_Tuning.Nudge(m_Field, steps);
            Apply();
            UpdateLabels();
        }

        void ResetTuning()
        {
            if (m_Tuning == null)
                return;
            m_Tuning.Reset();
            Apply();
            UpdateLabels();
        }

        // Keeps what's on show under its name, replacing the viewer it started from if that's its name.
        void Save()
        {
            var viewer = MakeViewer();
            var profile = ViewerProfileStore.Save(m_Name, viewer);
            m_Rig.ApplyViewer(viewer);
            Debug.Log($"[Viewer] Saved {profile.name}: {profile.viewer.interLensDistance * 1000f:F1} mm lenses, " +
                      $"{profile.viewer.screenToLensDistance * 1000f:F1} mm to the lens, k " +
                      string.Join("/", Array.ConvertAll(profile.viewer.distortionCoefficients, c => c.ToString("F2"))));
            End(true);
        }

        // Keeps what's on show as a viewer of its own, rather than over the one tuning started from.
        void SaveAsNew()
        {
            m_Name = ViewerProfileStore.NewName();
            Save();
        }

        void Cancel()
        {
            Debug.Log($"[Viewer] Tuning {m_Name} cancelled");
            End(false);
        }

        void End(bool saved)
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            if (m_Pattern != null)
                M3Fade.Hide(m_Pattern);
            M3Fade.Hide(m_Panel);
            if (!saved)
            {
                m_Rig.ApplyViewer(m_SavedViewer);
                if (m_WasMono)
                    m_Rig.SetMono(true);
            }
            m_Tuning = null;
            Finished?.Invoke(saved);
        }

        // The viewer as the panel has it, named after the profile it's kept under.
        ViewerParams MakeViewer()
        {
            var viewer = m_Tuning.ToViewerParams();
            viewer.vendor = "Manual";
            viewer.model = m_Name;
            return viewer;
        }

        void Apply()
        {
            if (m_Rig == null || m_Tuning == null)
                return;
            m_Rig.ApplyViewer(MakeViewer());
        }

        void UpdateLabels()
        {
            if (m_FieldLabel == null || m_Value == null || m_Tuning == null)
                return;
            m_FieldLabel.text = $"{ViewerTuning.Label(m_Field)}  {(int)m_Field + 1}/{ViewerTuning.FieldCount}";
            m_Value.text = m_Tuning.Formatted(m_Field);
        }

        // The pattern and the panel, built once, on the head so they stay where the eyes look. The
        // panel's buttons answer the gaze through a GazeCanvas, as the app's other world panels do.
        void BuildUi()
        {
            if (m_Built || m_Rig == null)
                return;
            m_Built = true;
            var head = m_Rig.transform;

            if (m_PatternMaterial != null)
            {
                m_Pattern = GameObject.CreatePrimitive(PrimitiveType.Quad);
                m_Pattern.name = "Viewer Test Pattern";
                Destroy(m_Pattern.GetComponent<Collider>());
                m_Pattern.transform.SetParent(head, false);
                m_Pattern.transform.localPosition = new Vector3(0f, 0f, k_PatternDistance);
                m_Pattern.transform.localScale = new Vector3(k_PatternSize, k_PatternSize, 1f);
                m_Texture = ViewerTestPattern.CreateTexture();
                m_Material = new Material(m_PatternMaterial) { name = "Viewer Test Pattern", mainTexture = m_Texture };
                m_Pattern.GetComponent<MeshRenderer>().sharedMaterial = m_Material;
                m_Pattern.SetActive(false);
            }
            else
            {
                Debug.LogError("[Viewer] No test pattern material: the tuning screen has nothing to tune against");
            }

            var panel = M3.Canvas("Viewer Tuning", head, new Vector2(k_PanelWidth, k_PanelHeight),
                k_PanelDistance, M3Theme.SurfaceContainer, M3Theme.k_ShapeLarge);
            panel.gameObject.AddComponent<GazeCanvas>();
            var direction = Quaternion.Euler(k_PanelPitch, 0f, 0f) * Vector3.forward;
            panel.localPosition = direction * k_PanelDistance;
            panel.localRotation = Quaternion.LookRotation(direction, Vector3.up);
            m_Panel = panel.gameObject;
            m_Panel.SetActive(false);

            M3.Text("Hint", panel, "Keep the lines straight and the cross in the middle of each lens",
                M3Theme.Type.BodyMedium, new Vector2(0f, k_HintY), new Vector2(k_PanelWidth - 32f, 44f),
                M3Theme.OnSurfaceVariant);
            M3.IconButton("Previous", panel, "chevron_left", M3Button.Variant.Text,
                new Vector2(-(k_PanelWidth / 2f - 24f), k_SelectorY), () => Page(-1), 48f);
            M3.IconButton("Next", panel, "chevron_right", M3Button.Variant.Text,
                new Vector2(k_PanelWidth / 2f - 24f, k_SelectorY), () => Page(1), 48f);
            m_FieldLabel = M3.Text("Field", panel, "", M3Theme.Type.TitleMedium, new Vector2(0f, k_SelectorY),
                new Vector2(k_PanelWidth - 2f * (48f + 16f), 28f));
            M3.IconButton("Less", panel, "remove", M3Button.Variant.Tonal, new Vector2(-160f, k_ValueY),
                () => Nudge(-1), 64f);
            M3.IconButton("More", panel, "add", M3Button.Variant.Tonal, new Vector2(160f, k_ValueY),
                () => Nudge(1), 64f);
            m_Value = M3.Text("Value", panel, "", M3Theme.Type.TitleLarge, new Vector2(0f, k_ValueY),
                new Vector2(200f, 40f));

            M3.Button("Reset", panel, "Reset", M3Button.Variant.Text, new Vector2(-144f, k_ActionsY), ResetTuning,
                null, 104f);
            M3.Button("New", panel, "New", M3Button.Variant.Text, new Vector2(-42f, k_ActionsY), SaveAsNew, null, 84f);
            M3.Button("Cancel", panel, "Cancel", M3Button.Variant.Text, new Vector2(56f, k_ActionsY), Cancel, null, 96f);
            M3.Button("Save", panel, "Save", M3Button.Variant.Filled, new Vector2(154f, k_ActionsY), Save, null, 84f);
        }

        void OnDestroy()
        {
            if (m_Material != null)
                Destroy(m_Material);
            if (m_Texture != null)
                Destroy(m_Texture);
            if (m_Pattern != null)
                Destroy(m_Pattern);
            if (m_Panel != null)
                Destroy(m_Panel);
        }
    }
}
