﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatAsset.Runtime;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;

namespace CatAsset.Editor
{
    /// <summary>
    /// 填充资源包构建信息参数
    /// </summary>
    public class FillBuildInfoParam : IBuildTask
    {
        public int Version { get; }
        
        
        [InjectContext(ContextUsage.In)]
        private IBundleBuildParameters buildParam;

        [InjectContext(ContextUsage.InOut)]
        private IBundleBuildInfoParam buildInfoParam;
        
        [InjectContext(ContextUsage.In)]
        private IBundleBuildConfigParam configParam;

        [InjectContext(ContextUsage.In)]
        private IBuildCache buildCache;
        
        [InjectContext(ContextUsage.InOut)]
        private IBundleBuildContent content;

        [InjectContext(ContextUsage.InOut)]
        private IBuildContent content2;
        
        public ReturnCode Run()
        {
            BundleBuildConfigSO config = configParam.Config;

            if (!configParam.IsBuildPatch)
            {
                //构建完整资源包
                buildInfoParam = new BundleBuildInfoParam(config.GetAssetBundleBuilds(),config.GetNormalBundleBuilds(),
                    config.GetRawBundleBuilds());
            }
            else
            {

                //构建补丁资源包
                
                //双向依赖记录
                Dictionary<string, List<string>> upStreamDict = new Dictionary<string, List<string>>();
                Dictionary<string, List<string>> downStreamDict = new Dictionary<string, List<string>>();
                Dictionary<string, string> assetToBundle = new Dictionary<string, string>();
                foreach (var bundle in config.Bundles)
                {
                    foreach (var asset in bundle.Assets)
                    {
                        //上游依赖
                        var deps = EditorUtil.GetDependencies(asset.Name);
                        upStreamDict.Add(asset.Name,deps);

                        //下游依赖
                        foreach (string dep in deps)
                        {
                            if (!downStreamDict.TryGetValue(dep,out List<string> list))
                            {
                                list = new List<string>();
                                downStreamDict.Add(dep,list);
                            }
                            list.Add(asset.Name);
                        }
                        
                        assetToBundle.Add(asset.Name,bundle.BundleIdentifyName);
                    }
                }

                //读取上次完整构建时的资源包信息
                string folder = EditorUtil.GetBundleCacheFolder(config.OutputRootDirectory, configParam.TargetPlatform);
                string path = RuntimeUtil.GetRegularPath(Path.Combine(folder, CatAssetManifest.ManifestJsonFileName));
                CatAssetManifest cachedManifest = CatAssetManifest.DeserializeFromJson(File.ReadAllText(path));
                Dictionary<string, string> cacheAssetToBundle = new Dictionary<string, string>();
                foreach (var bundle in cachedManifest.Bundles)
                {
                    foreach (var asset in bundle.Assets)
                    {
                        cacheAssetToBundle.Add(asset.Name,bundle.BundleIdentifyName);
                    }
                }
                
                //计算补丁包
                //读取上次完整构建时的资源文件MD5信息
                folder = EditorUtil.GetAssetCacheManifestFolder(config.OutputRootDirectory);
                path = RuntimeUtil.GetRegularPath(Path.Combine(folder, AssetCacheManifest.ManifestJsonFileName));
                string json = File.ReadAllText(path);
                AssetCacheManifest assetCacheManifest = JsonUtility.FromJson<AssetCacheManifest>(json);
                Dictionary<string, string> cacheMD5Dict = assetCacheManifest.GetCacheDict();
                
                //当前资源文件的md5字典
                Dictionary<string, string> curMD5Dict = new Dictionary<string, string>();
                
                //资源名 -> 是否已变化
                Dictionary<string, bool> assetChangeStateDict = new Dictionary<string, bool>();

                //深拷贝一份构建配置进行操作
                BundleBuildConfigSO clonedConfig = Object.Instantiate(config);  
                
                for (int i = clonedConfig.Bundles.Count - 1; i >= 0; i--)
                {
                    var bundle = clonedConfig.Bundles[i];

                    for (int j = bundle.Assets.Count - 1; j >= 0; j--)
                    {
                        var asset = bundle.Assets[j];

                        //1.自身是否变化
                        bool isChanged = IsChangedAsset(asset.Name, assetChangeStateDict, cacheMD5Dict, curMD5Dict,
                            assetToBundle, cacheAssetToBundle);

                        if (!isChanged)
                        {
                            //2.此资源依赖的资源是否变化
                            if (upStreamDict.TryGetValue(asset.Name,out var upStreamList))
                            {
                                foreach (string upStream in upStreamList)
                                {
                                    isChanged = IsChangedAsset(upStream, assetChangeStateDict, cacheMD5Dict, curMD5Dict,
                                        assetToBundle, cacheAssetToBundle);
                                    if (isChanged)
                                    {
                                        break;
                                    }
                                }
                            }
                        }

                        if (!isChanged)
                        {
                            //3.依赖此资源的资源是否变化
                            if (downStreamDict.TryGetValue(asset.Name,out var downStreamList))
                            {
                                foreach (string downStream in downStreamList)
                                {
                                    isChanged = IsChangedAsset(downStream, assetChangeStateDict, cacheMD5Dict, curMD5Dict,
                                        assetToBundle, cacheAssetToBundle);
                                    if (isChanged)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (isChanged)
                        {
                            Debug.Log($"发现补丁资源:{asset.Name}");
                        }
                        else
                        {
                            //移除非补丁资源
                            bundle.Assets.RemoveAt(j);
                        }
                    }

                    if (bundle.Assets.Count > 0)
                    {
                        //是补丁包 需要改名
                        var part = bundle.BundleName.Split('.');
                        bundle.BundleName = $"{part[0]}_patch.{part[1]}";
                        bundle.BundleIdentifyName =
                            BundleBuildInfo.GetBundleIdentifyName(bundle.DirectoryName, bundle.BundleName);
                    }
                    else
                    {
                        //不是补丁包 需要移除
                        clonedConfig.Bundles.RemoveAt(i);
                    }
                }
                
                buildInfoParam = new BundleBuildInfoParam(clonedConfig.GetAssetBundleBuilds(),clonedConfig.GetNormalBundleBuilds(),
                    clonedConfig.GetRawBundleBuilds());
            }

            ((BundleBuildParameters)buildParam).SetBundleBuilds(buildInfoParam.NormalBundleBuilds);
            content = new BundleBuildContent(buildInfoParam.AssetBundleBuilds);
            content2 = content;
            
            return ReturnCode.Success;
        }

        /// <summary>
        /// 是否为已变化的资源
        /// </summary>
        private bool IsChangedAsset(string assetName, Dictionary<string, bool> assetChangeStateDict,
            Dictionary<string, string> cacheMD5Dict, Dictionary<string, string> curMD5Dict,
            Dictionary<string, string> assetToBundle, Dictionary<string, string> cacheAssetToBundle)

        {
            //0.已经计算过状态了
            if (assetChangeStateDict.TryGetValue(assetName, out bool isPatch))
            {
                return isPatch;
            }

            //1.新资源 
            if (!cacheMD5Dict.TryGetValue(assetName, out string cachedMD5))
            {
                assetChangeStateDict.Add(assetName, true);
                return true;
            }

            //2.变化的旧资源
            if (!curMD5Dict.TryGetValue(assetName, out string curMD5))
            {
                curMD5 = RuntimeUtil.GetFileMD5(assetName);
                curMD5Dict.Add(assetName, curMD5);
            }
            if (curMD5 != cachedMD5)
            {
                assetChangeStateDict.Add(assetName, true);
                return true;
            }

            //3.被移动到新包的旧资源
            if (assetToBundle[assetName] != cacheAssetToBundle[assetName])
            {
                assetChangeStateDict.Add(assetName, true);
                return true;
            }

            
            
            //未变化
            assetChangeStateDict.Add(assetName, false);
            return false;
        }
    }
}