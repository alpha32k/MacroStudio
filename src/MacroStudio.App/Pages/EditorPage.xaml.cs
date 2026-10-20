using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MacroStudio.App.Pages;

public partial class EditorPage : ContentPage
{
    private readonly EditorViewModel _viewModel;
    private EditorRow? _pressedRow;
    private Point _pressPoint;
    private bool _isShowingSelection;

    public EditorPage(EditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        viewModel.SelectionRequested += (_, indexes) => ShowSelection(indexes);

        // Opening the editor from the menu shows the newest macro instead of an empty page.
        Loaded += (_, _) =>
        {
            if (!viewModel.IsOpen && viewModel.Macros.FirstOrDefault() is { } newest)
            {
                viewModel.Open(newest.Path);
            }
        };
    }

    // The table's text boxes hand over their value when they lose the focus; Enter and Ctrl+S hand it over at once.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        bool isSave = e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control;
        if (Keyboard.FocusedElement is TextBox box && (e.Key == Key.Enter || isSave))
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        base.OnPreviewKeyDown(e);
    }

    private static bool IsInsideControl(DependencyObject? element)
    {
        while (element is not null and not ListViewItem)
        {
            if (element is TextBoxBase or ButtonBase)
            {
                return true;
            }

            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }

        return false;
    }

    private EditorRow? RowAt(object source) =>
        source is DependencyObject element && ItemsControl.ContainerFromElement(StepList, element) is ListViewItem item
            ? item.DataContext as EditorRow
            : null;

    private void ShowSelection(IReadOnlyList<int> indexes)
    {
        _isShowingSelection = true;
        StepList.SelectedItems.Clear();
        foreach (int index in indexes)
        {
            StepList.SelectedItems.Add(_viewModel.Rows[index]);
        }

        _isShowingSelection = false;
        if (indexes.Count > 0)
        {
            StepList.ScrollIntoView(_viewModel.Rows[indexes[0]]);
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isShowingSelection)
        {
            _viewModel.SetSelection(StepList.SelectedItems.Cast<EditorRow>().Select(row => row.Index));
        }
    }

    // Typing a wait or a note in a line makes it the selected line, so the tools act on it.
    private void OnListFocusChanging(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is TextBox box && ItemsControl.ContainerFromElement(StepList, box) is ListViewItem { IsSelected: false } item)
        {
            StepList.SelectedItem = item.DataContext;
        }
    }

    private void OnListMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressedRow = IsInsideControl(e.OriginalSource as DependencyObject) ? null : RowAt(e.OriginalSource);
        _pressPoint = e.GetPosition(StepList);
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedRow is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Vector moved = e.GetPosition(StepList) - _pressPoint;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        EditorRow dragged = _pressedRow;
        _pressedRow = null;
        DragDrop.DoDragDrop(StepList, dragged, DragDropEffects.Move);
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(EditorRow)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    // Dropped on a line: the step takes that line's place. Dropped below the last line: it goes to the end.
    private void OnListDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(EditorRow)) is EditorRow dragged)
        {
            int target = RowAt(e.OriginalSource)?.Index ?? _viewModel.Rows.Count - 1;
            _viewModel.Move(dragged.Index, target);
        }
    }
}
