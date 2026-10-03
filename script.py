import urllib.request
import os

url = 'https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Buggy/glTF-Binary/Buggy.glb'
out_path = 'C:/Users/Geeta Sugara/Documents/ChatGPT/Sniper/Assets/Resources/Models/CarAsset.glb'

try:
    urllib.request.urlretrieve(url, out_path)
    print('Downloaded Buggy.glb')
except Exception as e:
    print('Error:', e)
