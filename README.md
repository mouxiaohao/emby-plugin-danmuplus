# emby-plugin-danmuplus

Emby 弹幕插件增强版，参考 [fengymi/emby-plugin-danmu](https://github.com/fengymi/emby-plugin-danmu) 开发。项目从原移植版继续演进，当前版本为 **2.0.1-r1**。

已验证的服务器环境：Synology 套件版 Emby **4.9.3.0**。其他 Emby 版本可能需要调整配置页或前端菜单兼容代码。

## 主要功能

- 爱奇艺、腾讯、优酷、哔哩哔哩、芒果 TV、弹弹 Play 等来源的弹幕搜索与下载。
- 多站点统一候选评分；季名、父剧名、年份和集数综合匹配。
- 同分候选按站点优先级排序。
- 过滤预告、PV、花絮等非正片候选，避免集数错乱和异常小 XML。
- 腾讯分段重试、连接重置重试，以及已完成分段合并为部分弹幕 XML。
- 七天 XML 重复跳过；支持强制刷新、后台队列、强制停止、单集重试和流式进度。
- STRM、115 挂载等普通 Emby 媒体库场景兼容。
- 配置页面版本显示为 `2.0.1-r1`。

## 2.0.1-r1 补丁

- 为所有弹幕来源增加统一的 XML 1.0 字符安全防线，过滤非法控制字符、`U+FFFE`、`U+FFFF` 和孤立代理项。
- 保留中文、TAB/LF/CR、合法字符引用和有效 Unicode 补充字符（包括 emoji）。
- 爱奇艺和 Bilibili 原始 XML 首次解析失败时，会在清理非法字符后重试一次；其他 JSON/protobuf 来源由最终 XML 输出防线统一保护。
- 下载结果改为按弹幕条目和序列化结果判断，不再仅因合法 XML 小于 1 KB 而拒绝保存。
- 空内容和最终 XML 序列化失败会返回对应错误；来源解析异常保留在日志中，不再统一误报为“弹幕内容少于 1KB”。

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

## 弹弹 Play API 凭据

插件不会自动申请、生成或附带第三方凭据。如需启用弹弹 Play，请先从弹弹 Play 开放平台取得属于自己的应用 API ID 与 API Secret，然后在 Emby 管理后台的“弹幕配置”中同时填写两项并保存。

- API ID 与 API Secret 必须作为完整的一对填写；只填写一项会明确报配置不完整，不会与其他来源的值拼接。
- 插件配置中的完整凭据优先；配置为空时才依次尝试环境变量 `DANDAN_API_ID`、`DANDAN_API_SECRET` 和旧版内置值。
- API Secret 在管理页面中以密码框遮罩，但仍以明文保存在 Emby 插件配置 XML 中；请依靠群晖和 Emby 文件权限保护配置文件。
- 插件不会把 API ID、API Secret 或签名材料写入日志。提交诊断日志前仍应检查并移除其他令牌和个人路径。

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

构建产物为 `bin/Release/netstandard2.0/Emby.Plugin.Danmu.dll`。

## 下载 DLL

仓库中的 [`dist/Emby.Plugin.Danmu.dll`](dist/Emby.Plugin.Danmu.dll) 是 2.0.1-r1 Release 构建，可直接下载后复制到 Emby 插件目录。该 DLL 保留程序集文件名 `Emby.Plugin.Danmu.dll`，以兼容已有插件配置。

SHA-256：`636675edb9eb1f97f7b215f6b14adf495e62e966a60b4a2f226e0241d425a0f2`

## 按版本下载

每个正式版本的 DLL、源码压缩包和智能搜索前端都会保存在对应版本目录中：

- [`releases/v2.0.0/`](releases/v2.0.0/)
- [`releases/v2.0.1-r1/`](releases/v2.0.1-r1/)

后续版本不会覆盖旧版本文件，便于按 Emby 环境回退或比较。

## 安全提醒

不要提交 Emby 配置 XML、服务器日志、备份文件、API Secret、账号密码、`bin/` 或 `obj/`。站点接口变更时请保留失败日志中的请求上下文，但先删除访问令牌和个人路径。

## 致谢与许可证

本项目参考原始 Emby 弹幕插件及其上游 Jellyfin 弹幕插件的实现。请同时遵守原项目许可证及各弹幕站点的服务条款。
