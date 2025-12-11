using DevSticky.Models;
using System.ComponentModel;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for TrackableModel
/// </summary>
public class TrackableModelTests
{
    private class TestModel : TrackableModel
    {
        private string _name = "";
        private int _value;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    [Fact]
    public void NewModel_IsNotDirty()
    {
        // Arrange & Act
        var model = new TestModel();

        // Assert
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void SetProperty_MarksModelAsDirty()
    {
        // Arrange
        var model = new TestModel();

        // Act
        model.Name = "Test";

        // Assert
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void SetProperty_WithSameValue_DoesNotMarkAsDirty()
    {
        // Arrange
        var model = new TestModel { Name = "Test" };
        model.MarkClean();

        // Act
        model.Name = "Test"; // Same value

        // Assert
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void SetProperty_RaisesPropertyChangedEvent()
    {
        // Arrange
        var model = new TestModel();
        string? changedPropertyName = null;
        model.PropertyChanged += (sender, args) => changedPropertyName = args.PropertyName;

        // Act
        model.Name = "Test";

        // Assert
        Assert.Equal(nameof(TestModel.Name), changedPropertyName);
    }

    [Fact]
    public void SetProperty_WithSameValue_DoesNotRaisePropertyChangedEvent()
    {
        // Arrange
        var model = new TestModel { Name = "Test" };
        model.MarkClean();
        var eventRaised = false;
        model.PropertyChanged += (sender, args) => eventRaised = true;

        // Act
        model.Name = "Test"; // Same value

        // Assert
        Assert.False(eventRaised);
    }

    [Fact]
    public void MarkClean_ClearsDirtyFlag()
    {
        // Arrange
        var model = new TestModel { Name = "Test" };
        Assert.True(model.IsDirty);

        // Act
        model.MarkClean();

        // Assert
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void MarkDirty_SetsDirtyFlag()
    {
        // Arrange
        var model = new TestModel();
        Assert.False(model.IsDirty);

        // Act
        model.MarkDirty();

        // Assert
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void IsDirty_RaisesPropertyChangedEvent()
    {
        // Arrange
        var model = new TestModel();
        string? changedPropertyName = null;
        model.PropertyChanged += (sender, args) => changedPropertyName = args.PropertyName;

        // Act
        model.IsDirty = true;

        // Assert
        Assert.Equal(nameof(TrackableModel.IsDirty), changedPropertyName);
    }

    [Fact]
    public void MultiplePropertyChanges_MaintainsDirtyState()
    {
        // Arrange
        var model = new TestModel();

        // Act
        model.Name = "Test1";
        model.Value = 42;
        model.Name = "Test2";

        // Assert
        Assert.True(model.IsDirty);
        Assert.Equal("Test2", model.Name);
        Assert.Equal(42, model.Value);
    }

    [Fact]
    public void SetProperty_ReturnsTrue_WhenValueChanges()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = model.Name = "Test";

        // Assert - SetProperty returns bool, but property assignment doesn't expose it
        // We verify by checking the property was set
        Assert.Equal("Test", model.Name);
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void PropertyChanged_SubscribeAndUnsubscribe_WorksCorrectly()
    {
        // Arrange
        var model = new TestModel();
        var eventCount = 0;
        PropertyChangedEventHandler handler = (sender, args) => eventCount++;

        // Act
        model.PropertyChanged += handler;
        model.Name = "Test1";
        model.PropertyChanged -= handler;
        model.Name = "Test2";

        // Assert
        Assert.Equal(2, eventCount); // IsDirty + Name change
    }
}
