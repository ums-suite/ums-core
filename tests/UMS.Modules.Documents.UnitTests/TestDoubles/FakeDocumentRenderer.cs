using System.Text;
using UMS.Modules.Documents.Application.Abstractions;

namespace UMS.Modules.Documents.UnitTests.TestDoubles;

public sealed class FakeDocumentRenderer : IDocumentRenderer
{
    public byte[] Render(DocumentRenderRequest request) => Encoding.UTF8.GetBytes("%PDF-fake");
}
