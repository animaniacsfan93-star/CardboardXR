using CardboardXR.Hands;
using NUnit.Framework;

namespace CardboardXR.Stereo.Tests
{
    // The palm size learned without a depth sensor, from the hand model's own shapes: the median over
    // the recent ones, which is what keeps one bad shape from moving it.
    public class PalmSizeLearnerTests
    {
        [Test]
        public void StartsAtATypicalPalm()
        {
            var learner = new PalmSizeLearner();
            Assert.AreEqual(PalmSizeLearner.Typical, learner.Size, 1e-6f);
            Assert.AreEqual(0, learner.Count);
        }

        [Test]
        public void TakesOneShapeAtOnce()
        {
            var learner = new PalmSizeLearner();
            learner.AddShape(0.05f);
            Assert.AreEqual(0.05f, learner.Size, 1e-6f);
            Assert.AreEqual(1, learner.Count);
        }

        [Test]
        public void SizesNoPalmHasAreLeftOut()
        {
            var learner = new PalmSizeLearner();
            learner.AddShape(0.2f);
            learner.AddShape(float.NaN);
            Assert.AreEqual(0, learner.Count);
            Assert.AreEqual(PalmSizeLearner.Typical, learner.Size, 1e-6f);
        }

        [Test]
        public void OneBadShapeDoesNotMoveTheMedian()
        {
            var learner = new PalmSizeLearner();
            for (var i = 0; i < 9; i++)
                learner.AddShape(0.055f);
            learner.AddShape(0.075f);

            Assert.AreEqual(0.055f, learner.Size, 1e-6f, "the odd large shape is outvoted");
        }

        [Test]
        public void ItSettlesOnAHandThatWobbles()
        {
            var learner = new PalmSizeLearner();
            var wobble = new[] { 0.046f, 0.050f, 0.048f, 0.049f, 0.047f };
            for (var i = 0; i < 60; i++)
                learner.AddShape(wobble[i % wobble.Length]);

            Assert.AreEqual(0.048f, learner.Size, 1e-6f, "the middle of what the hand model saw");
            Assert.AreEqual(PalmSizeLearner.Window, learner.Count, "the window holds the recent ones");
        }

        [Test]
        public void ItCanStartOver()
        {
            var learner = new PalmSizeLearner();
            learner.AddShape(0.07f);
            learner.Reset();
            Assert.AreEqual(PalmSizeLearner.Typical, learner.Size, 1e-6f);
            Assert.AreEqual(0, learner.Count);
        }
    }
}
