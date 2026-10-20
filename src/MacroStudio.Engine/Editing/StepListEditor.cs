using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Engine.Editing;

/// <summary>
/// The step list of a macro being edited, with undo and redo. Steps are never changed in place: every edit
/// makes new step objects, so an earlier list can always be brought back as it was.
/// </summary>
public sealed class StepListEditor(IReadOnlyList<MacroStep> steps)
{
    public const int UndoLimit = 200;

    private readonly LinkedList<IReadOnlyList<MacroStep>> _undo = new();
    private readonly Stack<IReadOnlyList<MacroStep>> _redo = new();

    public event EventHandler? Changed;

    public IReadOnlyList<MacroStep> Steps { get; private set; } = steps;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>True after an edit, until <see cref="MarkSaved"/>.</summary>
    public bool HasChanges { get; private set; }

    public void MarkSaved()
    {
        HasChanges = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Delete(IReadOnlyCollection<int> indexes) =>
        Apply(Steps.Where((_, index) => !indexes.Contains(index)).ToList());

    /// <summary>Puts a copy right after each chosen step; copies get their own ids.</summary>
    public void Duplicate(IReadOnlyCollection<int> indexes)
    {
        var result = new List<MacroStep>();
        for (int i = 0; i < Steps.Count; i++)
        {
            result.Add(Steps[i]);
            if (indexes.Contains(i))
            {
                result.Add(Steps[i] with { Id = Guid.NewGuid() });
            }
        }

        Apply(result);
    }

    /// <summary>Moves one step so it ends up at <paramref name="newIndex"/>.</summary>
    public void Move(int index, int newIndex)
    {
        newIndex = Math.Clamp(newIndex, 0, Steps.Count - 1);
        if (index == newIndex)
        {
            return;
        }

        var result = Steps.ToList();
        MacroStep step = result[index];
        result.RemoveAt(index);
        result.Insert(newIndex, step);
        Apply(result);
    }

    public void SetEnabled(IReadOnlyCollection<int> indexes, bool isEnabled) =>
        Change(indexes, step => step.IsEnabled == isEnabled ? step : step with { IsEnabled = isEnabled });

    public void SetDelay(IReadOnlyCollection<int> indexes, int delayMs) =>
        Change(indexes, step => step.DelayBeforeMs == delayMs ? step : step with { DelayBeforeMs = delayMs });

    public void SetNote(int index, string? note)
    {
        string? cleaned = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Change([index], step => step.Note == cleaned ? step : step with { Note = cleaned });
    }

    public void Insert(int index, MacroStep step)
    {
        var result = Steps.ToList();
        result.Insert(Math.Clamp(index, 0, result.Count), step);
        Apply(result);
    }

    public void Replace(int index, MacroStep step) => Change([index], _ => step);

    /// <summary>Changes the whole list in one undoable step, for example after converting the Excel wheel.</summary>
    public void ReplaceAll(IReadOnlyList<MacroStep> steps) => Apply(steps);

    public void Undo()
    {
        if (_undo.Last is not { } last)
        {
            return;
        }

        _undo.RemoveLast();
        _redo.Push(Steps);
        Show(last.Value);
    }

    public void Redo()
    {
        if (!_redo.TryPop(out IReadOnlyList<MacroStep>? next))
        {
            return;
        }

        _undo.AddLast(Steps);
        Show(next);
    }

    private void Change(IReadOnlyCollection<int> indexes, Func<MacroStep, MacroStep> change)
    {
        var result = Steps.Select((step, index) => indexes.Contains(index) ? change(step) : step).ToList();
        Apply(result);
    }

    private void Apply(IReadOnlyList<MacroStep> result)
    {
        if (result.SequenceEqual(Steps, ReferenceEqualityComparer.Instance))
        {
            return;
        }

        _undo.AddLast(Steps);
        if (_undo.Count > UndoLimit)
        {
            _undo.RemoveFirst();
        }

        _redo.Clear();
        Show(result);
    }

    private void Show(IReadOnlyList<MacroStep> steps)
    {
        Steps = steps;
        HasChanges = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
