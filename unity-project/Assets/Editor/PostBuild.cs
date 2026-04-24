using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;

public class PostBuild
{
    [PostProcessBuild]
    public static void OnPostBuild(BuildTarget target, string pathToBuiltProject)
    {
        string buildDir = Path.GetDirectoryName(pathToBuiltProject);
        string workspaceRoot = Path.GetFullPath(Path.Combine(buildDir, "../app-backend/dist/"));
        string source = Path.Combine(workspaceRoot, "app");
        string dest = Path.Combine(buildDir, "app");
        File.Copy(source, dest, true);
    }
}