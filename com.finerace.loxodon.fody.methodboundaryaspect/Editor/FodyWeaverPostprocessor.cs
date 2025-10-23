using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

// Ensures this script runs when the Unity editor is launched.
[InitializeOnLoad]
public class FodyWeaverPostprocessor
{
    // Subscribes to the compilation finished event.
    static FodyWeaverPostprocessor()
    {
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
    }

    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        // Don't weave if there are compilation errors.
        if (messages.Any(m => m.type == CompilerMessageType.Error))
            return;
        
        
        // We only care about game assemblies, not editor scripts or packages.
        if (!assemblyPath.Contains("Library/ScriptAssemblies"))
            return;

        // Run the existing Fody weaving process.
        var assemblyDir = Path.GetDirectoryName(assemblyPath);
        Loxodon.Framework.Fody.FodyWeaver.Default.Weave(assemblyDir);

        // KEY STEP: Force Unity to reload the script assemblies from disk.
        // This solves the caching problem where Unity would use the old, unweaved DLL.
        EditorApplication.delayCall += EditorUtility.RequestScriptReload;
    }
}