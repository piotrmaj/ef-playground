using System.Diagnostics;

namespace EfToolsExecutor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ClearMigrationsFolder();

            RunCommand("ef migrations add Initial");
            //RunCommand("ef database drop --force");
            //RunCommand("ef database update");
        }

        static DirectoryInfo GetApiCsProjPath()
        {
            return new DirectoryInfo(Path.Combine(
                VisualStudioProvider.TryGetSolutionDirectoryInfo().FullName,
                "./Infrastructure"));
        }

        static void ClearMigrationsFolder()
        {
            var migrationsDir = new DirectoryInfo(Path.Combine(
              GetApiCsProjPath().FullName,
              "Migrations"
            ));
            Console.WriteLine($"Removing files from {migrationsDir.FullName}");
            migrationsDir.Empty();
        }

        static void RunCommand(string args)
        {
            Console.WriteLine($"Running command: `dotnet {args}`");
            var process = new Process();
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet.exe",
                Arguments = args,
                WorkingDirectory = GetApiCsProjPath().FullName
            };
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }
    }
}
