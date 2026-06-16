using System.Text;

namespace CyreneJson.BuildTask.Helpers;

public sealed class CodeBuilder
{
    private readonly StringBuilder Sb = new();
    private int IndentLevel;
    private const string IndentString = "  ";

    public CodeBuilder Indent()
    {
        IndentLevel++;
        return this;
    }

    public CodeBuilder Unindent()
    {
        if (IndentLevel > 0) IndentLevel--;
        return this;
    }

    public CodeBuilder AppendLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
            Sb.AppendLine();
        else
        {
            for (int i = 0; i < IndentLevel; i++)
                Sb.Append(IndentString);
            Sb.AppendLine(line);
        }
        return this;
    }

    public override string ToString()
    {
        return Sb.ToString();
    }
}
