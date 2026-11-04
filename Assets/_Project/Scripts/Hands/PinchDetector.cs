namespace CardboardXR.Hands
{
    // Whether the thumb and index fingertips are pinched together, from how far apart they are. A
    // pinch starts closer than it ends, so the state doesn't flicker near the threshold.
    public class PinchDetector
    {
        readonly float m_CloseDistance;
        readonly float m_OpenDistance;

        public PinchDetector(float closeDistance = 0.025f, float openDistance = 0.045f)
        {
            m_CloseDistance = closeDistance;
            m_OpenDistance = openDistance;
        }

        public bool IsPinched { get; private set; }

        // Takes the fingertips' distance in meters; returns whether they're pinched.
        public bool Update(float tipDistance)
        {
            if (IsPinched ? tipDistance > m_OpenDistance : tipDistance < m_CloseDistance)
                IsPinched = !IsPinched;
            return IsPinched;
        }

        // Starts a pinch without a distance, for when something other than the 3D gap says the fingers
        // are together: the gap in the image, on a phone whose palm size no depth sensor taught.
        public void Begin() => IsPinched = true;

        public void Reset() => IsPinched = false;
    }
}
