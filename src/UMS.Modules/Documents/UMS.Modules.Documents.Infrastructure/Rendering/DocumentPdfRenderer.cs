using System.Text.Json;
using Microsoft.Extensions.Options;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.Common;

namespace UMS.Modules.Documents.Infrastructure.Rendering;

/// <summary>
/// DOC-2/DOC-8: renders a single-page PDF for any <see cref="DocumentType"/> from a pinned
/// template version and caller-supplied field data, embedding a QR code that encodes the public
/// verify URL (requirement-spec.md documents §2 QR / Digital Verification).
///
/// <para>
/// <b>PDF library choice - QuestPDF (Community license).</b> No PDF library existed anywhere in
/// ums-core before this module. QuestPDF was chosen over the alternatives considered
/// (PdfSharpCore/MigraDoc - MIT, but a much lower-level, manual-layout API that would cost real
/// time getting Bengali line-wrapping/RTL-adjacent shaping right; iText7 - AGPL/commercial dual
/// license, a licensing model this platform's own commercial-product context makes a poor fit)
/// for its fluent, testable layout API and its SkiaSharp-backed text engine, which renders complex
/// scripts (Bengali conjuncts/diacritics) correctly out of the box once a Bengali-capable font is
/// registered (<see cref="DocumentFonts"/>) - directly satisfying edge-cases.md's Bengali-rendering
/// release-blocking check. <b>License caveat, called out explicitly per the build brief:</b>
/// QuestPDF's Community license is free only for a company/organization with less than $1M USD in
/// annual gross revenue (or a non-profit/government/educational use) - fine for this project today,
/// but a real production deployment must confirm which license tier applies before shipping and
/// budget for a commercial license if the operating institution's revenue crosses that threshold.
/// This is a procurement/licensing follow-up, not a code change.
/// </para>
/// </summary>
internal sealed class DocumentPdfRenderer(IOptions<DocumentRenderingOptions> options) : IDocumentRenderer
{
    private readonly DocumentRenderingOptions _options = options.Value;

    public byte[] Render(DocumentRenderRequest request)
    {
        DocumentFonts.EnsureRegistered();

        var translation = request.Template.ResolveTranslation(request.Language);
        var labels = JsonSerializer.Deserialize<Dictionary<string, string>>(translation.LabelsJson) ?? [];
        var verifyUrl = $"{_options.VerifyBaseUrl.TrimEnd('/')}/{request.DigitalVerificationId}";
        var qrCodeBytes = GenerateQrCodePng(verifyUrl);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);

                // Bengali-capable font as the document-wide default (edge-cases.md: Bengali name
                // rendering is release-blocking for every document type, not just Bengali-language
                // documents - a caller-supplied field value can carry a Bengali name even inside an
                // otherwise-English-labeled document).
                page.DefaultTextStyle(style => style.FontFamily(DocumentFonts.BengaliFamily).FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text(_options.InstitutionName).Bold().FontSize(16);
                    col.Item().PaddingTop(4).Text(translation.Title).SemiBold().FontSize(14);
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(16).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                    });

                    foreach (var (key, value) in request.Fields.OrderBy(f => f.Key, StringComparer.Ordinal))
                    {
                        var label = labels.GetValueOrDefault(key, key);
                        table.Cell().Padding(4).Text(label).SemiBold();
                        table.Cell().Padding(4).Text(value);
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"Issued: {request.IssuedAt:yyyy-MM-dd}").FontSize(9);
                        col.Item().Text($"Verification ID: {request.DigitalVerificationId}").FontSize(9);
                        col.Item().Text(verifyUrl).FontSize(8);
                    });

                    row.ConstantItem(70).Image(qrCodeBytes);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static byte[] GenerateQrCodePng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(10);
    }
}
