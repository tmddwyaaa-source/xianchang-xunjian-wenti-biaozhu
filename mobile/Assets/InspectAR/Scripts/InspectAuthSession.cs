using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 登录会话：JWT 存取、过期检查、Authorization。不删后端 URL。
/// </summary>
public sealed class InspectAuthSession
{
    public bool HasToken => !string.IsNullOrEmpty(LoadJwt());

    public string LoadJwt()
    {
        return PlayerPrefs.GetString(InspectARApp.PlayerPrefsJwtKey, "");
    }

    public string UserName => PlayerPrefs.GetString(InspectARApp.PlayerPrefsUserNameKey, "");
    public string Role => PlayerPrefs.GetString(InspectARApp.PlayerPrefsRoleKey, "");
    public string UserId => PlayerPrefs.GetString(InspectARApp.PlayerPrefsUserIdKey, "");

    public void SaveLogin(string token, string id, string username, string role)
    {
        PlayerPrefs.SetString(InspectARApp.PlayerPrefsJwtKey, token ?? "");
        PlayerPrefs.SetString(InspectARApp.PlayerPrefsUserIdKey, id ?? "");
        PlayerPrefs.SetString(InspectARApp.PlayerPrefsUserNameKey, username ?? "");
        PlayerPrefs.SetString(InspectARApp.PlayerPrefsRoleKey, role ?? "");
        PlayerPrefs.Save();
    }

    public void Clear()
    {
        PlayerPrefs.DeleteKey(InspectARApp.PlayerPrefsJwtKey);
        PlayerPrefs.DeleteKey(InspectARApp.PlayerPrefsUserIdKey);
        PlayerPrefs.DeleteKey(InspectARApp.PlayerPrefsRoleKey);
        PlayerPrefs.DeleteKey(InspectARApp.PlayerPrefsUserNameKey);
        PlayerPrefs.Save();
    }

    public void AttachAuth(UnityWebRequest req)
    {
        if (req == null)
            return;
        var jwt = LoadJwt();
        if (!string.IsNullOrEmpty(jwt))
            req.SetRequestHeader("Authorization", "Bearer " + jwt);
    }

    public bool IsExpired()
    {
        var jwt = LoadJwt();
        if (string.IsNullOrEmpty(jwt))
            return false;
        var exp = TryReadExp(jwt);
        if (!exp.HasValue)
            return false;
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= exp.Value;
    }

    public bool EnsureFresh()
    {
        if (!HasToken)
            return false;
        if (!IsExpired())
            return true;
        Clear();
        return false;
    }

    static long? TryReadExp(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2)
                return null;
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            var payload = JsonUtility.FromJson<JwtExpPayload>(json);
            if (payload == null || payload.exp <= 0)
                return null;
            return payload.exp;
        }
        catch
        {
            return null;
        }
    }

    static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2:
                s += "==";
                break;
            case 3:
                s += "=";
                break;
        }

        return Convert.FromBase64String(s);
    }

    [Serializable]
    sealed class JwtExpPayload
    {
        public long exp;
    }
}

[Serializable]
public sealed class InspectPasswordRequest
{
    public string oldPassword;
    public string newPassword;
}
