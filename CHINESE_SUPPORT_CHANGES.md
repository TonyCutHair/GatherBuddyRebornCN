# GatherBuddy Reborn - 中文游戏客户端支持

## 概述
本次更新为 GatherBuddy Reborn 插件添加了对中文游戏客户端的完整支持。

## 修改清单

### 1. **GatherBuddy.GameData/Utility/MultiString.cs**
   - **变更**: 为 `MultiString` 结构体添加中文(Chinese)语言支持
   - **具体修改**:
     - 修改构造函数签名: `MultiString(string en, string de, string fr, string jp, string zh = "")`
     - 添加新字段: `public readonly string Chinese = zh;`
     - 修改 `ToWholeString()` 方法包含中文
     - 修改 `FromPlaceName()` 方法加载中文地点名称
     - 修改 `FromItem()` 方法加载中文物品名称
     - 修改 `Name()` 方法的 switch 表达式处理 `ClientLanguage.Chinese`
     - 更新 `Empty` 常量以包含中文空字符串
   
### 2. **GatherBuddy/Plugin/Identificator.cs**
   - **变更**: 更新语言循环以支持5种语言而不是4种
   - **具体修改**:
     - 修改语言数组从 4 个元素扩展到 5 个
     - 将模运算符从 `% 4` 更改为 `% 5`
   
### 3. **GatherBuddy/Gui/Interface.ContextMenus.cs**
   - **变更**: 添加 TeamCraft 数据库中文语言代码映射
   - **具体修改**:
     - 在 `TeamCraftAddressEnd()` 方法的 switch 表达式中添加: `ClientLanguage.Chinese => "zh"`
   
### 4. **GatherBuddy/FishTimer/Parser/FishingParser.Regexes.cs**
   - **变更**: 为钓鱼解析器添加中文正则表达式
   - **具体修改**:
     - 在 `FromLanguage()` 方法添加: `ClientLanguage.Chinese => Chinese.Value`
     - 添加新的 `Chinese` 正则表达式定义:
       ```csharp
       private static readonly Lazy<Regexes> Chinese = new(() => new Regexes
       {
           Cast           = new Regex(@"(?:你在|.*?在)(?<FishingSpot>.+?)(?:垂钓|开始钓鱼)\。", 
                                     RegexOptions.Compiled | RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture),
           AreaDiscovered = new Regex(@"(?<FishingSpot>.+?)已添加至钓鱼笔记\。", 
                                     RegexOptions.Compiled | RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture),
           Mooch          = new Regex(@"钓竿上仍有鱼儿", 
                                     RegexOptions.Compiled | RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture),
           Undiscovered   = "未知的钓鱼点",
       });
       ```

## 支持的客户端语言
现在支持以下客户端语言:
- ✅ English (英语)
- ✅ German (德语)
- ✅ French (法语)
- ✅ Japanese (日语)
- ✅ Chinese (中文) **[新增]**

## 功能影响
这些修改使得当游戏客户端设置为中文时:
1. ✅ 所有物品和地点名称会以中文显示
2. ✅ 钓鱼日志解析能够识别中文钓鱼点名称
3. ✅ TeamCraft 数据库链接会指向中文版本 (`zh` 代码)
4. ✅ 物品搜索和识别支持中文名称

## 兼容性
- 所有修改都是向后兼容的
- 不存在破坏性更改
- 现有的中文字符串参数使用默认值 `""`
- 对其他语言的用户没有任何影响

## 测试建议
1. 在中文游戏客户端上安装此版本
2. 验证物品名称和地点名称显示为中文
3. 测试钓鱼日志解析功能
4. 验证 TeamCraft 数据库链接正确指向中文版本

## 后续工作
- 可根据需要为钓鱼点名称添加特殊的中文处理逻辑(类似德文的处理方式)
- 可添加中文特定的命令帮助和界面本地化文本
