using NUnit.Framework;
using FrameAnalyzer.Runtime.Collectors;
using FrameAnalyzer.Runtime.Data;

namespace FrameAnalyzer.Runtime.Tests
{
    public class HdrpPassCollectorTests
    {
        [Test]
        public void HdrpPassCollector_ImplementsInterface()
        {
            IFrameDataCollector collector = new HdrpPassCollector();
            Assert.IsNotNull(collector);
        }

        [Test]
        public void HdrpPassCollector_Begin_InitializesRecorders()
        {
            var collector = new HdrpPassCollector();
            collector.Begin();
            // No exception should be thrown
            collector.End();
        }

        [Test]
        public void HdrpPassCollector_Collect_PopulatesSnapshot()
        {
            var collector = new HdrpPassCollector();
            collector.Begin();

            var snapshot = new FrameSnapshot();
            collector.Collect(snapshot);

            // After collection, HdrpPasses should be populated (even if empty, WasCollected should reflect if recorders were active)
            Assert.IsNotNull(snapshot.HdrpPasses.Passes);
            collector.End();
        }

        [Test]
        public void HdrpPassCollector_FullLifecycle()
        {
            var collector = new HdrpPassCollector();

            // Begin should not throw
            collector.Begin();

            var snapshot1 = new FrameSnapshot();
            collector.Collect(snapshot1);

            var snapshot2 = new FrameSnapshot();
            collector.Collect(snapshot2);

            // End should not throw
            collector.End();

            Assert.IsNotNull(snapshot1.HdrpPasses.Passes);
            Assert.IsNotNull(snapshot2.HdrpPasses.Passes);
        }

        [Test]
        public void HdrpPassCollector_MultipleFrames()
        {
            var collector = new HdrpPassCollector();
            collector.Begin();

            for (int i = 0; i < 10; i++)
            {
                var snapshot = new FrameSnapshot { FrameIndex = i };
                collector.Collect(snapshot);
                Assert.AreEqual(i, snapshot.FrameIndex);
            }

            collector.End();
        }
    }
}
