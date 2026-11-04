using System;

namespace CardboardXR.Stereo
{
    // Physical description of a Cardboard-style viewer, mirroring the Cardboard SDK's DeviceParams
    // protobuf (proto/cardboard_device.proto). Lengths are in meters, angles in degrees.
    // Beyond the protobuf there are per-eye offsets, for a viewer whose lenses aren't exactly a lens
    // separation apart or level with the phone: no QR code describes them, and a viewer tuned by hand
    // (ViewerTuning) often needs them. Copy and Repair go with ViewerProfileStore, which keeps what
    // the user chose between launches.
    [Serializable]
    public class ViewerParams
    {
        public enum VerticalAlignment
        {
            Bottom = 0, // phone rests against a fixed bottom tray
            Center = 1, // screen is centered on the lenses
            Top = 2,    // phone rests against a fixed top tray
        }

        public enum ButtonType
        {
            None = 0,
            Magnet = 1,
            Touch = 2,
            IndirectTouch = 3,
        }

        public string vendor = "";
        public string model = "";
        public float screenToLensDistance;
        public float interLensDistance;
        // Left eye's view extent (left, right, bottom, top) from the optical axis. The right eye
        // mirrors left and right.
        public float[] leftEyeFieldOfViewAngles = new float[4];
        public VerticalAlignment verticalAlignment = VerticalAlignment.Bottom;
        public float trayToLensDistance;
        // K1, K2, ... of p' = p (1 + K1 r^2 + K2 r^4 + ...), r in tan-angle units.
        public float[] distortionCoefficients = Array.Empty<float>();
        public ButtonType primaryButton = ButtonType.Magnet;
        // Each eye's own offset from where the lens separation would put its lens, in meters: the
        // left eye's x and y, then the right's, with x rightward and y upward on the landscape
        // screen. A lens that sits a little inward, or a phone held a little high in the viewer, is
        // lined up by moving where that eye looks from.
        public float[] perEyeOffset = new float[4];

        // The original 2014 Cardboard. The Cardboard SDK falls back to these when no viewer QR code
        // has been scanned; values decoded from sdk/qrcode/cardboard_v1/cardboard_v1.cc.
        public static ViewerParams CardboardV1 => new()
        {
            vendor = "Google, Inc.",
            model = "Cardboard v1",
            screenToLensDistance = 0.042f,
            interLensDistance = 0.06f,
            leftEyeFieldOfViewAngles = new[] { 40f, 40f, 40f, 40f },
            verticalAlignment = VerticalAlignment.Bottom,
            trayToLensDistance = 0.035f,
            distortionCoefficients = new[] { 0.441f, 0.156f },
            primaryButton = ButtonType.Magnet,
        };

        // Where a viewer with no code starts when it's tuned by hand: the lenses of a common plastic
        // viewer about a hand's width apart, 45 mm from the screen, a 50 degree view (a plain lens
        // shows about that much) and a little barrel distortion. Centered on the lenses, since there's
        // no tray to measure.
        public static ViewerParams ManualDefault => new()
        {
            vendor = "Manual",
            model = "Viewer",
            screenToLensDistance = 0.045f,
            interLensDistance = 0.062f,
            leftEyeFieldOfViewAngles = new[] { 50f, 50f, 50f, 50f },
            verticalAlignment = VerticalAlignment.Center,
            trayToLensDistance = 0f,
            distortionCoefficients = new[] { 0.3f, 0.1f },
            primaryButton = ButtonType.None,
        };

        // This eye's offset across the screen, in meters, from where the lens separation puts it.
        public float EyeOffsetX(Eye eye) => Offset(eye, 0);

        // And its offset up the screen.
        public float EyeOffsetY(Eye eye) => Offset(eye, 1);

        // A copy that can be changed without changing this one: the arrays are copied too.
        public ViewerParams Copy()
        {
            var copy = (ViewerParams)MemberwiseClone();
            copy.leftEyeFieldOfViewAngles = leftEyeFieldOfViewAngles != null
                ? (float[])leftEyeFieldOfViewAngles.Clone()
                : new[] { 40f, 40f, 40f, 40f };
            copy.distortionCoefficients = distortionCoefficients != null
                ? (float[])distortionCoefficients.Clone()
                : Array.Empty<float>();
            copy.perEyeOffset = perEyeOffset != null ? (float[])perEyeOffset.Clone() : new float[4];
            return copy.Repair();
        }

        // Puts back what a profile read from JSON, or made up by hand, may be missing: four angles of
        // field of view, the arrays, and no offset that isn't a number. Rendering has to have them.
        public ViewerParams Repair()
        {
            if (leftEyeFieldOfViewAngles == null || leftEyeFieldOfViewAngles.Length != 4)
                leftEyeFieldOfViewAngles = new[] { 40f, 40f, 40f, 40f };
            if (distortionCoefficients == null)
                distortionCoefficients = Array.Empty<float>();
            if (perEyeOffset == null || perEyeOffset.Length != 4)
            {
                var offsets = new float[4];
                var given = Math.Min(perEyeOffset?.Length ?? 0, offsets.Length);
                Array.Copy(perEyeOffset ?? Array.Empty<float>(), offsets, given);
                perEyeOffset = offsets;
            }
            for (var i = 0; i < perEyeOffset.Length; i++)
            {
                var value = perEyeOffset[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                    perEyeOffset[i] = 0f;
            }
            return this;
        }

        float Offset(Eye eye, int axis)
        {
            if (perEyeOffset == null || perEyeOffset.Length < 4)
                return 0f;
            return perEyeOffset[(int)eye * 2 + axis];
        }
    }
}
