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

        var collector = new TypeCollector(projectDir, refFile).Collect();
        if (collector.Errors.Count > 0)
        {
            foreach (var error in collector.Errors) Console.Error.WriteLine($"CyreneJson Error: {error}");
            return 1;
        }

        new ContextEmitter(collector).Emit(outDir);
        return 0;
    }
}
