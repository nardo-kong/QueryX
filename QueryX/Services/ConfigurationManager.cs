using QueryX.Models; // 引入你的模型命名空间
using System;
using System.Diagnostics;
using System.IO; // 用于文件操作
using System.Text.Json; // 用于 JSON 序列化/反序列化

namespace QueryX.Services // 确保命名空间正确
{
    public class ConfigurationManager
    {
        // 配置文件的名称
        private const string ConfigFileName = "config.json";
        // 推荐将配置文件存储在用户的应用程序数据目录中
        private readonly string _configFilePath;

        public ConfigurationManager()
        {
            // 获取当前用户的 ApplicationData 文件夹路径
            // 例如 C:\Users\<YourUsername>\AppData\Roaming
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            // 在 AppData 下为你的应用程序创建一个子目录（如果不存在）
            string appFolderPath = Path.Combine(appDataPath, "QueryX"); // 使用你的应用程序名称
            Directory.CreateDirectory(appFolderPath); // 确保目录存在
            // 组合成完整的配置文件路径
            _configFilePath = Path.Combine(appFolderPath, ConfigFileName);
        }

        // 获取配置文件的完整路径 (可选，可能用于调试或显示给用户)
        public string GetConfigFilePath()
        {
            return _configFilePath;
        }

        // 加载配置
        public AppConfiguration LoadConfiguration()
        {
            try
            {
                // 检查配置文件是否存在
                if (File.Exists(_configFilePath))
                {
                    // 读取文件所有内容
                    string json = File.ReadAllText(_configFilePath);
                    Debug.WriteLine($"Attempting to deserialize JSON: {json}");
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json);
                    if (config == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Deserialization resulted in null AppConfiguration.");
                        return new AppConfiguration();
                    }
                    System.Diagnostics.Debug.WriteLine($"Loaded {config.Connections.Count} connections and {config.Queries.Count} queries.");
                    return config;
                }
            }
            catch (JsonException jsonEx)
            {
                // 处理 JSON 格式错误
                Console.Error.WriteLine($"Error deserializing configuration file '{_configFilePath}': {jsonEx.Message}");
                // 可以考虑通知用户配置文件损坏，或者备份旧文件并创建新文件
            }
            catch (IOException ioEx)
            {
                // 处理文件读写错误
                Console.Error.WriteLine($"Error reading configuration file '{_configFilePath}': {ioEx.Message}");
            }
            catch (Exception ex)
            {
                // 处理其他意外错误
                Console.Error.WriteLine($"Unexpected error loading configuration: {ex.Message}");
            }

            // 如果文件不存在或加载失败，返回一个默认的空配置对象
            return new AppConfiguration();
        }

        // 保存配置
        public bool SaveConfiguration(AppConfiguration configuration)
        {
            try
            {
                // 配置 JSON 序列化选项：启用缩进以提高可读性
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    // 如果需要处理循环引用或更复杂的场景，可能需要调整选项
                    // ReferenceHandler = ReferenceHandler.Preserve // 例如
                };

                // 将 AppConfiguration 对象序列化为 JSON 字符串
                string json = JsonSerializer.Serialize(configuration, options);

                // 将 JSON 字符串写入文件，如果文件已存在则覆盖
                File.WriteAllText(_configFilePath, json);
                return true; // 保存成功
            }
            catch (JsonException jsonEx)
            {
                Console.Error.WriteLine($"Error serializing configuration: {jsonEx.Message}");
            }
            catch (IOException ioEx)
            {
                // 处理文件写错误
                Console.Error.WriteLine($"Error writing configuration file '{_configFilePath}': {ioEx.Message}");
            }
            catch (Exception ex)
            {
                // 处理其他意外错误
                Console.Error.WriteLine($"Unexpected error saving configuration: {ex.Message}");
            }
            return false; // 保存失败
        }
    }
}