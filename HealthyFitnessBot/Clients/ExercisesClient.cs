using TGFitnessBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Telegram.Bot.Types;

namespace TGFitnessBot.Clients {
    public class Constants {
        public static string address = "https://localhost:7006/";
    }
    public class ExercisesClient {

        private HttpClient _httpClient;
        private static string _address;
        string url;

        public ExercisesClient() {
            _address = Constants.address;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(_address); 

        }
        public async Task<List<Exercise>> GetExercise(int a, string param) {

            if (a == 0) {
                url = $"/exercises/muscle/{param}";
            }
            else if (a == 1) {
                url = $"/exercises/type/{param}";
            }
            else if (a == 2) {
                url = $"/exercises/difficulty/{param}";
            }
            else {
                url = $"/exercises/name/{param}";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<List<Exercise>>(content);
            return result;
        }

        public async Task<List<Exercise>> GetAllExercise(long userID) {

            Console.WriteLine(userID);
            var response = await _httpClient.GetAsync($"/database/exercises/get/{userID}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<List<Exercise>>(content);
            return result;
        }

        public async Task CreateExercise(Exercise exercise) {
            var json = JsonConvert.SerializeObject(exercise);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"/database/exercises/post/", content);
            response.EnsureSuccessStatusCode();
            
            return;
        }

        public async Task EditExercise(Exercise exercise) {
            var json = JsonConvert.SerializeObject(exercise);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"/database/exercises/insert", content);
            response.EnsureSuccessStatusCode();
            return;
        }

        public async Task DeleteExercise(int id, long userid) {
            var response = await _httpClient.DeleteAsync($"/database/exercises/delete/{userid}/{id}");
            response.EnsureSuccessStatusCode();

            return;
        }

        public async Task<List<Exercise>> SearchExercise(int a, string param, long userId) {

            if (a == 0) {
                url = $"/database/exercises/get/{userId}/{param}";
            }
            else if (a == 1) {
                url = $"/database/exercises/get/muscle/{userId}/{param}";
            }
            else if (a == 2) {
                url = $"/database/exercises/get/difficulty/{userId}/{param}";
            }
            else {
                url = $"/database/exercises/get/type/{userId}/{param}";
            }

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<List<Exercise>>(content);
            return result;
        }
    }
}
