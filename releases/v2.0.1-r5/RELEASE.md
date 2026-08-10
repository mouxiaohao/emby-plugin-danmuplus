# 2.0.1-r5

这是一个联合功能与可靠性版本，基于 `2.0.1-r4` 增加弹弹 Play 官方 CORS 选择、配置页缓存版本化、电影/单集智能匹配，以及接近高分候选的站点优先级选择。

## 主要变化

- 弹弹 Play 代理 API 新增“使用插件官方 CORS 地址”选项；也可继续使用管理员自行填写的兼容 CORS 前缀，或关闭代理后使用 AppId/AppSecret 直连官方 API。
- 新安装以及缺少新字段且没有自定义地址的旧配置默认选择官方 CORS；缺少新字段但已有自定义地址的旧配置继续使用原地址。用户明确保存后以保存的布尔值为准。
- 切换官方/自定义 CORS 不会清空用户保存的自定义地址。内建地址只存在于后端路由，不写入配置 XML、配置 API 响应、HTML、JavaScript、隐藏字段或输入框。
- 三种路由继续共用原有 `search`、`bangumi`、`comment`、标题/年份/季度/集数评分及 XML/ASS 处理，不计算媒体 Hash，也不调用 `/match`。
- 配置页名称和控制器名称由同一个构建版本生成；旧浏览器配置页缓存无需手工清理即可加载 r5 控件。
- 智能匹配菜单扩展到电视剧、季度、单集和电影。单集支持来源集数建议与手动覆盖，且不会改写整季绑定；电影和单集任务支持超时、停止、重试与一行明细进度。
- 高置信度候选进入接近分数区间后按站点优先级选择，但列表继续严格按分数降序显示。同一优先站点的最高分并列仍保持歧义并要求手动确认。
- CustomCssJS 插件本体没有被替换或修改；发行的 JavaScript 文件仅是 Danmu Smart Match 脚本。

## 现场验证结果

- 官方 CORS：代表剧集预览成功，得到 13 个候选；弹弹 Play 条目详情与评论下载成功，其他提供商的 11 个候选仍可用。
- 自定义 CORS：相同的搜索、条目详情和评论下载路径成功，自定义地址在切换与恢复过程中保持不变。
- 官方直连：在管理员配置完整 AppId/AppSecret 后，代表性 `search`、`bangumi` 与跟踪式 `comment` 成功；生成了非空 XML 和 ASS，验证结束后恢复原配置语义。
- 代表性接近分数案例中，Bilibili 候选得分 `1.0000`、弹弹 Play 候选得分 `0.9679`；因两者位于接近高分区间，最终按站点优先级选择 Bilibili，同时候选显示顺序仍按分数降序。
- 已扫描最终进程日志，未发现已配置的 Dandan 凭据值、Emby 访问令牌、签名查询值、NAS 密码或自定义代理字面量。

## 已知验证边界

- 没有为现场验证执行会改写真实媒体库状态的破坏性手动绑定，因此不声称完成全部自动/手动绑定矩阵。
- 电影与单集的 Bilibili/非 Bilibili、STRM、歧义手选、编辑来源集数、已保存绑定、重复跳过、强制刷新、失败与取消组合尚未全部做完现场矩阵。
- 自动化已覆盖集中路由、精确 URL 构造和代理分支跳过本地凭据解析，但尚未通过伪 HTTP 对三个端点逐一完成实际请求头名称/集合及凭据解析行为的严格断言。
- Emby 自身的请求日志会记录 `X-Emby-Authorization` 请求头名称和非敏感客户端元数据；秘密值未出现，但“认证请求头名称完全不出现”的严格断言未满足。
- 因此相关 OpenSpec 项目仍按实际证据保持未完成状态；本说明不宣称全部 OpenSpec 任务已完成。

## 自动化与构建

- Release 构建通过，0 个错误（保留既有警告）。
- C# 回归测试通过。
- 前端确定性回归测试通过。
- `add-dandan-api-proxy-mode`、`cache-bust-plugin-config-page`、`add-official-dandan-cors-option` 和 `extend-smart-match-menus-and-movies` 均通过 OpenSpec strict validation；任务完成度仍以上述各 change 的任务清单为准。
- 发布前部署 DLL 与候选 DLL 的 SHA-256 一致，并保留了 r4 DLL、Danmu 配置及 CustomCssJS 配置的可回滚快照。

## 发行文件

- `Emby.Plugin.Danmu.dll`
  - SHA-256：`b08186751fec8a407d1ae8ffb9975a952f25f8960b7143dc0bf159d012515d5c`
- `DanmuSmartMatch.CustomCssJS.js`
  - SHA-256：`058ab6b2385ae10a5b2bd4b1ab7e172e742cf84e793e6d63e6f8e06114d223f1`
- `emby-plugin-danmuplus-2.0.1-r5-source.zip`
  - SHA-256：`35a3d48b63688058c3ba76f5b3092128096afe663e3e4f40c5cb480f974c9707`
