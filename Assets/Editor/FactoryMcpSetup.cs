using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Services;

[InitializeOnLoad]
public static class FactoryMcpSetup
{
    static FactoryMcpSetup() { EditorApplication.delayCall += Connect; }
    [MenuItem("Factory/Connect Unity MCP")]
    public static async void Connect()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Connect; return; }
        EditorConfigurationCache.Instance.SetUseHttpTransport(true);
        EditorConfigurationCache.Instance.SetHttpBaseUrl("http://127.0.0.1:8080");
        try { Debug.Log("Factory MCP connected: " + await MCPServiceLocator.Bridge.StartAsync()); }
        catch (System.Exception e) { Debug.LogWarning("Factory MCP: " + e.Message); }
    }
}
