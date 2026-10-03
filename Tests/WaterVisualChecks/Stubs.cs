using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper {
 public static class BallisticsSystem {public static event System.Action<Vector3,float> OnShotNoise;}
 public sealed class ExplosiveProp:MonoBehaviour {public bool isDetonated;}
 public enum MobileGraphicsPreset {Performance,Balanced,High}
 public static class MobileGraphics {public static MobileGraphicsPreset Selected=>MobileGraphicsPreset.Balanced;}
 public sealed class SectorElevation {public float Sample(float x,float z)=>0;}
 public sealed partial class SectorWorld:MonoBehaviour {
 public static readonly List<SectorWorld> LoadedWorlds=new List<SectorWorld>();
 readonly List<Material> materials=new List<Material>();readonly List<Mesh> meshes=new List<Mesh>();
        public static Texture2D CreateNormalMap(int width, int height, System.Func<int, int, float> heightFunc, float strength)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                int yPrev = (y - 1 + height) % height;
                int yNext = (y + 1) % height;
                for (int x = 0; x < width; x++)
                {
                    int xPrev = (x - 1 + width) % width;
                    int xNext = (x + 1) % width;

                    float hL = heightFunc(xPrev, y);
                    float hR = heightFunc(xNext, y);
                    float hD = heightFunc(x, yPrev);
                    float hU = heightFunc(x, yNext);

                    float dx = (hR - hL) * strength;
                    float dy = (hU - hD) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;

                    pixels[y * width + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
 public float BaseHeight;
 float RawGround(float x,float z)=>BaseHeight;
 public float ReviewGround(float x,float z)=>ShapedGround(x,z,RawGround(x,z));
 public float ReviewShore(float x,float z)=>ShoreBlend(x,z);
 public float Ground(float x,float z)=>ReviewGround(x,z);
 public void Review(List<MapFeature> features){LoadedWorlds.Add(this);PrepareWater(features);foreach(var f in features)BuildWater(f);}
 }
}
