# AudioPilot

AudioPilot 是一个面向 Windows 的轻量级 .NET 10 工具。它监听音频端点的新增、移除和状态变化，并自动在指定耳机与备用扬声器之间切换默认输出。

## 配置

编辑 `appsettings.json`：

```json
{
  "HeadsetNameContains": "ROG",
  "FallbackNameContains": "U2790B",
  "SwitchDelayMilliseconds": 750,
  "PollIntervalMilliseconds": 2000,
  "HeadsetVendorId": 2821,
  "HeadsetProductId": 6906
}
```

- `HeadsetNameContains`：耳机设备名称中包含的文字。
- `FallbackNameContains`：备用输出设备名称中包含的文字；中文 Windows 上通常需要改成设备实际名称，例如“扬声器”。
- `SwitchDelayMilliseconds`：设备变化后等待多久再切换，用于避开驱动初始化抖动。
- `PollIntervalMilliseconds`：查询 ROG 2.4G 无线链路的间隔。
- `HeadsetVendorId` / `HeadsetProductId`：ROG Delta II 接收器的 USB VID/PID（默认 `0B05:1AFA` 的十进制值）。

## 运行

```powershell
dotnet run
```

按 `Ctrl+C` 退出。

## ROG Delta II / 棱镜 2 检测

ROG 2.4G 接收器在耳机关机后仍会保持 Windows 音频端点为 `Active`。AudioPilot 因此会额外通过接收器的厂商 HID 接口查询无线链路，而不是仅依赖 Core Audio 端点状态。无线链路断开时切换到备用输出，恢复时切回耳机。

## 技术说明

项目调用 Windows Core Audio COM API，并使用 HidSharp 访问 ROG 接收器的 HID 接口。默认输出会同时设置到 Console、Multimedia 和 Communications 三种角色。

## License

[MIT](LICENSE)
