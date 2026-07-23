# 架构设计

## 原则

应用以 `PetApplicationContext` 持有全部生命周期资源，宠物窗口不是进程生命锚点。依赖方向为 App → Platform/Rendering/Assets/Configuration/Runtime，Platform → Rendering/Configuration，Rendering → Assets/Runtime，Assets → Runtime；Runtime 与 Configuration 保持平台无关。

## 纵向流程

启动时，单实例协调器先取得 Mutex；已有实例通过只接受固定命令的 named pipe 被激活。主实例加载原子配置、建立托盘、从统一资产管线载入当前或内置宠物，再创建小尺寸 layered window 和按帧时长唤醒的调度器。资源切换先在窗口外完整加载和校验，成功后才交换运行时对象。

渲染器在加载时将 PNG 解码为 `Format32bppPArgb` spritesheet 并保留 CPU Alpha。单帧渲染复用目标 Bitmap、内存 DC 和 HBITMAP，将指定源格缩放/翻转到窗口后调用 `UpdateLayeredWindow`。运行时只发出动画名、行列和时序，不接触 Win32/GDI。

## 模块边界

| 模块 | 拥有 | 不拥有 |
| --- | --- | --- |
| App | 生命周期、菜单、设置 UI、日志、编排 | 像素操作、动画时间算法 |
| Platform.Windows | HWND、窗口样式、DPI/显示器、启动注册 | Manifest/动画策略 |
| Rendering | 解码后图像、后备缓冲、Alpha 坐标换算 | 菜单、配置持久化 |
| Runtime | 动画/Idle 状态和单调时间 | WinForms Timer、文件与图片 |
| Assets | 不可信包解析、Profile、校验、安装事务 | 当前窗口状态 |
| PetValidator | Assets 校验管线的控制台适配、JSON 与退出码 | 独立校验规则、正式宠物目录、桌面 UI |
| Configuration | 用户覆盖和原子 JSON 文件 | 资产包原始 Manifest |

## 资源所有权

`PetPackage` 持有解码 spritesheet；渲染器取得所有权后在切换/退出时释放。Layered window 持有 renderer，ApplicationContext 持有 window、托盘、timer、pipe 和应用服务。原生调用失败抛出带 Win32 error code 的异常并记录。

## 调度

`AnimationPlayer` 使用毫秒单调时间并允许一次 Tick 跨越多帧。UI `Timer` 只是唤醒适配器，每次按当前帧剩余时间重新安排；暂停时冻结时间基准，隐藏/静止时停止 timer。拖拽位置由窗口消息即时更新，不依赖动画帧率。

## 外部依赖

生产项目没有第三方 NuGet 包。`SeedDormitoryCorridor.PetValidator` 仅引用 Assets 项目，将共享 staging 与正式资源校验结果投影为机器可读 JSON；它不依赖 App、MessageBox 或交互式桌面。测试使用 xUnit（断言与执行模型）、Microsoft.NET.Test.Sdk（`dotnet test` 适配器）、xunit.runner.visualstudio 和 coverlet.collector（覆盖率采集）；它们不进入发布目录。

## 风险与对策

- `UpdateLayeredWindow` 与 Per-Monitor DPI 只能在交互桌面最终确认：保持 Win32 面最小，并维护逐项人工清单。
- GDI 泄漏：复用资源、成对 SelectObject/DeleteObject/DeleteDC，并让资源所有权单一。
- 图片解压炸弹/ZIP 穿越：限制压缩与解码尺寸、条目数/总大小，并使用规范化路径包含检查。
- 当前资产损坏：加载失败不交换旧对象；启动时回退内置资产或安全空状态。
- 配置/安装中断：同卷临时文件写入、flush 后原子 replace/move，替换安装保留备份直到提交。
