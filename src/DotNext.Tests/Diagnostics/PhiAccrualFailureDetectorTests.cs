using System.Diagnostics.CodeAnalysis;

namespace DotNext.Diagnostics
{
    [ExcludeFromCodeCoverage]
    public sealed class PhiAccrualFailureDetectorTests : Test
    {
        [Fact]
        public static void EmptyPhi()
        {
            var detector = new PhiAccrualFailureDetector(TimeSpan.FromMilliseconds(500));

            False(detector.IsMonitoring);
            Equal(0.0D, detector.Value);
        }

        [Fact]
        public static void TreatUnknownValueAsUnhealthy()
        {
            var detector = new PhiAccrualFailureDetector(TimeSpan.FromMilliseconds(500))
            {
                TreatUnknownValueAsUnhealthy = true,
            };

            True(detector.IsMonitoring);
            Equal(double.PositiveInfinity, detector.Value);
            False(detector.IsHealthy);
        }

        [Fact]
        public static void PhiNaNRegression()
        {
            var detector = new PhiAccrualFailureDetector(TimeSpan.FromMilliseconds(500));
            detector.ReportHeartbeat(new(TimeSpan.Parse("02:50:45.1408563")));
            detector.ReportHeartbeat(new(TimeSpan.Parse("02:50:45.9669910")));
            detector.ReportHeartbeat(new(TimeSpan.Parse("02:50:46.7933090")));
            var v = detector.GetValue(new(TimeSpan.Parse("02:50:47.7933090")));
            False(double.IsNaN(v));
            True(detector.IsMonitoring);
        }

        [Fact]
        public static void MainTest()
        {
            var detector = new PhiAccrualFailureDetector(TimeSpan.FromMilliseconds(500));
            long now = 1420070400000L;
            for (int i = 0; i < 300; i++)
            {
                var ts = new Timestamp(TimeSpan.FromMilliseconds(now + i * 1000));
                double phi;

                if (i > 290)
                {
                    phi = detector.GetValue(ts);
                    switch (i)
                    {
                        case 291:
                            True(1 < phi && phi < 3);
                            True(phi < detector.Threshold);
                            continue;
                        case 292:
                            True(3 < phi && phi < 8);
                            True(phi < detector.Threshold);
                            continue;
                        case 293:
                            True(8 < phi && phi < 16);
                            True(phi < detector.Threshold);
                            continue;
                        case 294:
                            True(16 < phi && phi < 30);
                            False(phi < detector.Threshold);
                            continue;
                        case 295:
                            True(30 < phi && phi < 50);
                            False(phi < detector.Threshold);
                            continue;
                        case 296:
                            True(50 < phi && phi < 70);
                            False(phi < detector.Threshold);
                            continue;
                        case 297:
                            True(70 < phi && phi < 100);
                            False(phi < detector.Threshold);
                            continue;
                        default:
                            True(100 < phi);
                            False(phi < detector.Threshold);
                            continue;
                    }
                }
                else if (i > 200)
                {
                    if (i % 5 is 0)
                    {
                        phi = detector.GetValue(ts);
                        True(0.1 < phi && phi < 0.5);
                        True(phi < detector.Threshold);
                        continue;
                    }
                }

                detector.ReportHeartbeat(ts);
                phi = detector.GetValue(ts);
                True(phi < 0.1D);
                True(phi < detector.Threshold);
            }

            detector.Reset();
            Equal(0.0D, detector.Value);
        }

        [Fact]
        public static void RegressionIssue151()
        {
            var detector = new PhiAccrualFailureDetector(TimeSpan.FromMilliseconds(500)) { MaxSampleSize = 10 };
            var ts = new Timestamp();

            for (var i = 0; i < 50; i++)
            {
                detector.ReportHeartbeat(ts + TimeSpan.FromMilliseconds(i));
            }
        }
    }
}