using System.Buffers.Binary;
using System.IO.Compression;
using ImageGen.LiveCheck;

namespace ImageGen.LiveCheck.Tests;

public sealed class LiveCheckAssetsTests
{
    [Fact]
    public void ReferenceAndDescriptionRequests_ContainValidPngDataUrls()
    {
        var referenceRequest = LiveCheckAssets.CreateReferenceImageRequest("image-model", "prompt", 2);
        var descriptionRequest = LiveCheckAssets.CreateDescriptionRequest("vision-model");
        var referenceUrls = referenceRequest.ReferenceImageDataUrls!;
        var descriptionUrl = Assert.Single(descriptionRequest.Messages).ImageDataUrls!.Single();

        Assert.Equal(2, referenceUrls.Count);
        Assert.All(referenceUrls, AssertValidPngDataUrl);
        AssertValidPngDataUrl(descriptionUrl);
    }

    private static void AssertValidPngDataUrl(string dataUrl)
    {
        const string prefix = "data:image/png;base64,";
        Assert.StartsWith(prefix, dataUrl, StringComparison.Ordinal);
        var png = Convert.FromBase64String(dataUrl[prefix.Length..]);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal(8, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(8, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));

        using var idat = new MemoryStream();
        var offset = 8;
        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            var chunkType = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            if (chunkType == "IDAT")
            {
                idat.Write(png, offset + 8, length);
            }

            offset += 12 + length;
            if (chunkType == "IEND")
            {
                break;
            }
        }

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        zlib.CopyTo(decoded);
        Assert.Equal(8 * (1 + 8 * 4), decoded.Length);
    }
}
