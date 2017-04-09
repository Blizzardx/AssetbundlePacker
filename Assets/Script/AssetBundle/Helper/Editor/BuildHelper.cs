using System.IO;
using UnityEngine;
using System.Collections.Generic;
using Assets.Scripts.AssetBundle.BuildAssetbundleTool.Editor;
using UnityEditor;
using Assets.Script.AssetBundle.InternalAssetHandler;
using Assets.Script.AssetBundle.ReplaceInternalAssetHandler.Editor;
using System.Reflection;

public class BuildHelper
{
    [MenuItem("BuildAssetbundle/Build")]
    static void BuildAB()
    {
        BuildAssetbundleTool tool = new BuildAssetbundleTool();
       var e = tool.Build("Data", "Pack", "UGUI", "StreamingAssets/AssetsBundle");
        if(null != e)
        {
            Debug.LogException(e);
        }
        Debug.Log("Done");
    }
    [MenuItem("BuildAssetbundle/ForceBuild")]
    static void BuildABForce()
    {
        var output = "Assets/StreamingAssets/AssetsBundle";
        var res = BuildPipeline.BuildAssetBundles
       (output,
       BuildAssetBundleOptions.DeterministicAssetBundle |
       //  BuildAssetBundleOptions.UncompressedAssetBundle |
       BuildAssetBundleOptions.StrictMode,
       EditorUserBuildSettings.activeBuildTarget);
       
        if (null == res)
        {
            Debug.LogError("Build with error");
        }
        Debug.Log("Done");
    }
    [MenuItem("BuildAssetbundle/ExportInternalAssets")]
    static void UnpackInternalAssets()
    {
        InternalAssetsExportTool tool = new InternalAssetsExportTool();
        tool.ExportInternalAsset("InternalAssets");
        //AssetInfo info = new AssetInfo("E:\\Project\\unityProjcet\\ABPacker\\Assets\\Art\\tmp 1.mat");
        //var assets = EditorUtility.CollectDependencies(new Object[] { AssetDatabase.LoadMainAssetAtPath(info.GetRelativePath()) });
        //foreach (var asset in assets)
        //{
        //    Debug.Log(AssetDatabase.GetAssetPath(asset) + '/' + asset.name);
        //}
        //var deps = AssetDatabase.GetDependencies(info.GetRelativePath());
        //foreach (var asset in deps)
        //{
        //    Debug.Log(asset);
        //}
    }
    [MenuItem("BuildAssetbundle/ReplaceInternalAssets")]
    static void ReplaceInternalAssets()
    {
        //var asset = AssetDatabase.LoadAllAssetsAtPath("Resources/unity_builtin_extra");
        //foreach(var elem in asset)
        //{
        //    Debug.Log(elem.name + " : " + elem.GetFileID());
        //}
        //Debug.Log(AssetDatabase.AssetPathToGUID("Resources/unity_builtin_extra"));
        //Debug.Log(AssetDatabase.GUIDToAssetPath("0000000000000000f000000000000000"));
        ReplaceInternalAssetTool tool = new ReplaceInternalAssetTool();
        tool.ReplaceInternalAssetToBuildInAsset("Art1", "InternalAssets", "");        
        //tool.ReplaceInternalAssetToBuildInAsset("InternalAssets1", "InternalAssets", "");
    }

    
}
