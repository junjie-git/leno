using FluentValidation;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Services;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.Product.Application.Tests;

/// <summary>
/// 分类树 keyword 过滤单元测试。
/// 验证 <see cref="CategoryAppService.GetTreeAsync(string?, CancellationToken)"/> 在不同 keyword 场景下的过滤行为：
/// 1. keyword 为空返回完整分类树；
/// 2. keyword 匹配叶子节点时返回叶子节点及其所有祖先节点（构建父链）；
/// 3. keyword 无匹配时返回空树。
/// </summary>
public class CategoryTreeKeywordFilterTests
{
    private readonly Mock<ICategoryRepository> _categoryRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IValidator<CreateCategoryDto>> _createValidatorMock = new();
    private readonly Mock<IValidator<UpdateCategoryDto>> _updateValidatorMock = new();
    private readonly CategoryAppService _sut;

    public CategoryTreeKeywordFilterTests()
    {
        _sut = new CategoryAppService(
            _categoryRepoMock.Object,
            _uowMock.Object,
            _createValidatorMock.Object,
            _updateValidatorMock.Object);
    }

    [Fact]
    public async Task GetTreeAsync_EmptyKeyword_ShouldReturnFullTree()
    {
        var (root1, child1, grandchild1, root2, child2) = BuildSampleTree();
        var all = new List<Category> { root1, child1, grandchild1, root2, child2 };
        _categoryRepoMock.Setup(r => r.GetTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(all);

        var result = await _sut.GetTreeAsync(keyword: null);

        result.Should().HaveCount(2);
        var firstRoot = result.First(r => r.Id == root1.Id);
        firstRoot.Children.Should().HaveCount(1);
        firstRoot.Children.First().Children.Should().HaveCount(1);
        var secondRoot = result.First(r => r.Id == root2.Id);
        secondRoot.Children.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTreeAsync_WhitespaceKeyword_ShouldReturnFullTree()
    {
        var (root1, child1, grandchild1, root2, child2) = BuildSampleTree();
        var all = new List<Category> { root1, child1, grandchild1, root2, child2 };
        _categoryRepoMock.Setup(r => r.GetTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(all);

        var result = await _sut.GetTreeAsync(keyword: "   ");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTreeAsync_KeywordMatchesLeaf_ShouldReturnLeafAndAncestors()
    {
        var (root1, child1, grandchild1, root2, child2) = BuildSampleTree();
        var all = new List<Category> { root1, child1, grandchild1, root2, child2 };
        _categoryRepoMock.Setup(r => r.GetTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(all);

        // grandchild1 名称包含 "iPhone"
        var result = await _sut.GetTreeAsync(keyword: "iPhone");

        // 应返回 root1 -> child1 -> grandchild1 的父链，不含 root2 与 child2
        result.Should().HaveCount(1);
        var onlyRoot = result.Single();
        onlyRoot.Id.Should().Be(root1.Id);
        onlyRoot.Children.Should().HaveCount(1);
        var onlyChild = onlyRoot.Children.Single();
        onlyChild.Id.Should().Be(child1.Id);
        onlyChild.Children.Should().HaveCount(1);
        onlyChild.Children.Single().Id.Should().Be(grandchild1.Id);
    }

    [Fact]
    public async Task GetTreeAsync_KeywordMatchesMiddleNode_ShouldReturnMiddleAndAncestorsAndExcludeUnmatchedSiblings()
    {
        var (root1, child1, grandchild1, root2, child2) = BuildSampleTree();
        var all = new List<Category> { root1, child1, grandchild1, root2, child2 };
        _categoryRepoMock.Setup(r => r.GetTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(all);

        // child1 名称包含 "手机"
        var result = await _sut.GetTreeAsync(keyword: "手机");

        // 应返回 root1 -> child1，不含 grandchild1（既非匹配也非祖先）
        result.Should().HaveCount(1);
        var onlyRoot = result.Single();
        onlyRoot.Id.Should().Be(root1.Id);
        onlyRoot.Children.Should().HaveCount(1);
        onlyRoot.Children.Single().Id.Should().Be(child1.Id);
        onlyRoot.Children.Single().Children.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTreeAsync_KeywordMatchesRoot_ShouldReturnRootOnly()
    {
        var (root1, child1, grandchild1, root2, child2) = BuildSampleTree();
        var all = new List<Category> { root1, child1, grandchild1, root2, child2 };
        _categoryRepoMock.Setup(r => r.GetTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(all);

        // root2 名称包含 "家居"
        var result = await _sut.GetTreeAsync(keyword: "家居");

        // 仅返回 root2，不含其子节点 child2（child2 既非匹配也非祖先）
        result.Should().HaveCount(1);
        var onlyRoot = result.Single();
        onlyRoot.Id.Should().Be(root2.Id);
        onlyRoot.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTreeAsync_KeywordCaseInsensitive_ShouldMatch()
    {
        var (root1, child1, grandchild1, root2, child2) = BuildSampleTree();
        var all = new List<Category> { root1, child1, grandchild1, root2, child2 };
        _categoryRepoMock.Setup(r => r.GetTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(all);

        var result = await _sut.GetTreeAsync(keyword: "IPHONE");

        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(root1.Id);
    }

    [Fact]
    public async Task GetTreeAsync_NoMatch_ShouldReturnEmptyTree()
    {
        var (root1, child1, grandchild1, root2, child2) = BuildSampleTree();
        var all = new List<Category> { root1, child1, grandchild1, root2, child2 };
        _categoryRepoMock.Setup(r => r.GetTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(all);

        var result = await _sut.GetTreeAsync(keyword: "不存在的分类");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTreeAsync_KeywordMatchesMultipleBranches_ShouldReturnAllMatchedBranches()
    {
        var (root1, child1, grandchild1, root2, child2) = BuildSampleTree();
        var all = new List<Category> { root1, child1, grandchild1, root2, child2 };
        _categoryRepoMock.Setup(r => r.GetTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(all);

        // "子" 同时匹配 child1（"手机子类" 不含"子"，但 child2 名称含"家具"）
        // 实际我们使用 "家" 来匹配 root2（"家居"）和 child2（"家具"）
        var result = await _sut.GetTreeAsync(keyword: "家");

        // 应返回 root2 -> child2
        result.Should().HaveCount(1);
        var onlyRoot = result.Single();
        onlyRoot.Id.Should().Be(root2.Id);
        onlyRoot.Children.Should().HaveCount(1);
        onlyRoot.Children.Single().Id.Should().Be(child2.Id);
    }

    private static (Category root1, Category child1, Category grandchild1, Category root2, Category child2) BuildSampleTree()
    {
        // 树结构：
        // 电子产品 (root1)
        //   └── 手机 (child1)
        //        └── iPhone (grandchild1)
        // 家居 (root2)
        //   └── 家具 (child2)
        var root1 = Category.Create(Guid.NewGuid(), "电子产品", parentId: null, parentLevel: null, sortOrder: 1);
        var child1 = Category.Create(Guid.NewGuid(), "手机", parentId: root1.Id, parentLevel: root1.Level, sortOrder: 1);
        var grandchild1 = Category.Create(Guid.NewGuid(), "iPhone", parentId: child1.Id, parentLevel: child1.Level, sortOrder: 1);
        var root2 = Category.Create(Guid.NewGuid(), "家居", parentId: null, parentLevel: null, sortOrder: 2);
        var child2 = Category.Create(Guid.NewGuid(), "家具", parentId: root2.Id, parentLevel: root2.Level, sortOrder: 1);

        return (root1, child1, grandchild1, root2, child2);
    }
}
