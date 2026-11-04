using NUnit.Framework;
using UnityEngine;

namespace CardboardXR.Stereo.Tests
{
    // The viewers a user keeps between launches, and the numbers a viewer with no QR code is set up
    // with by hand: the parts of it that need no device.
    public class ViewerProfileTests
    {
        [Test]
        public void ViewerParamsKeepTheirOwnArrays()
        {
            var original = ViewerParams.ManualDefault;
            var copy = original.Copy();
            copy.perEyeOffset[0] = 0.02f;
            copy.distortionCoefficients[0] = 0.9f;
            copy.leftEyeFieldOfViewAngles[0] = 20f;

            Assert.AreEqual(0f, original.perEyeOffset[0], "the original's offsets are untouched");
            Assert.AreEqual(0.3f, original.distortionCoefficients[0], 1e-5f);
            Assert.AreEqual(50f, original.leftEyeFieldOfViewAngles[0], 1e-5f);
        }

        [Test]
        public void RepairFillsInWhatIsMissing()
        {
            var viewer = new ViewerParams
            {
                leftEyeFieldOfViewAngles = null,
                distortionCoefficients = null,
                perEyeOffset = null,
            }.Repair();

            Assert.AreEqual(4, viewer.leftEyeFieldOfViewAngles.Length);
            Assert.IsNotNull(viewer.distortionCoefficients);
            Assert.AreEqual(4, viewer.perEyeOffset.Length, "a viewer without offsets has four zeros");
            Assert.AreEqual(0f, viewer.EyeOffsetX(Eye.Left));
        }

        [Test]
        public void CardboardV1StartsWithoutOffsets()
        {
            Assert.AreEqual(4, ViewerParams.CardboardV1.perEyeOffset.Length);
            Assert.AreEqual(0f, ViewerParams.CardboardV1.EyeOffsetY(Eye.Right));
        }

        [Test]
        public void TuningStartsFromTheViewerItIsGiven()
        {
            var tuning = new ViewerTuning(ViewerParams.CardboardV1);

            Assert.AreEqual(60f, tuning.Value(ViewerTuning.Field.InterLensDistance), 1e-4f);
            Assert.AreEqual(42f, tuning.Value(ViewerTuning.Field.ScreenToLensDistance), 1e-4f);
            Assert.AreEqual(40f, tuning.Value(ViewerTuning.Field.FieldOfView), 1e-4f);
            Assert.AreEqual(0.441f, tuning.Value(ViewerTuning.Field.DistortionK1), 1e-4f);
            Assert.AreEqual(0.156f, tuning.Value(ViewerTuning.Field.DistortionK2), 1e-4f);
            Assert.AreEqual(0f, tuning.Value(ViewerTuning.Field.LeftEyeX), 1e-4f);
        }

        [Test]
        public void TuningWritesTheViewerItShows()
        {
            var tuning = new ViewerTuning(ViewerParams.CardboardV1);
            tuning.Nudge(ViewerTuning.Field.InterLensDistance, -3);
            tuning.Nudge(ViewerTuning.Field.FieldOfView, 5);
            tuning.Nudge(ViewerTuning.Field.LeftEyeX, 3);
            tuning.Nudge(ViewerTuning.Field.RightEyeY, -2);

            var viewer = tuning.ToViewerParams();
            Assert.AreEqual(0.0585f, viewer.interLensDistance, 1e-5f, "three half-millimetre steps in");
            Assert.AreEqual(45f, viewer.leftEyeFieldOfViewAngles[0], 1e-5f);
            Assert.AreEqual(45f, viewer.leftEyeFieldOfViewAngles[3], 1e-5f, "both eyes' view follows the one number");
            Assert.AreEqual(0.0015f, viewer.EyeOffsetX(Eye.Left), 1e-6f);
            Assert.AreEqual(-0.001f, viewer.EyeOffsetY(Eye.Right), 1e-6f);
            Assert.AreEqual(0f, viewer.EyeOffsetY(Eye.Left), 1e-6f);
        }

        [Test]
        public void TuningStaysWithinItsRange()
        {
            var tuning = new ViewerTuning(ViewerParams.ManualDefault);
            tuning.Nudge(ViewerTuning.Field.InterLensDistance, -1000);
            Assert.AreEqual(45f, tuning.Value(ViewerTuning.Field.InterLensDistance), 1e-4f, "no nearer than 45 mm");
            tuning.Nudge(ViewerTuning.Field.InterLensDistance, 1000);
            Assert.AreEqual(80f, tuning.Value(ViewerTuning.Field.InterLensDistance), 1e-4f, "no farther than 80 mm");
            tuning.Nudge(ViewerTuning.Field.DistortionK1, 1000);
            Assert.AreEqual(1f, tuning.Value(ViewerTuning.Field.DistortionK1), 1e-4f);
        }

        [Test]
        public void TuningResetsToWhereItStarted()
        {
            var tuning = new ViewerTuning(ViewerParams.ManualDefault);
            tuning.Nudge(ViewerTuning.Field.ScreenToLensDistance, 10);
            tuning.Reset();
            Assert.AreEqual(45f, tuning.Value(ViewerTuning.Field.ScreenToLensDistance), 1e-4f);
        }

        [Test]
        public void ProfilesAreNamedAndKeptInUse()
        {
            var set = new ViewerProfileSet();
            Assert.AreEqual("Viewer 1", set.NewName());

            set.Save("Viewer 1", ViewerParams.CardboardV1);
            Assert.AreEqual("Viewer 1", set.active);
            Assert.AreEqual("Viewer 2", set.NewName());

            set.Save("Viewer 2", ViewerParams.ManualDefault);
            Assert.AreEqual(2, set.profiles.Count);
            Assert.AreEqual("Viewer 2", set.active);

            // The same name replaces its viewer rather than adding another.
            set.Save("Viewer 1", ViewerParams.ManualDefault);
            Assert.AreEqual(2, set.profiles.Count);
            Assert.AreEqual("Viewer 1", set.active);
            Assert.AreEqual(0.062f, set.Find("Viewer 1").viewer.interLensDistance, 1e-6f);
        }

        [Test]
        public void RemovingTheOneInUseLeavesTheNextOne()
        {
            var set = new ViewerProfileSet();
            set.Save("Viewer 1", ViewerParams.CardboardV1);
            set.Save("Viewer 2", ViewerParams.ManualDefault);

            Assert.IsTrue(set.Remove("Viewer 2"));
            Assert.AreEqual("", set.active, "nothing is in use");
            set.Sanitize();
            Assert.AreEqual("Viewer 1", set.active, "the one left is");
            Assert.IsFalse(set.Remove("Missing"));
        }

        [Test]
        public void NamesWithoutOneGetTheirOwn()
        {
            var set = new ViewerProfileSet();
            set.Save("Viewer 1", ViewerParams.CardboardV1);
            set.Save("  ", ViewerParams.ManualDefault);
            Assert.AreEqual("Viewer 2", set.profiles[1].name);

            set.profiles.Add(new ViewerProfile("Viewer 1", ViewerParams.CardboardV1));
            set.Sanitize();
            Assert.AreEqual(3, set.profiles.Count);
            Assert.AreEqual("Viewer 1 2", set.profiles[2].name, "a repeated name gets a number of its own");
        }

        [Test]
        public void SanitizeDropsWhatCantBeShown()
        {
            var set = new ViewerProfileSet();
            set.profiles.Add(null);
            set.profiles.Add(new ViewerProfile("Broken", null));
            set.profiles.Add(new ViewerProfile("Fine", ViewerParams.CardboardV1));
            set.active = "Broken";

            set.Sanitize();
            Assert.AreEqual(1, set.profiles.Count);
            Assert.AreEqual("Fine", set.active, "the one in use was dropped, so the one left is");
        }

        [Test]
        public void TestPatternDrawsItsGridCrossAndMiddle()
        {
            const int size = 64;
            var pixels = ViewerTestPattern.Build(size, size);

            Assert.AreEqual(ViewerTestPattern.Cross, pixels[(size / 2) * size + size / 2], "the cross is at the middle");
            Assert.AreEqual(ViewerTestPattern.Line, pixels[6 * size + 4], "a minor grid line");
            Assert.AreEqual(ViewerTestPattern.MajorLine, pixels[6 * size], "a major one, every fourth");
            Assert.AreEqual(ViewerTestPattern.Background, pixels[18 * size + 10], "between the lines");
        }

        [Test]
        public void TestPatternIsSquareAndComplete()
        {
            var pixels = ViewerTestPattern.Build(32, 16);
            Assert.AreEqual(32 * 16, pixels.Length);
            foreach (var pixel in pixels)
                Assert.AreEqual(255, pixel.a, "every pixel is opaque");
        }
    }
}
