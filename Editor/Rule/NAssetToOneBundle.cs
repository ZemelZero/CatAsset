﻿using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CatAsset.Editor
{
    /// <summary>
    /// 将指定目录下所有资源构建为一个资源包
    /// </summary>
    public class NAssetToOneBundle : IBundleBuildRule
    {
        public virtual List<BundleBuildInfo> GetBundleList(BundleBuildDirectory bundleBuildDirectory)
        {
            List<BundleBuildInfo> result = new List<BundleBuildInfo>();

            if (Directory.Exists(bundleBuildDirectory.DirectoryName))
            {
                //此构建规则只返回一个资源包
                BundleBuildInfo info = GetNAssetToOneBundle(bundleBuildDirectory.DirectoryName, bundleBuildDirectory.Group);
                if (info != null)
                {
                    result.Add(info);
                }
            }

            return result;
        }

        /// <summary>
        /// 将指定目录下所有资源构建为一个资源包
        /// </summary>
        protected BundleBuildInfo GetNAssetToOneBundle(string targetDirectory,string group)
        {
            //注意：targetDirectory在这里被假设为一个形如Assets/xxx/yyy....格式的目录
            
            DirectoryInfo dirInfo = new DirectoryInfo(targetDirectory);
            FileInfo[] files = dirInfo.GetFiles("*", SearchOption.AllDirectories);  //递归获取所有文件
            List<string> assetNames = new List<string>();
            
            foreach (FileInfo file in files)
            {
                if (Util.ExcludeSet.Contains(file.Extension))
                {
                    continue;
                }
                    
                int index = file.FullName.IndexOf("Assets\\");
                string path = file.FullName.Substring(index); //获取Asset开头的资源全路径
                assetNames.Add(path.Replace('\\', '/'));
            }

            if (assetNames.Count == 0)
            {
                //是空目录
                return null;
            }

            int firstIndex = targetDirectory.IndexOf("/");
            int lastIndex = targetDirectory.LastIndexOf("/");
            string directoryName = targetDirectory.Substring(firstIndex + 1, lastIndex - firstIndex - 1);
            string bundleName = targetDirectory.Substring(lastIndex + 1).ToLower() + ".bundle"; //以目录名作为资源包名
            
            BundleBuildInfo bundleBuildInfo = new BundleBuildInfo(directoryName,bundleName,group,false);
            for (int i = 0; i < assetNames.Count; i++)
            {
                AssetBuildInfo assetBuildInfo = new AssetBuildInfo(assetNames[i]);
                bundleBuildInfo.Assets.Add(assetBuildInfo);
            }

            return bundleBuildInfo;
        }
    }
}