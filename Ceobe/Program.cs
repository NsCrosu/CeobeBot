using System.Text.Json;
using Konata.Core;
using Konata.Core.Common;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces;
using Konata.Core.Interfaces.Api;
using Ceobe.Process;

namespace Ceobe;

public static class Ceobe
{
    private static Bot _bot = null!;
    private static bool _isLoggedIn;
    public const uint BotOwnerUin = 2770857328;

    public static async Task Main()
    {
        _bot = BotFather.Create(GetConfig(), GetDevice(), GetKeyStore());

        _bot.OnLog += (_, e) => Console.Write(!_isLoggedIn ? e.EventMessage + "\n" : "");

        // 登陆验证
        _bot.OnCaptcha += (sender, e) =>
        {
            switch (e.Type)
            {
                // 滑条验证
                case CaptchaEvent.CaptchaType.Slider:
                    Console.WriteLine(e.SliderUrl);
                    Console.WriteLine("请输入返回的 ticket");
                    sender.SubmitSliderTicket(Console.ReadLine());
                    break;

                // 短信验证
                case CaptchaEvent.CaptchaType.Sms:
                    Console.WriteLine(e.Phone);
                    Console.WriteLine("请输入验证码");
                    sender.SubmitSmsCode(Console.ReadLine());
                    break;

                // 未知验证
                default:
                case CaptchaEvent.CaptchaType.Unknown:
                    Console.WriteLine("未知的验证类型");
                    break;
            }
        };

        // 登录
        var loginResult = await _bot.Login();
        if (loginResult)
        {
            Console.WriteLine("\x1b[38;2;150;206;84m登陆成功\x1b[39m");
            UpdateKeystore(_bot.KeyStore);
            _isLoggedIn = true;
        }
        else
        {
            Console.WriteLine("\x1b[38;2;239;122;130m登陆失败, 可能是账号已被冻结或封禁\x1b[39m");
        }

        // 消息处理
        // 处理群消息
        _bot.OnGroupMessage += Command.OnGroupMessage;
        // 处理戳一戳
        //_bot.OnGroupPoke += Poke.OnGroupPoke;

        #region [CLI]

        while (true)
        {
            var input = Console.ReadLine();

            if (input == null) continue;
            if (input.StartsWith("/send"))
            {
                var rawText = input[6..];
                var stringParts = rawText.Split(" ");
                var groupUin = uint.Parse(stringParts[0]);
                var sendContent = rawText[(groupUin.ToString().Length + 1)..];

                try
                {
                    await _bot.SendGroupMessage(groupUin,
                        new Konata.Core.Message.MessageBuilder(sendContent));
                    Console.WriteLine("\x1b[38;2;150;206;84m发送成功\x1b[39m");
                }
                catch (Exception e)
                {
                    Console.WriteLine("\x1b[38;2;239;122;130m发送失败\x1b[39m");
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }
            else if (input.StartsWith("/stop"))
            {
                _isLoggedIn = false;
                await _bot.Logout();
                _bot.Dispose();
                return;
            }
        }

        #endregion
    }

    private static BotConfig GetConfig()
    {
        return new BotConfig
        {
            EnableAudio = true,
            TryReconnect = true,
            //Protocol = OicqProtocol.AndroidPad,
            HighwayChunkSize = 8192,
        };
    }

    private static BotDevice? GetDevice()
    {
        if (File.Exists("device.json"))
            return JsonSerializer.Deserialize<BotDevice>(File.ReadAllText("device.json"));

        var device = BotDevice.Default();
        {
            device.Model.Name = "Ceobe 13 Pro";
            device.Model.Manufacturer = "Bolivar";
            device.Model.CodeName = "CROSU";
            device.System.Name = "Perro";
        }
        var deviceJson = JsonSerializer.Serialize(device,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("device.json", deviceJson);

        return device;
    }

    private static BotKeyStore? GetKeyStore()
    {
        if (File.Exists("keystore.json"))
            return JsonSerializer.Deserialize<BotKeyStore>(File.ReadAllText("keystore.json"));

        Console.WriteLine("首次运行, 请输入账号与密码");
        Console.Write("Account: ");
        var account = Console.ReadLine();
        Console.Write("Password: ");
        var password = Console.ReadLine();
        Console.WriteLine("Bot 已创建");

        return UpdateKeystore(new BotKeyStore(account, password));
    }

    private static BotKeyStore UpdateKeystore(BotKeyStore keystore)
    {
        var deviceJson = JsonSerializer.Serialize(keystore,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("keystore.json", deviceJson);

        return keystore;
    }
}
