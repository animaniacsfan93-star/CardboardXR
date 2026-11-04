using UnityEngine;

namespace CardboardXR.Stereo
{
    // The numbers of a viewer being set up by hand, one at a time, each with its own step and range,
    // in the units the tuning panel shows them in: millimeters for lengths, degrees for the field of
    // view, and as they are for the distortion's coefficients.
    // Pure, so the panel stays a view of it and it can be tested without a device. Everything it
    // writes goes through ToViewerParams, which is the viewer the app renders with.
    public class ViewerTuning
    {
        public enum Field
        {
            InterLensDistance = 0,
            ScreenToLensDistance,
            FieldOfView,
            DistortionK1,
            DistortionK2,
            LeftEyeX,
            LeftEyeY,
            RightEyeX,
            RightEyeY,
        }

        public const int FieldCount = 9;

        // What each can be, and how far one press moves it. A viewer's lenses sit a hand's width
        // apart and 3 to 5 cm from the screen; an offset moves one by a centimeter at most, more than
        // any viewer needs.
        static readonly float[] s_Min = { 45f, 25f, 30f, -0.2f, -0.2f, -10f, -10f, -10f, -10f };
        static readonly float[] s_Max = { 80f, 70f, 65f, 1f, 1f, 10f, 10f, 10f, 10f };
        static readonly float[] s_Step = { 0.5f, 0.5f, 1f, 0.02f, 0.02f, 0.5f, 0.5f, 0.5f, 0.5f };
        static readonly string[] s_Names =
        {
            "Lens separation", "Screen to lens", "Field of view", "Distortion k1", "Distortion k2",
            "Left eye across", "Left eye up", "Right eye across", "Right eye up",
        };

        // Values are nudged on a 1/200 grid, so a step of 0.5 mm or 0.02 stays exact.
        const float k_Grid = 0.005f;

        readonly ViewerParams m_Base;
        readonly float[] m_Values = new float[FieldCount];
        readonly float[] m_Start = new float[FieldCount];

        public ViewerTuning(ViewerParams start)
        {
            m_Base = (start ?? ViewerParams.ManualDefault).Copy();
            var fov = m_Base.leftEyeFieldOfViewAngles;
            Set(Field.InterLensDistance, m_Base.interLensDistance * 1000f);
            Set(Field.ScreenToLensDistance, m_Base.screenToLensDistance * 1000f);
            // The viewer's narrowest limit: a wider view than the lens has would show the screen
            // beyond its edge.
            Set(Field.FieldOfView, Mathf.Min(Mathf.Min(fov[0], fov[1]), Mathf.Min(fov[2], fov[3])));
            Set(Field.DistortionK1, Coefficient(0));
            Set(Field.DistortionK2, Coefficient(1));
            Set(Field.LeftEyeX, m_Base.EyeOffsetX(Eye.Left) * 1000f);
            Set(Field.LeftEyeY, m_Base.EyeOffsetY(Eye.Left) * 1000f);
            Set(Field.RightEyeX, m_Base.EyeOffsetX(Eye.Right) * 1000f);
            Set(Field.RightEyeY, m_Base.EyeOffsetY(Eye.Right) * 1000f);
            System.Array.Copy(m_Values, m_Start, FieldCount);
        }

        public static string Label(Field field) => s_Names[(int)field];

        // In the units it's shown in: millimeters, degrees, or nothing for the coefficients.
        public float Value(Field field) => m_Values[(int)field];

        public string Formatted(Field field)
        {
            var value = m_Values[(int)field];
            switch (field)
            {
                case Field.FieldOfView:
                    return $"{value:F0}\u00B0";
                case Field.DistortionK1:
                case Field.DistortionK2:
                    return value.ToString("F2");
                case Field.InterLensDistance:
                case Field.ScreenToLensDistance:
                    return $"{value:F1} mm";
                default:
                    return $"{value:+0.0;-0.0;0.0} mm";
            }
        }

        // Moves a value a number of steps, within its range.
        public void Nudge(Field field, int steps)
        {
            var index = (int)field;
            var value = m_Values[index] + steps * s_Step[index];
            m_Values[index] = Mathf.Clamp(Mathf.Round(value / k_Grid) * k_Grid, s_Min[index], s_Max[index]);
        }

        // Back to what tuning started from.
        public void Reset() => System.Array.Copy(m_Start, m_Values, FieldCount);

        // The viewer these values describe, as a copy that's safe to keep.
        public ViewerParams ToViewerParams()
        {
            var viewer = m_Base.Copy();
            viewer.screenToLensDistance = Value(Field.ScreenToLensDistance) * 0.001f;
            viewer.interLensDistance = Value(Field.InterLensDistance) * 0.001f;
            var fov = Value(Field.FieldOfView);
            viewer.leftEyeFieldOfViewAngles = new[] { fov, fov, fov, fov };
            viewer.distortionCoefficients = new[] { Value(Field.DistortionK1), Value(Field.DistortionK2) };
            viewer.perEyeOffset = new[]
            {
                Value(Field.LeftEyeX) * 0.001f,
                Value(Field.LeftEyeY) * 0.001f,
                Value(Field.RightEyeX) * 0.001f,
                Value(Field.RightEyeY) * 0.001f,
            };
            return viewer;
        }

        void Set(Field field, float value)
        {
            var index = (int)field;
            m_Values[index] = Mathf.Clamp(value, s_Min[index], s_Max[index]);
        }

        float Coefficient(int index) =>
            m_Base.distortionCoefficients.Length > index ? m_Base.distortionCoefficients[index] : 0f;
    }
}
