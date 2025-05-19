namespace CustomMuseumFramework.Commands;

public abstract class ConsoleCommand(string name)
{
    public string Name { get; } = name;
    public string Description => GetDescription();

    public abstract string GetDescription();
    public abstract void Handle(string[] args);
}