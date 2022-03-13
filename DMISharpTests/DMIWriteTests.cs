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

        [Theory]
        [InlineData(@"Data/Input/broadMobs.dmi", @"Data/Output/broadMobs.dmi")]
        public void ResavingFileMatchesOriginalMetadata(string inputPath, string outputPath)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            
            using (var fs = File.OpenWrite(outputPath))
            using (var originalFile = new DMIFile(inputPath))
                originalFile.Save(fs);
            
            // Check metadata is equal
            using (var oldFile = File.OpenRead(inputPath))
            using (var newFile = File.OpenRead(outputPath))
                Assert.Equal(DMIMetadata.GetDMIMetadata(oldFile).ToString(), DMIMetadata.GetDMIMetadata(newFile).ToString());
        }
        
        [Theory]
        [InlineData(@"Data/Input/broadMobs.dmi", @"Data/Output/broadMobs.dmi")]
        public void ResavingFileMatchesOriginalImage_RectSprites(string inputPath, string outputPath)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            
            using (var fs = File.OpenWrite(outputPath))
            using (var originalFile = new DMIFile(inputPath))
                originalFile.Save(fs);
            
            // Check image is equal
            var pixelDiffs = 0;
            using (var oldFile = Image.Load<Rgba32>(inputPath))
            using (var newFile = Image.Load<Rgba32>(outputPath))
            {
                // Check overall dimensions
                Assert.Equal(oldFile.Width, newFile.Width);
                Assert.Equal(oldFile.Height, newFile.Height);
                
                // Check pixel content
                oldFile.ProcessPixelRows(newFile, (oldAccessor, newAccessor) =>
                {
                    for (var ypx = 0; ypx < oldFile.Height; ypx++)
                    {
                        var oldSpan = oldAccessor.GetRowSpan(ypx);
                        var newSpan = newAccessor.GetRowSpan(ypx);
                        for (var xpx = 0; xpx < oldFile.Width; xpx++)
                        {
                            if (!(oldSpan[xpx].A == 0 && newSpan[xpx].A == 0) && oldSpan[xpx] != newSpan[xpx])
                                pixelDiffs++;
                        }
                    }
                });
            }
            
            Assert.Equal(0, pixelDiffs);
        }
        
        [Theory]
        [InlineData(@"Data/Input/animal.dmi", @"Data/Output/animal.dmi")]
        public void ResavingFileMatchesOriginalImage_SquareSprites(string inputPath, string outputPath)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            
            using (var fs = File.OpenWrite(outputPath))
            using (var originalFile = new DMIFile(inputPath))
                originalFile.Save(fs);
            
            // Check image is equal
            var pixelDiffs = 0;
            using (var oldFile = Image.Load<Rgba32>(inputPath))
            using (var newFile = Image.Load<Rgba32>(outputPath))
            {
                // Check overall dimensions
                Assert.Equal(oldFile.Width, newFile.Width);
                Assert.Equal(oldFile.Height, newFile.Height);
                
                // Check pixel content
                oldFile.ProcessPixelRows(newFile, (oldAccessor, newAccessor) =>
                {
                    for (var ypx = 0; ypx < oldFile.Height; ypx++)
                    {
                        var oldSpan = oldAccessor.GetRowSpan(ypx);
                        var newSpan = newAccessor.GetRowSpan(ypx);
                        for (var xpx = 0; xpx < oldFile.Width; xpx++)
                        {
                            if (!(oldSpan[xpx].A == 0 && newSpan[xpx].A == 0) && oldSpan[xpx] != newSpan[xpx])
                                pixelDiffs++;
                        }
                    }
                });
            }
            
            Assert.Equal(0, pixelDiffs);
        }
        
        [Theory]
        [InlineData(@"Data/Input/light_64.dmi", @"Data/Output/light_64.dmi")]
        public void ResavingFileMatchesOriginalImage_SingleSprite(string inputPath, string outputPath)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            
            using (var fs = File.OpenWrite(outputPath))
            using (var originalFile = new DMIFile(inputPath))
                originalFile.Save(fs);
            
            // Check image is equal
            var pixelDiffs = 0;
            using (var oldFile = Image.Load<Rgba32>(inputPath))
            using (var newFile = Image.Load<Rgba32>(outputPath))
            {
                // Check overall dimensions
                Assert.Equal(oldFile.Width, newFile.Width);
                Assert.Equal(oldFile.Height, newFile.Height);
                
                // Check pixel content
                oldFile.ProcessPixelRows(newFile, (oldAccessor, newAccessor) =>
                {
                    for (var ypx = 0; ypx < oldFile.Height; ypx++)
                    {
                        var oldSpan = oldAccessor.GetRowSpan(ypx);
                        var newSpan = newAccessor.GetRowSpan(ypx);
                        for (var xpx = 0; xpx < oldFile.Width; xpx++)
                        {
                            if (!(oldSpan[xpx].A == 0 && newSpan[xpx].A == 0) && oldSpan[xpx] != newSpan[xpx])
                                pixelDiffs++;
                        }
                    }
                });
            }
            
            Assert.Equal(0, pixelDiffs);
        }
    }
}
