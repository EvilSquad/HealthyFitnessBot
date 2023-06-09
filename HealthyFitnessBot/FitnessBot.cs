using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.VisualBasic;
using System.Threading;
using Telegram.Bot.Args;
using TGFitnessBot.Clients;
using TGFitnessBot.Models;
using System.Threading.Tasks.Dataflow;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;

namespace TGFitnessBot {
    public class FitnessBot {
        private TelegramBotClient botClient;
        private CancellationToken cancellationToken;
        private ReceiverOptions receiverOptions;
        private InlineKeyboardMarkup inlineKeyboardMarkup;
        private List<Exercise> exercises;

        public async Task Start() {
            botClient = new TelegramBotClient("6152630190:AAF8cGQ5YB4PjmU52kMsZHV4-4PtciUquFU");
            cancellationToken = new CancellationToken();
            receiverOptions = new ReceiverOptions { AllowedUpdates = { } };


            botClient.StartReceiving(HandlerUpdateAsync, HandlerError, receiverOptions, cancellationToken);
            var botMe = await botClient.GetMeAsync();
            Console.WriteLine($"Бот {botMe.Username} почав працювати");
            Console.ReadKey();
        }
        private Dictionary<long, string> currentStage = new Dictionary<long, string>();
        private Dictionary<long, int> currentIndex = new Dictionary<long, int>();

        private Task HandlerError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken) {
            var errorMessage = exception switch {
                ApiRequestException apiRequestException => $"An error occurred in the telegram bot's API:\n {apiRequestException.ErrorCode}" +
                $"\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }

        private async Task HandlerUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken) {
            if (update.Type == UpdateType.Message && update?.Message?.Text != null) {
                await HandlerMessageAsync(botClient, update);
            }

            if (update.Type == UpdateType.CallbackQuery && update?.CallbackQuery?.Data != null) {
                await HandlerCallbackQueryAsync(botClient, update);
            }
        }

        private async Task HandlerMessageAsync(ITelegramBotClient botClient, Update update) {
            var replyKeyboardRemove = new ReplyKeyboardRemove();
            var message = update.Message;
            if (!currentStage.ContainsKey(message.Chat.Id)) {
                currentStage.Add(message.Chat.Id, "");
            }
            if (!currentIndex.ContainsKey(message.Chat.Id)) {
                currentIndex.Add(message.Chat.Id, 0);
            }
            switch (message.Text) {
                case "/start":
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Hello, <b>{message.From.FirstName}!</b>\n\nThe <b>Fitness 🧘‍♀️</b> bot was created to help you find exercises and create a list of your exercises!\n\n<b>Enter /menu to start</b>", parseMode: ParseMode.Html);
                    break;
                case "/menu":
                    currentStage[message.Chat.Id] = "/menu";
                    SendMenuMessage(message);

                    break;
                case "🔎 Find exercises":
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[]
                    {new [] { InlineKeyboardButton.WithCallbackData("🏋🏻 By name"), },
                     new [] { InlineKeyboardButton.WithCallbackData("🤸🏻 By type"), },
                     new [] { InlineKeyboardButton.WithCallbackData("💪🏻 By muscle"), },
                     new [] { InlineKeyboardButton.WithCallbackData("📈 By difficulty") },
                     new [] { InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                    });
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Select a menu item:", replyMarkup: inlineKeyboardMarkup);
                    break;

                case "📋 View saved exercises":
                    currentStage[message.Chat.Id] = "database";
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[]
                    {new [] { InlineKeyboardButton.WithCallbackData("📋 View all your exercises"), },
                     new [] { InlineKeyboardButton.WithCallbackData("🔍 Search your exercises"), },
                     new [] { InlineKeyboardButton.WithCallbackData("📝 Create your exercise"), },
                     new [] { InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                    });
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Choose an action:", replyMarkup: inlineKeyboardMarkup);
                    break;

                case "🤖 About bot":
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Bot version: 0.0.4\nUpdate date: 09.06.2023\n\nAuthor: @BohdanOverianov");
                    break;

            }
            switch (currentStage[message.Chat.Id]) {
                case "/name":
                    exercises = await GetExercisePublicAPI(3, message.Text);
                    if (message == null || exercises == null || exercises.Count == 0) {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Exercises not found! Try again!");
                        break;
                    }
                    else {
                        SendExerciseMessage(botClient, message);
                        currentStage[message.Chat.Id] = "";
                        break;
                    }
                case "/namedb":
                    exercises = await GetExerciseDatabase(0, message.Text, message.From.Id);
                    if (message == null || exercises == null || exercises.Count == 0) {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Exercises not found! Try again!");
                        break;
                    }
                    else {
                        currentStage[message.Chat.Id] = "database";
                        SendExerciseMessage(botClient, message);
                        break;
                    }
                case "/typedb":
                    exercises = await GetExerciseDatabase(3, message.Text, message.From.Id);
                    if (message == null || exercises == null || exercises.Count == 0) {
                        inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                        new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                        });
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Exercises not found! Try again!", replyMarkup: inlineKeyboardMarkup);
                        break;
                    }
                    else {
                        currentStage[message.Chat.Id] = "database";
                        SendExerciseMessage(botClient, message);
                        break;
                    }
                case "/difficultdb":
                    exercises = await GetExerciseDatabase(2, message.Text, message.From.Id);
                    if (message == null || exercises == null || exercises.Count == 0) {
                        inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                        new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                        });
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Exercises not found! Try again!", replyMarkup: inlineKeyboardMarkup);
                        break;
                    }
                    else {
                        currentStage[message.Chat.Id] = "database";
                        SendExerciseMessage(botClient, message);
                        break;
                    }
                case "/muscledb":
                    exercises = await GetExerciseDatabase(1, message.Text, message.From.Id);
                    if (message == null || exercises == null || exercises.Count == 0) {
                        inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                        new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                        });
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Exercises not found! Try again!", replyMarkup: inlineKeyboardMarkup);
                        break;
                    }
                    else {
                        currentStage[message.Chat.Id] = "database";
                        SendExerciseMessage(botClient, message);
                        break;
                    }
                case "/creare_exercise":
                    await GetExercise(message.Text);
                    break;

                    async Task GetExercise(string input) {
                        string[] fill = input.Split('\n');
                        if (fill.Length >= 6) {

                            string name = fill[0];
                            string type = fill[1];
                            string muscle = fill[2];
                            string equipment = fill[3];
                            string difficulty = fill[4];
                            string instructions = fill[5];

                            ExercisesClient exerciseUser = new ExercisesClient();
                            Exercise exercise = new Exercise();
                            exercise.name = name;
                            exercise.type = type;
                            exercise.muscle = muscle;
                            exercise.equipment = equipment;
                            exercise.difficulty = difficulty;
                            exercise.instructions = instructions;
                            exercise.userid = message.From.Id;
                            await exerciseUser.CreateExercise(exercise);

                            var messageText = $"<b><u>Your exercise:</u></b>\n\n" +
                                              $"<b>Name:</b> {exercise.name}\n\n" +
                                              $"<b>Type:</b> {exercise.type}\n\n" +
                                              $"<b>Muscle:</b> {exercise.muscle}\n\n" +
                                              $"<b>Difficulty:</b> {exercise.difficulty}\n\n" +
                                              $"<b>Equipment:</b> {exercise.equipment}\n\n" +
                                              $"<b>Instructions:</b> \n{exercise.instructions}";
                            inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                                                   new [] {InlineKeyboardButton.WithCallbackData("✏ Edit")},
                                                   new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                                                   });
                            await botClient.SendTextMessageAsync(message.Chat.Id, messageText, replyMarkup: inlineKeyboardMarkup, parseMode: ParseMode.Html);
                            currentStage[message.Chat.Id] = "";
                        }
                        else {
                            inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                            new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                            });
                            await botClient.SendTextMessageAsync(message.Chat.Id, "<u>Input Error!</u>\n<b>Try entering the data again, or return to the menu.</b>", replyMarkup: inlineKeyboardMarkup, parseMode: ParseMode.Html);

                        }
                    }

                case "/edit_exercise":
                    var exercise = exercises[currentIndex[message.Chat.Id]];
                    int id = exercise.id;
                    await PutExercise(message.Text, id);
                    break;

                    async Task PutExercise(string input, int id) {
                        string[] fill = input.Split('\n');
                        if (fill.Length >= 6) {

                            string name = fill[0];
                            string type = fill[1];
                            string muscle = fill[2];
                            string equipment = fill[3];
                            string difficulty = fill[4];
                            string instructions = fill[5];

                            ExercisesClient exerciseUser = new ExercisesClient();
                            Exercise exercise = new Exercise();
                            exercise.name = name;
                            exercise.type = type;
                            exercise.muscle = muscle;
                            exercise.equipment = equipment;
                            exercise.difficulty = difficulty;
                            exercise.instructions = instructions;
                            exercise.userid = message.From.Id;
                            exercise.id = id;
                            await exerciseUser.EditExercise(exercise);

                            var messageText = $"<b><u>Your updated exercise:</u></b>\n\n" +
                                              $"<b>Name:</b> {exercise.name}\n\n" +
                                              $"<b>Type:</b> {exercise.type}\n\n" +
                                              $"<b>Muscle:</b> {exercise.muscle}\n\n" +
                                              $"<b>Difficulty:</b> {exercise.difficulty}\n\n" +
                                              $"<b>Equipment:</b> {exercise.equipment}\n\n" +
                                              $"<b>Instructions:</b> \n{exercise.instructions}";
                            inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                                                   new [] {InlineKeyboardButton.WithCallbackData("✏ Edit")},
                                                   new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                                                   });
                            await botClient.SendTextMessageAsync(message.Chat.Id, messageText, replyMarkup: inlineKeyboardMarkup, parseMode: ParseMode.Html);
                            currentStage[message.Chat.Id] = "";
                        }
                        else {
                            inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                            new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                            });
                            await botClient.SendTextMessageAsync(message.Chat.Id, "<u>Input Error!</u>\n<b>Try entering the data again, or return to the menu.</b>", replyMarkup: inlineKeyboardMarkup, parseMode: ParseMode.Html);

                        }
                    }
            }
        }

        private async Task HandlerCallbackQueryAsync(ITelegramBotClient botClient, Update update) {
            var message = update.CallbackQuery.Message;
            var data = update.CallbackQuery.Data;
            ExercisesClient exerciseUser = new ExercisesClient();

            switch (data) {
                case "🏋🏻 By name":
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<u><b>Enter the exercise name</b></u> (e.g. \"press\" will match Dumbbell Bench Press\nIndicate the exact name of the exercise (if it is written with a dash, then indicate it here as well)", parseMode: ParseMode.Html);
                    currentStage[message.Chat.Id] = "/name";
                    currentIndex[message.Chat.Id] = 0;
                    break;
                case "🏋🏻 Name":
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<u><b>Enter the exercise name:</b></u>", parseMode: ParseMode.Html);
                    currentStage[message.Chat.Id] = "/namedb";
                    currentIndex[message.Chat.Id] = 0;
                    break;
                case "🤸🏻 By type":
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[]
                    {new [] { InlineKeyboardButton.WithCallbackData("🫀 Cardio"), InlineKeyboardButton.WithCallbackData("💪 Strength")},
                     new [] { InlineKeyboardButton.WithCallbackData("🏃 Plyometrics"), InlineKeyboardButton.WithCallbackData("🏋️ Powerlifting")},
                     new [] { InlineKeyboardButton.WithCallbackData("🧘 Stretching"), InlineKeyboardButton.WithCallbackData("👨 Strongman")},
                     new [] { InlineKeyboardButton.WithCallbackData("🏋🏻 Olympic Weightlifting")},
                     new [] { InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                    });
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Select the type of exercise:", replyMarkup: inlineKeyboardMarkup);
                    break;

                case "💪🏻 By muscle":
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[]
                    {new [] { InlineKeyboardButton.WithCallbackData("🧑 Abdominals"), InlineKeyboardButton.WithCallbackData("💪 Abductorsh"), InlineKeyboardButton.WithCallbackData("🦾 Adductors"),},
                     new [] { InlineKeyboardButton.WithCallbackData("💪🏻️ Biceps"), InlineKeyboardButton.WithCallbackData("🦵 Calves"), InlineKeyboardButton.WithCallbackData("👨 Chest")},
                     new [] { InlineKeyboardButton.WithCallbackData("🧍 Forearms"), InlineKeyboardButton.WithCallbackData("🦿 Glutes"), InlineKeyboardButton.WithCallbackData("🏃‍♂️ Hamstrings") },
                     new [] { InlineKeyboardButton.WithCallbackData("🏋️‍♂️ Lats"), InlineKeyboardButton.WithCallbackData("🏋️‍♀️ Lower Back"), InlineKeyboardButton.WithCallbackData("🏋️ Middle Back") },
                     new [] { InlineKeyboardButton.WithCallbackData("🧏 Neck"), InlineKeyboardButton.WithCallbackData("🤸 Quadriceps"), InlineKeyboardButton.WithCallbackData("💆‍♂️ Traps") },
                     new [] { InlineKeyboardButton.WithCallbackData("💪🏼 Triceps") },
                     new [] { InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                     });
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Select the muscle:", replyMarkup: inlineKeyboardMarkup);
                    break;
                case "📈 By difficulty":
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[]
                    {new [] { InlineKeyboardButton.WithCallbackData("👶 Beginner")},
                     new [] { InlineKeyboardButton.WithCallbackData("👦 Intermediate")},
                     new [] { InlineKeyboardButton.WithCallbackData("🧔 Expert") },
                     new [] { InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                    });
                    currentIndex[message.Chat.Id] = 0;
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Select the difficulty of exercise:", replyMarkup: inlineKeyboardMarkup);
                    break;
                case "🫀 Cardio":
                    exercises = await GetExercisePublicAPI(1, "cardio");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "💪 Strength":
                    exercises = await GetExercisePublicAPI(1, "strength");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🏃 Plyometrics":
                    exercises = await GetExercisePublicAPI(1, "plyometrics");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🏋️ Powerlifting":
                    exercises = await GetExercisePublicAPI(1, "powerlifting");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🧘 Stretching":
                    exercises = await GetExercisePublicAPI(1, "stretching");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "👨 Strongman":
                    exercises = await GetExercisePublicAPI(1, "strongman");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🏋🏻 Olympic Weightlifting":
                    exercises = await GetExercisePublicAPI(1, "olympic_weightlifting");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🧑 Abdominals":
                    exercises = await GetExercisePublicAPI(0, "abdominals");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "💪 Abductors":
                    exercises = await GetExercisePublicAPI(0, "abductors");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🦾 Adductors":
                    exercises = await GetExercisePublicAPI(0, "adductors");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "💪🏻️ Biceps":
                    exercises = await GetExercisePublicAPI(0, "biceps");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🦵 Calves":
                    exercises = await GetExercisePublicAPI(0, "calves");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "👨 Chest":
                    exercises = await GetExercisePublicAPI(0, "chest");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🧍 Forearms":
                    exercises = await GetExercisePublicAPI(0, "forearms");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🦿 Glutes":
                    exercises = await GetExercisePublicAPI(0, "glutes");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🏃‍♂️ Hamstrings":
                    exercises = await GetExercisePublicAPI(0, "hamstrings");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🏋️‍♂️ Lats":
                    exercises = await GetExercisePublicAPI(0, "lats");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🏋️‍♀️ Lower Back":
                    exercises = await GetExercisePublicAPI(0, "lower_back");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🏋️ Middle Back":
                    exercises = await GetExercisePublicAPI(0, "middle_back");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🧏 Neck":
                    exercises = await GetExercisePublicAPI(0, "neck");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🤸 Quadriceps":
                    exercises = await GetExercisePublicAPI(0, "quadriceps");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "💆‍♂️ Traps":
                    exercises = await GetExercisePublicAPI(0, "traps");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "💪🏼 Triceps":
                    exercises = await GetExercisePublicAPI(0, "triceps");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "👶 Beginner":
                    exercises = await GetExercisePublicAPI(2, "beginner");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "👦 Intermediate":
                    exercises = await GetExercisePublicAPI(2, "intermediate");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "🧔 Expert":
                    exercises = await GetExercisePublicAPI(2, "expert");
                    await SendExerciseMessage(botClient, message);
                    break;
                case "◀ Back to menu":
                    currentStage[message.Chat.Id] = "/menu";
                    currentIndex[message.Chat.Id] = 0;
                    await SendMenuMessage(message);
                    break;
                case "⬅️ Previous exercise":
                    currentIndex[message.Chat.Id]--;
                    await SendExerciseMessage(botClient, message);
                    break;
                case "Next exercise ➡️":
                    currentIndex[message.Chat.Id]++;
                    await SendExerciseMessage(botClient, message);
                    break;
                case "📝 Create your exercise":
                    currentStage[message.Chat.Id] = "/creare_exercise";
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<b><u>Enter all parameters (each on a new line without numbers):</u></b>\n\n<b>1. Exercise name</b>\n<b>2. Exercise type</b>\n<b>3. Exercise muscle</b>\n<b>4. Exercise difficulty</b>\n<b>5. Exercise equipment</b>\n<b>6. Exercise instructions</b>\n", parseMode: ParseMode.Html);
                    break;
                case "📋 View all your exercises":
                    exercises = await GetAllExercise(message);
                    if (message == null || exercises == null || exercises.Count == 0) {
                        inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                        new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                        });
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Exercise not found! Create an exercise with a button \"📝 Create your exercise\"", replyMarkup: inlineKeyboardMarkup);
                        break;
                    }
                    else {
                        await SendExerciseMessage(botClient, message);
                        currentStage[message.Chat.Id] = "database";
                        break;
                    }
                case "Delete 🗑️":
                    var exercise = exercises[currentIndex[message.Chat.Id]];
                    int id = exercise.id;
                    long userId = exercise.userid;
                    await exerciseUser.DeleteExercise(id, userId);
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                        new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                        });
                    await botClient.EditMessageTextAsync(message.Chat.Id, message.MessageId, "Exercise successfully removed!", replyMarkup: inlineKeyboardMarkup, parseMode: ParseMode.Html);
                    break;
                case "✏ Edit":
                    currentStage[message.Chat.Id] = "/edit_exercise";
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<b><u>Enter all parameters (each on a new line without numbers):</u></b>\n\n<b>1. Exercise name</b>\n<b>2. Exercise type</b>\n<b>3. Exercise muscle</b>\n<b>4. Exercise difficulty</b>\n<b>5. Exercise equipment</b>\n<b>6. Exercise instructions</b>\n", parseMode: ParseMode.Html);
                    break;
                case "🔍 Search your exercises":
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[]
                    {new [] { InlineKeyboardButton.WithCallbackData("🏋🏻 Name"), },
                     new [] { InlineKeyboardButton.WithCallbackData("🤸🏻 Type"), },
                     new [] { InlineKeyboardButton.WithCallbackData("💪🏻 Muscle"), },
                     new [] { InlineKeyboardButton.WithCallbackData("📈 Difficulty") },
                     new [] { InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                    });
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Search by:", replyMarkup: inlineKeyboardMarkup);
                    break;
                case "🤸🏻 Type":
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<u><b>Enter the exercise type:</b></u>", parseMode: ParseMode.Html);
                    currentStage[message.Chat.Id] = "/typedb";
                    currentIndex[message.Chat.Id] = 0;
                    break;
                case "💪🏻 Muscle":
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<u><b>Enter the exercise muscle:</b></u>", parseMode: ParseMode.Html);
                    currentStage[message.Chat.Id] = "/typedb";
                    currentIndex[message.Chat.Id] = 0;
                    break;
                case "📈 Difficulty":
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<u><b>Enter the exercise difficulty:</b></u>", parseMode: ParseMode.Html);
                    currentStage[message.Chat.Id] = "/difficultdb";
                    currentIndex[message.Chat.Id] = 0;
                    break;
            }
        }

        private async Task<List<Exercise>> GetExercisePublicAPI(int a, string param) {
            ExercisesClient exerciseUser = new ExercisesClient();
            var result = await exerciseUser.GetExercise(a, param);
            return result;
        }

        private async Task<List<Exercise>> GetExerciseDatabase(int a, string param, long userId) {
            ExercisesClient exerciseUser = new ExercisesClient();
            var result = await exerciseUser.SearchExercise(a, param, userId);
            return result;
        }

        private async Task<List<Exercise>> GetAllExercise(Message message) {
            ExercisesClient exerciseUser = new ExercisesClient();
            var result = await exerciseUser.GetAllExercise(message.Chat.Id);
            return result;
        }

        private async Task SendExerciseMessage(ITelegramBotClient botClient, Message message) {
            Console.WriteLine(currentIndex[message.Chat.Id]);
            var exercise = exercises[currentIndex[message.Chat.Id]];
            var messageText = $"<b><u>Exercise №{currentIndex[message.Chat.Id] + 1}</u></b>\n\n" +
              $"<b>Name:</b> {exercise.name}\n\n" +
              $"<b>Type:</b> {exercise.type}\n\n" +
              $"<b>Muscle:</b> {exercise.muscle}\n\n" +
              $"<b>Difficulty:</b> {exercise.difficulty}\n\n" +
              $"<b>Equipment:</b> {exercise.equipment}\n\n" +
              $"<b>Instructions:</b> \n{exercise.instructions}";

            if (currentStage[message.Chat.Id] == "database") {
                if (currentIndex[message.Chat.Id] == 0) {
                    if (exercises.Count == 1) {
                        inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                        new [] {InlineKeyboardButton.WithCallbackData("✏ Edit"), InlineKeyboardButton.WithCallbackData("Delete 🗑️") },
                        new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                        });
                    }
                    else {
                        inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                        new [] {InlineKeyboardButton.WithCallbackData("Next exercise ➡️")},
                        new [] {InlineKeyboardButton.WithCallbackData("✏ Edit"), InlineKeyboardButton.WithCallbackData("Delete 🗑️") },
                        new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                        });
                    }
                }
                else if (currentIndex[message.Chat.Id] == exercises.Count - 1) {
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                    new [] {InlineKeyboardButton.WithCallbackData("⬅️ Previous exercise")},
                    new [] {InlineKeyboardButton.WithCallbackData("✏ Edit"), InlineKeyboardButton.WithCallbackData("Delete 🗑️") },
                    new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                    });
                }
                else {
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                    new [] {InlineKeyboardButton.WithCallbackData("⬅️ Previous exercise"),InlineKeyboardButton.WithCallbackData("Next exercise ➡️")},
                    new [] {InlineKeyboardButton.WithCallbackData("✏ Edit"), InlineKeyboardButton.WithCallbackData("Delete 🗑️") },
                    new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                    });
                }
            }
            else {
                if (currentIndex[message.Chat.Id] == 0) {
                    if (exercises.Count == 1) {
                        inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                        new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                        });
                    }
                    else {
                        inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                        new [] {InlineKeyboardButton.WithCallbackData("Next exercise ➡️")},
                        new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                        });
                    }
                }
                else if (currentIndex[message.Chat.Id] == exercises.Count - 1) {
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                    new [] {InlineKeyboardButton.WithCallbackData("⬅️ Previous exercise")},
                    new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                    });
                }
                else {
                    inlineKeyboardMarkup = new InlineKeyboardMarkup(new[] {
                    new [] {InlineKeyboardButton.WithCallbackData("⬅️ Previous exercise"),InlineKeyboardButton.WithCallbackData("Next exercise ➡️")},
                    new [] {InlineKeyboardButton.WithCallbackData("◀ Back to menu")}
                    });
                }
            }

            if (messageText.Length > 4096) {
                for (int x = 0; x < messageText.Length; x += 4096) {
                    int endIndex = Math.Min(x + 4096, messageText.Length);
                    string messagePart = messageText.Substring(x, endIndex - x);
                    await botClient.SendTextMessageAsync(message.Chat.Id, messagePart, replyMarkup: inlineKeyboardMarkup, parseMode: ParseMode.Html);
                }
                return;
            }

            if (message.ReplyMarkup != null && message.ReplyMarkup.InlineKeyboard != null) {
                await botClient.EditMessageTextAsync(message.Chat.Id, message.MessageId, messageText, replyMarkup: inlineKeyboardMarkup, parseMode: ParseMode.Html);
            }
            else {
                await botClient.SendTextMessageAsync(message.Chat.Id, messageText, replyMarkup: inlineKeyboardMarkup, parseMode: ParseMode.Html);
            }
        }

        private async Task SendMenuMessage(Message message) {
            ReplyKeyboardMarkup replyKeyboardMarkup = new ReplyKeyboardMarkup(new[] {
            new [] {new KeyboardButton("🔎 Find exercises"), new KeyboardButton("📋 View saved exercises"),new KeyboardButton("🤖 About bot")}
            }) { 
                ResizeKeyboard = true
            };

            await botClient.SendTextMessageAsync(message.Chat.Id, "<u><b>Select a menu item:</b></u>\n\n<b>🔎 Find exercises</b> - Search for exercises by conditions\n<b>📋 View saved exercises</b> - Managing saved exercises\n<b>🤖 About bot</b> - Information about the bot and creator", replyMarkup: replyKeyboardMarkup, parseMode: ParseMode.Html);
            return;
        }
    }
} 