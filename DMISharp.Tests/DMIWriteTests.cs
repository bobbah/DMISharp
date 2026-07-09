using System.IO;
using System.Linq;
using DMISharp.Metadata;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DMISharp.Tests;

public class DMIWriteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveProducesIdenticalOutputAcrossStreamCapabilities(bool trueColor)
    {
        using var file = CreateSyntheticFile(trueColor);
        using var expectedStream = new MemoryStream();
        file.Save(expectedStream);
        var expected = expectedStream.ToArray();

        var prefix = new byte[] { 1, 2, 3, 4 };
        using var appendStream = new MemoryStream();
        appendStream.Write(prefix);
        file.Save(appendStream);
        Assert.Equal(prefix.Concat(expected), appendStream.ToArray());

        using var nonSeekableOutput = new MemoryStream();
        using (var nonSeekableStream = new NonSeekableWriteStream(nonSeekableOutput))
            file.Save(nonSeekableStream);
        Assert.Equal(expected, nonSeekableOutput.ToArray());

        var overwriteBuffer = Enumerable.Repeat((byte)0xA5, expected.Length + 64).ToArray();
        using var overwriteStream = new MemoryStream(overwriteBuffer, writable: true);
        file.Save(overwriteStream);
        Assert.Equal(expected, overwriteBuffer.Take(expected.Length));
        Assert.All(overwriteBuffer.Skip(expected.Length), value => Assert.Equal(0xA5, value));
    }

    [Fact]
    public void SaveKeepsUnusedAtlasCellsTransparent()
    {
        using var file = CreateSyntheticFile(false, 3);
        using var output = new MemoryStream();
        file.Save(output);
        output.Position = 0;

        using var image = Image.Load<Rgba32>(output);
        Assert.Equal(34, image.Width);
        Assert.Equal(34, image.Height);
        Assert.Equal(0, image[33, 33].A);
    }

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
        using var oldFile = File.OpenRead(inputPath);
        using var newFile = File.OpenRead(outputPath);
        Assert.Equal(DMIMetadata.GetDMIMetadata(oldFile).ToString(), DMIMetadata.GetDMIMetadata(newFile).ToString());
    }
        
    [Theory]
    [InlineData(@"Data/Input/broadMobs.dmi", @"Data/Output/broadMobs.dmi")]
    public void ResavingFileMatchesOriginalImageRectSprites(string inputPath, string outputPath)
    {
        if (File.Exists(outputPath))
            File.Delete(outputPath);
            
        using (var fs = File.OpenWrite(outputPath))
        using (var originalFile = new DMIFile(inputPath))
            originalFile.Save(fs);
            
        // Check image is equal
        var pixelDiffs = 0;
        using var oldFile = Image.Load<Rgba32>(inputPath);
        using var newFile = Image.Load<Rgba32>(outputPath);
        
        // Check overall dimensions
        Assert.Equal(oldFile.Width, newFile.Width);
        Assert.Equal(oldFile.Height, newFile.Height);
                
        // Check pixel content
        var height = oldFile.Height;
        var width = oldFile.Width;
        oldFile.ProcessPixelRows(newFile, (oldAccessor, newAccessor) =>
        {
            for (var ypx = 0; ypx < height; ypx++)
            {
                var oldSpan = oldAccessor.GetRowSpan(ypx);
                var newSpan = newAccessor.GetRowSpan(ypx);
                for (var xpx = 0; xpx < width; xpx++)
                {
                    if (!(oldSpan[xpx].A == 0 && newSpan[xpx].A == 0) && oldSpan[xpx] != newSpan[xpx])
                        pixelDiffs++;
                }
            }
        });

        Assert.Equal(0, pixelDiffs);
    }
        
    [Theory]
    [InlineData(@"Data/Input/animal.dmi", @"Data/Output/animal.dmi")]
    public void ResavingFileMatchesOriginalImageSquareSprites(string inputPath, string outputPath)
    {
        if (File.Exists(outputPath))
            File.Delete(outputPath);
            
        using (var fs = File.OpenWrite(outputPath))
        using (var originalFile = new DMIFile(inputPath))
            originalFile.Save(fs);
            
        // Check image is equal
        var pixelDiffs = 0;
        using var oldFile = Image.Load<Rgba32>(inputPath);
        using var newFile = Image.Load<Rgba32>(outputPath);
        
        // Check overall dimensions
        Assert.Equal(oldFile.Width, newFile.Width);
        Assert.Equal(oldFile.Height, newFile.Height);
                
        // Check pixel content
        var height = oldFile.Height;
        var width = oldFile.Width;
        oldFile.ProcessPixelRows(newFile, (oldAccessor, newAccessor) =>
        {
            for (var ypx = 0; ypx < height; ypx++)
            {
                var oldSpan = oldAccessor.GetRowSpan(ypx);
                var newSpan = newAccessor.GetRowSpan(ypx);
                for (var xpx = 0; xpx < width; xpx++)
                {
                    if (!(oldSpan[xpx].A == 0 && newSpan[xpx].A == 0) && oldSpan[xpx] != newSpan[xpx])
                        pixelDiffs++;
                }
            }
        });

        Assert.Equal(0, pixelDiffs);
    }
        
    [Theory]
    [InlineData(@"Data/Input/light_64.dmi", @"Data/Output/light_64.dmi")]
    public void ResavingFileMatchesOriginalImageSingleSprite(string inputPath, string outputPath)
    {
        if (File.Exists(outputPath))
            File.Delete(outputPath);
            
        using (var fs = File.OpenWrite(outputPath))
        using (var originalFile = new DMIFile(inputPath))
            originalFile.Save(fs);
            
        // Check image is equal
        var pixelDiffs = 0;
        using var oldFile = Image.Load<Rgba32>(inputPath);
        using var newFile = Image.Load<Rgba32>(outputPath);
        
        // Check overall dimensions
        Assert.Equal(oldFile.Width, newFile.Width);
        Assert.Equal(oldFile.Height, newFile.Height);
                
        // Check pixel content
        var height = oldFile.Height;
        var width = oldFile.Width;
        oldFile.ProcessPixelRows(newFile, (oldAccessor, newAccessor) =>
        {
            for (var ypx = 0; ypx < height; ypx++)
            {
                var oldSpan = oldAccessor.GetRowSpan(ypx);
                var newSpan = newAccessor.GetRowSpan(ypx);
                for (var xpx = 0; xpx < width; xpx++)
                {
                    if (!(oldSpan[xpx].A == 0 && newSpan[xpx].A == 0) && oldSpan[xpx] != newSpan[xpx])
                        pixelDiffs++;
                }
            }
        });

        Assert.Equal(0, pixelDiffs);
    }

    private static DMIFile CreateSyntheticFile(bool trueColor, int frameCount = 1)
    {
        const int frameSize = 17;
        var file = new DMIFile(frameSize, frameSize);
#pragma warning disable CA2000
        var state = new DMIState("synthetic", DirectionDepth.One, frameCount, frameSize, frameSize);
#pragma warning restore CA2000

        for (var frame = 0; frame < frameCount; frame++)
        {
#pragma warning disable CA2000
            var image = new Image<Rgba32>(frameSize, frameSize);
#pragma warning restore CA2000
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < frameSize; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < frameSize; x++)
                    {
                        row[x] = trueColor
                            ? new Rgba32((byte)x, (byte)y, (byte)(y * frameSize + x), byte.MaxValue)
                            : new Rgba32((byte)((x + y + frame) % 16 * 17), 64, 128, byte.MaxValue);
                    }
                }
            });
            state.SetFrame(image, frame);
        }

        file.AddState(state);
        return file;
    }

    private sealed class NonSeekableWriteStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableWriteStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new System.NotSupportedException();

        public override long Position
        {
            get => throw new System.NotSupportedException();
            set => throw new System.NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new System.NotSupportedException();

        public override void SetLength(long value) => throw new System.NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }
}