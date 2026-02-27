using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Kiosk.Services
{
    public class WeatherInfo
    {
        public double Temperature { get; set; }
        public double FeelsLike { get; set; }
        public int WeatherCode { get; set; }
        public double WindSpeed { get; set; }
        public int Humidity { get; set; }
        public string CityName { get; set; }
        public bool IsLoaded { get; set; }
        public string ErrorMessage { get; set; }

        public string WeatherEmoji => WeatherCode switch
        {
            0 => "☀️",
            1 => "🌤️",
            2 => "⛅",
            3 => "☁️",
            45 or 48 => "🌫️",
            51 or 53 or 55 => "🌦️",
            61 or 63 or 65 => "🌧️",
            71 or 73 or 75 => "❄️",
            77 => "🌨️",
            80 or 81 or 82 => "🌧️",
            85 or 86 => "🌨️",
            95 => "⛈️",
            96 or 99 => "⛈️",
            _ => "🌡️"
        };

        public string WeatherDescription => WeatherCode switch
        {
            0 => "Ясно",
            1 => "Преимущественно ясно",
            2 => "Переменная облачность",
            3 => "Пасмурно",
            45 => "Туман",
            48 => "Изморозь",
            51 => "Лёгкая морось",
            53 => "Морось",
            55 => "Сильная морось",
            61 => "Небольшой дождь",
            63 => "Дождь",
            65 => "Сильный дождь",
            71 => "Небольшой снег",
            73 => "Снег",
            75 => "Сильный снег",
            77 => "Снежные зёрна",
            80 => "Небольшой ливень",
            81 => "Ливень",
            82 => "Сильный ливень",
            85 => "Снегопад",
            86 => "Сильный снегопад",
            95 => "Гроза",
            96 => "Гроза с градом",
            99 => "Гроза с сильным градом",
            _ => "Неизвестно"
        };
    }

    public class WeatherService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        static WeatherService()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SchoolKiosk/1.0");
        }

        /// <summary>
        /// Получает текущую погоду. Если координаты не заданы — определяет геолокацию по IP.
        /// </summary>
        public static async Task<WeatherInfo> GetWeatherAsync(
            double? lat = null, double? lon = null, string cityName = null)
        {
            try
            {
                // Автоопределение по IP если координаты не заданы
                if (lat == null || lon == null)
                {
                    var geo = await GetLocationByIpAsync();
                    if (geo == null)
                        return new WeatherInfo { ErrorMessage = "Не удалось определить местоположение" };

                    lat = geo.Item1;
                    lon = geo.Item2;
                    if (string.IsNullOrWhiteSpace(cityName))
                        cityName = geo.Item3;
                }

                var url = $"https://api.open-meteo.com/v1/forecast" +
                          $"?latitude={lat.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}" +
                          $"&longitude={lon.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}" +
                          $"&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,relative_humidity_2m" +
                          $"&wind_speed_unit=ms&timezone=auto";

                var json = await _http.GetStringAsync(url);
                var root = JsonConvert.DeserializeObject<OpenMeteoResponse>(json);

                return new WeatherInfo
                {
                    Temperature = Math.Round(root.Current.Temperature, 1),
                    FeelsLike = Math.Round(root.Current.ApparentTemperature, 1),
                    WeatherCode = root.Current.WeatherCode,
                    WindSpeed = Math.Round(root.Current.WindSpeed, 1),
                    Humidity = root.Current.Humidity,
                    CityName = string.IsNullOrWhiteSpace(cityName) ? "Ваш город" : cityName,
                    IsLoaded = true
                };
            }
            catch (Exception ex)
            {
                return new WeatherInfo { ErrorMessage = ex.Message };
            }
        }

        private static async Task<Tuple<double, double, string>> GetLocationByIpAsync()
        {
            try
            {
                var json = await _http.GetStringAsync("http://ip-api.com/json/?fields=lat,lon,city");
                var obj = JsonConvert.DeserializeObject<IpApiResponse>(json);
                if (obj != null)
                    return Tuple.Create(obj.Lat, obj.Lon, obj.City);
            }
            catch { }
            return null;
        }

        // JSON-модели ────────────────────────────────────────────────────────────

        private class OpenMeteoResponse
        {
            [JsonProperty("current")]
            public CurrentWeather Current { get; set; }
        }

        private class CurrentWeather
        {
            [JsonProperty("temperature_2m")]
            public double Temperature { get; set; }

            [JsonProperty("apparent_temperature")]
            public double ApparentTemperature { get; set; }

            [JsonProperty("weather_code")]
            public int WeatherCode { get; set; }

            [JsonProperty("wind_speed_10m")]
            public double WindSpeed { get; set; }

            [JsonProperty("relative_humidity_2m")]
            public int Humidity { get; set; }
        }

        private class IpApiResponse
        {
            [JsonProperty("lat")] public double Lat { get; set; }
            [JsonProperty("lon")] public double Lon { get; set; }
            [JsonProperty("city")] public string City { get; set; }
        }
    }
}
