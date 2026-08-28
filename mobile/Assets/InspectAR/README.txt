InspectAR（M6）

真机后端地址
- 界面可改，会写入 PlayerPrefs。
- 不要用 localhost / 127.0.0.1。填电脑局域网 IP，例如 http://192.168.1.8:8080
- 电脑与手机同一 WiFi；Go 监听 0.0.0.0:8080。

编辑器
- 菜单 InspectAR/Setup Project：写入 XR Origin 场景、ARCore Loader、Android minSdk 25 / ARM64 / 允许 HTTP。
- 菜单 InspectAR/Verify：检查上述项。
- 批处理：Unity.exe -batchmode -nographics -projectPath <本目录> -executeMethod InspectARSetup.Run -quit -logFile Logs/inspect-setup.log

出包
- File > Build Settings > Android > Build。
- 需 ARCore 真机；本窗口不打开游戏、不代替 M1 查收。
