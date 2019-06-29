using System;
using Xunit;
using DMISharp;
using System.Linq;

namespace DMISharpTests
{
    public class DMILoadTests
    {
        [Fact]
        public void AnimalDMIStateCount()
        {
            using (var file = new DMIFile(@"Data/Input/animal.dmi"))
            {
                Assert.Equal(149, file.States.Count());
            }
        }

        [Fact]
        public void AirMeterDMIStateCount()
        {
            using (var file = new DMIFile(@"Data/Input/air_meter.dmi"))
            {
                Assert.Equal(16, file.States.Count());
            }
        }

        [Fact]
        public void AtmosTestingDMIStateCount()
        {
            using (var file = new DMIFile(@"Data/Input/atmos_testing.dmi"))
            {
                Assert.Equal(5, file.States.Count());
            }
        }

        [Fact]
        public void LightingDMIStateCount()
        {
            using (var file = new DMIFile(@"Data/Input/lighting.dmi"))
            {
                Assert.Equal(3, file.States.Count());
            }
        }

        [Fact]
        public void TurfAnalysisDMIStateCount()
        {
            using (var file = new DMIFile(@"Data/Input/turf_analysis.dmi"))
            {
                Assert.Equal(16, file.States.Count());
            }
        }
    }
}
