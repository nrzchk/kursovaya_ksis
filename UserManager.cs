using System.Collections.Generic;
using System.IO;
using System.Xml;
using Newtonsoft.Json;

public class UserData
{
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public int HighScore { get; set; }
}

public static class UserManager
{
    private static string filePath = "users.json";
    private static Dictionary<string, UserData> users = new Dictionary<string, UserData>();


    static UserManager()
    {
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            users = JsonConvert.DeserializeObject<Dictionary<string, UserData>>(json)
                    ?? new Dictionary<string, UserData>();
        }
    }

    public static bool IsUserExists(string username) => users.ContainsKey(username);

    public static bool CheckPassword(string username, string password)
    {
        if (!users.ContainsKey(username)) return false;
        return users[username].PasswordHash == password; // пока без шифрования
    }

    public static void Register(string username, string password)
    {
        users[username] = new UserData
        {
            Username = username,
            PasswordHash = password
        };
        Save();
    }

    public static void TryUpdateHighScore(string username, int newScore)
    {
        if (users.ContainsKey(username))
        {
            if (newScore > users[username].HighScore)
            {
                users[username].HighScore = newScore;
                Save();
            }
        }
    }


    private static void Save()
    {
        var json = JsonConvert.SerializeObject(users, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

}
