using System.Windows.Input;

namespace FreeTrain.Modern;

public sealed class MiniCommand : ICommand
{
    private readonly Action action;

    private MiniCommand(Action action)
    {
        this.action = action;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public static MiniCommand Create(Action action)
    {
        return new MiniCommand(action);
    }

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter)
    {
        action();
    }
}
