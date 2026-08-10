# 2.0.1-r4

这是一个维护版本，用于从仓库、文档、验证记录和发行源码包中移除共享测试 Worker 的具体地址。

## 默认行为

- 新安装默认保持弹弹 Play“自定义 API”直连模式。
- `UseProxyApi` 默认值为 `false`。
- `ProxyCorsUrl` 默认值为空字符串。
- 配置页面不会预填或内置任何公共代理地址。
- 管理员主动选择“代理 API”模式后，仍可填写自己部署或信任的、兼容 `cf_worker.js` 的 CORS 前缀。

## 发行文件

- `Emby.Plugin.Danmu.dll`
  - SHA-256：`6839e21f8def5ab1227143fea7d08d6be4331641f03302eb76a73d9f04d0faa5`
- `emby-plugin-danmuplus-2.0.1-r4-source.zip`
  - SHA-256：`1adafd9211660f42b2ddcc7189b58365bc3b609c344fc807fe4c44a49262cc20`
- `DanmuSmartMatch.CustomCssJS.js`
  - SHA-256：`6ee79653b903288b1f1dcfa98ddc133be301a1806060ffd6b0829fe088d2e484`

## 验证结果

- Release 构建通过，0 个错误。
- C# 回归测试通过。
- 前端确定性回归测试及配置脚本语法检查通过。
- 两个活动 OpenSpec 变更均通过严格校验。
- 使用管理员提供的凭据完成直连签名搜索，HTTP 200、`success=true`，匹配到动画 ID `17617`；凭据未写入日志或发行文件。
- 已扫描文本源码、源码 ZIP、DLL 和前端产物，均未发现已移除的具体 Worker 域名。
