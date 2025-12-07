using System.Text.Json;
using System.Text.Json.Serialization;
using DevSticky.Models;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for Note serialization round-trip
/// **Feature: devsticky, Property 1: Note Serialization Round-Trip**
/// **Validates: Requirements 1.5, 4.4, 4.5, 11.3, 11.4, 11.5**
/// </summary>
public class SerializationPropertyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Property 1: Note Serialization Round-Trip
    /// For any valid Note object with all properties set, serializing to JSON 
    /// and then deserializing back SHALL produce a Note object with identical property values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Note_SerializeDeserialize_ShouldPreserveAllProperties()
    {
        return Prop.ForAll(NoteGenerator(), note =>
        {
            var json = JsonSerializer.Serialize(note, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<Note>(json, JsonOptions);

            return deserialized != null &&
                   deserialized.Id == note.Id &&
                   deserialized.Content == note.Content &&
                   deserialized.Language == note.Language &&
                   deserialized.IsPinned == note.IsPinned &&
                   Math.Abs(deserialized.Opacity - note.Opacity) < 0.001 &&
                   deserialized.WindowRect.Top == note.WindowRect.Top &&
                   deserialized.WindowRect.Left == note.WindowRect.Left &&
                   deserialized.WindowRect.Width == note.WindowRect.Width &&
                   deserialized.WindowRect.Height == note.WindowRect.Height;
        });
    }

    [Property(MaxTest = 100)]
    public Property AppData_SerializeDeserialize_ShouldPreserveNoteCount()
    {
        return Prop.ForAll(AppDataGenerator(), appData =>
        {
            var json = JsonSerializer.Serialize(appData, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<AppData>(json, JsonOptions);

            return deserialized != null &&
                   deserialized.Notes.Count == appData.Notes.Count;
        });
    }


    private static Arbitrary<Note> NoteGenerator()
    {
        var gen = from id in Gen.Constant(Guid.NewGuid())
                  from content in Arb.Generate<NonEmptyString>()
                  from language in Gen.Elements("PlainText", "CSharp", "JavaScript", "Json")
                  from isPinned in Arb.Generate<bool>()
                  from opacity in Gen.Choose(20, 100).Select(x => x / 100.0)
                  from top in Gen.Choose(0, 1000).Select(x => (double)x)
                  from left in Gen.Choose(0, 1000).Select(x => (double)x)
                  from width in Gen.Choose(200, 800).Select(x => (double)x)
                  from height in Gen.Choose(150, 600).Select(x => (double)x)
                  select new Note
                  {
                      Id = id,
                      Content = content.Get,
                      Language = language,
                      IsPinned = isPinned,
                      Opacity = opacity,
                      WindowRect = new WindowRect
                      {
                          Top = top,
                          Left = left,
                          Width = width,
                          Height = height
                      },
                      CreatedDate = DateTime.UtcNow
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<AppData> AppDataGenerator()
    {
        var noteGen = NoteGenerator().Generator;
        var gen = from noteCount in Gen.Choose(0, 5)
                  from notes in Gen.ListOf(noteCount, noteGen)
                  from defaultOpacity in Gen.Choose(20, 100).Select(x => x / 100.0)
                  select new AppData
                  {
                      AppSettings = new AppSettings
                      {
                          DefaultOpacity = defaultOpacity,
                          Theme = "Dark",
                          StartWithWindows = false
                      },
                      Notes = notes.ToList()
                  };

        return Arb.From(gen);
    }
}
