﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Helper.SystemInfo;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.Version;
using FileInfo = ProjBobcat.Class.Model.FileInfo;

namespace ProjBobcat.DefaultComponent.Launch;

/// <summary>
///     默认的版本定位器
/// </summary>
public sealed class DefaultVersionLocator : VersionLocatorBase
{
#if WINDOWS
    public const string OS_Symbol = "windows";
#elif OSX
    public const string OS_Symbol = "osx";
#elif LINUX
    public const string OS_Symbol = "linux";
#endif

    /// <summary>
    ///     构造函数。
    ///     Constructor.
    /// </summary>
    /// <param name="rootPath">指.minecraft/ Refers to .minecraft/</param>
    /// <param name="clientToken"></param>
    public DefaultVersionLocator(string rootPath, Guid clientToken)
    {
        RootPath = rootPath; // .minecraft/
        LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken);

        //防止给定路径不存在的时候Parser遍历文件夹爆炸。
        //Prevents errors in the parser's folder traversal when the given path does not exist.
        if (!Directory.Exists(GamePathHelper.GetVersionPath(RootPath)))
            Directory.CreateDirectory(GamePathHelper.GetVersionPath(RootPath));
    }

    public override IEnumerable<VersionInfo> GetAllGames()
    {
        // 把每个DirectoryInfo类映射到VersionInfo类。
        // Map each DirectoryInfo dir to VersionInfo class.
        var di = new DirectoryInfo(GamePathHelper.GetVersionPath(RootPath));

        foreach (var dir in di.EnumerateDirectories())
        {
            var version = ToVersion(dir.Name);
            if (version == null) continue;
            yield return version;
        }
    }

    public override VersionInfo GetGame(string id)
    {
        var version = ToVersion(id);
        return version;
    }

    public override IEnumerable<string> ParseJvmArguments(IEnumerable<object> arguments)
    {
        if (!(arguments?.Any() ?? false))
            yield break;

        /*
        var pArgIndex = arguments.IndexOf("-p");
        if (pArgIndex != -1)
        {
            if (arguments[pArgIndex + 1] is string pArg)
            {
                arguments[pArgIndex + 1] = $"\"{pArg.Replace('/', '\\')}\"";
            }
        }

        
        var legacyLibPathIndex = arguments.IndexOf("-DlibraryDirectory=${library_directory}");
        if (legacyLibPathIndex != -1)
        {
            if (arguments[legacyLibPathIndex] is string pArg)
            {
                arguments[legacyLibPathIndex] = pArg.Replace("${library_directory}", $"\"${{library_directory}}\"");
            }
        }
        */

        foreach (var jvmRule in arguments)
        {
            if (jvmRule is not JObject jvmRuleObj)
            {
                yield return jvmRule.ToString();
                continue;
            }

            var flag = true;
            if (jvmRuleObj.ContainsKey("rules"))
                foreach (var rule in jvmRuleObj["rules"].Select(r => r.ToObject<JvmRules>()))
                {
                    if (rule.OperatingSystem.ContainsKey("arch"))
                    {
                        flag = rule.Action.Equals("allow", StringComparison.Ordinal) &&
                               rule.OperatingSystem["arch"].Equals(SystemArch.CurrentArch.ToString(),
                                   StringComparison.Ordinal);
                        break;
                    }

                    if (!rule.OperatingSystem.ContainsKey("version"))
                        flag = rule.Action.Equals("allow", StringComparison.Ordinal) &&
                               rule.OperatingSystem["name"].Equals(OS_Symbol, StringComparison.Ordinal);
                    else
                        flag = rule.Action.Equals("allow", StringComparison.Ordinal) &&
                               rule.OperatingSystem["name"].Equals(OS_Symbol, StringComparison.Ordinal) &&
                               rule.OperatingSystem["version"].Equals($"^{WindowsSystemVersion.CurrentVersion}\\.",
                                   StringComparison.Ordinal);
                }

            if (!flag) continue;

            if (!jvmRuleObj.ContainsKey("value")) continue;

            if (jvmRuleObj["value"].Type == JTokenType.Array)
                foreach (var arg in jvmRuleObj["value"])
                    yield return StringHelper.FixArgument(arg.ToString()); // arg.ToString();
            else
                yield return
                    StringHelper.FixArgument(jvmRuleObj["value"].ToString()); // jvmRuleObj["value"].ToString();
        }
    }

    /// <summary>
    ///     解析游戏参数
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns></returns>
    private protected override (IEnumerable<string>, Dictionary<string, string>) ParseGameArguments(
        (string, List<object>) arguments)
    {
        var argList = new List<string>();
        var availableArguments = new Dictionary<string, string>();

        var (item1, item2) = arguments;
        if (!string.IsNullOrEmpty(item1))
        {
            argList.Add(item1);
            return (argList, availableArguments);
        }

        if (!(item2?.Any() ?? false))
            return (argList, availableArguments);

        foreach (var gameRule in item2)
        {
            if (gameRule is not JObject gameRuleObj)
            {
                argList.Add(gameRule.ToString());
                continue;
            }

            if (!gameRuleObj.ContainsKey("rules")) continue;

            var ruleKey = string.Empty;
            var ruleValue = string.Empty;

            foreach (var rule in gameRuleObj["rules"].Select(r => r.ToObject<GameRules>()))
            {
                if (!rule.Action.Equals("allow", StringComparison.Ordinal)) continue;
                if (!rule.Features.Any()) continue;
                if (!rule.Features.First().Value) continue;

                ruleKey = rule.Features.First().Key;

                if (!gameRuleObj.ContainsKey("value")) continue;
                ruleValue = gameRuleObj["value"].Type == JTokenType.String
                    ? gameRuleObj["value"].ToString()
                    : string.Join(" ", gameRuleObj["value"]);
            }

            if (!string.IsNullOrEmpty(ruleValue)) availableArguments.Add(ruleKey, ruleValue);
        }

        return (argList, availableArguments);
    }

    /// <summary>
    ///     获取Natives与Libraries（核心依赖）列表
    ///     Fetch list of Natives and Libraries.
    /// </summary>
    /// <param name="libraries">反序列化后的库数据。Deserialized library data.</param>
    /// <returns>二元组（包含一组list，T1是Natives列表，T2是Libraries列表）。A tuple.(T1 -> Natives, T2 -> Libraries)</returns>
    public override (List<NativeFileInfo>, List<FileInfo>) GetNatives(IEnumerable<Library> libraries)
    {
        var result = (new List<NativeFileInfo>(), new List<FileInfo>());
        var isForge = libraries.Any(l => l.Name.Contains("minecraftforge", StringComparison.OrdinalIgnoreCase));

        // 扫描库数据。
        // Scan the library data.
        foreach (var lib in libraries)
        {
            if (!lib.ClientRequired && !isForge) continue;
            if (!lib.Rules.CheckAllow()) continue;

            // 不同版本的Minecraft有不同的library JSON字符串的结构。
            // Different versions of Minecraft have different library JSON's structure.

            var isNative = lib.Natives?.Any() ?? false;
            if (isNative)
            {
                var key = lib.Natives.ContainsKey(OS_Symbol)
                    ? lib.Natives[OS_Symbol].Replace("${arch}", SystemArch.CurrentArch.ToString("{0}"))
                    : $"natives-{OS_Symbol}";

                FileInfo libFi;
                if (lib.Downloads?.Classifiers?.ContainsKey(key) ?? false)
                {
                    lib.Downloads.Classifiers[key].Name = lib.Name;
                    libFi = lib.Downloads.Classifiers[key];
                }
                else
                {
                    var libName = lib.Name;

                    if (!lib.Name.EndsWith($":{key}", StringComparison.OrdinalIgnoreCase)) libName += $":{key}";

                    var mavenInfo = libName.ResolveMavenString();
                    var downloadUrl = string.IsNullOrEmpty(lib.Url)
                        ? mavenInfo.OrganizationName.Equals("net.minecraftforge", StringComparison.Ordinal)
                            ? "https://files.minecraftforge.net/maven/"
                            : "https://libraries.minecraft.net/"
                        : lib.Url;

                    libFi = new FileInfo
                    {
                        Name = lib.Name,
                        Url = $"{downloadUrl}{mavenInfo.Path}",
                        Path = mavenInfo.Path
                    };
                }

                result.Item1.Add(new NativeFileInfo
                {
                    Extract = lib.Extract,
                    FileInfo = libFi
                });

                continue;
            }

            if (lib.Downloads == null)
            {
                // 一些Library项不包含下载数据，所以我们直接解析Maven的名称来猜测下载链接。
                // Some library items don't contain download data, so we directly resolve maven's name to guess the download link.
                var mavenInfo = lib.Name.ResolveMavenString();
                var downloadUrl = string.IsNullOrEmpty(lib.Url)
                    ? mavenInfo.OrganizationName.Equals("net.minecraftforge", StringComparison.Ordinal)
                        ? "https://files.minecraftforge.net/maven/"
                        : "https://libraries.minecraft.net/"
                    : lib.Url;

                result.Item2.Add(new FileInfo
                {
                    Name = lib.Name,
                    Path = mavenInfo.Path,
                    Url = $"{downloadUrl}{mavenInfo.Path}"
                });
                continue;
            }

            if (lib.Downloads?.Artifact != null)
            {
                if (lib.Downloads.Artifact.Name == null)
                {
                    lib.Downloads.Artifact.Name = lib.Name;

                    if (!result.Item2.Any(l => l.Name.Equals(lib.Name, StringComparison.OrdinalIgnoreCase)))
                        result.Item2.Add(lib.Downloads.Artifact);
                }
            }
            else
            {
                if (!(lib.Natives?.Any() ?? false))
                    if (!result.Item2.Any(l => l.Name.Equals(lib.Name, StringComparison.OrdinalIgnoreCase)))
                        result.Item2.Add(new FileInfo
                        {
                            Name = lib.Name
                        });
            }
        }

        return result;
    }

    /// <summary>
    ///     反序列化基础游戏JSON信息，以供解析器处理。
    ///     Deserialize basic JSON data of the game to make the data processable for the analyzer.
    /// </summary>
    /// <param name="id">游戏文件夹名。Name of the game's folder.</param>
    /// <returns></returns>
    public override RawVersionModel ParseRawVersion(string id)
    {
        // 预防I/O的错误。
        // Prevents errors related to I/O.
        if (!Directory.Exists(Path.Combine(RootPath, GamePathHelper.GetGamePath(id))))
            return null;
        if (!File.Exists(GamePathHelper.GetGameJsonPath(RootPath, id)))
            return null;

        var versionJson =
            JsonConvert.DeserializeObject<RawVersionModel>(
                File.ReadAllText(GamePathHelper.GetGameJsonPath(RootPath, id)));

        if (string.IsNullOrEmpty(versionJson.MainClass))
            return null;
        if (string.IsNullOrEmpty(versionJson.MinecraftArguments) && versionJson.Arguments == null)
            return null;

        return versionJson;
    }

    /// <summary>
    ///     游戏信息解析。
    ///     Game info analysis.
    /// </summary>
    /// <param name="id">游戏文件夹名。Name of the game version's folder.</param>
    /// <returns>一个VersionInfo类。A VersionInfo class.</returns>
    private protected override VersionInfo ToVersion(string id)
    {
        // 反序列化。
        // Deserialize.
        var rawVersion = ParseRawVersion(id);
        if (rawVersion == null)
            return null;

        List<RawVersionModel> inherits = null;
        // 检查游戏是否存在继承关系。
        // Check if there is inheritance.
        if (!string.IsNullOrEmpty(rawVersion.InheritsFrom))
        {
            // 存在继承关系。
            // Inheritance exists.

            inherits = new List<RawVersionModel>();
            var current = rawVersion;
            var first = true;

            // 递归式地将所有反序列化的版本继承塞进一个表中。
            // Add all deserialized inherited version to a list recursively.
            while (current != null && !string.IsNullOrEmpty(current.InheritsFrom))
            {
                if (first)
                {
                    inherits.Add(current);
                    first = false;
                    current = ParseRawVersion(current.InheritsFrom);
                    inherits.Add(current);
                    continue;
                }

                inherits.Add(ParseRawVersion(current.InheritsFrom));
                current = ParseRawVersion(current.InheritsFrom);
            }

            if (inherits.Contains(null)) return null;
        }

        // 生成一个随机的名字来防止重复。
        // Generates a random name to avoid duplication.
        /*
        var rs = new RandomStringHelper().UseLower().UseUpper().UseNumbers().Shuffle(1);
        var randomName =
            $"{id}-{rs.Generate(5)}-{rs.Generate(5)}";
        */

        var result = new VersionInfo
        {
            Assets = rawVersion.AssetsVersion,
            AssetInfo = rawVersion.AssetIndex,
            MainClass = rawVersion.MainClass,
            Libraries = new List<FileInfo>(),
            Natives = new List<NativeFileInfo>(),
            Id = rawVersion.Id,
            DirName = id,
            Name = id, //randomName,
            JavaVersion = rawVersion.JavaVersion
        };

        // 检查游戏是否存在继承关系。
        // Check if there is inheritance.
        if (inherits?.Any() ?? false)
        {
            // 存在继承关系。
            // Inheritance exists.

            var flag = true;
            var jvmArgList = new List<string>();
            var gameArgList = new List<string>();

            result.RootVersion = inherits.Last().Id;

            // 遍历所有的继承
            // Go through all inherits
            for (var i = inherits.Count - 1; i >= 0; i--)
            {
                if (result.JavaVersion == null && inherits[i].JavaVersion != null)
                    result.JavaVersion = inherits[i].JavaVersion;
                if (result.AssetInfo == null && inherits[i].AssetIndex != null)
                    result.AssetInfo = inherits[i].AssetIndex;

                if (flag)
                {
                    var rootLibs = GetNatives(inherits[i].Libraries);

                    result.Libraries = rootLibs.Item2;
                    result.Natives = rootLibs.Item1;

                    jvmArgList.AddRange(ParseJvmArguments(inherits[i].Arguments?.Jvm));

                    var rootArgs = ParseGameArguments((inherits[i].MinecraftArguments,
                        inherits[i].Arguments?.Game));

                    gameArgList.AddRange(rootArgs.Item1);
                    result.AvailableGameArguments = rootArgs.Item2;

                    flag = false;
                    continue;
                }

                var middleLibs = GetNatives(inherits[i].Libraries);

                // result.Libraries.AddRange(middleLibs.Item2);

                foreach (var mL in middleLibs.Item2)
                {
                    var mLMaven = mL.Name.ResolveMavenString();
                    var mLFlag = false;

                    for (var j = 0; j < result.Libraries.Count; j++)
                    {
                        var lMaven = result.Libraries[j].Name.ResolveMavenString();
                        if (!lMaven.GetMavenFullName().Equals(mLMaven.GetMavenFullName(), StringComparison.Ordinal))
                            continue;

                        var v1 = new ComparableVersion(lMaven.Version);
                        var v2 = new ComparableVersion(mLMaven.Version);

                        if (v2 > v1)
                            result.Libraries[j] = mL;

                        mLFlag = true;
                    }

                    if (mLFlag)
                        continue;

                    result.Libraries.Add(mL);
                }


                var currentNativesNames = new List<string>();
                result.Natives.ForEach(n => { currentNativesNames.Add(n.FileInfo.Name); });
                var moreMiddleNatives =
                    middleLibs.Item1.AsParallel().Where(mL => !currentNativesNames.Contains(mL.FileInfo.Name))
                        .ToList();
                result.Natives.AddRange(moreMiddleNatives);


                var jvmArgs = ParseJvmArguments(inherits[i].Arguments?.Jvm);
                var middleGameArgs = ParseGameArguments(
                    (inherits[i].MinecraftArguments, inherits[i].Arguments?.Game));

                if (string.IsNullOrEmpty(inherits[i].MinecraftArguments))
                {
                    jvmArgList.AddRange(jvmArgs);
                    gameArgList.AddRange(middleGameArgs.Item1);
                    result.AvailableGameArguments = result.AvailableGameArguments
                        .Union(middleGameArgs.Item2)
                        .ToDictionary(x => x.Key, y => y.Value);
                }
                else
                {
                    result.JvmArguments = jvmArgs;
                    result.GameArguments = middleGameArgs.Item1;
                    result.AvailableGameArguments = middleGameArgs.Item2;
                }

                result.Id = inherits[i].Id ?? result.Id;
                result.MainClass = inherits[i].MainClass ?? result.MainClass;
            }

            var finalJvmArgs = result.JvmArguments?.ToList() ?? new List<string>();
            finalJvmArgs.AddRange(jvmArgList);
            result.JvmArguments = finalJvmArgs; //.Distinct();

            var finalGameArgs = result.GameArguments?.ToList() ?? new List<string>();
            finalGameArgs.AddRange(gameArgList);
            finalGameArgs = finalGameArgs.Select(arg => arg.Split(' ')).SelectMany(a => a).Distinct().ToList();
            result.GameArguments = finalGameArgs; //.Distinct();

            goto ProcessProfile;
        }

        var libs = GetNatives(rawVersion.Libraries);
        result.Libraries = libs.Item2;
        result.Natives = libs.Item1;

        result.JvmArguments = ParseJvmArguments(rawVersion.Arguments?.Jvm);

        var gameArgs =
            ParseGameArguments((rawVersion.MinecraftArguments,
                rawVersion.Arguments?.Game));
        result.GameArguments = gameArgs.Item1;
        result.AvailableGameArguments = gameArgs.Item2;

        ProcessProfile:
        var oldProfile = LauncherProfileParser.LauncherProfile.Profiles.FirstOrDefault(p =>
            p.Value.LastVersionId?.Equals(id, StringComparison.Ordinal) ?? true);

        var gamePath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
        if (oldProfile.Equals(default(KeyValuePair<string, GameProfileModel>)))
        {
            LauncherProfileParser.LauncherProfile.Profiles.Add(id.ToGuidHash().ToString("N"),
                new GameProfileModel
                {
                    GameDir = gamePath,
                    LastVersionId = id,
                    Name = id, // randomName,
                    Created = DateTime.Now
                });
            LauncherProfileParser.SaveProfile();
            return result;
        }

        result.Name = oldProfile.Value.Name;
        oldProfile.Value.GameDir = gamePath;
        oldProfile.Value.LastVersionId = id;
        LauncherProfileParser.LauncherProfile.Profiles[oldProfile.Key] = oldProfile.Value;
        LauncherProfileParser.SaveProfile();

        return result;
    }
}