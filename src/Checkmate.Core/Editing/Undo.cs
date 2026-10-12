namespace Checkmate.Core.Editing;

public interface IUndoableCommand
{
    string Name { get; }
    void Do();
    void Undo();
}

public sealed class ActionCommand : IUndoableCommand
{
    private readonly Action _do, _undo;
    public string Name { get; }
    public ActionCommand(string name, Action @do, Action undo) { Name = name; _do = @do; _undo = undo; }
    public void Do() => _do();
    public void Undo() => _undo();
}

public sealed class UndoStack
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Run a command and record it.</summary>
    public void Execute(IUndoableCommand cmd) { cmd.Do(); Push(cmd); }

    /// <summary>Record a command whose effect was already applied (e.g. a live brush stroke).</summary>
    public void Push(IUndoableCommand cmd) { _undo.Push(cmd); _redo.Clear(); }

    public bool Undo()
    {
        if (!CanUndo) return false;
        var c = _undo.Pop(); c.Undo(); _redo.Push(c); return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        var c = _redo.Pop(); c.Do(); _undo.Push(c); return true;
    }

    public void Clear() { _undo.Clear(); _redo.Clear(); }
}
