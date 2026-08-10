## Purpose

为 Dandanplay 代理模式提供插件维护的默认 CORS 路由，同时兼容已有自定义代理配置，并保证内建地址不会通过插件配置界面或配置数据暴露。

## ADDED Requirements

### Requirement: 官方 CORS 选项具有确定的默认与迁移语义
系统 SHALL 在 Dandanplay 的代理 API 模式中提供“使用插件官方 CORS 地址”布尔选项。新安装，以及读取到缺少该字段且 `ProxyCorsUrl` 为空的配置时，系统 SHALL 将该选项解析为已勾选；读取到缺少该字段但 `ProxyCorsUrl` 非空的升级配置时，系统 SHALL 将其解析为未勾选并继续使用既有自定义地址。用户明确保存该字段后，系统 MUST 遵循保存值，不得再以 `ProxyCorsUrl` 是否为空覆盖用户选择。

#### Scenario: 新安装默认使用官方 CORS
- **WHEN** 系统创建新的插件配置
- **THEN** “使用插件官方 CORS 地址”解析为已勾选
- **AND** 用户无需填写代理地址即可启用代理 API 模式

#### Scenario: 缺少字段且没有旧代理地址
- **WHEN** 系统读取缺少官方 CORS 选择字段且 `ProxyCorsUrl` 为空的配置
- **THEN** 系统将官方 CORS 选择解析为已勾选

#### Scenario: 已有自定义代理地址的配置升级
- **WHEN** 系统读取缺少官方 CORS 选择字段但 `ProxyCorsUrl` 非空的配置
- **THEN** 系统将官方 CORS 选择解析为未勾选
- **AND** 后续代理请求继续使用原有 `ProxyCorsUrl`

#### Scenario: 明确取消勾选得到保留
- **WHEN** 用户明确取消勾选官方 CORS 并保存配置
- **THEN** 后续加载 SHALL 保持未勾选状态
- **AND** 系统不得因自定义地址为空而自动重新勾选

### Requirement: 配置页面隐藏内建地址并保留自定义值
系统 SHALL 仅在 `UseProxyApi` 已启用时呈现官方 CORS 选择与自定义 CORS 控件。官方 CORS 已勾选时，自定义输入 SHALL 禁用；未勾选时 SHALL 启用。切换两种代理来源 MUST 保留用户保存或当前输入的自定义地址。内建地址 `https://danmuplus-dandan-proxy.mouxiaohao.workers.dev/cors/` MUST 只存在于后端路由实现中，不得渲染到 HTML 或 JavaScript，不得写入持久化配置、配置 API 响应或输入框，也不得作为隐藏字段、占位符、帮助文本或其他界面内容返回。

#### Scenario: 勾选官方 CORS
- **WHEN** 用户在代理 API 模式中勾选“使用插件官方 CORS 地址”
- **THEN** 自定义 CORS 输入框被禁用
- **AND** 页面中任何可见或隐藏的前端数据均不包含内建地址

#### Scenario: 取消勾选官方 CORS
- **WHEN** 用户取消勾选“使用插件官方 CORS 地址”
- **THEN** 自定义 CORS 输入框被启用
- **AND** 切换前已有的自定义值仍然存在

#### Scenario: 配置数据不返回内建地址
- **WHEN** 客户端读取或保存已勾选官方 CORS 的插件配置
- **THEN** 配置载荷不包含内建地址
- **AND** `ProxyCorsUrl` 不会被内建地址覆盖

#### Scenario: 非代理模式不提供代理来源交互
- **WHEN** `UseProxyApi` 未启用
- **THEN** 官方 CORS 选择与自定义 CORS 输入不得成为当前请求路由的有效来源

### Requirement: 三路 Dandanplay 请求路由保持明确
系统 SHALL 保持 `UseProxyApi` 为一级模式开关：未启用时，`search`、`bangumi` 与 `comment` 请求 MUST 直连 Dandanplay 官方 API 并由插件使用用户配置的 AppId 与 Secret 本地签名；启用且选择官方 CORS 时，这三个端点 MUST 通过内建官方 CORS 根地址发送且插件不得附加本地签名；启用且选择自定义 CORS 时，这三个端点 MUST 通过保存的 `ProxyCorsUrl` 发送且插件不得附加本地签名。

#### Scenario: 官方直连并本地签名
- **WHEN** `UseProxyApi` 为 false
- **THEN** `search`、`bangumi` 与 `comment` 请求直连 Dandanplay 官方 API
- **AND** 插件使用已配置的 AppId 与 Secret 生成官方认证请求头

#### Scenario: 官方 CORS 代理请求
- **WHEN** `UseProxyApi` 为 true 且官方 CORS 选项已勾选
- **THEN** `search`、`bangumi` 与 `comment` 请求使用后端内建官方 CORS 根地址
- **AND** 插件不生成或发送本地 Dandanplay 签名

#### Scenario: 自定义 CORS 代理请求
- **WHEN** `UseProxyApi` 为 true 且官方 CORS 选项未勾选
- **THEN** `search`、`bangumi` 与 `comment` 请求使用保存的自定义 `ProxyCorsUrl`
- **AND** 插件不生成或发送本地 Dandanplay 签名

#### Scenario: 自定义代理地址缺失
- **WHEN** `UseProxyApi` 为 true、官方 CORS 选项未勾选且 `ProxyCorsUrl` 为空或无效
- **THEN** 系统拒绝发送 Dandanplay 请求并返回可诊断但不包含凭据的配置错误

### Requirement: 代理来源选择不改变匹配与弹幕处理
系统 MUST 对三种 Dandanplay 路由使用相同的现有标题、年份、季度与集数评分规则，并保持自动匹配、手动绑定、`search`、`bangumi`、`comment`、XML 与 ASS 行为。系统 MUST NOT 因官方 CORS 选项引入媒体 Hash 识别或 Dandanplay `/match` 调用，也不得改变其他弹幕提供商的请求行为。

#### Scenario: 自动导入使用现有评分
- **WHEN** 自动媒体库导入通过任一 Dandanplay 路由搜索候选
- **THEN** 候选仍按现有标题、年份、季度与集数规则评分和选择

#### Scenario: 手动匹配使用现有候选与绑定
- **WHEN** 用户通过任一 Dandanplay 路由执行手动匹配并保存绑定
- **THEN** 系统保持现有候选呈现、手动绑定和后续评论下载行为

#### Scenario: 输出格式保持兼容
- **WHEN** 任一路由成功取得 Dandanplay 评论
- **THEN** 系统保持现有 XML 与 ASS 生成行为

#### Scenario: 不使用 Hash 或 match
- **WHEN** 系统执行 Dandanplay 搜索或评论下载
- **THEN** 请求流程不计算用于匹配的媒体 Hash
- **AND** 不调用 Dandanplay `/match` 端点

#### Scenario: 其他提供商不受影响
- **WHEN** 系统搜索或下载非 Dandanplay 提供商的弹幕
- **THEN** 官方 CORS 选择不改变其路由、匹配或输出行为

### Requirement: 发布验证覆盖安全与三路回归
`2.0.1-r5` 发布前 MUST 在实施产物上完成官方 CORS、自定义 CORS、官方直连三路 live smoke，覆盖 `search`、`bangumi` 与 `comment` 的代表性成功路径，并检查运行日志不包含 Dandanplay AppId、Secret、签名或认证头。此前对部署 Worker 的人工在线验证 SHALL 仅作为前置可行性证据，不得替代 r5 实施产物的回归。

#### Scenario: 三路 live smoke
- **WHEN** r5 候选构建准备部署验证
- **THEN** 官方 CORS、自定义 CORS和官方直连均完成代表性 Dandanplay live smoke
- **AND** 每一路均验证搜索、条目详情与评论下载路径

#### Scenario: 日志不泄露认证材料
- **WHEN** 三路 live smoke 完成
- **THEN** 相关日志不包含 AppId、Secret、签名值或认证请求头

#### Scenario: 前置人工验证不替代回归
- **WHEN** 发布检查引用本次已完成的 Worker 人工验证
- **THEN** 该证据只证明代理可行性
- **AND** r5 候选构建仍须完成全部规定回归
