// Minimal engine substitutes; these checks execute the production codec and UDP transport.
namespace UnityEngine {
 public class MonoBehaviour {
  protected static T FindAnyObjectByType<T>() where T:class => null;
  protected static void Destroy(object o) {} protected static void DontDestroyOnLoad(object o) {}
  public GameObject gameObject = null;
 }
 public class GameObject { public GameObject(string name) {} public T AddComponent<T>() where T:new()=>new T(); }
 public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} }
 public static class Mathf { public static int RoundToInt(float v)=>(int)System.Math.Round(v); public static int Clamp(int v,int a,int b)=>System.Math.Clamp(v,a,b); }
 public static class Time { private static readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew(); public static float unscaledTime=>(float)clock.Elapsed.TotalSeconds; }
 public static class Debug { public static void LogWarning(string message) {} }
 public static class PlayerPrefs { public static string GetString(string key,string fallback)=>fallback;public static int GetInt(string key,int fallback)=>fallback; }
 public static class Random { public static int Range(int a,int b)=>System.Random.Shared.Next(a,b); }
}
