using SkiaSharp;

namespace SportFrog.Api.Infrastructure.Storage;

/// <summary>An image after normalization, ready to be stored.</summary>
public sealed record NormalizedImage(
    byte[] Content,
    string ContentType,
    string Extension,
    int Width,
    int Height);

/// <summary>
/// Turns whatever somebody uploaded into one predictable image.
/// </summary>
/// <remarks>
/// Three things happen here, and each one is the answer to a real failure.
///
/// The image is decoded. Until this point "it is a PNG" is a claim the
/// uploader made in a header, and a credential generator that trusts it
/// discovers the truth halfway through a batch of four hundred, at which
/// point the useful moment to refuse was hours earlier. Something that does
/// not decode is refused at the door.
///
/// It is turned upright. A photograph from a phone is almost always stored
/// sideways with a rotation recorded in its metadata, which browsers honour
/// and drawing libraries generally do not — so the picture looks right while
/// somebody uploads it and lies on its side on the printed credential.
/// Applying the rotation to the pixels here means every later reader agrees
/// about which way up it is.
///
/// It is re-encoded. That is what strips the metadata, and the metadata is
/// the point: a photograph taken on a phone carries the coordinates of where
/// it was taken, which for a photograph of a child is usually their school or
/// their home. Nobody uploading a squad list intends to publish that, and
/// re-encoding removes it without anybody having to remember to.
/// </remarks>
public static class ImageNormalizer
{
    /// <summary>
    /// Longest edge kept, in pixels.
    /// </summary>
    /// <remarks>
    /// Sized for what these images are for: a face on a credential printed at
    /// about 25 mm wide, which at 300 dpi is some 300 pixels. A thousand
    /// leaves room to crop and to reprint larger, and refuses to carry the
    /// twelve megapixels a phone produced for a picture that ends up the size
    /// of a postage stamp.
    /// </remarks>
    public const int MaximumEdge = 1000;

    private const int Quality = 85;

    /// <summary>
    /// Photographs are stored as JPEG: it has no transparency to lose here,
    /// every printer and PDF reader understands it, and it embeds into a PDF
    /// without being re-compressed a second time.
    /// </summary>
    private const string StoredContentType = "image/jpeg";

    private const string StoredExtension = "jpg";

    /// <summary>
    /// Normalizes an uploaded image, or answers null if it is not one.
    /// </summary>
    /// <param name="maximumEdge">
    /// How large it may stay. Defaults to what a face on a credential needs;
    /// artwork printed at card size asks for more, and passing it here keeps
    /// the decoding, the straightening and the stripping of the camera's
    /// metadata identical for both.
    /// </param>
    public static NormalizedImage? Normalize(byte[] source, int maximumEdge = MaximumEdge)
    {
        using var data = SKData.CreateCopy(source);

        // The codec is read separately from the pixels because the rotation
        // lives in the container, not in the bitmap: decoding straight to a
        // bitmap throws the orientation away.
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return null;
        }

        var origin = codec.EncodedOrigin;

        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null || decoded.Width == 0 || decoded.Height == 0)
        {
            return null;
        }

        var turns = Turned(origin);
        var sourceWidth = turns ? decoded.Height : decoded.Width;
        var sourceHeight = turns ? decoded.Width : decoded.Height;

        // Only ever downwards. Enlarging a small photograph invents detail it
        // does not have and makes the file bigger for the privilege.
        var scale = Math.Min(1d, (double)maximumEdge / Math.Max(sourceWidth, sourceHeight));
        var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));

        // Opaque, cleared to white: JPEG cannot carry transparency, and
        // whatever was see-through in a PNG would otherwise come out black.
        using var canvasBitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));

        using (var canvas = new SKCanvas(canvasBitmap))
        {
            canvas.Clear(SKColors.White);

            // Rotate first, then scale into place: the matrix maps the
            // decoded pixels onto an upright image of the target size in one
            // pass, so the picture is resampled once rather than twice.
            canvas.SetMatrix(Upright(origin, width, height));

            canvas.DrawBitmap(
                decoded,
                new SKRect(0, 0, turns ? height : width, turns ? width : height),
                Sampling,
                paint: null);
        }

        using var image = SKImage.FromBitmap(canvasBitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, Quality);

        return encoded is null
            ? null
            : new NormalizedImage(
                encoded.ToArray(), StoredContentType, StoredExtension, width, height);
    }

    /// <summary>Mitchell, which is the usual choice for shrinking photographs.</summary>
    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    /// <summary>
    /// Whether the recorded orientation swaps the two edges.
    /// </summary>
    private static bool Turned(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// The transform that puts a stored image the right way up, in a frame of
    /// the given size.
    /// </summary>
    /// <remarks>
    /// The eight cases are the eight EXIF orientations. Six of them are rare
    /// enough that no one would notice them missing until the day a scanner
    /// or an unusual phone produced one, so they are all written out rather
    /// than reduced to the two that turn up in testing.
    ///
    /// <see cref="SKMatrix"/> reads as x' = ScaleX·x + SkewX·y + TransX, and
    /// y' = SkewY·x + ScaleY·y + TransY.
    /// </remarks>
    private static SKMatrix Upright(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        // Mirrored horizontally.
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),

        // Half a turn.
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),

        // Mirrored vertically.
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),

        // Transposed.
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),

        // A quarter turn clockwise, which is a phone held upright.
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, width, 1, 0, 0, 0, 0, 1),

        // Transposed the other way.
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, width, -1, 0, height, 0, 0, 1),

        // A quarter turn anticlockwise.
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, height, 0, 0, 1),

        // TopLeft, and anything a future Skia adds: leave it alone.
        _ => SKMatrix.Identity,
    };
}
