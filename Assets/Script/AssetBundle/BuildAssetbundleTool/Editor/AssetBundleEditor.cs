using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

class AssetBundleEditor
{
    private static string[] CheckNameExclodePath = new string[]
    {
        "CameraPath3",
        "Plugins",
        "StreamingAssets",
        "Standard Assets"
    };
    private static string[] CheckSceneNameExclodePath = new string[]
   {
       // "CameraPath3",
        //"Plugins",
        "StreamingAssets",
       // "Standard Assets"
   };
    [MenuItem("资源打包/检查文件名")]
    static void FixAssetName()
    {
        CheckAssetNameVailed(false);
    }
    [MenuItem("资源打包/自动命名非法文件名")]
    static void CheckAssetName()
    {
        CheckAssetNameVailed(true);
    }
    static void CheckAssetNameVailed(bool isRename)
    {
        DirectoryInfo dir = new DirectoryInfo(Application.dataPath);
        var files = dir.GetFiles("*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; ++i)
        {
            var fileInfo = files[i];
            string fullName = fileInfo.FullName.Replace('\\', '/');
            // 统计不是以meta结尾的文件，以后可能还要排除别的文件再添加
            if (fileInfo.Name.EndsWith(".meta"))
            {
                continue;
            }
            bool isExclude = false;
            for (int j = 0; j < CheckNameExclodePath.Length; ++j)
            {
                if (fullName.StartsWith(Application.dataPath + "/" + CheckNameExclodePath[j]))
                {
                    isExclude = true;
                    break;
                }
            }
            if (isExclude)
            {
                continue;
            }
            if (!IsVailed(fileInfo.Name))
            {
                Debug.LogError("Name error " + fileInfo.FullName);
                if (isRename)
                {
                    int index = Application.dataPath.Length;
                    string relatePath = "Assets" + fileInfo.FullName.Replace('\\', '/').Substring(index);
                    string fixedName = fileInfo.Name.Replace(' ', '_');
                    string renameFix = string.Empty;
                    string tmpFix = fixedName.Substring(fixedName.LastIndexOf('.') + 1);
                    fixedName = fixedName.Substring(0, fixedName.LastIndexOf('.'));

                    int tmpi = 0;
                    while (true)
                    {
                        string newFullName = fileInfo.FullName.Replace(fileInfo.Name,
                            fixedName + renameFix + "." + tmpFix);
                        if (File.Exists(newFullName))
                        {
                            Debug.Log("already exist file with name " + newFullName);
                            ++tmpi;
                            renameFix = tmpi.ToString();
                        }
                        else
                        {
                            break;
                        }
                    }

                    AssetDatabase.RenameAsset(relatePath, fixedName + renameFix);
                    Debug.LogFormat("rename from {0} to {1} ", fileInfo.Name, fixedName);
                }
            }

        }
        AssetDatabase.SaveAssets();
    }
    [MenuItem("资源打包/清除文件包名")]
    static public void ClearAllAssetbundleName()
    {
        var assets = AssetDatabase.GetAllAssetBundleNames();
        for (int i = 0; i < assets.Length; ++i)
        {
            AssetDatabase.RemoveAssetBundleName(assets[i], true);
        }
    }
    [MenuItem("资源打包/Force build")]
    static public void Testbuild()
    {
        //BuildScript.BuildAssetBundles();
        return;
        string outputPath = Application.streamingAssetsPath + "/AssetBundles/iOS";
        //return;
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
            Directory.CreateDirectory(outputPath);
        }
        BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.DeterministicAssetBundle, EditorUserBuildSettings.activeBuildTarget);
        Debug.Log(outputPath);
    }
    [MenuItem("资源打包/Test print")]
    static public void TestPrint()
    {
        if (Directory.Exists(Application.dataPath))
        {
            var dir = new DirectoryInfo(Application.dataPath);
            var files = dir.GetFiles("*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; ++i)
            {
                var fileInfo = files[i];
                // 统计不是以meta结尾的文件，以后可能还要排除别的文件再添加
                if (!fileInfo.Name.EndsWith(".meta") && !fileInfo.Name.EndsWith(".cs"))
                {
                    //Debug.Log(fileInfo.FullName);
                    var fullName = fileInfo.FullName.Replace('\\', '/');
                    var index = fullName.IndexOf("Assets/");
                    if (index != -1)
                    {
                        var path = fullName.Substring(index);
                        AssetImporter tmp = AssetImporter.GetAtPath(path);
                        if (tmp != null)
                        {
                            if (tmp.assetBundleName == "pvpscene02.bundle")
                            {
                                Debug.Log(fullName);
                            }
                        }
                    }
                }
            }
        }
    }
    [MenuItem("资源打包/检查场景命名规范")]
    static public void CheckIsHasAssetsHaveSameNameWithUntiyScene()
    {
        HashSet<string> unitySceneNameList = new HashSet<string>();

        DirectoryInfo dir = new DirectoryInfo(Application.dataPath);
        var files = dir.GetFiles("*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; ++i)
        {
            var fileInfo = files[i];
            string fullName = fileInfo.FullName.Replace('\\', '/');
            // 统计不是以meta结尾的文件，以后可能还要排除别的文件再添加
            if (fileInfo.Name.EndsWith(".meta"))
            {
                continue;
            }
            bool isExclude = false;
            for (int j = 0; j < CheckSceneNameExclodePath.Length; ++j)
            {
                if (fullName.StartsWith(Application.dataPath + "/" + CheckSceneNameExclodePath[j]))
                {
                    isExclude = true;
                    break;
                }
            }
            if (isExclude)
            {
                continue;
            }
            if (fileInfo.Name.EndsWith(".unity"))
            {
                if (unitySceneNameList.Contains(fileInfo.Name))
                {
                    Debug.LogError("场景资源名重名：" + fileInfo.FullName);
                    continue;
                }
                unitySceneNameList.Add(fileInfo.Name.ToLower());
            }
            else
            {
                var assetName = fileInfo.Name.Split('.')[0];
                assetName += ".unity";
                if (unitySceneNameList.Contains(assetName.ToLower()))
                {
                    Debug.LogError("资源名不能与场景同名：" + fileInfo.FullName);
                    continue;
                }
            }
        }
    }
    static string[] specialAsset = new string[]
    {
        "mask",
        "NavMesh.asset",
        "LightingData.asset",
    };
    [MenuItem("资源打包/特殊包名处理")]
    public static void SpecialAssetsHandle()
    {
        string abDataPath = Application.dataPath + "/Data/";
        string excDataPath = "Assets/Data/";
        List<string> tmpStore = new List<string>();

        var exitLen = abDataPath.Length - excDataPath.Length;
        if (Directory.Exists(Application.dataPath))
        {
            var dir = new DirectoryInfo(Application.dataPath);
            var files = dir.GetFiles("*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; ++i)
            {
                var fileInfo = files[i];
                // 统计不是以meta结尾的文件，以后可能还要排除别的文件再添加
                if (!fileInfo.Name.EndsWith(".meta") && !fileInfo.Name.EndsWith(".cs"))
                {
                    //Debug.Log(fileInfo.FullName);
                    var fullName = fileInfo.FullName.Replace('\\', '/');
                    var index = fullName.IndexOf("Assets/");
                    if (index != -1)
                    {
                        string tmp = fileInfo.FullName.Replace('\\', '/').Substring(exitLen);
                        tmpStore.Add(tmp);
                    }
                }
            }
        }
        var allDeps = AssetDatabase.GetDependencies(tmpStore.ToArray());
        for (int i = 0; i < allDeps.Length; i++)
        {
            AssetImporter tmp = AssetImporter.GetAtPath(allDeps[i]);
            if (tmp != null)
            {
                int startIndex = allDeps[i].LastIndexOf('/') + 1;
                int endIndex = allDeps[i].LastIndexOf('.');
                if (endIndex == -1)
                {
                    continue;
                }
                string abName = allDeps[i].Substring(startIndex, endIndex - startIndex);
                abName = abName.ToLower();
                //if (tmp.assetBundleName == abName + abExtName)
                {
                    for (int j = 0; j < specialAsset.Length; ++j)
                    {
                        if (allDeps[i].EndsWith(specialAsset[j]))
                        {
                            tmp.assetBundleName = null;
                            Debug.Log(allDeps[i]);
                        }
                    }
                }
            }
        }
        Debug.Log("Done");
    }
    [MenuItem("资源打包/自动命名ui")]
    public static void AutoRenameUIFileName()
    {
        string abUIPath = Application.dataPath + "/UGUI/";
        string excUIPath = "Assets/UGUI/";

        if (Directory.Exists(abUIPath))
        {
            var exitLen = abUIPath.Length - excUIPath.Length;
            var dir = new DirectoryInfo(abUIPath);
            RenameUI(dir, exitLen);
            var allDirs = dir.GetDirectories("*", SearchOption.AllDirectories);
            for (var i = 0; i < allDirs.Length; ++i)
            {
                RenameUI(allDirs[i], exitLen);
            }
        }
        AssetDatabase.SaveAssets();
    }
    public static void RenameUI(DirectoryInfo dirInfo, int exitLen)
    {
        var name = dirInfo.Name;
        name = name.ToLower();
        var allFiles = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);
        for (int j = 0; j < allFiles.Length; j++)
        {
            var fileInfo = allFiles[j];
            // 统计不是以meta结尾的文件，以后可能还要排除别的文件再添加
            if (!fileInfo.Name.EndsWith(".meta"))
            {
                var path = fileInfo.FullName.Replace('\\', '/').Substring(exitLen);
                if (path.Length > 0 && !path.Contains(".cs"))
                {
                    // 如果没有重命名UI 附带Bundle名
                    if (!fileInfo.Name.Contains("@"))
                    {
                        string[] bn = fileInfo.Name.Split('.');
                        if (AssetDatabase.RenameAsset(path, bn[0] + "@" + name).Length > 0)
                        {
                            Debug.LogError("Rename UI " + fileInfo.Name + "Error ！！！");
                        }
                    }
                }
            }
        }
    }
    static public bool IsVailed(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return false;
        }
        if (str.Length <= 0)
        {
            return false;
        }
        var tmpList = str.Split('/');
        if (tmpList.Length > 0)
        {
            str = tmpList[tmpList.Length - 1];
        }
        //Debug.Log("check name " + str);
        var res = str.Split(' ');
        return res.Length <= 1;
    }
}