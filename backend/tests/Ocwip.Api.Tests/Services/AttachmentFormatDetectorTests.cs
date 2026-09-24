using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// Format detection reads bytes, not names and not declarations (T-32). Every
/// case here is what a client could lie about, and the assertion is that the
/// lie does not work.
/// </summary>
public sealed class AttachmentFormatDetectorTests
{
    [Theory]
    [InlineData("%PDF-1.4\n", "raport.pdf", AllowedFileFormat.Pdf)]
    [InlineData("%PDF-1.7\n", "RAPORT.PDF", AllowedFileFormat.Pdf)]
    public void Pdf_signature_with_matching_extension_is_recognised(
        string header, string fileName, AllowedFileFormat expected)
    {
        Assert.True(
            AttachmentFormatDetector.TryDetect(
                System.Text.Encoding.ASCII.GetBytes(header), fileName, out var format));
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData("zdjecie.jpg")]
    [InlineData("zdjecie.jpeg")]
    public void Jpeg_signature_with_matching_extension_is_recognised(string fileName)
    {
        byte[] header = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        Assert.True(AttachmentFormatDetector.TryDetect(header, fileName, out var format));
        Assert.Equal(AllowedFileFormat.Jpg, format);
    }

    [Theory]
    [InlineData("umowa.docx", AllowedFileFormat.Docx)]
    [InlineData("budzet.xlsx", AllowedFileFormat.Xlsx)]
    [InlineData("opis.odt", AllowedFileFormat.Odt)]
    [InlineData("budzet.ods", AllowedFileFormat.Ods)]
    public void Zip_signature_is_disambiguated_by_extension(
        string fileName, AllowedFileFormat expected)
    {
        byte[] header = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];

        Assert.True(AttachmentFormatDetector.TryDetect(header, fileName, out var format));
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData("statut.doc", AllowedFileFormat.Doc)]
    [InlineData("wykaz.xls", AllowedFileFormat.Xls)]
    public void Ole_signature_is_disambiguated_by_extension(
        string fileName, AllowedFileFormat expected)
    {
        byte[] header = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

        Assert.True(AttachmentFormatDetector.TryDetect(header, fileName, out var format));
        Assert.Equal(expected, format);
    }

    [Fact]
    public void A_zip_signature_with_an_unlisted_extension_is_refused()
    {
        // A renamed generic archive: the signature matches, but nothing tells
        // it apart from a real docx without deeper inspection, so the
        // extension mismatch is the refusal.
        byte[] header = [0x50, 0x4B, 0x03, 0x04];

        Assert.False(
            AttachmentFormatDetector.TryDetect(header, "archiwum.zip", out _));
    }

    [Fact]
    public void A_pdf_signature_with_a_mismatched_extension_is_refused()
    {
        // The bytes say PDF, the name says something else: trusting either
        // one alone would accept a file the applicant did not actually send
        // as what its name claims.
        byte[] header = "%PDF-1.4"u8.ToArray();

        Assert.False(
            AttachmentFormatDetector.TryDetect(header, "raport.jpg", out _));
    }

    [Fact]
    public void Garbage_bytes_are_refused_regardless_of_extension()
    {
        byte[] header = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];

        Assert.False(AttachmentFormatDetector.TryDetect(header, "cokolwiek.pdf", out _));
    }

    [Fact]
    public void A_declared_content_type_never_enters_the_decision()
    {
        // There is no content type parameter at all: TryDetect cannot read
        // one, which is the point. A caller that wanted the declaration to
        // matter would have to add a parameter for it.
        var method = typeof(AttachmentFormatDetector).GetMethod("TryDetect");

        Assert.NotNull(method);
        Assert.DoesNotContain(
            method.GetParameters(), p => p.Name is "contentType" or "declaredContentType");
    }

    [Theory]
    [InlineData(AllowedFileFormat.Pdf, "application/pdf")]
    [InlineData(AllowedFileFormat.Jpg, "image/jpeg")]
    [InlineData(AllowedFileFormat.Odt, "application/vnd.oasis.opendocument.text")]
    public void CanonicalContentType_returns_a_fixed_value_per_format(
        AllowedFileFormat format, string expected)
    {
        Assert.Equal(expected, AttachmentFormatDetector.CanonicalContentType(format));
    }
}
