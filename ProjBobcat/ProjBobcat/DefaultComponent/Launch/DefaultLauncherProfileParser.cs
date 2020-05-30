﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Exceptions;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch
{
    /// <summary>
    /// 默认的官方launcher_profile.json适配器
    /// </summary>
    public sealed class DefaultLauncherProfileParser : LauncherProfileParserBase, ILauncherProfileParser
    {
        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="clientToken"></param>
        public DefaultLauncherProfileParser(string rootPath, Guid clientToken)
        {
            RootPath = rootPath;

            if (!File.Exists(GamePathHelper.GetLauncherProfilePath(RootPath)))
            {
                var launcherProfile = new LauncherProfileModel
                {
                    AuthenticationDatabase = new Dictionary<string, AuthInfoModel>(),
                    ClientToken = clientToken.ToString("D"),
                    LauncherVersion = new LauncherVersionModel
                    {
                        Format = 1,
                        Name = ""
                    },
                    Profiles = new Dictionary<string, GameProfileModel>()
                };

                LauncherProfile = launcherProfile;

                var launcherProfileJson = JsonConvert.SerializeObject(launcherProfile, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });

                if (!Directory.Exists(RootPath))
                    Directory.CreateDirectory(RootPath);

                FileHelper.Write(GamePathHelper.GetLauncherProfilePath(RootPath), launcherProfileJson);
            }
            else
            {
                var launcherProfileJson =
                    File.ReadAllText(GamePathHelper.GetLauncherProfilePath(rootPath), Encoding.UTF8);
                LauncherProfile = JsonConvert.DeserializeObject<LauncherProfileModel>(launcherProfileJson);
            }
        }

        public LauncherProfileModel LauncherProfile { get; set; }

        public void AddNewAuthInfo(AuthInfoModel authInfo, string guid)
        {
            if (IsAuthInfoExist(guid, authInfo.UserName)) return;
            if (!(LauncherProfile.AuthenticationDatabase?.Any() ?? false))
                LauncherProfile.AuthenticationDatabase = new Dictionary<string, AuthInfoModel>();

            LauncherProfile.AuthenticationDatabase.Add(
                authInfo.Properties.Any() ? authInfo.Properties.First().UserId : authInfo.Profiles.First().Key,
                authInfo);
            SaveProfile();
        }

        public void AddNewGameProfile(GameProfileModel gameProfile)
        {
            if (IsGameProfileExist(gameProfile.Name)) return;

            LauncherProfile.Profiles.Add(gameProfile.Name, gameProfile);
            SaveProfile();
        }

        public void EmptyAuthInfo()
        {
            LauncherProfile.AuthenticationDatabase?.Clear();
            SaveProfile();
        }

        public void EmptyGameProfiles()
        {
            LauncherProfile.Profiles?.Clear();
            SaveProfile();
        }

        public AuthInfoModel GetAuthInfo(string uuid)
        {
            return LauncherProfile.AuthenticationDatabase.TryGetValue(uuid, out var authInfo) ? authInfo : null;
        }

        public GameProfileModel GetGameProfile(string name)
        {
            return LauncherProfile.Profiles.FirstOrDefault(
                p => p.Value.Name.Equals(name, StringComparison.Ordinal)).Value ??
                throw new UnknownGameNameException(name);
        }

        public bool IsAuthInfoExist(string uuid, string userName)
        {
            if (!(LauncherProfile.AuthenticationDatabase?.Any() ?? false)) return false;

            return LauncherProfile.AuthenticationDatabase.Any(a =>
                       a.Value.Profiles?.First().Key.Equals(uuid, StringComparison.Ordinal) ?? false) &&
                   LauncherProfile.AuthenticationDatabase.Any(a =>
                       a.Value.Profiles?.First().Value.DisplayName.Equals(userName, StringComparison.Ordinal) ?? false);
        }

        public bool IsGameProfileExist(string name)
        {
            return LauncherProfile.Profiles.Any(p => p.Value.Name.Equals(name, StringComparison.Ordinal));
        }

        
        public void RemoveAuthInfo(string uuid)
        {
            LauncherProfile.AuthenticationDatabase.Remove(uuid);
        }

        public void RemoveGameProfile(string name)
        {
            LauncherProfile.Profiles.Remove(name);
        }

        public void SaveProfile()
        {
            if (File.Exists(GamePathHelper.GetLauncherProfilePath(RootPath)))
                File.Delete(GamePathHelper.GetLauncherProfilePath(RootPath));

            var launcherProfileJson = JsonConvert.SerializeObject(LauncherProfile, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            FileHelper.Write(GamePathHelper.GetLauncherProfilePath(RootPath), launcherProfileJson);
        }

        public void SelectGameProfile(string name)
        {
            if (!IsGameProfileExist(name)) throw new KeyNotFoundException();

            LauncherProfile.SelectedUser.Profile = name;
            SaveProfile();
        }

        public void SelectUser(string uuid)
        {
            LauncherProfile.SelectedUser.Account = uuid;
            SaveProfile();
        }
    }
}