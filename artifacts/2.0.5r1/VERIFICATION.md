# 2.0.5r1 最终验证记录

本记录只保存可公开、无凭据的发布证据，不包含服务器地址、账号、密码、
token、认证请求、原始提供商响应、私有绝对路径或备份位置。

## 基线与发布身份

- 实现分支：`codex/2.0.5r1-matching-behavior`
- 2.0.4r2 / `origin/develop` 基线：
  `f8a4356537dcf0c8f913bb970bb2bcdc689096fd`
- 当前最新正式 GitHub Release 基线：`v2.0.3r10`
  (`51722a3e6e3050e6e817232b8987486f54792e62`)
- 最终 sparse-alignment 源码增量：
  `cd2d4387fc64fd8ce35dd0f9bb22e5d6c8039be1`
- l1-l10 独立回退提交见 [`COMMIT_MAP.md`](COMMIT_MAP.md)。
- 本记录不为发布文档预先指定或推测 Git commit、tag 或 GitHub Release
  身份；发布时应以实际创建并回读的不可变引用为准。

## 最终发布元数据

| 字段 | 最终值 | 结果 |
| --- | --- | --- |
| Assembly/File version | `2.0.5.1` | PASS |
| Product/Configuration version | `2.0.5r1` | PASS |
| TMDB User-Agent | `DanmuPlus/2.0.5r1` | PASS |
| 映射协议 | V22 | PASS |
| 前端安装标记 | V28 | PASS |

## 最终发布文件

发布清单以 `review-package-v28-sparse-0cda5510` 中的已验证文件为准：

| 文件 | 大小 | SHA-256 |
| --- | ---: | --- |
| `Emby.Plugin.Danmu.dll` | 1656832 | `0cda55107003c5e491c763969b55c02a51601f3870610a0a6a5418d03833b1aa` |
| `DanmuSmartMatch.CustomCssJS.js` | 233307 | `01c7c6400f70146a22b2d1cbac65691017ed159a86398e1e653bb1e2fc0fcbb3` |

DLL 的 Assembly/File version 为 `2.0.5.1`，Product version 为
`2.0.5r1`；JavaScript 与 DLL 配套使用 V28/V22。包内只包含最终 DLL、
匹配的 JavaScript、校验和与无凭据说明，不含部署脚本、配置、日志、备份、
诊断转储或凭据。

以下早期候选已被取代并从当前发布清单排除：

- 所有 V27/V21 候选；
- DLL SHA-256 以 `4892a83a` 开头的早期 sparse 候选；
- DLL SHA-256 以 `0b05af38` 开头的早期 sparse 候选。

这些文件只可作为本地历史证据，不得复制到 2.0.5r1 当前 Release。

## 提交后 clean rebuild 对照

本轮提交后的 clean rebuild 生成了另一个 `Emby.Plugin.Danmu.dll`：

| 项目 | clean rebuild | 现场已部署/发布权威 DLL |
| --- | --- | --- |
| 大小 | 1656832 bytes | 1656832 bytes |
| SHA-256 | `e3bb181f7682d186b78aad32bb4fae6be50b53875e457e710bfac8eebe9743fa` | `0cda55107003c5e491c763969b55c02a51601f3870610a0a6a5418d03833b1aa` |
| Assembly/File/Product version | `2.0.5.1` / `2.0.5.1` / `2.0.5r1` | `2.0.5.1` / `2.0.5.1` / `2.0.5r1` |

逐 RID 对双方各 5856 个 managed method 的名称、签名、属性、IL 与异常区域
进行核对，结果全部相同。双方 591344-byte managed resources 的 SHA-256
也均为 `a54438c1c4c39a0d76d41d60fd9e2428e6265300d6e9d73ea1b7aa1ef7a704fb`。
已观察到的二进制差异包括 MVID 与 PDB debug identifiers；这些结构化比较
不能证明整个二进制除已列字段外逐字节完全一致。

Release 仍使用已在群晖部署后逐字节哈希回读验证的 `0cda5510…` DLL；
`e3bb181f…` clean rebuild 只作为提交后可重复构建与 managed 内容对照证据，
不替换发布权威文件。

## 本地验证

| 检查 | 结果 | 证据范围 |
| --- | --- | --- |
| 完整后端回归与所有受影响专项回归 | PASS | bounded search、R3、search-term、TitleFidelity、TMDB alias、candidate detail/evidence、composite planner、R5 scope、single-target、seven-day replay |
| 完整前端与配置资源回归 | PASS | 服务端分数/原因/顺序、无预选、TMDB 耗尽隐藏、V22/V28、稀疏对齐与 stale 状态 |
| clean sequential Release build | PASS | 0 errors；无竞争 .NET 构建输出锁 |
| strict OpenSpec validation | PASS | 相关 change 严格校验通过 |
| diff、变更范围、凭据与包清单审计 | PASS | 无白名单外发布文件、无凭据或私有部署细节 |
| Sol-high 最终审查 | PASS | 无阻塞发现；稀疏编号、显式锚点、fallback、fingerprint、写入栅栏、S0 与 frozen replay 边界通过 |

用户已免除 l1-l10 十棵逆向树的穷举运行。本记录只保留独立提交与普通
`git revert` 边界，不声明未运行的逆向树通过。

## 最终行为验收

- Season 普通评分为父剧名 `60`、季名 `20`、精确年份 `20`、集数 `0`；
  集数差异只驱动黄色提示，不改变评分、候选顺序或下载资格。
- TMDB alias 父标题证据在当前别名与原始父剧名间取最大值；严格“双空残余”
  完整标题 NFKC 符号兜底只补足同一标题对的季名分。
- 临时映射错误显示明确原因；整剧空 Season 只执行一次受控重试。
- 显式可靠编号采用零偏移或用户锚点的编号差；编号不可靠时整窗位置兜底，
  同一窗口不混用两种模式。精确 `SourceStartEpisodeId` 是权威来源锚点。
- V22 计划指纹覆盖对齐意图、窗口、来源编号/顺序、锚点和精确映射；
  代际或指纹变化会在下载、标识符及元数据写入前 fail closed。
- 显式单 Season 0 保持可用；whole-Series 与 unattended/media-import 在
  provider search 前跳过 Season 0。
- 逐集 retry 与 seven-day replay 使用冻结的本地 ItemId、来源 EpisodeId 和
  CommentId 元组；重验证时 CommentId 变化会视为 stale，不按编号或位置替换。

## 群晖与 Emby 只读现场验证

- 已先备份部署中的 DLL、CustomCssJS 与插件配置，并完成可用回退副本的
  校验和回读；随后只部署最终 DLL/V28 JavaScript 配对。
- 部署文件哈希回读与上表一致；Emby 进程运行，健康接口 HTTP 200，
  `Emby.Plugin.Danmu, Version=2.0.5.1` 加载成功，启动日志无插件加载/DI 错误。
- Spy Family Season 3 的库内 E1-E6/E10-E13 映射保持 E10→来源 E10。
- Frieren 显式 E29→来源 E1 时，缺少本地 E30 后 E31→来源 E3。
- 只调用只读预览；未启动下载、绑定、元数据写入或强制刷新。
- 旧 XML 未删除、未覆盖、未自动修复；任何后续强制刷新仍需要单独确认。
- 有界日志检查未发现映射协议、stale-plan 或插件加载失败。

## 发布结论

最终 V22/V28 包、本地回归、clean Release、strict OpenSpec、范围/凭据审计、
Sol-high 审查以及授权的只读现场验证全部 PASS。当前发布只应使用本记录中的
两个最终文件与哈希；不得回退到中途 V27/V21 或两个早期 sparse 包。
