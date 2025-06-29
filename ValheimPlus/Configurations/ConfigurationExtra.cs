﻿using BepInEx;
using IniParser;
using IniParser.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using UnityEngine;
using ValheimPlus.Configurations.Sections;

namespace ValheimPlus.Configurations
{
    public class ConfigurationExtra
    {
        public static string GetServerHashFor(Configuration config)
        {
            var serialized = "";
            foreach (var prop in typeof(Configuration).GetProperties())
            {
                var keyName = prop.Name;
                var method = prop.PropertyType.GetMethod("ServerSerializeSection", BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);

                if (method != null)
                {
                    var instance = prop.GetValue(config, null);
                    string result = (string)method.Invoke(instance, new object[] { });
                    serialized += result;
                }
            }

            return Helper.CreateMD5(serialized);
        }

        public static string ConfigIniPath = Path.GetDirectoryName(Paths.BepInExConfigPath) + Path.DirectorySeparatorChar + "valheim_plus.cfg";

        private static string GetCurrentWebIniFile()
        {
            var client = new WebClient();
            client.Headers.Add("User-Agent: V+ Server");
            try
            {
                ValheimPlusPlugin.Logger.LogInfo($"Downloading config from: '{ValheimPlusPlugin.IniFile}'");
                return client.DownloadString(ValheimPlusPlugin.IniFile);
            }
            catch (Exception e)
            {
                ValheimPlusPlugin.Logger.LogError($"Error downloading config from '{ValheimPlusPlugin.IniFile}': {e}");
                return null;
            }
        }

        public static bool LoadSettings()
        {
            try
            {
                if (File.Exists(ConfigIniPath))
                {
                    ValheimPlusPlugin.Logger.LogInfo($"Found config file at: '{ConfigIniPath}'");
                    Configuration.Current = LoadFromIni(ConfigIniPath, verbose: true);
                    FileIniDataParser parser = new FileIniDataParser();
                    IniData configdata = parser.ReadFile(ConfigIniPath);

                    string compareIni = null;
                    if (!Configuration.Current.ValheimPlus.disableConfigAutoUpdates)
                    {
                        try
                        {
                            // get the current versions ini data
                            compareIni = GetCurrentWebIniFile();
                        }
                        catch (Exception) 
                        {
                            ValheimPlusPlugin.Logger.LogWarning("Unable to download config file from the web.");
                        }
                    } 
                    else
                    {
                        ValheimPlusPlugin.Logger.LogWarning("Auto config file updates have been disabled!");
                    }

                    if (compareIni != null)
                    {
                        StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.ASCII.GetBytes(compareIni)));
                        IniData webConfig = parser.ReadData(reader);

                        // Duplication of comments otherwise with this merge function.
                        configdata.ClearAllComments();

                        webConfig.Merge(configdata);
                        parser.WriteFile(ConfigIniPath, webConfig);
                    }
                }
                else
                {
                    ValheimPlusPlugin.Logger.LogWarning($"Error: Configuration not found at: '{ConfigIniPath}'. Trying to download latest config.");

                    // download latest ini if not present
                    bool status = false;
                    try
                    {
                        string defaultIni = GetCurrentWebIniFile();
                        if (defaultIni != null)
                        {
                            File.WriteAllText(ConfigIniPath, defaultIni);
                            ValheimPlusPlugin.Logger.LogInfo($"Default Configuration downloaded to '{ConfigIniPath}'. Loading downloaded default settings.");
                            Configuration.Current = LoadFromIni(ConfigIniPath, verbose: false);
                            status = true;
                        }
                    }
                    catch (Exception) { }

                    return status;
                }
            }
            catch (Exception ex)
            {
                ValheimPlusPlugin.Logger.LogError($"Could not load config file: {ex}");
                return false;
            }

            return true;
        }
        static public bool SyncHotkeys { get; private set; } = false;

        //loading local configuration
        public static Configuration LoadFromIni(string filename, bool verbose)
        {
            FileIniDataParser parser = new FileIniDataParser();
            IniData configdata = parser.ReadFile(filename);
            Configuration conf = new Configuration();
            var configProps = typeof(Configuration).GetProperties();
            Array.Sort(configProps, (o1, o2) => (new CaseInsensitiveComparer()).Compare(o1.Name, o2.Name));
            if (verbose)
            {
                ValheimPlusPlugin.Logger.LogInfo($"Loading config...");
                ValheimPlusPlugin.Logger.LogInfo($"");
            }
            foreach (var prop in configProps)
            {
                string keyName = prop.Name;
                MethodInfo method = prop.PropertyType.GetMethod("LoadIni", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                if (method != null)
                {
                    var result = method.Invoke(null, new object[] { configdata, keyName, verbose });
                    prop.SetValue(conf, result, null);
                }
            }

            return conf;
        }
        /// <summary>
        /// Loading remote configuration
        /// </summary>
        /// <param name="iniStream">remote configuration stream</param>
        /// <returns>new configuration</returns>
        public static Configuration LoadFromIni(Stream iniStream)
        {
            using (StreamReader iniReader = new StreamReader(iniStream))
            {
                FileIniDataParser parser = new FileIniDataParser();
                IniData configdata = parser.ReadData(iniReader);
                var serverSection = configdata[nameof(Configuration.Server)];
                var serverSyncsConfig = serverSection.GetBool(nameof(ServerConfiguration.serverSyncsConfig));
                ValheimPlusPlugin.Logger.LogInfo($"ServerSyncsConfig = {serverSyncsConfig}");

                if (!serverSyncsConfig) return Configuration.Current;

                var serverSyncsHotkeys = Configuration.Current.Server.serverSyncHotkeys;
                ValheimPlusPlugin.Logger.LogInfo($"ServerSyncsHotkeys = {serverSyncsConfig}");
                SyncHotkeys = serverSyncsHotkeys;

                Configuration conf = new Configuration();
                foreach (var prop in typeof(Configuration).GetProperties())
                {
                    string keyName = prop.Name;
                    MethodInfo method = prop.PropertyType.GetMethod("LoadIni",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                    if (method != null)
                    {
                        var verbose = true;
                        object result = method.Invoke(null, new object[] { configdata, keyName, verbose });
                        prop.SetValue(conf, result, null);
                    }
                }

                return conf;
            }
        }
    }
    public static class IniDataExtensions
    {
        public static float GetFloat(this KeyDataCollection data, string key, float defaultVal)
        {
            if (float.TryParse(data[key], NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out var result))
            {
                return result;
            }

            ValheimPlusPlugin.Logger.LogWarning($" [Float] Could not read {key}, using default value of {defaultVal}");
            return defaultVal;
        }

        public static bool GetBool(this KeyDataCollection data, string key)
        {
            var truevals = new[] { "y", "yes", "true", "1", "enabled" };
            return truevals.Contains($"{data[key]}".ToLower());
        }

        public static int GetInt(this KeyDataCollection data, string key, int defaultVal)
        {
            if (int.TryParse(data[key], NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out var result))
            {
                return result;
            }

            ValheimPlusPlugin.Logger.LogWarning($" [Int] Could not read {key}, using default value of {defaultVal}");
            return defaultVal;
        }

        public static object GetEnumValue(this KeyDataCollection data, string key, object defaultVal)
        {
            var enumType = defaultVal.GetType();
            try
            {
                return Enum.Parse(enumType, data[key], true);
            }
            catch
            {
                ValheimPlusPlugin.Logger.LogWarning(
                    $" [{enumType}] Could not read {key}, using default value of {defaultVal}");
                return defaultVal;
            }
        }

        public static object GetFlags(this KeyDataCollection data, string key, object defaultVal)
        {
            var enumType = defaultVal.GetType();
            var flags = new List<object>();

            var values = data[key].Split(',').ToList();
            values.ForEach(x => x.Trim());

            foreach (var opt in values)
            {
                try
                {
                    var flag = Enum.Parse(enumType, opt, true);
                    flags.Add(flag);
                }
                catch
                {
                    ValheimPlusPlugin.Logger.LogWarning($" [{enumType.Name}] Unrecognized value `{opt}` in {key}");
                }
            }

            var value = flags.Aggregate(0, (current, flag) => current | (int)flag);
            try
            {
                return Enum.ToObject(enumType, value);
            }
            catch
            {
                ValheimPlusPlugin.Logger.LogWarning(
                    $" [{enumType}] Could not read {key}, using default value of {defaultVal}");
                return defaultVal;
            }
        }

        public static KeyCode GetKeyCode(this KeyDataCollection data, string key, KeyCode defaultVal)
        {
            if (Enum.TryParse<KeyCode>(data[key].Trim(), out var result))
            {
                return result;
            }

            ValheimPlusPlugin.Logger.LogWarning($" [KeyCode] Could not read {key}, using default value of {defaultVal}");
            return defaultVal;
        }

        // unused and looks broken?
        public static T LoadConfiguration<T>(this IniData data, string key) where T : BaseConfig<T>, new()
        {
            // this function gives null reference error
            KeyDataCollection idata = data[key];
            return (T)typeof(T).GetMethod("LoadIni").Invoke(null, new[] { idata });
        }
    }
}
