﻿using System.IO;
using CatAsset.Runtime;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;

namespace CatAsset.Editor
{
    /// <summary>
    /// 写入资源清单文件
    /// </summary>
    public class WriteManifestFile : IBuildTask
    {
        [InjectContext(ContextUsage.In)] 
        private IManifestParam manifestParam;
 
        /// <inheritdoc />
        public int Version => 1;
        
        public ReturnCode Run()
        {
            string writePath = manifestParam.WritePath;
            CatAssetManifest manifest = manifestParam.Manifest;
                
            //写入清单文件json
            string json = CatJson.JsonParser.ToJson(manifest);
            using (StreamWriter sw = new StreamWriter(Path.Combine(writePath, CatAsset.Runtime.Util.ManifestFileName)))
            {
                sw.Write(json);
            }

            return ReturnCode.Success;
        }

      
    }
}