using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot
{
    public partial class BotForm : Form
    {
        static ITelegramBotClient bot = new TelegramBotClient(""); //Вставить сюда API бота
        private HttpClient client = new HttpClient();
        private List<HtmlTemplate> templates = new List<HtmlTemplate>();
        public BotForm()
        {
            InitializeComponent();
        }

        private void startBtn_Click(object sender, EventArgs e)
        {
            Thread getProxy = new Thread(async () => { await StartWork(); });
            getProxy.IsBackground = true;
            getProxy.Start();
        }
        private async Task StartWork()
        {
            await WriteToLog($"Запустили бот");

            var resp = await Get(client, "http://localhost:36255/api/Home/getHtml");
            if (resp != "")
                templates = JsonConvert.DeserializeObject<List<HtmlTemplate>>(resp);

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { },
            };
            bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            await WriteToLog(Newtonsoft.Json.JsonConvert.SerializeObject(update));
            string type = "";

            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            {
                var message = update.Message;

                if (System.IO.File.Exists($"Data/{message.From.Id}.txt"))
                    type = System.IO.File.ReadAllText($"Data/{message.From.Id}.txt");

                if (message.Text.ToLower() == "/start")
                    StartMessage(message);

                else if (message.Text == "Оставить заявку")
                {
                    if (!System.IO.File.Exists($"Data/{message.From.Id}.txt"))
                    {
                        using (FileStream fs = System.IO.File.Create($"Data/{message.From.Id}.txt"))
                        {
                            byte[] info = new UTF8Encoding(true).GetBytes("");
                            fs.Write(info, 0, info.Length);
                        }
                    }

                    System.IO.File.WriteAllText($"Data/{message.From.Id}.txt", "req");

                    ReplyKeyboardMarkup replyMarkup = new[]
                                        {
                            new[] {    "Отмена"   }
                        };

                    await bot.SendTextMessageAsync(
                        chatId: message.From.Id,
                        text: "Введите свои данные в формате:\n-Имя\n-Email\n-Текст заявки",
                        replyMarkup: replyMarkup
                    );
                }
                else if (message.Text == "Отмена")
                    StartMessage(message);
                else if (message.Text == "Назад")
                    StartMessage(message);
                else if (message.Text == "Посмотреть наши продукты")
                    GetServices(message);
                else if (message.Text == "Проекты" || message.Text == "Услуги")
                    GetHtml(message, message.Text, message.From.Id.ToString());
                else if (message.Text.ToLower() != "/start" && type == "req")
                {
                    var list = message.Text.Split('\n').ToList();
                    if (list.Count < 3)
                        await bot.SendTextMessageAsync(
                        chatId: message.From.Id,
                        text: "Введены не корректные данные. Потворите ввод"
                        );
                    else
                    {
                        bool send = await SendReqToApi(list);
                        string mess = "";
                        if (send)
                            mess = "Заявка принята. Спасибо за обращение :)";
                        else
                            mess = "К сожалению не удалось отправить заявку. Повторите попытку позже :(";

                        await bot.SendTextMessageAsync(
                        chatId: message.From.Id,
                        text: mess,
                        replyMarkup: new ReplyKeyboardRemove()
                        );


                        System.IO.File.WriteAllText($"Data/{message.From.Id}.txt", "");
                    }
                }

            }
            else if (update.Type == Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
            {
                var id = update.CallbackQuery.Data;
                var item = templates.FirstOrDefault(x => x.Id == Convert.ToInt32(id));

                if(item != null)
                    await botClient.SendTextMessageAsync(chatId: update.CallbackQuery.Message.Chat.Id, $"{item.Title}\n{item.Text}\n{item?.Info}", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
            }
        }
        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            await WriteToLog($"Ошибка в работе бота. {exception.Message}");
        }

        private async Task<bool> SendReqToApi(List<string> list)
        {
            string json = JsonConvert.SerializeObject(new Req() { Name = list[0], Email = list[1], Text = list[2], Time = DateTime.Now, Status = "Получена" });

            var PostAddFav = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, "http://localhost:36255/api/Home/addReq");
            PostAddFav.Content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
            var PostAddFavSend = await client.SendAsync(PostAddFav);
            var text = await PostAddFavSend.Content.ReadAsStringAsync();

            if (text.Contains("Заявка добавлена"))
                return true;

            return false;
        }

        public async void StartMessage(Telegram.Bot.Types.Message message)
        {
            if (System.IO.File.Exists($"Data/{message.From.Id}.txt"))
                System.IO.File.WriteAllText($"Data/{message.From.Id}.txt", "");

            ReplyKeyboardMarkup replyMarkup = new[]
                                {
                            new[] {    "Оставить заявку"   },
                            new[] { "Посмотреть наши продукты" }
                        };

            await bot.SendTextMessageAsync(
                chatId: message.From.Id,
                text: "Доброе время суток! Чего бы вы хотели?",
                replyMarkup: replyMarkup
            );
        }
        public async void GetServices(Telegram.Bot.Types.Message message)
        {
            if (System.IO.File.Exists($"Data/{message.From.Id}.txt"))
                System.IO.File.WriteAllText($"Data/{message.From.Id}.txt", "");

            ReplyKeyboardMarkup replyMarkup = new[]
                                {
                            new[] {    "Проекты"   },
                            new[] {    "Услуги"   },
                            new[] {    "Назад"   }
                        };

            await bot.SendTextMessageAsync(
                chatId: message.From.Id,
                text: "Выберете нужный раздел",
                replyMarkup: replyMarkup
            );
        }
        public async void GetHtml(Telegram.Bot.Types.Message message, string type, string id)
        {
            if (type == "Проекты")
                type = "project";
            else if (type == "Услуги")
                type = "service";
            else if (type == "Блог")
                type = "blog";

            var list = templates.Where(x => x.Type == type).ToList();


            if (System.IO.File.Exists($"Data/{message.From.Id}.txt"))
                System.IO.File.WriteAllText($"Data/{message.From.Id}.txt", "");

            var sendkeyboard = SendKeyboard(list, message.From.Id.ToString());

            //for (int i = 0; i < list.Count; i++)
            //{
            //    if (list.Count - i >= 3)
            //    {
            //        InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(new[]
            //        {
            //            new []
            //            {
            //                InlineKeyboardButton.WithCallbackData(text: list[i].Title.Truncate(32) + "...", callbackData: list[i].Title.Truncate(32) + "..."),
            //                InlineKeyboardButton.WithCallbackData(text: list[i + 1].Title.Truncate(32) + "...", callbackData: list[i + 1].Title.Truncate(32) + "..."),
            //                InlineKeyboardButton.WithCallbackData(text: list[i + 2].Title.Truncate(32) + "...", callbackData: list[i + 2].Title.Truncate(32) + "..."),
            //            }
            //        });

            //        Telegram.Bot.Types.Message sentMessage = await bot.SendTextMessageAsync(
            //        chatId: id,
            //        text: "Выберете интересующий продукт",
            //        replyMarkup: inlineKeyboard,
            //        cancellationToken: new CancellationToken());

            //        i += 2;
            //    }
            //    else
            //    {
            //        InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(new[]
            //        {
            //            new []
            //            {
            //                InlineKeyboardButton.WithCallbackData(text: list[i].Title, callbackData: list[i].Title),
            //            }
            //        });

            //        Telegram.Bot.Types.Message sentMessage = await bot.SendTextMessageAsync(
            //        chatId: id,
            //        text: "Выберете интересующий продукт",
            //        replyMarkup: inlineKeyboard,
            //        cancellationToken: new CancellationToken());
            //    }
            //}
        }
        private async Task<bool> SendKeyboard(List<HtmlTemplate> stringArray, string id)
        {
            var count = Math.Ceiling((double)((double)stringArray.Count / (double)3));
            int add = 0;
            int btnsCount = 0;

            for (int t = 0; t < count; t++)
            {
                var keyboardInline = new InlineKeyboardButton[3][];

                if (count - t > 1)
                    btnsCount = 3;
                else if (count == 1 && stringArray.Count == 3)
                    btnsCount = 3;
                else if (count == 1 && stringArray.Count < 3)
                    btnsCount = stringArray.Count;
                else
                    btnsCount = stringArray.Count % 3;

                var keyboardButtons = new InlineKeyboardButton[btnsCount];

                for (var i = 0; i < 3; i++)
                {
                    var title = stringArray[add].Title.Truncate(32) + "...";
                    keyboardButtons[i] = new InlineKeyboardButton("")
                    {
                        Text = title,
                        CallbackData = stringArray[add].Id.ToString(),
                    };
                    add += 1;
                    if (add % 3 == 0 || add == stringArray.Count)
                        break;
                }

                InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(keyboardButtons);
                Telegram.Bot.Types.Message sentMessage = await bot.SendTextMessageAsync(
                chatId: id,
                text: $"Страница {t+1}",
                replyMarkup: inlineKeyboard,
                cancellationToken: new CancellationToken());
            }
            return true;
        }

        public async Task WriteToLog(string text)
        {
            try
            {
                Invoke(new Action(() =>
                {
                    logBox.Text += $"[{DateTime.Now.ToString("hh:mm:ss")}] {text}" + Environment.NewLine;

                    var count = logBox.Lines.Length;
                    if (count > 200)
                        logBox.Lines = logBox.Lines.Where((str, pos) => pos != 0).ToArray();

                    logBox.SelectionStart = logBox.TextLength;
                    logBox.ScrollToCaret();
                }));

            }
            catch { }

            await Task.Delay(100);
        }
        public async Task<string> Get(HttpClient hc, string url)
        {
            string text = "";
            try
            {
                var get = await hc.SendAsync(new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url));
                text = await get.Content.ReadAsStringAsync();
                return text;
            }
            catch (Exception m)
            {
                await WriteToLog($"Ошибка в гет запросе {url} {m.Message}");
                return text;
            }
        }
        public async Task<string> Post(HttpClient hc, string url, string data)
        {
            try
            {
                var PostAddFav = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, $"{url}");
                PostAddFav.Content = new System.Net.Http.StringContent($"{data}", Encoding.UTF8, "application/x-www-form-urlencoded");
                var PostAddFavSend = await hc.SendAsync(PostAddFav);
                string text = await PostAddFavSend.Content.ReadAsStringAsync();
                return text;
            }
            catch (Exception)
            {
                return null;
            }
        }
        public Image Base64ToImage(string base64String)
        {
            byte[] imageBytes = Convert.FromBase64String(base64String);
            using (var ms = new MemoryStream(imageBytes, 0, imageBytes.Length))
            {
                Image image = Image.FromStream(ms, true);
                return image;
            }
        }
    }
    public static class StringExt
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
