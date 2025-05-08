# 🧩 SledChannel - TCP 通信模块

一个轻量、高效、具备请求响应匹配能力的 TCP 异步通信组件。

---

## 📝 关键词

- TCP 双工通信  
- 请求-响应模式  
- 异步队列  
- CRLF 编解码  
- 高并发 / 限流  
- 零拷贝（TryDecode）  
- 双线程协程收发模型  
- IPv4 / IPv6 自动适配  

---

## 📦 模块目标

SledChannel 实现了一个基于 TCP 的通信通道，用于发送指令、接收响应，支持并发请求且能确保**响应准确匹配请求**，具备**不乱序、不丢包**的特性，适合小消息高频交互场景。

---

## 📐 模块组成

### 1️⃣ `SledLinkTcp`

- 建立 TCP 链接，自动适配 IPv4 / IPv6  
- 配置优化 Socket 参数：启用 `NoDelay` 和 `KeepAlive`  
- 使用 `NetworkStream` 作为读写介质  

### 2️⃣ `KvAsciiCodec`

- 实现基于 CRLF（`\r\n`） 的 ASCII 文本协议  
- 示例：发送 `"PING\r\n"`，接收 `"PONG\r\n"`  
- 编码：自动追加 CRLF；解码：按 CRLF 拆帧  

### 3️⃣ `SledChannel`

模块调度核心，管理请求队列、数据发送、响应匹配与资源释放。

#### 发送端：

- 使用 Channel 管理请求队列  
- 每个请求绑定 `TaskCompletionSource` 等待返回  
- 通过 `_maxInFlight` 限制并发数量防止过载  

#### 接收端：

- 后台协程监听 `NetworkStream`  
- 使用 `PipeReader` 拆包并调用 `TryDecode`  
- 接收响应后与请求进行一一匹配  

#### 异常处理与资源管理：

- 所有缓冲区通过 `ArrayPool` 管理，确保零拷贝  
- 每次通信后归还缓冲  
- 实现 `DisposeAsync()`，确保资源释放与协程安全退出  

---

## 🔁 通信流程（简化示意）

```plaintext
          [调用方 CallAsync()]
                   ↓
         ┌───── 加入发送队列 ─────┐
         ↓                       ↓
    [发送协程] ─── send → [远程设备]
         ↑                       ↓
         └──── 配对 Task 返回 ───┘
                   ↑
            [接收协程解帧]
```

---

## 🔧 使用示例（伪码）

```csharp
var link = new SledLinkTcp();
await link.ConnectAsync("127.0.0.1", 9001);

var channel = new SledChannel(link, new KvAsciiCodec());

var reply = await channel.CallAsync(Encoding.ASCII.GetBytes("PING"));
Console.WriteLine(Encoding.ASCII.GetString(reply.Span));  // 输出：PONG
```

---

## 🧠 小结

虽然结构简单，SledChannel 实际上是一个**高度优化的异步 TCP 通信模块**。  
特别适用于以下场景：

- 高频率、小消息请求  
- 保证响应顺序准确  
- 不想与裸 `Stream` 打交道  

> 它是你在构建现代高性能客户端或中间层通信时，可以信赖的工具之一。

---
