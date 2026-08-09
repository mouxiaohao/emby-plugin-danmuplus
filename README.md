# emby-plugin-danmuplus

Emby 弹幕插件增强版，参考 [fengymi/emby-plugin-danmu](https://github.com/fengymi/emby-plugin-danmu) 开发。项目从原移植版继续演进，当前 `main` 对应版本为 **2.0.0**。

已验证的服务器环境：Synology 套件版 Emby **4.9.3.0**。其他 Emby 版本可能需要调整配置页或前端菜单兼容代码。

## 主要功能

- 爱奇艺、腾讯、优酷、哔哩哔哩、芒果 TV、弹弹 Play 等来源的弹幕搜索与下载。
- 多站点统一候选评分；季名、父剧名、年份和集数综合匹配。
- 同分候选按站点优先级排序。
- 过滤预告、PV、花絮等非正片候选，避免集数错乱和异常小 XML。
- 腾讯分段重试、连接重置重试，以及已完成分段合并为部分弹幕 XML。
- 七天 XML 重复跳过；支持强制刷新、后台队列、强制停止、单集重试和流式进度。
- STRM、115 挂载等普通 Emby 媒体库场景兼容。
- 配置页面版本显示为 `2.0.0`。

## 2.0.0 相比旧版 emby-plugin-danmu 的改动

本版本不是简单改名，而是在原项目基础上的独立增强线。主要差异如下：

### 搜索与匹配

- 爱奇艺搜索接口适配了新版返回结构，修复“缺少集号或集号超过弹幕数，忽略处理”等常见失败。
- B 站搜索增加多种请求参数和响应解析兼容，减少动画、日韩剧无候选的问题。
- 搜索时先使用父剧名轮询全部站点，再结合季名、年份、集数和别名评分；不再由站点优先级决定先搜哪个关键词。
- 候选按综合评分排序；最高分相同才使用站点优先级决定绑定结果。
- 识别并过滤预告、PV、花絮、特别篇等非正片候选，避免把这些内容当成正片集数。
- 每季独立匹配；某一季失败不会阻塞其他季度，并提供季度级手动绑定。
- 腾讯、爱奇艺、优酷、芒果、B 站和弹弹 Play 的结果统一纳入匹配流程。

### 下载稳定性

- 腾讯等分段弹幕下载支持失败重试、连接重置重试。
- 某一分段失败时，已成功分段仍会合并成可用 XML，并明确标记“部分弹幕缺失”。
- 下载结果区分成功、失败、跳过、部分缺失等状态，每集提供单独重试按钮。
- 默认按 XML 文件判断七天内是否重复下载，显示“重复已跳过”；可勾选强制刷新。
- 支持后台队列和强制停止，不要求前端一直保持下载窗口打开。
- 修复普通目录、STRM 文件和 115 媒体库季目录在季识别上的兼容问题。
- 增加弹弹 Play API ID/API Secret 配置输入，并避免凭据进入日志和错误提示。

### 前端操作

- 新增可独立安装的 CustomCssJS 前端脚本，电视剧详情页和季度详情页“更多”菜单可以一键下载整部剧或整季弹幕。
- 前端支持流式显示季度、集数和实时结果，支持自动匹配结果调整、手动候选选择、后台下载、停止和重试。
- 2.0.0 的 DLL 本身不修改 Emby 的 `dashboard-ui` 文件；需要菜单时手动安装 `Frontend/DanmuSmartMatch.CustomCssJS.js`。
- Android 原生客户端不加载 CustomCssJS，因此菜单需要通过 Emby Web 客户端或浏览器使用；如需 Android 客户端支持，请参考 [Emby.CustomCssJS 仓库](https://github.com/Shurelol/Emby.CustomCssJS) 中针对 Android 客户端的注入/修改方法，将 CustomCssJS 支持集成到客户端后再安装本项目脚本。
  <img width="1326" height="895" alt="image" src="https://github.com/user-attachments/assets/d6dba7ef-783e-4866-833e-c4eeece563db" />
  <img width="1194" height="811" alt="image" src="https://github.com/user-attachments/assets/0decfc18-b4b5-4702-aabb-ec348d83624e" />
  <img width="991" height="774" alt="image" src="https://github.com/user-attachments/assets/e8408009-98ef-4ce3-9d2b-b858365bbca5" />




## 安装 DLL

1. 在 Emby 管理后台停用旧版弹幕插件。
2. 将编译得到的 `Emby.Plugin.Danmu.dll` 复制到 Emby 插件目录。
3. 重启 Emby，在“弹幕配置”中保存站点优先级和弹弹 Play API 凭据（如使用）。
4. 先扫描一个测试媒体，确认日志和 XML 输出正常，再用于整个媒体库。

本项目保留原程序集名称 `Emby.Plugin.Danmu.dll`，这样可以兼容现有 Emby 插件配置和数据；仓库名称与项目发行名称为 `emby-plugin-danmuplus`。

## 一键智能匹配前端

`Frontend/DanmuSmartMatch.CustomCssJS.js` 是独立的 Emby.CustomCssJS 前端脚本，适用于不希望 DLL 修改 Emby `dashboard-ui` 文件的部署方式。

使用方法：

1. 安装并启用 [Shurelol/Emby.CustomCssJS](https://github.com/Shurelol/Emby.CustomCssJS)。
2. 在 CustomCssJS 中新建一个自定义 JavaScript 条目。
3. 将 `Frontend/DanmuSmartMatch.CustomCssJS.js` 的完整内容粘贴到脚本框。
4. 将脚本状态设为启用，并刷新 Emby 网页端。
5. 在电视剧或季度详情页的“更多”菜单中使用“智能匹配并下载整部剧弹幕”或“智能匹配并下载本季弹幕”。

脚本提供季度候选确认、手动绑定、强制刷新、后台下载、强制停止、流式进度和单集重试。它只调用插件已有的 `plugin/danmu` API，不包含账号、密码或 API Secret。

Android 原生客户端不加载 Emby 网页端的 CustomCssJS，因此不会显示该菜单。需要在 Android 客户端使用时，请先参考 [Emby.CustomCssJS 仓库](https://github.com/Shurelol/Emby.CustomCssJS) 的 Android 客户端修改方法，将对应脚本注入支持集成到客户端；未修改客户端时请使用 Emby Web 客户端或浏览器操作。

## 构建

需要 .NET SDK 和 NuGet 网络访问：

```powershell
dotnet restore Emby.Plugin.Danmu.sln
dotnet build Emby.Plugin.Danmu.sln -c Release
dotnet run --project RegressionTests/Emby.Plugin.Danmu.RegressionTests.csproj -c Release
```

构建产物为 `Emby.Plugin.Danmu/bin/Release/netstandard2.0/Emby.Plugin.Danmu.dll`。

## 下载 DLL

release中下载dll，注意适配emby版本

## 致谢与许可证

本项目参考原始 Emby 弹幕插件及其上游 Jellyfin 弹幕插件的实现。请同时遵守原项目许可证及各弹幕站点的服务条款。
