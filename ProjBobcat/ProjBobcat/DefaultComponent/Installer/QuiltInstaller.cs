﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Quilt;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Installer;

public class QuiltInstaller : InstallerBase, IQuiltInstaller
{
    const string DefaultMetaUrl = "https://meta.quiltmc.org";

    static HttpClient Client => HttpClientHelper.GetNewClient(HttpClientHelper.DefaultClientName);

    public QuiltLoaderModel LoaderArtifact { get; set; }

    public string Install()
    {
        return InstallTaskAsync().Result;
    }

    public async Task<string> InstallTaskAsync()
    {
        if (string.IsNullOrEmpty(InheritsFrom))
            throw new NullReferenceException("InheritsFrom 不能为 null");
        if (string.IsNullOrEmpty(RootPath))
            throw new NullReferenceException("RootPath 不能为 null");

        InvokeStatusChangedEvent("开始安装", 0);

        var url = $"{DefaultMetaUrl}/v3/versions/loader/{InheritsFrom}/{LoaderArtifact.Version}/profile/json";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var res = await Client.SendAsync(req);

        res.EnsureSuccessStatusCode();

        var resStr = await res.Content.ReadAsStringAsync();
        var versionModel = JsonConvert.DeserializeObject<RawVersionModel>(resStr);

        InvokeStatusChangedEvent("生成版本总成", 70);

        if (versionModel == null)
            throw new NullReferenceException(nameof(versionModel));

        var hashed = versionModel.Libraries.FirstOrDefault(l =>
            l.Name.StartsWith("org.quiltmc:hashed", StringComparison.OrdinalIgnoreCase));

        if (hashed != default)
        {
            var index = versionModel.Libraries.IndexOf(hashed);

            hashed.Name = hashed.Name.Replace("org.quiltmc:hashed", "net.fabricmc:intermediary");

            if (!string.IsNullOrEmpty(hashed.Url)) hashed.Url = "https://maven.fabricmc.net/";

            versionModel.Libraries[index] = hashed;
        }

        if (!string.IsNullOrEmpty(CustomId))
            versionModel.Id = CustomId;

        var id = versionModel.Id;
        var installPath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
        var di = new DirectoryInfo(installPath);

        if (!di.Exists)
            di.Create();
        else
            DirectoryHelper.CleanDirectory(di.FullName);

        var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);
        var jsonContent = JsonConvert.SerializeObject(versionModel, JsonHelper.CamelCasePropertyNamesSettings);

        InvokeStatusChangedEvent("将版本 Json 写入文件", 90);

        await File.WriteAllTextAsync(jsonPath, jsonContent);

        InvokeStatusChangedEvent("安装完成", 100);

        return id;
    }
}