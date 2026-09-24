using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Decides which of the eight allowed attachment formats an uploaded file
/// actually is (T-32). The declared content type never enters this decision:
/// a client can send any Content-Type header it likes, so the format comes
/// from the first bytes of the file plus the extension the applicant typed,
/// never from what the request claims about itself.
///
/// Four byte signatures cover the eight formats, because the two
/// office document families each share one signature: every ZIP-based format
/// (docx, xlsx, odt, ods) starts identically, and so does every legacy OLE
/// format (doc, xls). The extension is what tells those apart. This is a
/// lighter check than unzipping the file to read its internal content-type
/// entry, and is deliberately so: the two failure modes it accepts are a
/// generic .zip renamed to .docx and a legacy .doc renamed to .xls, neither of
/// which is a route to reading somebody else's data, only to a document
/// nobody can open, which the applicant discovers immediately.
/// </summary>
internal static class AttachmentFormatDetector
{
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] OleSignature =
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>
    /// Bytes needed from the start of the file to decide. Callers read at
    /// least this many before asking, and a shorter file is simply not one of
    /// the eight formats.
    /// </summary>
    public const int RequiredHeaderBytes = 8;

    public static bool TryDetect(
        ReadOnlySpan<byte> header, string fileName, out AllowedFileFormat format)
    {
        var extension = ExtensionOf(fileName);

        if (header.StartsWith(PdfSignature))
        {
            return Match(AllowedFileFormat.Pdf, extension, "pdf", out format);
        }

        if (header.StartsWith(JpegSignature))
        {
            return extension is "jpg" or "jpeg"
                ? Set(out format, AllowedFileFormat.Jpg)
                : None(out format);
        }

        if (header.StartsWith(ZipSignature))
        {
            return extension switch
            {
                "docx" => Set(out format, AllowedFileFormat.Docx),
                "xlsx" => Set(out format, AllowedFileFormat.Xlsx),
                "odt" => Set(out format, AllowedFileFormat.Odt),
                "ods" => Set(out format, AllowedFileFormat.Ods),
                _ => None(out format),
            };
        }

        if (header.StartsWith(OleSignature))
        {
            return extension switch
            {
                "doc" => Set(out format, AllowedFileFormat.Doc),
                "xls" => Set(out format, AllowedFileFormat.Xls),
                _ => None(out format),
            };
        }

        return None(out format);
    }

    private static bool Match(
        AllowedFileFormat candidate,
        string extension,
        string expectedExtension,
        out AllowedFileFormat format) =>
        extension == expectedExtension
            ? Set(out format, candidate)
            : None(out format);

    private static bool Set(out AllowedFileFormat format, AllowedFileFormat value)
    {
        format = value;
        return true;
    }

    private static bool None(out AllowedFileFormat format)
    {
        format = default;
        return false;
    }

    private static string ExtensionOf(string fileName)
    {
        var dot = fileName.LastIndexOf('.');

        return dot < 0 || dot == fileName.Length - 1
            ? string.Empty
            : fileName[(dot + 1)..].ToLowerInvariant();
    }

    /// <summary>
    /// The content type a download answers with, decided by the verified
    /// format rather than repeated from whatever the uploader declared: a
    /// browser told to render a mislabelled file inline is exactly the risk a
    /// declared, unverified content type creates.
    /// </summary>
    public static string CanonicalContentType(AllowedFileFormat format) => format switch
    {
        AllowedFileFormat.Pdf => "application/pdf",
        AllowedFileFormat.Doc => "application/msword",
        AllowedFileFormat.Docx =>
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        AllowedFileFormat.Xls => "application/vnd.ms-excel",
        AllowedFileFormat.Xlsx =>
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        AllowedFileFormat.Jpg => "image/jpeg",
        AllowedFileFormat.Odt => "application/vnd.oasis.opendocument.text",
        AllowedFileFormat.Ods => "application/vnd.oasis.opendocument.spreadsheet",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };
}
