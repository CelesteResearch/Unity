namespace Celeste.Tools.DependencyIsolation;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--help")
            {
                Console.WriteLine(
                    "--input-directory DIR --output-directory EMPTYDIR --identity-map FILE --cecil-key FILE --monomod-key FILE");
                return 0;
            }

            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length || !options.TryAdd(args[i], args[i + 1]))
                    throw new ArgumentException("Expected distinct option/value pairs.");
            }

            string Require(string name) => options.Remove(name, out var value)
                ? Path.GetFullPath(value)
                : throw new ArgumentException("Missing " + name);

            var input = Require("--input-directory");
            var output = Require("--output-directory");
            var map = Require("--identity-map");
            var cecilKey = Require("--cecil-key");
            var monoModKey = Require("--monomod-key");
            if (options.Count != 0) throw new ArgumentException("Unknown option.");
            DependencyIsolation.Run(input, output, map, cecilKey, monoModKey);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
