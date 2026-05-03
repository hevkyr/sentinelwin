using System.Reflection;
using System.Runtime.Loader;
using SentinelWin.Core.Abstractions;

namespace SentinelWin.Core.Services;

/// <summary>
/// Discovers and loads IScanner implementations from external DLL plugins.
///
/// BUG FIX: Original used isCollectible: false — this prevents the AssemblyLoadContext
/// (and thus the loaded assembly) from ever being garbage collected, causing a memory leak
/// if plugins are reloaded. Changed to isCollectible: true so contexts can be unloaded.
/// </summary>
public sealed class PluginLoader
{
    /// <summary>
    /// Loads all IScanner implementations found in DLL files under <paramref name="pluginsDir"/>.
    /// Each DLL gets its own isolated AssemblyLoadContext (isCollectible: true).
    /// </summary>
    public IReadOnlyList<IScanner> LoadScanners(string pluginsDir)
    {
        var list = new List<IScanner>();
        if (!Directory.Exists(pluginsDir)) return list;

        foreach (var dll in Directory.EnumerateFiles(pluginsDir, "*.dll"))
        {
            // BUG FIX: isCollectible: true — allows the context to be unloaded when
            //          no live references remain (e.g. on plugin hot-reload).
            var ctx = new AssemblyLoadContext(
                Path.GetFileNameWithoutExtension(dll),
                isCollectible: true);

            Assembly asm;
            try
            {
                asm = ctx.LoadFromAssemblyPath(Path.GetFullPath(dll));
            }
            catch (Exception ex)
            {
                // Log and continue — one bad plugin should not block others
                System.Diagnostics.Debug.WriteLine($"[PluginLoader] Failed to load '{dll}': {ex.Message}");
                ctx.Unload();
                continue;
            }

            foreach (var t in asm.GetTypes()
                                  .Where(t => typeof(IScanner).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass))
            {
                try
                {
                    if (Activator.CreateInstance(t) is IScanner scanner)
                        list.Add(scanner);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PluginLoader] Failed to instantiate '{t.FullName}': {ex.Message}");
                }
            }
        }

        return list;
    }
}
