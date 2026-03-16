namespace MailAgent.Application.Tests;

[TestFixture]
public class EmailBodyConverterTests
{
  private EmailBodyConverter _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _sut = new EmailBodyConverter();
  }

  [Test]
  public void ConvertToMarkdown_ReturnsNormalizedTextBody_WhenHtmlBodyIsEmpty()
  {
    // Given.
    const string textBody = "line 1\r\n\r\n\r\nline 2 &amp; more";

    // When.
    var result = _sut.ConvertToMarkdown(null, textBody);

    // Then.
    Assert.That(result, Is.EqualTo("line 1\n\nline 2 & more"));
  }

  [Test]
  public void ConvertToMarkdown_ReturnsEmptyString_WhenBothBodiesAreEmpty()
  {
    // When.
    var result = _sut.ConvertToMarkdown(" ", null);

    // Then.
    Assert.That(result, Is.Empty);
  }

  [Test]
  public void ConvertToMarkdown_RemovesJunkHtmlAndPreservesSupportedAttributes()
  {
    // Given.
    const string html = """
                        <html>
                          <head>
                            <style>.x { color: red; }</style>
                            <script>alert('x');</script>
                          </head>
                          <body>
                            <p class="intro">Hello&nbsp;world</p>
                            <a href="https://example.com" onclick="evil()">docs</a>
                            <img src="https://example.com/image.png" alt="preview" width="100" />
                          </body>
                        </html>
                        """;

    // When.
    var result = _sut.ConvertToMarkdown(html, null);

    // Then.
    Assert.That(result, Does.Contain("Hello\u00A0world"));
    Assert.That(result, Does.Contain("[docs](https://example.com)"));
    Assert.That(result, Does.Contain("![preview](https://example.com/image.png)"));
    Assert.That(result, Does.Not.Contain("alert"));
    Assert.That(result, Does.Not.Contain("color: red"));
    Assert.That(result, Does.Not.Contain("onclick"));
    Assert.That(result, Does.Not.Contain("width=\"100\""));
  }
}
