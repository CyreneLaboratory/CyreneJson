namespace CyreneJson.BuildTask;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: CyreneJson <project> <ref> <output>");
            return 1;
        }

        var projectDir = args[0];
        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"Project not found: {projectDir}");
            return 1;
        }

        var refFile = args[1];
        if (!File.Exists(refFile))
        {
            Console.Error.WriteLine($"RefFile not found: {refFile}");
            return 1;
        }

        var outDir = args[2];
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        new ContextEmitter(new TypeCollector(projectDir, refFile).Collect()).Emit(outDir);
        return 0;
    }
}
