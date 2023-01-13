using System.Text.Json;
using System.Text.Json.Nodes;
using Konata.Core;
using Konata.Core.Common;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces.Api;
using Konata.Core.Message;
using Konata.Core.Message.Model;
using Ceobe.Develop;
using ArcaeaUnlimitedAPI.Lib;

namespace Ceobe.Process;

public static class Command
{
    private static uint _messageCounter;
    private const string CommandSymbol = "/";

    internal static async void OnGroupMessage(Bot bot, GroupMessageEvent group)
    {
        var messageStruct = group.Message;
        var textChain = group.Chain.GetChain<TextChain>();
        var replyChain = ReplyChain.Create(messageStruct);
        var formattedChain = group.Chain.ToString()
            .Replace(Convert.ToChar(13).ToString(), "\n")
            .Replace("\n", "\x1b[38;2;255;199;115m\\n\x1b[39m");
        Console.WriteLine($"{DateTime.Now} [{group.GroupName}({group.GroupUin})] {messageStruct.Sender}: {formattedChain}");
        
        if (group.MemberUin == bot.Uin) return;
        if (textChain == null) return;
        
        ++_messageCounter;
        try
        {
            MessageBuilder? reply = null;

            // 命令解析
            var textContent = textChain.Content;
            if (textContent.StartsWith(CommandSymbol))
            {
                var rawText = textContent[1..].TrimEnd();
                var commandType = rawText.Split(" ")[0];
                var commandContent = "";
                if (rawText.Length >= commandType.Length + 1)
                    commandContent = rawText[(commandType.Length + 1)..];
                // Console.WriteLine($"commandType: [{commandType}]");
                // Console.WriteLine($"commandContent: [{commandContent}]");
                reply = commandType switch
                {
                    "help" => OnCommandHelp(),
                    "ping" => OnCommandPing(),
                    "roll" => OnCommandRoll(commandContent).Add(replyChain),
                    "setu" => (await OnCommandSetu(bot, group, replyChain, commandContent)).Add(replyChain),
                    "a" => (await OnCommandArcaeaRecent(commandContent)).Add(replyChain),
                    _ => reply
                };
                var isAdmin = (await bot.GetGroupMemberInfo(group.GroupUin, group.MemberUin, true)).Role
                    is RoleType.Owner
                    or RoleType.Admin;
                if (group.MemberUin == Ceobe.BotOwnerUin || isAdmin)
                {
                    reply = commandType switch
                    {
                        "status" => OnCommandStatus(),
                        "echo" => OnCommandEcho(commandContent).Add(replyChain),
                        "eval" => OnCommandEval(commandContent),
                        "mute" => (await OnCommandMute(bot, group)).Add(replyChain),
                        "admin" => (await OnCommandAdmin(bot, group)).Add(replyChain),
                        "title" => (await OnCommandSetTitle(bot, group)).Add(replyChain),
                        _ => reply
                    };
                }
            }
            else
            {
                /* 占位 */
                return;
            }

            // 发送消息
            if (reply != null)
                await bot.SendGroupMessage(group.GroupUin, reply);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            await bot.SendGroupMessage(group.GroupUin, Text("有什么东西不对劲；；"));
        }
    }

    #region [Bot.Core]

    private static MessageBuilder Text(string text)
        => new MessageBuilder().Text(text);

    private static MessageBuilder OnCommandHelp()
        => new MessageBuilder()
            .Text("[Ceobe Bot]\n")
            .Text("/roll <小刻帮你做决定>\n")
            .Text("/setu <获取setu 可指定tag>\n\t<用&连接主tag, 用|连接可选tag, 可叠加>");

    private static MessageBuilder OnCommandPing()
        => Text("咬你噢!");

    private static MessageBuilder OnCommandStatus()
        => new MessageBuilder()
            .Text("[Ceobe Bot]\n")
            .Text($"[Konata.Core {BuildStamp.Version}]\n")
            .Text("\n")
            .Text($"已处理{_messageCounter}条消息\n")
            .Text($"内存占用 {System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 10e5:#0.00} MiB\n")
            .Text("\n")
            .Text("Crosu 2023");

    private static MessageBuilder OnCommandEcho(string text)
    {
        Console.WriteLine($"echo text: [{text}]");
        return Text(text == "" ? "[error]" : text);
    }

    private static MessageBuilder OnCommandEval(string text)
    {
        Console.WriteLine($"eval text: [{text}]");
        return text == "" ? Text("[err0r]") : MessageBuilder.Eval(text);
    }

    private static async Task<MessageBuilder> OnCommandMute(Bot bot, GroupMessageEvent group)
    {
        var atChain = group.Chain.GetChain<AtChain>();
        if (atChain == null) return Text("expection, 找不到at");

        var time = 60U;
        var textChains = group.Chain.FindChain<TextChain>();
        if (textChains.Count == 2 && uint.TryParse(textChains[1].Content.Trim(), out var t))
            time = t;

        try
        {
            var result = await bot.GroupMuteMember(group.GroupUin, atChain.AtUin, time);
            return Text(result ? $"烟了 [{atChain.AtUin}] 大概{time}秒" : "没烟上");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Text($"{e.Message}({e.HResult})");
        }
    }

    private static async Task<MessageBuilder> OnCommandAdmin(Bot bot, GroupMessageEvent group)
    {
        var atChain = group.Chain.GetChain<AtChain>();
        if (atChain == null) return Text("expection, 找不到at");

        var textChains = group.Chain.FindChain<TextChain>();
        if (textChains.Count != 2) return Text("expection, 找不到bool");

        var change = (textChains[1].Content.Trim() == "1");

        try
        {
            var result = await bot.GroupPromoteAdmin(group.GroupUin, atChain.AtUin, change);
            return Text(result ? $"[{atChain.AtUin}] 现在的管理员状态是:\n{change}!" : "有问题");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Text($"{e.Message}({e.HResult})");
        }
    }

    private static async Task<MessageBuilder> OnCommandSetTitle(Bot bot, GroupMessageEvent group)
    {
        var atChain = group.Chain.GetChain<AtChain>();
        if (atChain == null) return Text("expection, 找不到at");

        var textChains = group.Chain.FindChain<TextChain>();
        if (textChains.Count != 2) return Text("expection, 找不到帽子");

        try
        {
            var result =
                await bot.GroupSetSpecialTitle(group.GroupUin, atChain.AtUin, textChains[1].Content, uint.MaxValue);

            return Text(result ? $"[{atChain.AtUin}] 现在有了全新的称号:\n{textChains[1].Content}!" : "没设成");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Text($"{e.Message}({e.HResult})");
        }
    }

    #endregion

    #region [Common Features]

    private static MessageBuilder OnCommandRoll(string text)
    {
        var parts = text.Split(" ");
        if (parts.Length < 2) return Text("至少两个才能roll吧");
        string[] suggestPrefix = { "那我建议你选择", "我觉得还是", "那就", "要不就", "那必然是", "那还是", "肯定是", "" };
        return Text(
            $"{suggestPrefix[new Random().Next(suggestPrefix.Length)]}{parts[new Random().Next(parts.Length)]}");
    }

    private static async Task<MessageBuilder> OnCommandSetu(Bot bot, GroupMessageEvent group, BaseChain replyChain,
        string text)
    {
        var stringParts = text.Split("&");
        var tagCount = stringParts.Length;
        if (tagCount >= 3)
            return new MessageBuilder()
                .Text("tag太多了，看个setu要求还那么高")
                .Image(await File.ReadAllBytesAsync("Assets/amiya.jpg"));
        var addonParam = "";
        for (var i = 0; i < tagCount; i++)
            addonParam += $"&tag={stringParts[i]}";

        var targetUrl = $"https://api.lolicon.app/setu/v2?{addonParam[1..]}";
        var client = new HttpClient();
        var get = await client.GetAsync(targetUrl);
        var response = JsonSerializer.Deserialize<JsonObject>(await get.Content.ReadAsStringAsync());

        if (response?["error"]?.ToString() != "") return Text("呜呜炸了我也不知道为什么；；");
        if (response["data"]?.ToString() == "[]")
            return Text("悲报, tag太冷门了, 没有全年龄向的setu收录到站点, 要不就换个写法");

        var data = response["data"]?[0];
        var title = data?["title"]?.ToString();
        var author = data?["author"]?.ToString();
        var pid = data?["pid"]?.ToString();
        var date = data?["uploadDate"]?.ToString();
        var isAi = data?["aiType"]?.ToString() == "1" ? "== AI作品 ==" : "";
        var url = data?["urls"]?["original"]?.ToString();

        Console.WriteLine($"\x1b[38;2;75;92;196mhttps://www.pixiv.net/artworks/{pid}");
        Console.WriteLine(url);
        var startTime = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1), TimeZoneInfo.Local);
        var dt = startTime.AddMilliseconds(double.Parse(date + ""));
        Console.WriteLine($"Upload Date: {dt:yyyy/MM/dd HH:mm:ss}\x1b[39m");

        var image = await client.GetByteArrayAsync(url);
        var size = $"{Math.Round(image.Length / 1048576.0, 2)}";
        Console.WriteLine($"[pid: {pid} | size: {size}MiB] Download Done, Now Upload");
        await bot.SendGroupMessage(group.GroupUin,
            new MessageBuilder().Text($"图片大小: {size}MiB\n正在上传到群, 长时间没反应就是被tx截走看了").Add(replyChain));

        return new MessageBuilder()
            .Text($"「{title}」\n")
            .Text($"作者: {author}\n")
            .Text($"pid: {pid}\n")
            .Text($"上传日期: {dt:yyyy/MM/dd}\n")
            .Text($"{isAi}")
            .Image(image);
    }

    private static async Task<MessageBuilder> OnCommandArcaeaRecent(string text)
    {
        var targetUser = text.Trim();

        var client = new AuaClient
        {
            ApiUrl = "https://server.awbugl.top/botarcapi/",
            Token = "001d00a527ed7048da844b5150a9f11bb46c4951",
            UserAgent = "CrosuaU5Dt3t0Y5D4X5MS",
        }.Initialize();

        var recentInfo = await client.User.Info(targetUser);
        return new MessageBuilder()
            .Text($"[{recentInfo.AccountInfo.Name}@{recentInfo.AccountInfo.Rating.ToString().Insert(2, ".")}]\n")
            .Text($"{recentInfo.SongInfo}\n")
            .Text($"{recentInfo.RecentScore}\n");
    }

    #endregion
}
