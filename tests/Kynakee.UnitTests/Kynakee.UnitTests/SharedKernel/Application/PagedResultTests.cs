using FluentAssertions;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Application
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class PagedResultTests
    {
        [Fact]
        public void CreateShouldReturnPagedResultWithExpectedMetadata()
        {
            var items = new[] { "Project A", "Project B" };

            var result = PagedResultFactory.Create(
                items,
                page: 2,
                pageSize: 10,
                totalCount: 25);

            result.Items.Should().BeEquivalentTo(items);
            result.Page.Should().Be(2);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(25);
        }

        private static readonly string[] items = new[] { "Project A" };
        private static readonly string[] itemsArray = new[] { "Project A" };
        private static readonly string[] itemsArrayB = new[] { "Project B" };
        private static readonly string[] itemsArrayC = new[] { "Project C" };

        [Fact]
        public void CreateShouldCalculateTotalPagesByRoundingUp()
        {
            var result = PagedResultFactory.Create(
                items,
                page: 1,
                pageSize: 10,
                totalCount: 25);

            result.TotalPages.Should().Be(3);
        }

        [Fact]
        public void CreateShouldSetNavigationFlagsForFirstPage()
        {
            var result = PagedResultFactory.Create(
                itemsArray,
                page: 1,
                pageSize: 10,
                totalCount: 25);

            result.HasPreviousPage.Should().BeFalse();
            result.HasNextPage.Should().BeTrue();
        }

        [Fact]
        public void CreateShouldSetNavigationFlagsForMiddlePage()
        {
            var result = PagedResultFactory.Create(
                itemsArrayB,
                page: 2,
                pageSize: 10,
                totalCount: 25);

            result.HasPreviousPage.Should().BeTrue();
            result.HasNextPage.Should().BeTrue();
        }

        [Fact]
        public void CreateShouldSetNavigationFlagsForLastPage()
        {
            var result = PagedResultFactory.Create(
                itemsArrayC,
                page: 3,
                pageSize: 10,
                totalCount: 25);

            result.HasPreviousPage.Should().BeTrue();
            result.HasNextPage.Should().BeFalse();
        }

        [Fact]
        public void CreateShouldSupportEmptyResult()
        {
            var result = PagedResultFactory.Create(
                Array.Empty<string>(),
                page: 1,
                pageSize: 10,
                totalCount: 0);

            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
            result.TotalPages.Should().Be(0);
            result.HasPreviousPage.Should().BeFalse();
            result.HasNextPage.Should().BeFalse();
        }

        [Fact]
        public void EmptyShouldUseDefaultPageAndPageSize()
        {
            var result = PagedResultFactory.Empty<string>();

            result.Items.Should().BeEmpty();
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(20);
            result.TotalCount.Should().Be(0);
            result.TotalPages.Should().Be(0);
            result.HasPreviousPage.Should().BeFalse();
            result.HasNextPage.Should().BeFalse();
        }

        [Fact]
        public void EmptyShouldUseProvidedPageAndPageSize()
        {
            var result = PagedResultFactory.Empty<string>(
                page: 3,
                pageSize: 50);

            result.Items.Should().BeEmpty();
            result.Page.Should().Be(3);
            result.PageSize.Should().Be(50);
            result.TotalCount.Should().Be(0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void CreateShouldThrowWhenPageIsLessThanOne(int page)
        {
            var action = () => PagedResultFactory.Create(
                Array.Empty<string>(),
                page,
                pageSize: 10,
                totalCount: 0);

            action.Should()
                .Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void CreateShouldThrowWhenPageSizeIsLessThanOne(int pageSize)
        {
            var action = () => PagedResultFactory.Create(
                Array.Empty<string>(),
                page: 1,
                pageSize,
                totalCount: 0);

            action.Should()
                .Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-10)]
        public void CreateShouldThrowWhenTotalCountIsNegative(int totalCount)
        {
            var action = () => PagedResultFactory.Create(
                Array.Empty<string>(),
                page: 1,
                pageSize: 10,
                totalCount);

            action.Should()
                .Throw<ArgumentOutOfRangeException>();
        }

    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}
