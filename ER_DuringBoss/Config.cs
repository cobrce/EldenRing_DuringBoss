using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ER_DuringBoss
{
    internal class Config
    {
        public bool Attach { get; set; } = true;
        public string ProcessName { get; set; } = "eldenring";
        public string Fullpath { get; set; } = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\eldenring.exe";
        public Config() { }
        public static Config Load()
        {
            var defaultConfig = new Config();
            string currentDirectory = Path.GetDirectoryName(typeof(Config).Assembly.Location) ?? "";
            string configFile = Path.Combine(currentDirectory, "config.json");
            if (File.Exists(configFile))
            {
                string text = File.ReadAllText(configFile);
                return JsonSerializer.Deserialize<Config>(text) ?? defaultConfig;
            }
            else
            {
                File.WriteAllText(configFile, JsonSerializer.Serialize(defaultConfig, typeof(Config)));
            }
            return defaultConfig;
            
        }

    }
}
