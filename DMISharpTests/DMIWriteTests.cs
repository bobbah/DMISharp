using System.IO;
using System.Linq;
using DMISharp;
using DMISharp.Metadata;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DMISharpTests
{
    public class DMIWriteTests
    {
        [Fact]
        public void CanWriteDMIFile()
        {
            using var file = new DMIFile(@"Data/Input/air_meter.dmi");
            file.Save(@"Data/Output/air_meter_temp.dmi");
        }

        [Fact]
        public void CanSortDMIFile()
        {
            using var file = new DMIFile(@"Data/Input/animal.dmi");
            file.SortStates();
            file.Save(@"Data/Output/animal_sorted_alphabetically.dmi");
        }

        [Fact]
        public void CanWriteAnimations()
        {
            using var file = new DMIFile(@"Data/Input/animal.dmi");
            var toTest = file.States.First(x => x.Name == "mushroom");

            for (var dir = StateDirection.South; dir != StateDirection.SouthEast; dir++)
            {
                using var fs = File.OpenWrite($@"Data/Output/mushroom_gif_{dir}.gif");
                toTest.SaveAnimatedGIF(fs, dir);
            }
        }

        /// <summary>
        /// Tests if gif frames are accidentally disposed after creating animation
        /// </summary>
        [Fact]
        public void AnimationConstructDoesNotDisposeFrames()
        {
            using var file = new DMIFile(@"Data/Input/animal.dmi");
            var toTest = file.States.First(x => x.Name == "mushroom");

            for (var dir = StateDirection.South; dir != StateDirection.SouthEast; dir++)
            {
                using var fs = File.OpenWrite($@"Data/Output/mushroom_A_{dir}.gif");
                toTest.SaveAnimatedGIF(fs, dir);
            }

            for (var dir = StateDirection.South; dir != StateDirection.SouthEast; dir++)
            {
                using var fs = File.OpenWrite($@"Data/Output/mushroom_B_{dir}.gif");
                toTest.SaveAnimatedGIF(fs, dir);
            }
        }

        [Fact]
        public void AnimationOfBarsignsConstructsCorrectly()
        {
            using var fs = File.OpenWrite(@"Data/Output/thegreytide.gif");
            using var file = new DMIFile(@"Data/Input/barsigns.dmi");
            var toTest = file.States.First(x => x.Name == "thegreytide");
            toTest.SaveAnimatedGIF(fs, StateDirection.South);
        }

        [Fact]
        public void AnimationOfSingularityConstructsCorrectly()
        {
            using var fs = File.OpenWrite(@"Data/Output/singularity_s11.gif");
            using var file = new DMIFile(@"Data/Input/352x352.dmi");
            var toTest = file.States.First(x => x.Name == "singularity_s11");
            toTest.SaveAnimatedGIF(fs, StateDirection.South);
        }

        [Fact]
        public void ResavingFileMatchesOriginalMetadata()
        {
            using (var fs = File.OpenWrite(@"Data/Output/broadMobs.dmi"))
            using (var originalFile = new DMIFile(@"Data/Input/broadMobs.dmi"))
                originalFile.Save(fs);
            
            // Check metadata is equal
            using (var oldFile = File.OpenRead(@"Data/Input/broadMobs.dmi"))
            using (var newFile = File.OpenRead(@"Data/Output/broadMobs.dmi"))
                Assert.Equal(DMIMetadata.GetDMIMetadata(oldFile).ToString(), DMIMetadata.GetDMIMetadata(newFile).ToString());
        }
        
        [Fact]
        public void ResavingFileMatchesOriginalImage_RectSprites()
        {
            using (var fs = File.OpenWrite(@"Data/Output/broadMobs.dmi"))
            using (var originalFile = new DMIFile(@"Data/Input/broadMobs.dmi"))
                originalFile.Save(fs);
            
            // Check image is equal
            var pixelDiffs = 0;
            using (var oldFile = Image.Load<Rgba32>(@"Data/Input/broadMobs.dmi"))
            using (var newFile = Image.Load<Rgba32>(@"Data/Output/broadMobs.dmi"))
            {
                // Check overall dimensions
                Assert.Equal(oldFile.Width, newFile.Width);
                Assert.Equal(oldFile.Height, newFile.Height);
                
                // Check pixel content
                for (var ypx = 0; ypx < oldFile.Height; ypx++)
                {
                    var oldSpan = oldFile.GetPixelRowSpan(ypx);
                    var newSpan = newFile.GetPixelRowSpan(ypx);
                    for (var xpx = 0; xpx < oldFile.Width; xpx++)
                    {
                        if (oldSpan[xpx] != newSpan[xpx])
                            pixelDiffs++;
                    }
                }
            }
            
            Assert.Equal(0, pixelDiffs);
        }
        
        [Fact]
        public void ResavingFileMatchesOriginalImage_SquareSprites()
        {
            using (var fs = File.OpenWrite(@"Data/Output/animal.dmi"))
            using (var originalFile = new DMIFile(@"Data/Input/animal.dmi"))
                originalFile.Save(fs);
            
            // Check image is equal
            var pixelDiffs = 0;
            using (var oldFile = Image.Load<Rgba32>(@"Data/Input/animal.dmi"))
            using (var newFile = Image.Load<Rgba32>(@"Data/Output/animal.dmi"))
            {
                // Check overall dimensions
                Assert.Equal(oldFile.Width, newFile.Width);
                Assert.Equal(oldFile.Height, newFile.Height);
                
                // Check pixel content
                for (var ypx = 0; ypx < oldFile.Height; ypx++)
                {
                    var oldSpan = oldFile.GetPixelRowSpan(ypx);
                    var newSpan = newFile.GetPixelRowSpan(ypx);
                    for (var xpx = 0; xpx < oldFile.Width; xpx++)
                    {
                        if (oldSpan[xpx] != newSpan[xpx])
                            pixelDiffs++;
                    }
                }
            }
            
            Assert.Equal(0, pixelDiffs);
        }
        
        [Fact]
        public void ResavingFileMatchesOriginalImage_SingleSprite()
        {
            using (var fs = File.OpenWrite(@"Data/Output/light_64.dmi"))
            using (var originalFile = new DMIFile(@"Data/Input/light_64.dmi"))
                originalFile.Save(fs);
            
            // Check image is equal
            var pixelDiffs = 0;
            using (var oldFile = Image.Load<Rgba32>(@"Data/Input/light_64.dmi"))
            using (var newFile = Image.Load<Rgba32>(@"Data/Output/light_64.dmi"))
            {
                // Check overall dimensions
                Assert.Equal(oldFile.Width, newFile.Width);
                Assert.Equal(oldFile.Height, newFile.Height);
                
                // Check pixel content
                for (var ypx = 0; ypx < oldFile.Height; ypx++)
                {
                    var oldSpan = oldFile.GetPixelRowSpan(ypx);
                    var newSpan = newFile.GetPixelRowSpan(ypx);
                    for (var xpx = 0; xpx < oldFile.Width; xpx++)
                    {
                        if (oldSpan[xpx] != newSpan[xpx])
                            pixelDiffs++;
                    }
                }
            }
            
            Assert.Equal(0, pixelDiffs);
        }
    }
}
