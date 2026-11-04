using System;

namespace CardboardXR.Hands
{
    // The user's palm size in 3D (see HandGeometry.PalmSize), in meters, as the hand model's own shapes
    // estimate it: for a phone with no depth sensor to measure it, where the model's estimate is the
    // only witness there is. It's the median over the last few shapes that fitted their landmarks
    // well, so the odd collapsed or edge-on shape that an exponential average leaned on doesn't move
    // it: on the iPhone, before LiDAR taught the size, the model's estimates alone wandered 10% in
    // 20 s, and left a hand's depth 8-12% short against a ruler.
    // Without depth the palm size scales the hand's depth, its joints and the pinch's 3D gap, so a
    // steady value matters more than a quick one. Pure, for testing.
    public class PalmSizeLearner
    {
        // A typical adult's palm, which is where it starts, and the range the user's can be within.
        public const float Typical = 0.055f;
        public const float Min = 0.04f;
        public const float Max = 0.075f;
        // How many recent shapes the median is over: about a second's results.
        public const int Window = 31;

        readonly float[] m_Samples = new float[Window];
        readonly float[] m_Sorted = new float[Window];
        int m_Count;

        // The size learned so far, and how many shapes went into the median.
        public float Size { get; private set; } = Typical;
        public int Count => m_Count;

        // One shape's palm size, from a fit that missed its landmarks no more than a good fit may.
        // Sizes no adult's palm has are left out: they came from something else.
        public void AddShape(float palmSize)
        {
            if (!(palmSize >= Min && palmSize <= Max))
                return;

            if (m_Count < Window)
            {
                m_Samples[m_Count++] = palmSize;
            }
            else
            {
                Array.Copy(m_Samples, 1, m_Samples, 0, Window - 1);
                m_Samples[Window - 1] = palmSize;
            }

            Array.Copy(m_Samples, m_Sorted, m_Count);
            Array.Sort(m_Sorted, 0, m_Count);
            Size = m_Count % 2 == 1
                ? m_Sorted[m_Count / 2]
                : 0.5f * (m_Sorted[m_Count / 2 - 1] + m_Sorted[m_Count / 2]);
        }

        // Back to a typical palm, as when the learned size turns out to have been someone else's.
        public void Reset()
        {
            m_Count = 0;
            Size = Typical;
        }
    }
}
