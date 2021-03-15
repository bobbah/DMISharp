using System.Linq;
using DMISharp;
using Xunit;

namespace DMISharpTests
{
    public class DMIModifyTests
    {
        [Fact]
        public static void ShouldRemoveStateMetadata()
        {
            using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

            // Arrange
            var mdCount = file.Metadata.States.Count;
            var stateToRemove = file.States.Last();

            // Act
            var result = file.RemoveState(stateToRemove);

            // Assert
            Assert.True(result);
            Assert.Equal(mdCount - 1, file.Metadata.States.Count);
        }

        [Fact]
        public static void ShouldDetectValidUnmodifiedDMIState()
        {
            // Arrange
            using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

            // Act
            var arrowState = file.States.First(x => x.Name == "arrow");
            
            // Assert
            Assert.True(arrowState.IsReadyForSave());
        }

        [Fact]
        public static void ShouldDetectInvalidDMIState__MissingFrame()
        {
            // Arrange
            using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

            // Act
            var arrowState = file.States.First(x => x.Name == "arrow");
            arrowState.DeleteFrame(0);
            
            // Assert
            Assert.False(arrowState.IsReadyForSave());
            Assert.False(file.CanSave());
        }

        [Fact]
        public static void ShouldDetectInvalidDMIState__MissingDirection()
        {
            // Arrange
            using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

            // Act
            var arrowState = file.States.First(x => x.Name == "arrow");
            arrowState.SetDirectionDepth(DirectionDepth.Eight);
            
            // Assert
            Assert.False(arrowState.IsReadyForSave());
            Assert.False(file.CanSave());
        }
    }
}
