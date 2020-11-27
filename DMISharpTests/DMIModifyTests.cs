using DMISharp;
using System.Linq;
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
            var mdCount = file.Metadata.States.Count();
            var stateToRemove = file.States.Last();

            // Act
            var result = file.RemoveState(stateToRemove);

            // Assert
            Assert.True(result);
            Assert.Equal(mdCount - 1, file.Metadata.States.Count());
        }
    }
}
