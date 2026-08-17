# emby-plugin-danmuplus

Emby 弹幕插件增强版，参考 [fengymi/emby-plugin-danmu](https://github.com/fengymi/emby-plugin-danmu) 开发。当前版本为 **2.0.5r1**，面向 Synology 套件版 Emby **4.9.5.0**；现场部署与验收尚未执行，将在必要的 Release 构建、部署前冒烟检查与备份后进行。

版本变化请查看 [完整更新日志（UPDATE.md）](UPDATE.md)。README 只汇总 DanmuPlus 相比旧版 Danmu 插件的重要使用功能变化。

## 相比旧版 Danmu 插件的主要改动

### 2.0.5r1 搜索与呈现更新

- 一个弹幕网站搜索失败时，会保留该站点的有界诊断，同时继续使用其他已完成网站的候选；全部网站失败、明确取消或权威映射不完整时仍然不会自动下载或写入元数据。
- 自定义关键词继续使用各网站已有的手动搜索优化，并由服务端按普通评分、站点优先级和 60 条展示窗稳定呈现；该模式不调用 TMDB 别名、不自动选择，只有用户明确选择且通过目标绑定证据与权威详情/映射校验后才能继续。
- 动画季度的 TMDB 别名均未达到既有自动阈值时，不再公开累计别名候选或 TMDB 内部诊断；界面可用权威父剧名重新匹配，并重新进入普通季度评分。
- 共享智能匹配不再施加原有的单站 10 秒、交互 30 秒和自动 45 秒截止时间；明确的用户/父操作取消、全局与单站并发隔离、网站自身传输保护仍然保留。
- 移除季度冲突证据的 0.79 分上限和将 0.85 分符号保真候选抬到自动阈值的桥接；普通权重、90 分自动阈值及真正同分时的保真决胜保持不变。
- 复合季度下载映射提示在适用结果中只显示一次；配置页标题为 `DanmuPlus 配置`，源码链接指向本项目 `main` 分支，插件程序集、标识、配置路由与已保存设置兼容性不变。

### 统一智能匹配

- 电视剧、季度、单集和电影共用后端匹配、评分、排序与决策系统；前端只负责显示和交互，不在浏览器内重新评分或改动候选顺序。
- 支持爱奇艺、腾讯、优酷、哔哩哔哩、芒果 TV、弹弹 Play 等来源，并按管理页面中已启用网站的顺序处理多个有效外部标识符。
- 优先验证媒体自身的弹幕网站外部标识符。季度只使用季度标识符；单集先使用本集标识符，再回退所属季度；电影只使用电影标识符；Series 标识符不再替代子季度。
- 没有可用标识符时，按标题、季度、年份和集数执行全站搜索。标准标题不足时可追加受限的动态标题片段和同站别名搜索，以兼容不同译名。
- 90 分以上候选属于置信结果，并按已启用网站顺序选择；低置信、并列或无法解析的结果交给用户确认。
- 标题匹配同时保留宽松标点召回和精确符号证据：兼容全角/半角符号，同时区分符号类型、数量和顺序；真正同分时仍要求手动选择。
- 哔哩哔哩电影与番剧搜索合并受限的分类结果并保留局部失败诊断，可补全聚合搜索遗漏的有效 PGC 条目，同时排除普通视频结果。
- 过滤预告、PV、花絮、特别篇等非正片内容，减少集数错位和异常小 XML。

### 外部标识符与下载一致性

- 本地标识符命中时直接请求对应网站的精确详情，不经过关键词搜索和评分；网站没有明确返回的标题、年份、类型或集数保持“未知”。
- 精确单集和电影预览会显示来源作品标题、可信发行/首播年份与类型；上传或网页发布时间不会冒充作品年份。
- 哔哩哔哩仅使用 PGC：季度保存 `season_id`，电影和单集保存 `ep_id`；下载时才由后端解析 `aid,cid`。
- 整季第一条有效弹幕成功落盘后保存季度标识符；每个成功集保存自己的单集标识符，包括第一集。
- 季度或单集成功写入选中网站标识符时，会清除同一媒体上其他已注册弹幕网站的普通标识符，包括停用网站；不会清除 Manual、TMDB、TVDB、IMDb 或其他 Emby 元数据。
- 失败、跳过、取消、超时、过时代际和未落盘结果不会触发标识符写入或清理；元数据写入异常也不会反转已成功保存的 XML。

### 下载可靠性

- 腾讯等分段弹幕支持失败重试和连接重置重试；部分分段失败时，已成功内容仍可合并为明确标记的部分 XML。
- 下载状态区分成功、部分成功、失败和重复跳过，并支持逐集重试。
- 默认按 XML 文件判断七天内是否重复下载；可选择强制刷新。
- Movie/Episode 跟踪下载仍保留 180 秒截止时间；该下载边界与七天重复跳过/重放规则不受智能匹配搜索截止时间调整影响。
- 支持后台队列、强制停止、流式进度，以及普通目录、STRM 和 115 挂载媒体库。
- 所有来源统一过滤 XML 1.0 非法字符，同时保留合法中文、换行、字符引用和 Unicode 补充字符。

### 智能匹配前端

- 通过独立的 Emby.CustomCssJS 脚本提供电视剧、季度、单集和电影的智能匹配菜单，不修改 Emby `dashboard-ui`。
- 动画弹弹 Play 智能匹配可使用 TMDB 中文别名以及英文、日文主标题辅助搜索，并优先尝试更适合中文检索的短标题。
- 批量下载完成后，可只重下本次因 7 天重复策略跳过的集数，不影响本次已成功或其他原因未下载的集数。
- 显示后端返回的中文匹配来源和决策原因，支持重新智能匹配、自定义关键词、手动候选、来源集数调整、强制刷新、后台下载、停止和重试。
- 临时季度卡片保留来源标题、年份和类型；电影在来源能够证明多个独立可下载正片时提供可选版本，并以服务端短期证据校验所选正片，不公开原始分 P 标识符。
- 点击弹窗遮罩不会关闭界面；普通界面可用右上角 X 或 `Escape` 关闭。
- 支持 Android/WebView 回退层级：二级候选返回上级，顶层界面关闭；智能匹配请求进行中会拦截系统回退，只保留右上角 X，结果显示后恢复正常回退。
- Android 窄屏标题栏适配状态栏安全区域，避免右上角 X 被状态栏遮挡。

## 安装 DLL

1. 在 Emby 管理后台停用旧版弹幕插件。
2. 从 [最新 GitHub Release](https://github.com/mouxiaohao/emby-plugin-danmuplus/releases/latest) 下载 `Emby.Plugin.Danmu.dll`，复制到 Emby 插件目录。
3. 重启 Emby，在“弹幕配置”中保存站点顺序和弹弹 Play API 模式。
4. 先用少量测试媒体确认匹配、XML 输出和元数据写入正常，再用于整个媒体库。

项目保留程序集名称 `Emby.Plugin.Danmu.dll`，以兼容已有插件配置和数据。

## 弹弹 Play API 配置

Emby 管理后台的“弹幕配置”提供以下调用方式：

- **代理 API + 插件官方 CORS**：勾选“使用插件官方 CORS 地址”，由插件后端选择维护的代理并由代理完成应用签名；配置页和配置数据不会展示或保存该地址。
- **代理 API + 自定义 CORS**：取消勾选官方 CORS 后，填写你自己部署或信任的、兼容 `cf_worker.js` 的代理 CORS 前缀，例如 `https://worker.example/cors/`。代理完成应用签名，此模式不需要在 Emby 中填写 API ID 或 API Secret。
- **官方直连**：关闭代理 API，插件直连弹弹 Play 官方 API，并使用本地配置的 API ID 与 API Secret 生成签名。插件不会自动申请、生成或附带第三方凭据，请先从弹弹 Play 开放平台取得属于自己的凭据，并同时填写两项。

切换模式只改变请求的传输和签名位置，不会清空另一种模式已保存的值。三种路由均继续通过标题、年份、季度和集数进行服务端搜索与评分，使用相同的弹幕下载流程；不会计算视频 Hash，也不会调用弹弹 Play `/match` 接口。

自定义直连模式的 ID 与 Secret 必须成对填写；Secret 会以明文保存在 Emby 插件配置 XML 中，请依靠服务器文件权限保护配置文件。

## 安装智能匹配前端

1. 安装并启用 [Shurelol/Emby.CustomCssJS](https://github.com/Shurelol/Emby.CustomCssJS)。
2. 在 CustomCssJS 中新建 JavaScript 条目。
3. 从 [最新 GitHub Release](https://github.com/mouxiaohao/emby-plugin-danmuplus/releases/latest) 下载 `DanmuSmartMatch.CustomCssJS.js`，将完整内容粘贴到脚本框并启用。
4. 刷新 Emby 网页端；Android 客户端需先按 Emby.CustomCssJS 项目说明集成脚本注入，并在升级后彻底退出再重新打开。

菜单适用于电视剧、季度、单集和电影的详情页、卡片或列表“更多”菜单。脚本只调用插件的 `plugin/danmu` API，不包含 Emby 账号、密码或弹弹 Play API Secret。

### 智能匹配前端演示

<img width="1326" height="895" alt="智能匹配整部剧界面" src="https://github.com/user-attachments/assets/d6dba7ef-783e-4866-833e-c4eeece563db" />

<img width="1194" height="811" alt="智能匹配季度候选界面" src="https://github.com/user-attachments/assets/0decfc18-b4b5-4702-aabb-ec348d83624e" />

<img width="991" height="774" alt="智能匹配下载进度界面" src="https://github.com/user-attachments/assets/e8408009-98ef-4ce3-9d2b-b858365bbca5" />

## 构建与测试

需要 .NET SDK、Node.js 和 NuGet 网络访问：

```powershell
dotnet restore Emby.Plugin.Danmu.sln
dotnet build Emby.Plugin.Danmu.sln -c Release
dotnet run --project RegressionTests/Emby.Plugin.Danmu.RegressionTests.csproj -c Release
node Frontend/DanmuSmartMatch.RegressionTests.js
```

DLL 输出到 `bin/Release/netstandard2.0/Emby.Plugin.Danmu.dll`。

## 安全提醒

不要提交 Emby 配置 XML、服务器日志、备份文件、API Secret、账号密码、`bin/` 或 `obj/`。提交诊断资料前应移除访问令牌和个人路径。

## 致谢与许可证

本项目参考原始 Emby 弹幕插件及其上游 Jellyfin 弹幕插件实现。请同时遵守原项目许可证及各弹幕站点服务条款。
