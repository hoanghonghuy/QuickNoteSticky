using System.IO;
using System.Text.RegularExpressions;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for Markdown operations
/// **Feature: devsticky-v2, Properties 11-13: Markdown Operations**
/// **Validates: Requirements 4.4, 4.7, 4.8**
/// </summary>
public partial class MarkdownPropertyTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly MarkdownService _service;

    public MarkdownPropertyTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DevStickyMarkdownTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _service = new MarkdownService(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    /// <summary>
    /// Property 11: Markdown rendering preserves structure
    /// For any valid markdown input, rendering to HTML should produce valid HTML 
    /// that preserves the document structure (headers, lists, code blocks).
    /// **Feature: devsticky-v2, Property 11: Markdown rendering preserves structure**
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Markdown_Rendering_ShouldPreserveStructure()
    {
        return Prop.ForAll(MarkdownContentGenerator(), testCase =>
        {
            var html = _service.RenderToHtml(testCase.Markdown);

            // Verify HTML is not empty for non-empty input
            if (string.IsNullOrEmpty(testCase.Markdown))
                return string.IsNullOrEmpty(html);

            // Verify HTML contains expected elements based on markdown structure
            var hasExpectedStructure = true;

            // Check headers are preserved
            if (testCase.ExpectedHeaders > 0)
            {
                var headerMatches = HeaderTagRegex().Matches(html);
                hasExpectedStructure &= headerMatches.Count >= testCase.ExpectedHeaders;
            }

            // Check lists are preserved
            if (testCase.ExpectedListItems > 0)
            {
                var listItemMatches = ListItemTagRegex().Matches(html);
                hasExpectedStructure &= listItemMatches.Count >= testCase.ExpectedListItems;
            }

            // Check code blocks are preserved
            if (testCase.ExpectedCodeBlocks > 0)
            {
                var codeMatches = CodeTagRegex().Matches(html);
                hasExpectedStructure &= codeMatches.Count >= testCase.ExpectedCodeBlocks;
            }

            // Verify it's wrapped in HTML document structure
            var hasDocumentStructure = html.Contains("<!DOCTYPE html>") &&
                                       html.Contains("<html>") &&
                                       html.Contains("<body");

            return hasExpectedStructure && hasDocumentStructure;
        });
    }


    /// <summary>
    /// Property 12: Note link parsing consistency
    /// For any note link in format [[note-id|display-text]], parsing should extract 
    /// the correct note ID and display text.
    /// **Feature: devsticky-v2, Property 12: Note link parsing consistency**
    /// **Validates: Requirements 4.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoteLink_Parsing_ShouldExtractCorrectly()
    {
        return Prop.ForAll(NoteLinkContentGenerator(), testCase =>
        {
            var links = _service.ExtractNoteLinks(testCase.Content);

            // Verify correct number of links extracted
            if (links.Count != testCase.ExpectedLinks.Count)
                return false;

            // Verify each link has correct data
            for (int i = 0; i < testCase.ExpectedLinks.Count; i++)
            {
                var expected = testCase.ExpectedLinks[i];
                var actual = links.FirstOrDefault(l => l.TargetNoteId == expected.TargetNoteId);

                if (actual == null)
                    return false;

                // Verify display text
                if (actual.DisplayText != expected.DisplayText)
                    return false;

                // Verify position is valid
                if (actual.StartPosition < 0 || actual.Length <= 0)
                    return false;

                // Verify the link text at the position matches
                var extractedText = testCase.Content.Substring(actual.StartPosition, actual.Length);
                if (!extractedText.StartsWith("[[") || !extractedText.EndsWith("]]"))
                    return false;
            }

            return true;
        });
    }

    /// <summary>
    /// Property 13: Relative path resolution
    /// For any relative image path in markdown, the resolved path should be 
    /// within the application data directory.
    /// **Feature: devsticky-v2, Property 13: Relative path resolution**
    /// **Validates: Requirements 4.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ImagePath_Resolution_ShouldBeWithinBasePath()
    {
        return Prop.ForAll(RelativePathGenerator(), relativePath =>
        {
            var resolved = _service.ResolveImagePath(relativePath, _testDirectory);

            // Empty paths should return empty
            if (string.IsNullOrEmpty(relativePath))
                return string.IsNullOrEmpty(resolved);

            // Path traversal attempts should return empty (security)
            if (relativePath.Contains(".."))
            {
                // If the resolved path would escape the base directory, it should be empty
                var wouldEscape = !Path.GetFullPath(Path.Combine(_testDirectory, relativePath))
                    .StartsWith(_testDirectory, StringComparison.OrdinalIgnoreCase);
                
                if (wouldEscape)
                    return string.IsNullOrEmpty(resolved);
            }

            // Valid paths should be within the base directory
            if (!string.IsNullOrEmpty(resolved))
            {
                return resolved.StartsWith(_testDirectory, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        });
    }


    #region Regex Patterns

    [GeneratedRegex(@"<h[1-6][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderTagRegex();

    [GeneratedRegex(@"<li[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemTagRegex();

    [GeneratedRegex(@"<code[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex CodeTagRegex();

    #endregion

    #region Generators

    private static Arbitrary<MarkdownTestCase> MarkdownContentGenerator()
    {
        var gen = from headerCount in Gen.Choose(0, 3)
                  from listItemCount in Gen.Choose(0, 4)
                  from codeBlockCount in Gen.Choose(0, 2)
                  from headerLevel in Gen.Choose(1, 3)
                  from headerText in Gen.Elements("Title", "Section", "Subsection", "Overview")
                  from listItems in Gen.ListOf(listItemCount, Gen.Elements("Item one", "Item two", "Item three", "Task"))
                  from codeContent in Gen.Elements("console.log('test');", "var x = 1;", "print('hello')")
                  let headers = string.Join("\n\n", Enumerable.Range(0, headerCount)
                      .Select(i => $"{new string('#', headerLevel)} {headerText} {i + 1}"))
                  let list = listItemCount > 0 
                      ? string.Join("\n", listItems.Select(item => $"- {item}"))
                      : ""
                  let codeBlocks = string.Join("\n\n", Enumerable.Range(0, codeBlockCount)
                      .Select(_ => $"```\n{codeContent}\n```"))
                  let markdown = string.Join("\n\n", new[] { headers, list, codeBlocks }
                      .Where(s => !string.IsNullOrEmpty(s)))
                  select new MarkdownTestCase
                  {
                      Markdown = markdown,
                      ExpectedHeaders = headerCount,
                      ExpectedListItems = listItemCount,
                      ExpectedCodeBlocks = codeBlockCount
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<NoteLinkTestCase> NoteLinkContentGenerator()
    {
        var gen = from linkCount in Gen.Choose(1, 3)
                  from guids in Gen.ListOf(linkCount, Gen.Constant(0).Select(_ => Guid.NewGuid()))
                  from displayTexts in Gen.ListOf(linkCount, Gen.Elements("Note 1", "My Note", "Reference", "See Also"))
                  from useDisplayText in Gen.ListOf(linkCount, Gen.Elements(true, false))
                  let links = guids.Zip(displayTexts, useDisplayText)
                      .Select(t => new { Guid = t.First, DisplayText = t.Second, UseDisplay = t.Third })
                      .ToList()
                  let content = string.Join(" Some text between links. ", 
                      links.Select(l => l.UseDisplay 
                          ? $"[[{l.Guid}|{l.DisplayText}]]" 
                          : $"[[{l.Guid}]]"))
                  let expectedLinks = links.Select(l => new NoteLink
                  {
                      TargetNoteId = l.Guid,
                      DisplayText = l.UseDisplay ? l.DisplayText : l.Guid.ToString()
                  }).ToList()
                  select new NoteLinkTestCase
                  {
                      Content = content,
                      ExpectedLinks = expectedLinks
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<string> RelativePathGenerator()
    {
        var gen = Gen.OneOf(
            // Valid relative paths
            Gen.Elements(
                "images/photo.png",
                "assets/icon.svg",
                "docs/readme.md",
                "file.txt",
                "subfolder/nested/image.jpg"
            ),
            // Empty path
            Gen.Constant(""),
            // Path traversal attempts (should be blocked)
            Gen.Elements(
                "../outside.txt",
                "../../secret.txt",
                "folder/../../../escape.txt"
            )
        );

        return Arb.From(gen);
    }

    #endregion
}

#region Test Case Classes

public class MarkdownTestCase
{
    public string Markdown { get; set; } = string.Empty;
    public int ExpectedHeaders { get; set; }
    public int ExpectedListItems { get; set; }
    public int ExpectedCodeBlocks { get; set; }
}

public class NoteLinkTestCase
{
    public string Content { get; set; } = string.Empty;
    public List<NoteLink> ExpectedLinks { get; set; } = new();
}

#endregion
