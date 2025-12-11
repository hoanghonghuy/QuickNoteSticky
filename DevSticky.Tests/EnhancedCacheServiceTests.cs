using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for EnhancedCacheService
/// </summary>
public class EnhancedCacheServiceTests : IDisposable
{
    private readonly List<NoteTag> _tags;
    private readonly List<NoteGroup> _groups;
    private readonly EnhancedCacheService _cacheService;

    public EnhancedCacheServiceTests()
    {
        _tags = new List<NoteTag>();
        _groups = new List<NoteGroup>();
        _cacheService = new EnhancedCacheService(
            tagProvider: () => _tags,
            groupProvider: () => _groups,
            tagCacheSize: 10,
            groupCacheSize: 5);
    }

    [Fact]
    public void GetTag_WhenTagExists_ReturnsTag()
    {
        // Arrange
        var tag = new NoteTag { Id = Guid.NewGuid(), Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);

        // Act
        var result = _cacheService.GetTag(tag.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tag.Id, result.Id);
        Assert.Equal(tag.Name, result.Name);
    }

    [Fact]
    public void GetTag_WhenTagDoesNotExist_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = _cacheService.GetTag(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetTag_SecondCall_IncrementsHitCount()
    {
        // Arrange
        var tag = new NoteTag { Id = Guid.NewGuid(), Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);

        // Act
        _cacheService.GetTag(tag.Id); // First call - miss
        _cacheService.GetTag(tag.Id); // Second call - hit

        // Assert
        var stats = _cacheService.GetStatistics();
        Assert.Equal(1, stats.TotalHits);
        Assert.Equal(1, stats.TotalMisses);
        Assert.Equal(2, stats.TotalRequests);
    }

    [Fact]
    public void GetGroup_WhenGroupExists_ReturnsGroup()
    {
        // Arrange
        var group = new NoteGroup { Id = Guid.NewGuid(), Name = "Test Group" };
        _groups.Add(group);

        // Act
        var result = _cacheService.GetGroup(group.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(group.Id, result.Id);
        Assert.Equal(group.Name, result.Name);
    }

    [Fact]
    public void GetTags_WithMultipleIds_ReturnsMatchingTags()
    {
        // Arrange
        var tag1 = new NoteTag { Id = Guid.NewGuid(), Name = "Tag 1", Color = "#FF0000" };
        var tag2 = new NoteTag { Id = Guid.NewGuid(), Name = "Tag 2", Color = "#00FF00" };
        var tag3 = new NoteTag { Id = Guid.NewGuid(), Name = "Tag 3", Color = "#0000FF" };
        _tags.Add(tag1);
        _tags.Add(tag2);
        _tags.Add(tag3);

        // Act
        var result = _cacheService.GetTags(new[] { tag1.Id, tag2.Id });

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == tag1.Id);
        Assert.Contains(result, t => t.Id == tag2.Id);
    }

    [Fact]
    public void InvalidateTagCache_ClearsCache()
    {
        // Arrange
        var tag = new NoteTag { Id = Guid.NewGuid(), Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);
        _cacheService.GetTag(tag.Id); // Cache the tag

        // Act
        _cacheService.InvalidateTagCache();
        var stats = _cacheService.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TagCacheSize);
        Assert.Equal(1, stats.TagCacheInvalidations);
    }

    [Fact]
    public void InvalidateGroupCache_ClearsCache()
    {
        // Arrange
        var group = new NoteGroup { Id = Guid.NewGuid(), Name = "Test Group" };
        _groups.Add(group);
        _cacheService.GetGroup(group.Id); // Cache the group

        // Act
        _cacheService.InvalidateGroupCache();
        var stats = _cacheService.GetStatistics();

        // Assert
        Assert.Equal(0, stats.GroupCacheSize);
        Assert.Equal(1, stats.GroupCacheInvalidations);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectValues()
    {
        // Arrange
        var tag = new NoteTag { Id = Guid.NewGuid(), Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);

        // Act
        _cacheService.GetTag(tag.Id); // Miss
        _cacheService.GetTag(tag.Id); // Hit
        _cacheService.GetTag(tag.Id); // Hit
        var stats = _cacheService.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalHits);
        Assert.Equal(1, stats.TotalMisses);
        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(1, stats.TagCacheSize);
        Assert.Equal(10, stats.TagCacheMaxSize);
        Assert.Equal(5, stats.GroupCacheMaxSize);
    }

    [Fact]
    public void GetStatistics_CalculatesHitRateCorrectly()
    {
        // Arrange
        var tag = new NoteTag { Id = Guid.NewGuid(), Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);

        // Act
        _cacheService.GetTag(tag.Id); // Miss
        _cacheService.GetTag(tag.Id); // Hit
        _cacheService.GetTag(tag.Id); // Hit
        _cacheService.GetTag(tag.Id); // Hit
        var stats = _cacheService.GetStatistics();

        // Assert
        Assert.Equal(75.0, stats.HitRate); // 3 hits out of 4 requests = 75%
    }

    public void Dispose()
    {
        _cacheService?.Dispose();
    }
}
