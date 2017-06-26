using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using Common.Component;

namespace Assets.Scripts.AssetBundle.BuildAssetbundleTool.Editor
{
    public class BuildAssetElementChildInfo
    {
        public int refCount;
        public AssetInfo assetInfo;
    }
    public class BuildAssetElemmentInfo
    {
        public AssetInfo mainAssetInfo;
        public List<BuildAssetElementChildInfo> depAssetsList;
    }
    public class BuildAssetsRefrenceFilter
    {
        // first key - refrence count ,second key - assets group ,main assets 
        // value - child list
        public Dictionary<int, Dictionary<AssetInfo, List<BuildAssetElementChildInfo>>> filter;
    }
    public class BuildAssetbundleTool
    {
        private string m_strDataPath;
        private string m_strPackByFolderPath;
        private string m_strUguiPath;
        private string m_strOutputPath;
        private Exception m_ErrorInfo;
        private Dictionary<string, List<string>> m_AutoFixedNameList;
        private Dictionary<string, string> m_BundleNameReport;

        private const string m_strBundleSuffix = "bundle";
        private const string m_strSceneAssetExtSuffix = "scene";
        private const bool m_bIsEncryptBundleName = false;


        private string[] m_IgnoreSuffixList = new string[]
        {
            ".svn",
            ".git",
            ".meta",
            ".cs",
        };
        private string[] m_DataPathWhiteList = new string[]
        {
            ".prefab",
            ".unity",
            ".mat",
            ".wav",
            ".mp3",
            ".ogg",
            ".ttf",
        };
        private string[] m_PackPathWhiteList = new string[]
        {
            ".prefab",
            ".unity",
            ".mat",
            ".wav",
            ".mp3",
            ".ogg",
            ".png",
        };
        private string[] m_UguiPathWhiteList = new string[]
        {
            ".png",
            ".jpg",
        };
        private string[] m_AlwaysNullBundleNameSuffixList = new string[]
        {
            "mask",
            "NavMesh.asset",
            "LightingData.asset",

        };

        #region public interface
        public Exception Build(string dataPath, string packByFolderPath, string uguiPath, string outputPath)
        {
            m_strDataPath = dataPath;
            m_strPackByFolderPath = packByFolderPath;
            m_strUguiPath = uguiPath;
            m_strOutputPath = outputPath;
            m_ErrorInfo = null;
            m_AutoFixedNameList = new Dictionary<string, List<string>>();
            m_BundleNameReport = new Dictionary<string, string>();

            // make sure output path is include in StreamingAssets
            CheckOutputPaht(m_strOutputPath);
            if (m_ErrorInfo != null)
            {
                return m_ErrorInfo;
            }
            List<string> paths = new List<string>() { dataPath, packByFolderPath, uguiPath, outputPath };
            // check path argument
            CheckInputArgument(paths);
            if (m_ErrorInfo != null)
            {
                return m_ErrorInfo;
            }
            // check all assets in root path
            CheckAllAssets();
            if (m_ErrorInfo != null)
            {
                return m_ErrorInfo;
            }
            // check all packed assets ( with deps )
            CheckAllPackByFolderAssets();
            if (m_ErrorInfo != null)
            {
                return m_ErrorInfo;
            }
            // handler ugui ,pack atlas
            SetAndPackUguiAtlas();
            if (m_ErrorInfo != null)
            {
                return m_ErrorInfo;
            }
            // save
            AssetDatabase.SaveAssets();

            // begin build asset bundle & out put asset bundle
            BuildBundlesAndOutput();

            return m_ErrorInfo;
        }
        public Dictionary<string, string> GetReport()
        {
            return m_BundleNameReport;
        }
        #endregion

        #region system function
        private void CheckInputArgument(List<string> paths)
        {
            // check datapath is include in other path
            for (int i = 0; i < paths.Count; ++i)
            {
                for (int j = 0; j < paths.Count; ++j)
                {
                    if (i == j)
                    {
                        continue;
                    }
                    if (CheckPathInIncludeIn(paths[i], paths[j]))
                    {
                        m_ErrorInfo = new Exception(string.Format("Path {0} is Include In {1}", paths[j], paths[i]));
                        return;
                    }
                }
                // check folder content
                var folderPath = Application.dataPath + "/" + paths[i];
                if(!Directory.Exists(folderPath))
                {
                    m_ErrorInfo = new Exception("Path is not exist " + folderPath);
                    return;
                }
            }
            return;
        }
        private void CheckOutputPaht(string m_strOutputPath)
        {
            var realOutputPath = Application.dataPath + "/" + m_strOutputPath;
            if(!Directory.Exists(realOutputPath))
            {
                Directory.CreateDirectory(realOutputPath);
            }
            if(!CheckFileInInFolder(realOutputPath,Application.streamingAssetsPath))
            {
                m_ErrorInfo = new Exception("out put path must include in streaming assets");
            }
        }
        private void CheckAllAssets()
        {
            List<BuildAssetElemmentInfo> allAssetList = new List<BuildAssetElemmentInfo>();

            var dataPathPerfix = Application.dataPath.Substring(0, Application.dataPath.IndexOf("Assets"));
            var realPath = Application.dataPath + "/" + m_strDataPath;
            var dir = new DirectoryInfo(realPath);
            var files = dir.GetFiles("*", SearchOption.AllDirectories);
            
            for (var i = 0; i < files.Length; ++i)
            {                
                AssetInfo info = new AssetInfo(files[i].FullName);

                if (info.IsInSuffixList(m_IgnoreSuffixList))
                {
                    continue;
                }
                BuildAssetElemmentInfo assetInfo = new BuildAssetElemmentInfo();
                assetInfo.mainAssetInfo = info;

                var deps = AssetDatabase.GetDependencies(assetInfo.mainAssetInfo.GetRelativePath());
                assetInfo.depAssetsList = new List<BuildAssetElementChildInfo>(deps.Length);

                foreach(var elem in deps)
                {
                    BuildAssetElementChildInfo childInfo = new BuildAssetElementChildInfo();
                    childInfo.assetInfo = new AssetInfo(dataPathPerfix + elem);
                    if(childInfo.assetInfo.IsInSuffixList(m_IgnoreSuffixList))
                    {
                        // dep assets is in ignore list
                        continue;
                    }
                    if(elem.StartsWith("Assets/" + m_strUguiPath))
                    {
                        // dep assets is in ugui
                        //Debug.LogError("ugui: " + elem);
                        continue;
                    }
                    childInfo.refCount = 0;
                    assetInfo.depAssetsList.Add(childInfo);
                }

                allAssetList.Add(assetInfo);
            }
            //确保需要打包的主资源不重名 && 同时检查不跟pack by folder 下和ugui下的文件夹名重名
            if(IsMainAssetsHaveSameName(allAssetList,GetAllPackbyFolderAndUguiFolderNameList()))
            {
                return;
            }
            //确保需要打包的主资源在白名单中
            if (!IsMainAsseetsInWhiteList(allAssetList))
            {
                return;
            }
            // 确保所有资源以及引用的资源不在pack by folder 目录中
            if (IsAssetsInPackbyFolderPath(allAssetList))
            {
                return;
            }
            //清理列表  查找 mainasset 出现在 别的 dep asset list 中的情况
            CleanList(allAssetList);
            // 构建引用数
            BuildRefrenceCountMap(allAssetList);
            // 创建 引用数 - 主资源 - （依赖资源数组）的筛选器
            var filter = FilterByRerenceCount(allAssetList);
            // 根据筛选器重置bundle 名字 
            SetAssetbundleNameByFilter(filter);
        }
        private List<string> GetAllPackbyFolderAndUguiFolderNameList()
        {
            List<string> res = new List<string>();

            var dirs = GetAllDirectoryByDirectory(m_strPackByFolderPath);
            dirs.AddRange(GetAllDirectoryByDirectory(m_strUguiPath));
            for(int i=0;i<dirs.Count;++i)
            {
                res.Add(dirs[i].Name);
            }
            return res;
        }
        private bool IsMainAsseetsInWhiteList(List<BuildAssetElemmentInfo> allAssetList)
        {
            foreach(var elem in allAssetList)
            {
                if (!elem.mainAssetInfo.IsInSuffixList(m_DataPathWhiteList))
                {
                    m_ErrorInfo = new Exception("Asset is not in white list " + elem.mainAssetInfo.GetFullPath());
                    return false;
                }
            }
            return true;
        }
        private void CheckAllPackByFolderAssets()
        {
            var files = GetFilesByDirectory(m_strPackByFolderPath);            

            for (var i = 0; i < files.Count; ++i)
            {
                AssetInfo info = new AssetInfo(files[i].FullName);
                // check asset is in white list
                if (!info.IsInSuffixList(m_PackPathWhiteList))
                {
                    m_ErrorInfo = new Exception("Asset is not in white list " + info.GetFullPath());
                }
                SetAssetBundleName(info, files[i].Directory.Name);
            }
        }
        private void SetAndPackUguiAtlas()
        {
            var files = GetFilesByDirectory(m_strUguiPath);
          
            for (var i = 0; i < files.Count; ++i)
            {
                AssetInfo info = new AssetInfo(files[i].FullName);
                // check asset is in white list
                if(!info.IsInSuffixList(m_UguiPathWhiteList))
                {
                    m_ErrorInfo = new Exception("Asset is not in white list " + info.GetFullPath());
                }
                SetAssetBundleName(info, files[i].Directory.Name);
            }
        }
        private void BuildBundlesAndOutput()
        {
            var output = "Assets/" + m_strOutputPath;
            var res = BuildPipeline.BuildAssetBundles
           (output,
           BuildAssetBundleOptions.DeterministicAssetBundle |
           //  BuildAssetBundleOptions.UncompressedAssetBundle |
           BuildAssetBundleOptions.StrictMode,
           EditorUserBuildSettings.activeBuildTarget);
            if(null == res)
            {
                m_ErrorInfo = new Exception("Build with error");
            }
        }
        #endregion

        #region tool
        private List<FileInfo> GetFilesByDirectory(string directory)
        {
            List<FileInfo> res = new List<FileInfo>();
            
            var realPath = Application.dataPath + "/" + directory;
            var dir = new DirectoryInfo(realPath);
            var files = dir.GetFiles("*", SearchOption.AllDirectories);

            for (var i = 0; i < files.Length; ++i)
            {
                AssetInfo info = new AssetInfo(files[i].FullName);

                if (info.IsInSuffixList(m_IgnoreSuffixList))
                {
                    continue;
                }

                res.Add(files[i]);
            }

            return res;
        }
        private List<DirectoryInfo> GetAllDirectoryByDirectory(string directory)
        {
            var realPath = Application.dataPath + "/" + directory;
            var dir = new DirectoryInfo(realPath);

            List<DirectoryInfo> res = new List<DirectoryInfo>();
            GetAllDirectory(dir, ref res);

            return res;
        }
        private void GetAllDirectory(DirectoryInfo root,ref List<DirectoryInfo> result)
        {
            result.Add(root);
            var dirs = root.GetDirectories();
            if(null != dirs)
            {
                foreach(var elem in dirs)
                {
                    GetAllDirectory(elem,ref result);
                }
            }
        }
        private bool CheckPathInIncludeIn(string targetPath, string sourcePath)
        {
            targetPath = targetPath.Replace('\\', '/');
            targetPath = targetPath.ToLower();

            sourcePath = sourcePath.Replace('\\', '/');
            sourcePath = sourcePath.ToLower();

            return targetPath.Contains(sourcePath);
        }
        private bool CheckFileInInFolder(string filePath, string folderPath)
        {
            filePath = filePath.Replace('\\', '/');
            filePath = filePath.ToLower();

            folderPath = folderPath.Replace('\\', '/');
            folderPath = folderPath.ToLower();

            return filePath.StartsWith(folderPath);
        }
        private bool IsAssetsInPackbyFolderPath(List<BuildAssetElemmentInfo> targetList)
        {
            List<string> checkPathList = new List<string>()
            {
                Application.dataPath + "/" + m_strPackByFolderPath,
                //Application.dataPath + "/" + m_strUguiPath,
            };
            foreach (var elem in targetList)
            {
                foreach (var subElem in elem.depAssetsList)
                {
                    foreach(var path in checkPathList)
                    {
                        if(CheckFileInInFolder(subElem.assetInfo.GetFullPath(),path))
                        {
                            string errorInfo = string.Format("{0} dependent {1} ,but {1} at special path {2} ,is not not legal !", elem.mainAssetInfo.GetFullPath(), subElem.assetInfo.GetFullPath(), path);
                            m_ErrorInfo = new Exception(errorInfo);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private bool IsMainAssetsHaveSameName(List<BuildAssetElemmentInfo> targetList,List<string> packanduguiBundleNameList)
        {
            HashSet<string> tmpSet = new HashSet<string>();
            foreach(var elem in targetList)
            {
                if(tmpSet.Contains(elem.mainAssetInfo.GetFileNameWithoutSuffix()))
                {
                    m_ErrorInfo = new Exception("bundle name already exist at " + elem.mainAssetInfo.GetFullPath());
                    return true;
                }
                tmpSet.Add(elem.mainAssetInfo.GetFileNameWithoutSuffix());
            }
            foreach(var elem in packanduguiBundleNameList)
            {
                if (tmpSet.Contains(elem))
                {
                    m_ErrorInfo = new Exception("bundle name already exist at " + elem + " : " + m_strUguiPath + " : " + m_strPackByFolderPath);
                    return true;
                }
                tmpSet.Add(elem);
            }
            return false;
        }
        private void CleanList(List<BuildAssetElemmentInfo> targetList)
        {
            for(int i=0;i<targetList.Count;++i)
            {
                var assets = targetList[i];
                for(int j=0;j<targetList.Count;++j)
                {
                    if(i == j)
                    {
                        continue;
                    }
                    var curAssets = targetList[j];
                    bool isNeedClean = false;
                    foreach(var elem in curAssets.depAssetsList)
                    {
                        if(elem.assetInfo.GetFullPath() == assets.mainAssetInfo.GetFullPath())
                        {
                            // mark
                            isNeedClean = true;
                            break;
                        }
                    }
                    if(isNeedClean)
                    {
                        // do clean
                        for(int k=0;k<curAssets.depAssetsList.Count;)
                        {
                            bool needRemove = false;
                            for(int k1=0;k1<assets.depAssetsList.Count;++k1)
                            {
                                if(assets.depAssetsList[k1].assetInfo.GetFullPath() == curAssets.depAssetsList[k].assetInfo.GetFullPath())
                                {
                                    needRemove = true;
                                    break;
                                }
                            }
                            if(needRemove)
                            {
                                curAssets.depAssetsList.RemoveAt(k);
                            }
                            else
                            {
                                ++k;
                            }
                        }
                    }
                }
            }
        }
        private void BuildRefrenceCountMap(List<BuildAssetElemmentInfo> targetList)
        {
            Dictionary<string, int> refrenceCountMap = new Dictionary<string, int>();
            foreach(var elem in targetList)
            {
                foreach(var subElem in elem.depAssetsList)
                {
                    if (!refrenceCountMap.ContainsKey(subElem.assetInfo.GetFullPath()))
                    {
                        refrenceCountMap.Add(subElem.assetInfo.GetFullPath(), 1);
                    }
                    else
                    {
                        ++refrenceCountMap[subElem.assetInfo.GetFullPath()];
                    }
                }
            }
            // write & save refrence count
            foreach (var elem in targetList)
            {
                foreach (var subElem in elem.depAssetsList)
                {
                    subElem.refCount = refrenceCountMap[subElem.assetInfo.GetFullPath()];
                }
            }
        }
        private BuildAssetsRefrenceFilter FilterByRerenceCount(List<BuildAssetElemmentInfo> targetList)
        {
            BuildAssetsRefrenceFilter res = new BuildAssetsRefrenceFilter();
            // first key - refrence count ,second key - assets group ,main assets 
            // value - child list
            res.filter = new Dictionary<int, Dictionary<AssetInfo, List<BuildAssetElementChildInfo>>>();

            foreach (var elem in targetList)
            {
                foreach (var subElem in elem.depAssetsList)
                {
                    if (!res.filter.ContainsKey(subElem.refCount))
                    {
                        res.filter.Add(subElem.refCount, new Dictionary<AssetInfo, List<BuildAssetElementChildInfo>>());
                    }
                    if(!res.filter[subElem.refCount].ContainsKey(elem.mainAssetInfo))
                    {
                        res.filter[subElem.refCount].Add(elem.mainAssetInfo, new List<BuildAssetElementChildInfo>());
                    }
                    res.filter[subElem.refCount][elem.mainAssetInfo].Add(subElem);
                }
            }
            return res;
        }
        private void SetAssetbundleNameByFilter(BuildAssetsRefrenceFilter filter)
        {
            foreach(var elem in filter.filter)
            {
                int refrenceCount = elem.Key;
                if (refrenceCount == 0)
                {
                    Debug.LogError("error refrence count ");
                    continue;
                }
                else if (refrenceCount == 1)
                {
                    foreach (var subElem in elem.Value)
                    {
                        var mainAsset = subElem.Key;
                        foreach (var asset in subElem.Value)
                        {
                            SetAssetBundleName(asset.assetInfo, mainAsset);
                        }
                    }
                }
                else
                {
                    HandleAssets(refrenceCount, elem.Value);
                }
            }
        }
        private void HandleAssets(int refrenceCount, Dictionary<AssetInfo, List<BuildAssetElementChildInfo>> infoList)
        {
            Dictionary<string, AssetInfo> allAssetList = new Dictionary<string, AssetInfo>();
            HashSet<string> doneAssetList = new HashSet<string>();

            foreach (var elem in infoList)
            {
                foreach(var subElem in elem.Value)
                {
                    if(!allAssetList.ContainsKey(subElem.assetInfo.GetFullPath()))
                    {
                        allAssetList.Add(subElem.assetInfo.GetFullPath(),subElem.assetInfo);
                    }
                }
            }
            foreach(var asset in allAssetList)
            {
                if (doneAssetList.Contains(asset.Key))
                {
                    continue;
                }
                int tmpIndex = 0;
                List<AssetInfo> finalList = new List<AssetInfo>();

                foreach (var elem in infoList)
                {
                    foreach (var subElem in elem.Value)
                    {
                        if(subElem.assetInfo.GetFullPath() == asset.Key)
                        {
                            ++tmpIndex;
                            if (finalList.Count == 0)
                            {
                                finalList = InitFinalList(doneAssetList,elem.Value, asset.Key);
                            }
                            else
                            {
                                finalList = CombineToSaveSameItem(finalList, elem.Value, asset.Key);
                            }
                            break;
                        }
                    }
                    if(tmpIndex == refrenceCount)
                    {
                        break;
                    }
                }
                // add self to list
                AssetInfo self = new AssetInfo(asset.Key);
                finalList.Add(self);
                foreach(var elem in finalList)
                {
                    SetAssetBundleName(elem, self);
                    // add to done list
                    doneAssetList.Add(elem.GetFullPath());
                }
            }
        }
        private List<AssetInfo> InitFinalList(HashSet<string> doneList,List<BuildAssetElementChildInfo> targetList, string mainAssetFullPath)
        {
            List<AssetInfo> res = new List<AssetInfo>();
            foreach (var elem in targetList)
            {
                if (elem.assetInfo.GetFullPath() == mainAssetFullPath)
                {
                    continue;
                }               
                if (null == doneList || !doneList.Contains(elem.assetInfo.GetFullPath()))
                {
                    res.Add(elem.assetInfo);
                }                
            }
            return res;
        }
        private List<AssetInfo> CombineToSaveSameItem(List<AssetInfo> finalList, List<BuildAssetElementChildInfo> targetList, string mainAssetFullPath)
        {
            List<AssetInfo> res = new List<AssetInfo>();
            var tmpList = InitFinalList(null,targetList, mainAssetFullPath);

            HashSet<string> tmpMap = new HashSet<string>();
            foreach(var elem in finalList)
            {
                if(!tmpMap.Contains(elem.GetFullPath()))
                {
                    tmpMap.Add(elem.GetFullPath());
                }
                else
                {
                    Debug.LogError("sth wrong");
                }
            }
            foreach(var elem in tmpList)
            {
                if(tmpMap.Contains(elem.GetFullPath()))
                {
                    res.Add(elem);
                }
            }
            return res;
        }
        private void SetAssetBundleName(AssetInfo targetAsset,AssetInfo mainAsset)
        {
            string bundleName = string.Empty;
            //判断是不是主资源就是自己 & 是不是unity 场景文件
            if (targetAsset.GetFullPath() != mainAsset.GetFullPath() && mainAsset.GetFileSuffix() == ".unity")
            {
                //unity 不允许 scene bundle 和他的依赖放在一起，在这里分开
                bundleName = mainAsset.GetFileNameWithoutSuffix() + "_" + m_strSceneAssetExtSuffix ;
            }
            else
            {
                bundleName = mainAsset.GetFileNameWithoutSuffix();
            }
            var importer = AssetImporter.GetAtPath(targetAsset.GetRelativePath());
            DoSetBundleName(mainAsset.GetRelativePath(), importer, bundleName, "." + m_strBundleSuffix);
        }
        private void SetAssetBundleName(AssetInfo targetAsset, string bundleName)
        {            
            var importer = AssetImporter.GetAtPath(targetAsset.GetRelativePath());
            DoSetBundleName(string.Empty, importer, bundleName, "." + m_strBundleSuffix);
        }
        private void DoSetBundleName(string directory,AssetImporter importer, string bundleName, string suffix)
        {
            string realBundleName = null;
            if (m_bIsEncryptBundleName)
            {
                realBundleName = CaculateAssetBundleNameWithEncrypt(directory, importer, bundleName, suffix);
            }
            else
            {
                realBundleName = CaculateAssetBundleNameWithOutEncrypt(directory, importer, bundleName, suffix);
            }

            importer.assetBundleName = realBundleName;
        }
        private string CaculateAssetBundleNameWithOutEncrypt(string directory, AssetImporter importer, string bundleName, string suffix)
        {
            //为了防止重名，如果不是需要直接加载的资源，计算路径的crc32 加入到bundlename里边
            if (string.IsNullOrEmpty(directory) || IsDirectoryInDataOrPackOrUgui(directory))
            {
                directory = string.Empty;
            }
            else
            {
                directory = GetCRC32(directory) + "_";
            }

            if (string.IsNullOrEmpty(bundleName))
            {
                Debug.LogError("error bundle name ,name is null or empty " + bundleName);
            }

            // fix bundle name 
            var lastName = bundleName;
            var isChange = FixName(ref bundleName);
            if (isChange)
            {
                Debug.LogFormat("auto fix bundle name {0} to {1} ", lastName, bundleName);
            }
            CheckAutoFixedNameIsLegal(lastName, bundleName);
            //把路径的crc32编入到bundlename，如果是需要直接加载的资源，比如data 和 pack下的资源，不处理
            bundleName = directory + bundleName;

            for (int i = 0; i < m_AlwaysNullBundleNameSuffixList.Length; ++i)
            {
                if (importer.assetPath.EndsWith(m_AlwaysNullBundleNameSuffixList[i]))
                {
                    bundleName = null;
                    return null;
                }
            }
            if (null != bundleName)
            {
                if (bundleName.Length > 100)
                {
                    Debug.LogWarning("bundle name too long " + bundleName);
                    bundleName = CRC32.GetCRC32Str(bundleName).ToString();
                    Debug.LogWarning("auto fix long bundle name to " + bundleName);
                }
                bundleName += suffix;
                bundleName = bundleName.ToLower();
            }
            return bundleName;
        }
        private string CaculateAssetBundleNameWithEncrypt(string directory, AssetImporter importer, string bundleName, string suffix)
        {
            bool needGenReport = false;
            
            if (string.IsNullOrEmpty(directory) )
            {
                directory = string.Empty;
            }
            if(IsDirectoryInDataOrPackOrUgui(directory))
            {
                directory = string.Empty;
                needGenReport = true;
            }

            if (string.IsNullOrEmpty(bundleName))
            {
                Debug.LogError("error bundle name ,name is null or empty " + bundleName);
                return null;
            }
            
            bundleName = directory + bundleName;
            var lastName = bundleName;
            // caculate bundle name with crc32
            bundleName = CRC32.GetCRC32Str(bundleName).ToString();

            CheckAutoFixedNameIsLegal(lastName, bundleName);
            for (int i = 0; i < m_AlwaysNullBundleNameSuffixList.Length; ++i)
            {
                if (importer.assetPath.EndsWith(m_AlwaysNullBundleNameSuffixList[i]))
                {
                    bundleName = null;
                    return null;
                }
            }
            if(null != bundleName)
            {
                if (bundleName.Length > 100)
                {
                    Debug.LogWarning("bundle name too long " + bundleName);
                    bundleName = CRC32.GetCRC32Str(bundleName).ToString();
                    Debug.LogWarning("auto fix long bundle name to " + bundleName);
                }
                bundleName += suffix;
                bundleName = bundleName.ToLower();
                if (needGenReport)
                {
                    GenReport(lastName, bundleName);
                }
            }
            return bundleName;
        }
        private string GetCRC32(string directory)
        {
            return CRC32.GetCRC32Str(directory).ToString();
           // return directory.Replace('/', '_');
        }
        private bool IsDirectoryInDataOrPackOrUgui(string directory)
        {
            directory = directory.Replace('\\', '/');

            if (directory.StartsWith("Assets/" + m_strDataPath))
            {
                return true;
            }
            if(directory.StartsWith("Assets/" + m_strPackByFolderPath))
            {
                return true;
            }
            if (directory.StartsWith("Assets/" + m_strUguiPath))
            {
                return true;
            }

            return false;
        }
        private bool FixName(ref string curName)
        {
            if (string.IsNullOrEmpty(curName))
            {
                curName = string.Empty;
                return true;
            }
            bool isChange = false;
            StringBuilder newName = new StringBuilder();

            for (int i = 0; i < curName.Length; ++i)
            {
                if (IsNameLegal(curName[i]))
                {
                    newName.Append(curName[i]);
                }
                else
                {
                    isChange = true;
                    newName.Append("_");
                }
            }
            curName = newName.ToString();
            return isChange;
        }
        private bool IsNameLegal(char str)
        {
            if (str >= '0' && str <= '9')
            {
                return true;
            }
            if (str >= 'a' && str <= 'z')
            {
                return true;
            }
            if (str >= 'A' && str <= 'Z')
            {
                return true;
            }
            if (str == '_')
            {
                return true;
            }
            if (str == '-')
            {
                return true;
            }
            return false;
        }
        private string CutFullPathToDirectory(string fullPath)
        {
            int index = fullPath.LastIndexOf('/');
            int index1 = fullPath.LastIndexOf('.');
            if(index1 > index)
            {
                return fullPath.Substring(0, index + 1);
            }
            return fullPath;
        }
        private void CheckAutoFixedNameIsLegal(string lastName,string newName)
        {
            if(m_AutoFixedNameList.ContainsKey(newName))
            {
                if(m_AutoFixedNameList[newName].Count > 0)
                {
                    var lastNameList = m_AutoFixedNameList[newName];
                    for(int i=0;i<lastNameList.Count;++i)
                    {
                        var tmplastName = lastNameList[i];
                        if(tmplastName != lastName)
                        {
                            string errorInfo = "auto fix name with error " + lastName + " " + m_AutoFixedNameList[newName][0];
                            m_ErrorInfo = new Exception(errorInfo);
                            throw m_ErrorInfo;
                        }
                    }                    
                }
                m_AutoFixedNameList[newName].Add(lastName);
            }
            else
            {
                m_AutoFixedNameList.Add(newName, new List<string>() { lastName });
            }
        }
        private void GenReport(string lastName,string newName)
        {
            if(string.IsNullOrEmpty(newName) || string.IsNullOrEmpty(lastName))
            {
                return;
            }
            if (m_BundleNameReport.ContainsKey(lastName))
            {
                if (m_BundleNameReport[lastName] != newName)
                {
                    Debug.LogError(lastName + " " + newName + " " + m_BundleNameReport[lastName]);
                }
            }
            else
            {
                m_BundleNameReport.Add(lastName, newName);
            }
        }
        #endregion
    }
}
