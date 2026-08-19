# DanmuPlus 2.0.6 本地与脱敏实机验证记录

本记录只保存无凭据、可复核的构建、测试和脱敏实机证据；不记录服务器地址、
账号、密码、认证请求、原始响应、媒体项目 ID、私有部署路径或备份位置。

## 基线与范围

- 分支：`codex/release-2.0.6-continuous-title-similarity`
- 不可变基线：`d22a1069524bd891c5b36c758f75f4112a19e1f4`
- 变更：`release-2-0-6-continuous-title-similarity`
- 外部操作：按用户明确授权完成成套备份、DLL 部署、Emby 重启和只读预览；
  终审去除 CodeView 后再次成套备份并部署最终无调试路径 DLL，随后复跑关键预览；
  未 push、未合并、未 tag、未发布，未执行下载、绑定或元数据写入。

## 已确认产品决策

- 父剧名、季名分别以 `60 * similarity`、`20 * similarity` 连续计分。
- 普通自动阈值保持包含边界的 `MatchScore >= 0.90`。
- TMDB 别名阈值保持包含边界的 `MatchScore >= 0.80`。
- 不增加 `KeywordScore` 门槛、冲突分数上限或自动 veto。
- 不删除“之”等连接字；Movie 与共享 `StringExtension.Distance` 保持不变。
- 前端安装标记保持 V28，映射协议保持 V22。

## 代表性确定性结果

| 比较 | ParentTitleScore | KeywordScore | MatchScore | 结果 |
| --- | ---: | ---: | ---: | --- |
| 精确父剧名、季名、年份 | `1.0000` | `1.0000` | `1.0000` | PASS |
| `西行` / `之西行`，父剧名与年份精确 | `1.0000` | `0.6667` | `0.9333` | PASS |
| `西行` / `东行`，父剧名与年份精确 | `1.0000` | `0.5000` | `0.9000` | PASS，仍可自动选择 |
| `西行` / `行西` 或 `北斗` | `1.0000` | `0.0000` | `0.8000` | PASS |
| Bookworm 父标题（距离 3 / 最大长度 21） | `0.8571` | — | — | PASS，父标题约 `51.43/60` |
| 错误显式季号 | `1.0000` | `0.0000` | `0.8000` | PASS，无 cap/veto |

## 测试与审计矩阵

| 检查 | 结果 | 备注 |
| --- | --- | --- |
| 新增连续标题与 marker 回归 | PASS | 主回归项目覆盖四位小数与 90 分边界 |
| 主后端回归 | PASS | 去除 Release 调试目录后复跑：`Danmu plugin regression checks passed.` |
| 专项后端回归 | PASS | 去除 Release 调试目录后 TitleFidelity 复跑 PASS；R3、search-term、bounded、Episode selection、R4 parent context、R4 identifier、R5 scope、temporary range、MGTV 均 PASS |
| 前端与配置回归 | PASS | Node 前端回归及主项目配置资源断言通过；V28/V22 未变 |
| clean Release solution build | PASS | Release 关闭 PDB/CodeView 后 clean build 0 errors；所有 .NET 命令串行，既有 131 warnings |
| strict OpenSpec validation | PASS | change 严格校验有效 |
| diff、allowlist、凭据安全扫描 | PASS | `git diff --check` 通过；新增凭据赋值计数为 0；最终 DLL 的 PE CodeView、rooted PDB 字符串及私有绝对路径字符串计数均为 0 |
| 本地 review package 清单复核 | PASS | 仅 DLL、配套 JavaScript、校验和、累计日志与本记录 |

## 脱敏实机验证

- 部署前已成套备份活动 DLL、插件配置和前端配置，并逐项复核哈希、属主、权限；
  回滚副本保留。只替换 DLL，两份配置未改动且部署后哈希保持不变。
- 终审只调整 Release 调试符号生成后，最终 DLL 已完成 clean build、主回归、
  TitleFidelity、版本与 PE/字符串扫描，并重新部署。活动 DLL 的 SHA-256 为
  `a9524b271ce4065eae348973c4f0047f0b9818d31ff92a87a45dae373e226f5c`，与最终
  审查包一致；属主、权限和大小复核通过。Emby 4.9.5.0 重启后公共健康检查通过。
- 启动日志明确加载 `Emby.Plugin.Danmu, Version=2.0.6.0`；有界检查最近 300 条
  插件相关日志，`error|exception|fail` 计数为 0。
- 所有匹配请求均使用 `MatchPreview` 或手动关键词预览；未调用下载、绑定、候选
  提交或元数据更新接口。
- 最终 DLL 下再次复跑唐朝诡事录第 2、3 季和 Bookworm 长父标题预览，表中分数与
  自动选择结果均保持一致。

| 实机预览 | ParentTitleScore | KeywordScore | YearScore | MatchScore | 结果 |
| --- | ---: | ---: | ---: | ---: | --- |
| 唐朝诡事录第 2 季：西行 / 唐朝诡事录之西行 | `1.0000` | `0.6667` | `1.0000` | `0.9333` | 正确自动选择 |
| 唐朝诡事录第 3 季：长安 / 唐朝诡事录之长安 | `1.0000` | `0.6667` | `1.0000` | `0.9333` | 正确自动选择 |
| 唐朝诡事录第 1 季 / 唐朝诡事录 | `1.0000` | `1.0000` | `1.0000` | `1.0000` | 精确自动选择 |
| 爱书的下克上 / 小书痴的下克上长标题（手动只读检索） | `0.8571` | `0.0000` | `1.0000` | `0.7143` | 父标题连续分实机确认 |
| Bookworm 第 2 季正确 marker | `1.0000` | `1.0000` | `1.0000` | `1.0000` | TMDB alias 正确自动选择 |
| Bookworm 第 2 季目标 / 第 3、4 季候选 | `1.0000` | `0.0000` | `0.0000` | `0.6000` | 错误显式季号保持 0 |
| R1 / R2 替换候选 | `1.0000` | `0.5000` | `0.0000` | `0.7000` | 连续季名 0.5 实机确认 |
| Movie 控制样本 | — | — | — | `1.0000` | 既有 provider-id 路径正确自动选择 |

实机媒体库没有同时满足“父标题精确、年份精确、季名恰为 0.5”的自然来源候选。
已额外检查库内全部 5 组可能形成该边界的同父标题/同年份季名对；来源年份或候选
结构均不满足完整组合。因此 0.9000 的组合边界由本审查包对应 DLL 的确定性回归
验证：`1.0 * 0.60 + 0.5 * 0.20 + 1.0 * 0.20 = 0.9000`，普通阈值仍为
包含式 `>= 0.90`，唯一候选仍自动选择；实机预览独立确认了 `KeywordScore=0.5`
分项，没有伪造或写入媒体元数据来制造测试样本。

### R4 identifier-free 门禁维护

`R4IdentifierMetamorphic` 的九组功能性变形断言原本已经通过；既有失败来自源码门禁
把只投影启用来源键的 `DanmuProviderIdResolver.GetEnabledProviderIdKeys` 也当成了
本地标识符解析。门禁现仅白名单放行这一项元数据调用，仍拒绝任何其他
`DanmuProviderIdResolver` 调用、保存的手动绑定以及缺少 `SearchSeasonAsync` 的路径。
新增的四个门禁正反自测和完整九组变形回归均通过，末行输出为
`R4 identifier-free metamorphic regression checks passed for 9 identifier sets.`。
该修正只涉及回归测试，不修改插件业务代码、最终 DLL 或实机部署内容。

## 版本与最终文件

| 字段 | 期望值 | 结果 |
| --- | --- | --- |
| Assembly/File version | `2.0.6.0` | PASS |
| Informational/Configuration version | `2.0.6` | PASS |
| TMDB User-Agent | `DanmuPlus/2.0.6` | PASS |
| configuration cache token | `2-0-6` | PASS |
| 前端/映射协议 | V28 / V22 | PASS |

| 文件 | 大小 | SHA-256 |
| --- | ---: | --- |
| `Emby.Plugin.Danmu.dll` | 1657856 | `a9524b271ce4065eae348973c4f0047f0b9818d31ff92a87a45dae373e226f5c` |
| `DanmuSmartMatch.CustomCssJS.js` | 234032 | `a447671b98f991075254665bf3c74d029fd0f3b6ddb5aecd661377d0bd1cd3a3` |

Review package 的 `SHA256SUMS.txt` 覆盖最终 DLL、配套 JavaScript 和累计
`UPDATE.md`。本 `VERIFICATION.md` 不写入自身哈希，因为把自身哈希写回文件会
改变被哈希内容，无法形成稳定且可验证的值；不以占位或伪造值规避该自引用限制。

## 审批门

本次授权覆盖的现场 Emby 部署、重启与只读预览已经完成；成套回退副本继续保留。
当前活动 DLL 已是终审去除 CodeView 后的最终审查包文件，哈希复核一致；关键预览
已在该最终 DLL 下复跑通过。
该授权不扩展到 Git push、merge、tag、GitHub Release 或发布。本记录不得加入任何
现场凭据、认证令牌、原始认证数据或私有路径。
