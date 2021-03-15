using System.Linq;
using DMISharp;
using Xunit;

namespace DMISharpTests
{
    public class DMIReadTests
    {
        [Fact]
        public void AnimalDMIStateCount()
        {
            using var file = new DMIFile(@"Data/Input/animal.dmi");
            Assert.Equal(154, file.States.Count);
        }

        [Fact]
        public void AirMeterDMIStateCount()
        {
            using var file = new DMIFile(@"Data/Input/air_meter.dmi");
            Assert.Equal(16, file.States.Count);
        }

        [Fact]
        public void AtmosTestingDMIStateCount()
        {
            using var file = new DMIFile(@"Data/Input/atmos_testing.dmi");
            Assert.Equal(5, file.States.Count);
        }

        [Fact]
        public void LightingDMIStateCount()
        {
            using var file = new DMIFile(@"Data/Input/lighting.dmi");
            Assert.Equal(3, file.States.Count);
        }

        [Fact]
        public void TurfAnalysisDMIStateCount()
        {
            using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");
            Assert.Equal(16, file.States.Count);
        }

        [Fact]
        public void SpaceDragonDMIStateCount()
        {
            using var file = new DMIFile(@"Data/Input/spacedragon.dmi");
            Assert.Equal(2, file.States.Count);
        }

        [Fact]
        public void CanReadDirectionDepth()
        {
            using var file = new DMIFile(@"Data/Input/animal.dmi");

            // Arrange
            var bat = file.States.First(x => x.Name == "bat");
            var carp = file.States.First(x => x.Name == "parrot_fly");
            var rareFrogDead = file.States.First(x => x.Name == "rare_frog_dead");

            // Assert
            Assert.Equal(DirectionDepth.Four, bat.DirectionDepth);
            Assert.Equal(DirectionDepth.Four, carp.DirectionDepth);
            Assert.Equal(DirectionDepth.One, rareFrogDead.DirectionDepth);
        }

        [Fact]
        public void GoonTurfAnalysisDMIStateCount()
        {
            using var file = new DMIFile(@"Data/Input/turf_analysis_goon.dmi");
            Assert.Equal(16, file.States.Count);
        }
    }
}
