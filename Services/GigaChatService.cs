using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kiosk.Services
{
    /// <summary>
    /// Сервис для работы с GigaChat API (Sber).
    /// Документация: https://developers.sber.ru/docs/ru/gigachat/api/reference/rest/post-token
    /// </summary>
    public class GigaChatService
    {
        private static readonly HttpClientHandler _handler = new HttpClientHandler
        {
            // GigaChat использует самоподписанный сертификат Сбера
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        private static readonly HttpClient _http = new HttpClient(_handler);

        private string _accessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        private const string AuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
        private const string ApiUrl = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";
        private const string Scope = "GIGACHAT_API_PERS"; // для физлиц; для корп: GIGACHAT_API_CORP

        /// <summary>
        /// Получить ответ от GigaChat.
        /// </summary>
        /// <param name="authorizationKey">Ключ авторизации из личного кабинета (Base64 clientId:clientSecret)</param>
        /// <param name="systemPrompt">Системный контекст (данные приложения)</param>
        /// <param name="userMessage">Вопрос пользователя</param>
        public async Task<string> AskAsync(string authorizationKey, string systemPrompt, string userMessage)
        {
            if (string.IsNullOrWhiteSpace(authorizationKey))
                return "❌ API-ключ GigaChat не указан в настройках.";

            try
            {
                await EnsureTokenAsync(authorizationKey);

                var requestBody = new
                {
                    model = "GigaChat",
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user",   content = userMessage  }
                    },
                    max_tokens = 512,
                    temperature = 0.7
                };

                var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"❌ Ошибка GigaChat ({(int)response.StatusCode}): {json}";

                using var doc = JsonDocument.Parse(json);
                var text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return text ?? "Нет ответа";
            }
            catch (Exception ex)
            {
                return $"❌ Ошибка подключения: {ex.Message}";
            }
        }

        private async Task EnsureTokenAsync(string authorizationKey)
        {
            if (_accessToken != null && DateTime.Now < _tokenExpiry)
                return;

            var request = new HttpRequestMessage(HttpMethod.Post, AuthUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authorizationKey);
            request.Headers.Add("RqUID", Guid.NewGuid().ToString());
            request.Content = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("scope", Scope)
            });

            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Авторизация не удалась ({(int)response.StatusCode}): {json}");

            using var doc = JsonDocument.Parse(json);
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            // Токен живёт 30 минут, обновляем за 2 минуты до истечения
            _tokenExpiry = DateTime.Now.AddMinutes(28);
        }
    }
}