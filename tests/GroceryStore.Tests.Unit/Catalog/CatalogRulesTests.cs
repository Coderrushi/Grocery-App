using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Media;
using GroceryStore.Api.Modules.Catalog;

namespace GroceryStore.Tests.Unit.Catalog;

public sealed class CatalogRulesTests
{
    [Theory]
    [InlineData("Fruits & Vegetables", "fruits-vegetables")]
    [InlineData("  Atta, Rice & Dals ", "atta-rice-dals")]
    [InlineData("Café Crème", "cafe-creme")]
    [InlineData("Haldiram's Bhujia 400g", "haldiram-s-bhujia-400g")]
    [InlineData("दूध", "")] // non-Latin names produce no slug; callers fall back to a default
    public void Slugs_are_lowercase_ascii_with_single_hyphens(string input, string expected) =>
        Assert.Equal(expected, Slugs.From(input));

    [Theory]
    [InlineData(52, 56, 7)] // 7.14% is shown as 7%, never rounded up
    [InlineData(90, 100, 10)]
    [InlineData(28, 28, 0)]
    [InlineData(99.99, 100, 0)]
    public void Discount_percent_rounds_down(decimal price, decimal mrp, int expected) =>
        Assert.Equal(expected, ProductProjections.DiscountPercent(price, mrp));

    [Theory]
    [InlineData(0, 5, StockStatus.OutOfStock)]
    [InlineData(-1, 5, StockStatus.OutOfStock)]
    [InlineData(5, 5, StockStatus.LowStock)]
    [InlineData(6, 5, StockStatus.InStock)]
    [InlineData(1, 0, StockStatus.InStock)]
    public void Stock_status_follows_the_low_stock_threshold(int stock, int threshold, StockStatus expected) =>
        Assert.Equal(expected, ProductProjections.StatusOf(stock, threshold));

    [Fact]
    public void Images_are_recognised_by_their_bytes_not_their_names()
    {
        Assert.Equal(".jpg", ImageFile.DetectExtension([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal(".png", ImageFile.DetectExtension([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
        Assert.Equal(".webp", ImageFile.DetectExtension("RIFF\0\0\0\0WEBP"u8));
        Assert.Null(ImageFile.DetectExtension("<svg xmlns="u8)); // SVG can carry scripts: not accepted
        Assert.Null(ImageFile.DetectExtension("GIF89a"u8));
        Assert.Null(ImageFile.DetectExtension([]));
    }

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("  amul   milk ", new[] { "amul", "milk" })]
    [InlineData("a b c d e f g", new[] { "a", "b", "c", "d", "e" })] // capped at 5 terms
    [InlineData("rice - dal", new[] { "rice", "dal" })] // lone punctuation ignored
    public void Search_text_is_split_into_terms(string? input, string[] expected) =>
        Assert.Equal(expected, CatalogQueryService.SearchTerms(input));
}
