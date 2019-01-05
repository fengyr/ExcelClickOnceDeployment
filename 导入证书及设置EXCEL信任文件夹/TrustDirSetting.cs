﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace 导入证书及设置EXCEL信任文件夹
{

    class TrustDirSetting
    {
        public static void SettingTrustDir(string trustDir)
        {
            if (Environment.Is64BitOperatingSystem)
            {
                //64位的EXCEL
                AddTrustDirToRegister(trustDir, RegistryView.Registry64);
            }
            //32位的EXCEL
            AddTrustDirToRegister(trustDir, RegistryView.Registry32);
        }

        private static void AddTrustDirToRegister(string trustDir, RegistryView registryView)
        {
            List<string> listSubKeys = new List<string>();
            var localKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, registryView);
            RegisterManager registerManager = new RegisterManager() { BaseKey = localKey };
            registerManager.EnumerateKeyNames(@"Software\Microsoft\Office", ref listSubKeys);
            CreateTargetSubKey(registerManager, listSubKeys, localKey, trustDir);
        }

        private static void CreateTargetSubKey(RegisterManager registerManager, List<string> listSubKeys, RegistryKey localKey, string trustDir)
        {
            var regAllowNetworkLocations = listSubKeys.Where(s => s.EndsWith(@"Excel\Security\Trusted Locations"));
            //设置信任网络路径
            foreach (var item in regAllowNetworkLocations)
            {
                registerManager.SetRegeditKeyValue(item, "AllowNetworkLocations", "1");
            }


            //包含EXCEL字样的，并且有location节点
            var listSecurity = listSubKeys.Where(s => s.Contains(@"Excel\Security\Trusted Locations")).Where(s => Regex.IsMatch(s, @"Location\d+$")).ToList();

            foreach (var item in listSecurity)
            {
                if (registerManager.IsRegeditKeyAndValueExist(item, "Path", trustDir))
                {
                    return;
                }
            };

            var result = from s in listSecurity
                         select new { GroupName = Regex.Match(s, @".+?\\.+?\\.+?\\.+?\\").Value, Fullpath = s };

            //按HKEY_CURRENT_USER\Software\Microsoft\Office\15.0分组，防止多个EXCEL版本的原因引起信任位置添加不全
            var query = from s in result
                        group s by s.GroupName;

            foreach (var item in query)
            {
                //只取第1条记录，去掉最后一个尾数
                string locationName = Regex.Match(item.First().Fullpath, @".+Location").Value;
                //用最后的尾数来比较大小，不是用字符串，可以最终比较出11比2大
                int locationIndex = item.Max(s => int.Parse(Regex.Match(s.Fullpath, @".+Location(\d+)").Groups[1].Value) + 1);
                string newLocationName = Regex.Match(locationName, ".+Location").Value + locationIndex;
                RegistryKey Location = localKey.CreateSubKey(newLocationName);
                Location.SetValue("Path", trustDir);
                Location.SetValue("AllowSubfolders", "00000001", RegistryValueKind.DWord);
                Location.SetValue("Date", DateTime.Now.ToString());
                Location.SetValue("Description", "");
            }
        }



    }

    internal class RegisterManager
    {
        public RegistryKey BaseKey { get; set; }
        private bool IsRegeditKeyExist(string subKeyString, string key)
        {
            string[] subkeyNames;
            RegistryKey subKey = this.BaseKey.OpenSubKey(subKeyString);
            subkeyNames = subKey.GetValueNames();
            //取得该项下所有键值的名称的序列，并传递给预定的数组中
            foreach (string keyName in subkeyNames)
            {
                if (keyName == key) //判断键值的名称
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsRegeditKeyAndValueExist(string subKeyString, string key, string valueString)
        {
            string[] subkeyNames;
            RegistryKey subKey = this.BaseKey.OpenSubKey(subKeyString);
            subkeyNames = subKey.GetValueNames();
            //取得该项下所有键值的名称的序列，并传递给预定的数组中
            foreach (string keyName in subkeyNames)
            {
                if (keyName == key) //判断键值的名称
                {
                    if (subKey.GetValue(key).ToString() == valueString)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void SetRegeditKeyValue(string subKeyString, string key, string valueString)
        {
            RegistryKey subKey = this.BaseKey.OpenSubKey(subKeyString, true);

            subKey.SetValue(key, valueString, RegistryValueKind.DWord);

        }


        public void EnumerateKeyNames(string fatherKey, ref List<string> listSubKeys)
        {
            RegistryKey RegKey = this.BaseKey.OpenSubKey(fatherKey);
            string[] subKeys = RegKey.GetSubKeyNames();
            foreach (string subKey in subKeys)
            {
                string fullPath = fatherKey + "\\" + subKey;
                EnumerateKeyNames(fullPath, ref listSubKeys);
                listSubKeys.Add(fullPath);
            }
        }
    }
}
