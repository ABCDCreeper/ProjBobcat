﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;
using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Authenticator;

/// <summary>
///     表示一个离线凭据验证器。
/// </summary>
public class OfflineAuthenticator : IAuthenticator
{
    /// <summary>
    ///     获取或设置用户名。
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    ///     获取或设置启动程序配置文件分析器。
    /// </summary>
    public ILauncherAccountParser LauncherAccountParser { get; set; }

    /// <summary>
    ///     验证凭据。
    /// </summary>
    /// <param name="userField">该参数将被忽略。</param>
    /// <returns>身份验证结果。</returns>
    public AuthResultBase Auth(bool userField = false)
    {
        var authProperty = new AuthPropertyModel
        {
            Name = "preferredLanguage",
            ProfileId = string.Empty,
            UserId = PlayerUUID.Random(),
            Value = "zh-cn"
        };

        var uuid = PlayerUUID.FromOfflinePlayerName(Username);

        var localUuid = GuidHelper.NewGuidString();
        var accountModel = new AccountModel
        {
            Id = uuid.ToGuid(),
            AccessToken = GuidHelper.NewGuidString(),
            AccessTokenExpiresAt = DateTime.Now,
            EligibleForMigration = false,
            HasMultipleProfiles = false,
            Legacy = false,
            LocalId = localUuid,
            MinecraftProfile = new AccountProfileModel
            {
                Id = uuid.ToString(),
                Name = Username
            },
            Persistent = true,
            RemoteId = GuidHelper.NewGuidString(),
            Type = "Mojang",
            UserProperites = new List<AuthPropertyModel>
            {
                authProperty
            },
            Username = Username
        };

        if (!LauncherAccountParser.AddNewAccount(localUuid, accountModel, out var id))
            return new AuthResultBase
            {
                AuthStatus = AuthStatus.Failed,
                Error = new ErrorModel
                {
                    Cause = "添加记录时出现错误",
                    Error = "无法添加账户",
                    ErrorMessage = "请检查 launcher_accounts.json 的权限"
                }
            };

        var result = new AuthResultBase
        {
            Id = id ?? Guid.Empty,
            AccessToken = GuidHelper.NewGuidString(),
            AuthStatus = AuthStatus.Succeeded,
            SelectedProfile = new ProfileInfoModel
            {
                Name = Username,
                UUID = uuid
            },
            User = new UserInfoModel
            {
                UUID = uuid,
                Properties = new List<PropertyModel>
                {
                    new()
                    {
                        Name = authProperty.Name,
                        Value = authProperty.Value
                    }
                }
            }
        };

        return result;
    }

    /// <summary>
    ///     异步验证凭据。
    /// </summary>
    /// <param name="userField">改参数将被忽略。</param>
    /// <returns></returns>
    public Task<AuthResultBase> AuthTaskAsync(bool userField)
    {
        return Task.Run(() => Auth());
    }

    /// <summary>
    ///     验证凭据。
    /// </summary>
    /// <returns>验证结果。</returns>
    [Obsolete("此方法已过时，请使用 Auth(bool) 代替。")]
    public AuthResultBase GetLastAuthResult()
    {
        return Auth();
    }
}