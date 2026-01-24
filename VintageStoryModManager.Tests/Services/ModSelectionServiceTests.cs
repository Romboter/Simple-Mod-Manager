using NUnit.Framework;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Tests.Services;

/// <summary>
/// Example unit tests for ModSelectionService demonstrating the testability
/// of the extracted selection logic.
/// </summary>
[TestFixture]
public class ModSelectionServiceTests
{
    private ModSelectionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new ModSelectionService();
    }

    #region Normal Click Tests

    [Test]
    public void CalculateNewSelection_NormalClick_SelectsSingleItem()
    {
        // Arrange
        var currentSelection = new List<int> { 0, 1, 2 };

        // Act
        var result = _service.CalculateNewSelection(
            clickedIndex: 5,
            currentSelection: currentSelection,
            anchorIndex: 1,
            isShiftPressed: false,
            isCtrlPressed: false,
            viewSize: 10);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.SelectedIndices, Has.Count.EqualTo(1));
            Assert.That(result.SelectedIndices, Contains.Item(5));
            Assert.That(result.NewAnchorIndex, Is.EqualTo(5));
        });
    }

    [Test]
    public void CalculateNewSelection_NormalClick_ClearsPreviousSelection()
    {
        // Arrange
        var currentSelection = new List<int> { 0, 1, 2, 3, 4 };

        // Act
        var result = _service.CalculateNewSelection(
            clickedIndex: 7,
            currentSelection: currentSelection,
            anchorIndex: 2,
            isShiftPressed: false,
            isCtrlPressed: false,
            viewSize: 10);

        // Assert
        Assert.That(result.SelectedIndices, Is.EquivalentTo(new[] { 7 }));
    }

    #endregion

    #region Ctrl+Click Tests

    [Test]
    public void CalculateNewSelection_CtrlClick_AddsToSelection()
    {
        // Arrange
        var currentSelection = new List<int> { 0, 2 };

        // Act
        var result = _service.CalculateNewSelection(
            clickedIndex: 5,
            currentSelection: currentSelection,
            anchorIndex: 2,
            isShiftPressed: false,
            isCtrlPressed: true,
            viewSize: 10);

        // Assert
        Assert.That(result.SelectedIndices, Is.EquivalentTo(new[] { 0, 2, 5 }));
        Assert.That(result.NewAnchorIndex, Is.EqualTo(5));
    }

    [Test]
    public void CalculateNewSelection_CtrlClickOnSelected_RemovesFromSelection()
    {
        // Arrange
        var currentSelection = new List<int> { 0, 2, 5 };

        // Act - Click on already selected item with Ctrl
        var result = _service.CalculateNewSelection(
            clickedIndex: 2,
            currentSelection: currentSelection,
            anchorIndex: 0,
            isShiftPressed: false,
            isCtrlPressed: true,
            viewSize: 10);

        // Assert - Item 2 should be removed
        Assert.That(result.SelectedIndices, Is.EquivalentTo(new[] { 0, 5 }));
        Assert.That(result.NewAnchorIndex, Is.EqualTo(2));
    }

    [Test]
    public void CalculateToggleSelection_OnUnselectedItem_AddsIt()
    {
        // Arrange
        var currentSelection = new List<int> { 1, 3 };

        // Act
        var result = _service.CalculateToggleSelection(5, currentSelection);

        // Assert
        Assert.That(result, Is.EquivalentTo(new[] { 1, 3, 5 }));
    }

    [Test]
    public void CalculateToggleSelection_OnSelectedItem_RemovesIt()
    {
        // Arrange
        var currentSelection = new List<int> { 1, 3, 5 };

        // Act
        var result = _service.CalculateToggleSelection(3, currentSelection);

        // Assert
        Assert.That(result, Is.EquivalentTo(new[] { 1, 5 }));
    }

    #endregion

    #region Shift+Click Tests

    [Test]
    public void CalculateNewSelection_ShiftClick_SelectsRange()
    {
        // Arrange
        var currentSelection = new List<int> { 2 };

        // Act - Shift+Click from anchor 2 to index 5
        var result = _service.CalculateNewSelection(
            clickedIndex: 5,
            currentSelection: currentSelection,
            anchorIndex: 2,
            isShiftPressed: true,
            isCtrlPressed: false,
            viewSize: 10);

        // Assert - Should select 2, 3, 4, 5
        Assert.That(result.SelectedIndices, Is.EquivalentTo(new[] { 2, 3, 4, 5 }));
        Assert.That(result.NewAnchorIndex, Is.EqualTo(2)); // Anchor stays at 2
    }

    [Test]
    public void CalculateNewSelection_ShiftClickBackwards_SelectsRange()
    {
        // Arrange
        var currentSelection = new List<int> { 5 };

        // Act - Shift+Click from anchor 5 to index 2 (backwards)
        var result = _service.CalculateNewSelection(
            clickedIndex: 2,
            currentSelection: currentSelection,
            anchorIndex: 5,
            isShiftPressed: true,
            isCtrlPressed: false,
            viewSize: 10);

        // Assert - Should select 2, 3, 4, 5 (normalized)
        Assert.That(result.SelectedIndices, Is.EquivalentTo(new[] { 2, 3, 4, 5 }));
        Assert.That(result.NewAnchorIndex, Is.EqualTo(5)); // Anchor stays at 5
    }

    [Test]
    public void CalculateNewSelection_ShiftClickWithNoAnchor_SelectsSingleItem()
    {
        // Arrange
        var currentSelection = Array.Empty<int>();

        // Act - Shift+Click with no anchor
        var result = _service.CalculateNewSelection(
            clickedIndex: 3,
            currentSelection: currentSelection,
            anchorIndex: null,
            isShiftPressed: true,
            isCtrlPressed: false,
            viewSize: 10);

        // Assert - Should select just the clicked item and set it as anchor
        Assert.That(result.SelectedIndices, Is.EquivalentTo(new[] { 3 }));
        Assert.That(result.NewAnchorIndex, Is.EqualTo(3));
    }

    [Test]
    public void CalculateNewSelection_CtrlShiftClick_PreservesExistingAndAddsRange()
    {
        // Arrange
        var currentSelection = new List<int> { 0, 1 };

        // Act - Ctrl+Shift+Click from anchor 2 to index 5
        var result = _service.CalculateNewSelection(
            clickedIndex: 5,
            currentSelection: currentSelection,
            anchorIndex: 2,
            isShiftPressed: true,
            isCtrlPressed: true,
            viewSize: 10);

        // Assert - Should preserve 0, 1 and add 2, 3, 4, 5
        Assert.That(result.SelectedIndices, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4, 5 }));
    }

    [Test]
    public void CalculateRangeSelection_WithReverseRange_SelectsCorrectly()
    {
        // Arrange & Act
        var result = _service.CalculateRangeSelection(
            anchorIndex: 5,
            targetIndex: 2,
            currentSelection: Array.Empty<int>(),
            preserveExisting: false);

        // Assert - Should normalize to 2, 3, 4, 5
        Assert.That(result, Is.EquivalentTo(new[] { 2, 3, 4, 5 }));
    }

    [Test]
    public void CalculateRangeSelection_WithSingleItemRange_SelectsSingleItem()
    {
        // Arrange & Act
        var result = _service.CalculateRangeSelection(
            anchorIndex: 3,
            targetIndex: 3,
            currentSelection: Array.Empty<int>(),
            preserveExisting: false);

        // Assert
        Assert.That(result, Is.EquivalentTo(new[] { 3 }));
    }

    [Test]
    public void CalculateRangeSelection_PreserveExisting_CombinesSelections()
    {
        // Arrange
        var currentSelection = new List<int> { 0, 1 };

        // Act
        var result = _service.CalculateRangeSelection(
            anchorIndex: 3,
            targetIndex: 5,
            currentSelection: currentSelection,
            preserveExisting: true);

        // Assert
        Assert.That(result, Is.EquivalentTo(new[] { 0, 1, 3, 4, 5 }));
    }

    #endregion

    #region Select All Tests

    [Test]
    public void CalculateSelectAll_WithValidSize_ReturnsAllIndices()
    {
        // Act
        var result = _service.CalculateSelectAll(5);

        // Assert
        Assert.That(result, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }));
    }

    [Test]
    public void CalculateSelectAll_WithZeroSize_ReturnsEmpty()
    {
        // Act
        var result = _service.CalculateSelectAll(0);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void CalculateSelectAll_WithNegativeSize_ReturnsEmpty()
    {
        // Act
        var result = _service.CalculateSelectAll(-5);

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region Helper Method Tests

    [Test]
    public void ResolveSelection_WithValidIndices_ReturnsCorrectItems()
    {
        // Arrange
        var items = new List<string> { "A", "B", "C", "D", "E" };
        var selectedIndices = new List<int> { 1, 3 };

        // Act
        var result = _service.ResolveSelection(items, selectedIndices);

        // Assert
        Assert.That(result, Is.EquivalentTo(new[] { "B", "D" }));
    }

    [Test]
    public void ResolveSelection_WithOutOfBoundsIndices_IgnoresThem()
    {
        // Arrange
        var items = new List<string> { "A", "B", "C" };
        var selectedIndices = new List<int> { 1, 5, 10 }; // 5 and 10 are out of bounds

        // Act
        var result = _service.ResolveSelection(items, selectedIndices);

        // Assert
        Assert.That(result, Is.EquivalentTo(new[] { "B" }));
    }

    [Test]
    public void FindIndices_WithMatchingItems_ReturnsIndices()
    {
        // Arrange
        var items = new List<string> { "A", "B", "C", "D", "E" };
        var selectedItems = new List<string> { "B", "D" };

        // Act
        var result = _service.FindIndices(items, selectedItems);

        // Assert
        Assert.That(result, Is.EquivalentTo(new[] { 1, 3 }));
    }

    [Test]
    public void FindIndices_WithNonMatchingItems_ReturnsEmpty()
    {
        // Arrange
        var items = new List<string> { "A", "B", "C" };
        var selectedItems = new List<string> { "X", "Y" };

        // Act
        var result = _service.FindIndices(items, selectedItems);

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void CalculateNewSelection_WithInvalidIndex_ThrowsException()
    {
        // Arrange
        var currentSelection = new List<int> { 0 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.CalculateNewSelection(
                clickedIndex: -1,
                currentSelection: currentSelection,
                anchorIndex: 0,
                isShiftPressed: false,
                isCtrlPressed: false,
                viewSize: 10));
    }

    [Test]
    public void CalculateNewSelection_WithIndexBeyondViewSize_ThrowsException()
    {
        // Arrange
        var currentSelection = new List<int> { 0 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.CalculateNewSelection(
                clickedIndex: 15,
                currentSelection: currentSelection,
                anchorIndex: 0,
                isShiftPressed: false,
                isCtrlPressed: false,
                viewSize: 10));
    }

    [Test]
    public void ResolveSelection_WithNullItems_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.ResolveSelection<string>(null!, new List<int> { 0 }));
    }

    [Test]
    public void FindIndices_WithNullSelectedItems_ThrowsException()
    {
        // Arrange
        var items = new List<string> { "A", "B" };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.FindIndices(items, null!));
    }

    #endregion
}
