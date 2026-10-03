import ctypes
from ctypes import wintypes

user32 = ctypes.windll.user32

def enum_windows():
    results = []
    def callback(hwnd, extra):
        if user32.IsWindowVisible(hwnd):
            length = user32.GetWindowTextLengthW(hwnd)
            if length > 0:
                buff = ctypes.create_unicode_buffer(length + 1)
                user32.GetWindowTextW(hwnd, buff, length + 1)
                pid = wintypes.DWORD()
                user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
                results.append((hwnd, pid.value, buff.value))
        return True
    
    WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
    user32.EnumWindows(WNDENUMPROC(callback), 0)
    return results

windows = enum_windows()
for hwnd, pid, title in windows:
    if any(k in title.lower() for k in ['unity', 'sniper', 'chatgpt']):
        print(f"HWND: {hwnd}, PID: {pid}, Title: '{title}'")
