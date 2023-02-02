﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CatAsset.Runtime
{
    /// <summary>
    /// 加密工具类
    /// </summary>
    public static class EncryptUtil
    {
        /// <summary>
        /// 加密字节长度
        /// </summary>
        public const int EncryptBytesLength = 64;
        
        /// <summary>
        /// 异或加密Key
        /// </summary>
        private const byte encryptXOrKey = 64;
        
        private static Queue<byte[]> cachedBytesQueue = new Queue<byte[]>();
        
        private static byte[] GetCachedBytes(int length)
        {

            if (!cachedBytesQueue.TryDequeue(out var bytes))
            {
                return new byte[length];
            }

            if (bytes.Length < length)
            {
                return new byte[length];
            }

            return bytes;
        }

        private static void ReleaseCachedBytes(byte[] bytes)
        {
            cachedBytesQueue.Enqueue(bytes);
        }
        
        /// <summary>
        /// 偏移加密
        /// </summary>
        public static void EncryptOffset(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            int newLength = bytes.Length + EncryptBytesLength;

            byte[] cachedBytes = GetCachedBytes(newLength);

            //写入额外的头部数据
            for (int i = 0; i < EncryptBytesLength; i++)
            {
                cachedBytes[i] = (byte)UnityEngine.Random.Range(byte.MinValue, byte.MaxValue);
            }

            //写入原始数据
            Array.Copy(bytes,0,cachedBytes,EncryptBytesLength,bytes.Length);

            using (FileStream fs = File.OpenWrite(filePath))
            {
                fs.Write(cachedBytes, 0, cachedBytes.Length);
            }
            
            Array.Clear(cachedBytes,0,cachedBytes.Length);
            ReleaseCachedBytes(cachedBytes);
        }

        /// <summary>
        /// 使用Stream进行异或加密
        /// </summary>
        public static void EncryptXOr(string filePath)
        {
            byte[] cachedBytes = GetCachedBytes(EncryptBytesLength);
            using (FileStream fs = File.Open(filePath, FileMode.Open))
            {
                int _ = fs.Read(cachedBytes, 0, EncryptBytesLength);
                EncryptXOr(cachedBytes);
                fs.Write(cachedBytes,0,encryptXOrKey);
            }
            Array.Clear(cachedBytes,0,cachedBytes.Length);
            ReleaseCachedBytes(cachedBytes);
        }
        
        /// <summary>
        /// 使用二进制数据进行异或加密/解密
        /// </summary>
        public static void EncryptXOr(byte[] bytes,long length = EncryptBytesLength)
        {
            for (long i = 0; i < length; i++)
            {
                bytes[i] ^= encryptXOrKey;
            }
        }
    }
}