using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for EnhancedCacheService
/// **Feature: code-refactor, Property 3: Cache Size Limit**
/// **Validates: Requirements 4.3**
/// </summary>
public class CacheServicePropertyTests
{
    /// <summary>
    /// Property 3: Cache Size Limit
    /// For any cache with max size N, the cache size should never exceed N items.
    /// This property tests that the EnhancedCacheService properly enforces size limits
    /// on both tag and group caches through the underlying LRU cache.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CacheService_WhenAddingManyItems_ShouldNeverExceedMaxSize()
    {
        return Prop.ForAll(
            CacheSizeAndTagsGenerator(),
            data =>
            {
                var (tagCacheSize, groupCacheSize, tags, groups) = data;
                
                // Create lists to hold the data
                var tagList = new List<NoteTag>(tags);
                var groupList = new List<NoteGroup>(groups);
                
                // Create cache service with specified sizes
                using var cacheService = new EnhancedCacheService(
                    tagProvider: () => tagList,
                    groupProvider: () => groupList,
                    tagCacheSize: tagCacheSize,
                    groupCacheSize: groupCacheSize);
                
                // Access all tags to populate the cache
                foreach (var tag in tags)
                {
                    cacheService.GetTag(tag.Id);
                }
                
                // Access all groups to populate the cache
                foreach (var group in groups)
                {
                    cacheService.GetGroup(group.Id);
                }
                
                // Get statistics
                var stats = cacheService.GetStatistics();
                
                // Verify tag cache never exceeds max size
                if (stats.TagCacheSize > tagCacheSize)
                {
                    return false.ToProperty()
                        .Label($"Tag cache size {stats.TagCacheSize} exceeded max size {tagCacheSize}");
                }
                
                // Verify group cache never exceeds max size
                if (stats.GroupCacheSize > groupCacheSize)
                {
                    return false.ToProperty()
                        .Label($"Group cache size {stats.GroupCacheSize} exceeded max size {groupCacheSize}");
                }
                
                // Verify the cache sizes are correct
                var expectedTagCacheSize = Math.Min(tags.Length, tagCacheSize);
                var expectedGroupCacheSize = Math.Min(groups.Length, groupCacheSize);
                
                if (stats.TagCacheSize != expectedTagCacheSize)
                {
                    return false.ToProperty()
                        .Label($"Tag cache size {stats.TagCacheSize} should be {expectedTagCacheSize}");
                }
                
                if (stats.GroupCacheSize != expectedGroupCacheSize)
                {
                    return false.ToProperty()
                        .Label($"Group cache size {stats.GroupCacheSize} should be {expectedGroupCacheSize}");
                }
                
                return true.ToProperty();
            });
    }
    
    /// <summary>
    /// Generator for cache sizes and test data.
    /// Generates cache sizes between 1-50 and arrays of tags/groups.
    /// </summary>
    private static Arbitrary<(int tagCacheSize, int groupCacheSize, NoteTag[] tags, NoteGroup[] groups)> 
        CacheSizeAndTagsGenerator()
    {
        var gen = from tagCacheSize in Gen.Choose(1, 50)
                  from groupCacheSize in Gen.Choose(1, 50)
                  from tagCount in Gen.Choose(0, tagCacheSize + 20) // Generate more than cache size
                  from groupCount in Gen.Choose(0, groupCacheSize + 20)
                  from tags in Gen.ArrayOf(tagCount, GenerateTag())
                  from groups in Gen.ArrayOf(groupCount, GenerateGroup())
                  select (tagCacheSize, groupCacheSize, tags, groups);
        
        return Arb.From(gen);
    }
    
    /// <summary>
    /// Generates a random NoteTag with unique ID.
    /// </summary>
    private static Gen<NoteTag> GenerateTag()
    {
        return from name in Arb.Generate<NonEmptyString>()
               from color in Gen.Elements("#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF00FF", "#00FFFF")
               select new NoteTag
               {
                   Id = Guid.NewGuid(),
                   Name = name.Get,
                   Color = color
               };
    }
    
    /// <summary>
    /// Generates a random NoteGroup with unique ID.
    /// </summary>
    private static Gen<NoteGroup> GenerateGroup()
    {
        return from name in Arb.Generate<NonEmptyString>()
               select new NoteGroup
               {
                   Id = Guid.NewGuid(),
                   Name = name.Get
               };
    }
}
