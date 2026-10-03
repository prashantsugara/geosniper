using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoSniper
{
    public class LiveWeatherController : MonoBehaviour
    {
        [System.Serializable]
        private class WeatherResponse
        {
            public CurrentWeather current_weather;
        }

        [System.Serializable]
        private class CurrentWeather
        {
            public float temperature;
            public float windspeed;
            public float winddirection;
            public int weathercode;
            public int is_day;
            public string time;
        }

        public void FetchWeather(double latitude, double longitude, System.Action<int, bool> onWeatherFetched)
        {
            StartCoroutine(FetchWeatherCoroutine(latitude, longitude, onWeatherFetched));
        }

        private IEnumerator FetchWeatherCoroutine(double latitude, double longitude, System.Action<int, bool> onWeatherFetched)
        {
            string url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current_weather=true";

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogWarning("Failed to fetch live weather: " + webRequest.error);
                }
                else
                {
                    try
                    {
                        string json = webRequest.downloadHandler.text;
                        WeatherResponse response = JsonUtility.FromJson<WeatherResponse>(json);
                        
                        if (response != null && response.current_weather != null)
                        {
                            int weatherCode = response.current_weather.weathercode;
                            bool isDay = response.current_weather.is_day == 1;
                            float rad = response.current_weather.winddirection * Mathf.Deg2Rad;
                            float spd = response.current_weather.windspeed * 0.27778f; // km/h to m/s
                            BallisticsSystem.Wind = new Vector3(Mathf.Sin(rad) * spd, 0, Mathf.Cos(rad) * spd);
                            onWeatherFetched?.Invoke(weatherCode, isDay);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("Error parsing weather JSON: " + e.Message);
                    }
                }
            }
        }
    }
}
