# AudioPilot

AudioPilot 是一个面向 Windows 的轻量级 .NET 10 工具。它监听音频端点的新增、移除和状态变化，并自动在指定耳机与备用扬声器之间切换默认输出。

## 配置

编辑 `appsettings.json`：

```json
{
  "HeadsetNameContains": "ROG",
  "FallbackNameContains": "Speakers",
  "SwitchDelayMilliseconds": 750
}
```

- `HeadsetNameContains`：耳机设备名称中包含的文字。
- `FallbackNameContains`：备用输出设备名称中包含的文字；中文 Windows 上通常需要改成设备实际名称，例如“扬声器”。
- `SwitchDelayMilliseconds`：设备变化后等待多久再切换，用于避开驱动初始化抖动。

## 运行

```powershell
dotnet run
```

按 `Ctrl+C` 退出。

## ROG Delta II / 棱镜 2 的限制

如果 2.4G 接收器一直插着，耳机关机后 Windows 仍可能把接收器对应的音频端点报告为 `Active`。这种情况下 Windows Core Audio 不会发出可用于判断耳机无线链路断开的状态变化，AudioPilot 也就无法仅凭端点状态识别耳机本体是否关机。

当前版本适用于插拔接收器，或驱动会在耳机开关机时改变端点状态的设备。后续可以针对 ROG USB/HID 遥测继续增加检测方式。

## 技术说明

项目直接调用 Windows Core Audio COM API，不依赖第三方 NuGet 包。默认输出会同时设置到 Console、Multimedia 和 Communications 三种角色。

## License

[MIT](LICENSE)
