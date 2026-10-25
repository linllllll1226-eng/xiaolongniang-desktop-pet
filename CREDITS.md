# 来源与致谢

本页区分素材来源、效果参考、生成服务和构建依赖。说明依据为本项目保留的制作脚本与记录，以及以下可核对的原作者页面。

## 角色与美术

| 内容 | 来源 | 本项目中的用途与证据范围 |
| --- | --- | --- |
| 龙娘三视图 | **B 站 UP 主「这个刀子真甜」** | 由维护者提供图片及出处，用于角色外形、服装、角、翼和尾巴的参考。原始三视图未收入公开仓库或本次运行包；具体原始发布页尚未定位。 |
| 角色形象的上游署名 | **ZipZipPipe** | 「这个刀子真甜」的[相关视频](https://www.bilibili.com/video/BV1EvKK6NEoi/)简介注明女仆和龙女形象来源于 ZipZipPipe，因此一并致谢。此处不把该视频当作三视图原始发布页或授权证明。 |
| 二维角色主图及部分姿态图 | OpenAI 图像生成工具（开发环境内置 imagegen） | 以提供的角色图为参考进行图像整理和编辑，再由本地工具对齐、透明处理。使用 AI 辅助不改变参考形象的来源署名。 |
| 动作视频 | [即梦 AI](https://jimeng.jianying.com/) / Seedance | 按统一人物参考制作动作，再做透明导出、循环修整和接续。本仓库不提供生成账号、会员权益或原始下载地址。 |
| 图标、菜单与预览图 | 本项目的角色图及运行程序 / 离线预览 | 角色图像沿用上述来源；菜单与交互界面由当前 WPF 实现和本地预览生成。仓库中的 `docs/preview.png` 为 v10.9 离线预览截图。 |

## 项目参考

| 项目 | 作者 / 维护方 | 参考范围 |
| --- | --- | --- |
| [dsh-pet](https://github.com/PC2005-cloud/dsh-pet) | PC2005-cloud 及其贡献者 | 透明动画桌宠效果、桌面显示尺寸、AI 视频素材链及交互体验参考；本地 v10 制作计划保留该参考地址。 |
| [xiaoxiao-zhuochong](https://github.com/yshuang722-spec/xiaoxiao-zhuochong) | yshuang722-spec 及其贡献者 | 早期桌宠原型与使用体验参考；早期样片脚本保留该项目地址。该项目自身也注明基于 dsh-pet。 |

本次发布的是 C# / WPF 运行时及对应构建、检查工具。上述项目的 TypeScript / Electron 引擎、安装器和默认角色未打包到本次发布中；“参考”不表示本项目是对方官方版本或分支。上游仓库当前声明 MIT 许可证，各自的许可仍以原仓库为准。

## 软件与工具

| 工具 / 平台 | 用途 | 依赖范围 |
| --- | --- | --- |
| Microsoft .NET Framework、WPF、Windows Forms、Windows UI Automation | 桌面透明窗口、交互、菜单、状态桥、客户端控件观察 | Windows 运行与源码编译；使用系统框架程序集，没有捆绑第三方桌宠引擎。 |
| Windows PowerShell | 构建、资源导入、原生检查 | 源码构建与维护。 |
| [Node.js](https://nodejs.org/) | 本地浏览器检查和发布工具 | 可选检查工具；桌宠本体运行不需要 Node.js。 |
| [Chromium](https://www.chromium.org/)、Google Chrome / Microsoft Edge | 离线 HTML 动作预览、开发者协议检查 | 浏览器由使用者自行安装；运行包未捆绑浏览器。 |
| [Python](https://www.python.org/) | 原始素材加工与离线分析脚本 | 制作阶段；从运行包导入已加工资源后，当前源码构建不需要 Python。 |
| [OpenCV](https://opencv.org/) | 绿幕处理、配准、光流及补帧 | 素材加工阶段，未随运行包分发库二进制。 |
| [NumPy](https://numpy.org/) | 像素、透明度与帧数据计算 | 素材加工阶段。 |
| [Pillow](https://python-pillow.org/) | 图像读取、合成、裁剪与导出 | 素材加工阶段。 |
| [FFmpeg](https://ffmpeg.org/) | 动画视频编码、透明 WebM 导出 | 素材加工阶段；运行包中的媒体是加工结果，未捆绑 FFmpeg 可执行文件。 |
| OpenAI ChatGPT / Codex | 辅助开发、代码与素材检查；本地状态动作联动目标 | 未捆绑模型或 API 凭据；本项目为个人作品，未声称得到相关产品方背书。 |

制作使用的完整版本锁定、所有生成模型子版本和原图公开许可未形成完整可公开记录，本页不补写未经确认的版本或授权结论。权利说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
