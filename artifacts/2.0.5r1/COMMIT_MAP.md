# 2.0.5r1 提交与独立回退映射

本记录保存 2.0.5r1 的实现边界、独立回退命令与最终 sparse-alignment
增量。它不包含凭据、认证响应、服务器地址、私有路径或部署命令。

## 基线

- 分支：`codex/2.0.5r1-matching-behavior`
- `origin/develop` / 2.0.4r2 基线：
  `f8a4356537dcf0c8f913bb970bb2bcdc689096fd`
- 当前最新正式 Release：`v2.0.3r10`
- 原 dirty checkout 未用于实现写入。

## l1-l10 独立提交

| Slice | Commit | 最终有效语义 | 普通回退命令 |
| --- | --- | --- | --- |
| l1 | `f90f779ce1219693fd2ea2ba0713451a893c9a02` | 删除过时复合季警告 | `git revert f90f779ce1219693fd2ea2ba0713451a893c9a02` |
| l2 | `d6070f60f9344476ea677c968fd62da87e79856c` | 映射说明每个适用结果一次 | `git revert d6070f60f9344476ea677c968fd62da87e79856c` |
| l3 | `720732398d12293db1e86009bbbfcd498917a1b8` | 单站失败不阻塞已完成站点 | `git revert 720732398d12293db1e86009bbbfcd498917a1b8` |
| l4 | `9b008697274eb3cc12382eaf9b081101d3402e26` | `DanmuPlus 配置` 标题 | `git revert 9b008697274eb3cc12382eaf9b081101d3402e26` |
| l5 | `1594b53524614763d3d19a7084ef4f1fc45f87f9` | 源码链接指向本项目 `main` | `git revert 1594b53524614763d3d19a7084ef4f1fc45f87f9` |
| l6 | `5713498de45912384f013fa2180a1e7fadc4f002` | 别名耗尽后用权威父剧名重匹配 | `git revert 5713498de45912384f013fa2180a1e7fadc4f002` |
| l7 | `9a9b5e6a111abc2ca03b87a732a0acd51e1b3610` | 移除共享搜索 10/30/45 秒截止时间 | `git revert 9a9b5e6a111abc2ca03b87a732a0acd51e1b3610` |
| l8 | `67cd8f453ae2999e2e81406e5a5161b543e17792` | 删除季度/年份冲突分数上限 | `git revert 67cd8f453ae2999e2e81406e5a5161b543e17792` |
| l9 | `ee09089a5460d2e13bb3424432366a500fa9296f` | 删除低分符号保真阈值桥接 | `git revert ee09089a5460d2e13bb3424432366a500fa9296f` |
| l10 | `8f2dc5fd65df836e833712743deaaa6eb633e104` | 手动关键词保留评分/原因/60 条窗且不预选 | `git revert 8f2dc5fd65df836e833712743deaaa6eb633e104` |

l1-l10 均有对应聚焦回归和提交边界。用户明确免除十棵逆向树的穷举运行，
因此这里只提供普通单提交回退命令，不声明未运行的 resulting/restored tree
验证通过。

## 辅助与后续独立提交

| Commit | 状态 | 内容与最终关系 |
| --- | --- | --- |
| `419b730a9c874d3f98e3940769d3bbc946192915` | retained | 仅修正基线 Bilibili typed overload 测试断言 |
| `680d31f7cd4039091fb8ffd03efccf1e568bc6b4` | retained test-only | 更新自动稀疏计划契约；不改变生产代码或发布二进制 |
| `6e4c3c09df81150a3fa0d49fe9d5f11dc405d490` | retained | 2.0.5r1 版本元数据与早期发布准备，位于 l1-l10 之外 |
| `f00ebf13cc09c033d3d0e85a9984442ede12aea3` | retained | 向界面公开临时复合映射校验原因 |
| `81d565088a59fd67c7b304b161d3b9a4b71b1371` | retained | 瞬时空 Series/Season 预览只恢复一次 |
| `5bc658f51da73f7756cd6cc8aa29be17a70bbd12` | superseded behavior | 中途 `60/20/10/10` 权重；不属于最终发布语义 |
| `872aa157a39cd1b235b52f7a7b630305e7201a3d` | retained | 最终 `60/20/20/0`；集数仅驱动黄色来源盈余提示 |
| `6d20fc0a7ac0027aea0309ac78b4f316256f7516` | retained | TMDB alias parent-maximum 中受限的终止季标记恢复 |
| `b8b5ab26aeaf2eae26025eba5ebe4e8c61aa4f85` | retained | 严格“双空残余”完整标题 NFKC 符号兜底 |
| `cd2d4387fc64fd8ce35dd0f9bb22e5d6c8039be1` | final sparse delta | 稀疏集号、显式锚点、整窗 fallback、V22/V28、fingerprint/stale-write fence、S0 边界、frozen retry/replay |

后续实现提交的普通独立回退命令分别为：

```text
git revert f00ebf13cc09c033d3d0e85a9984442ede12aea3
git revert 81d565088a59fd67c7b304b161d3b9a4b71b1371
git revert 872aa157a39cd1b235b52f7a7b630305e7201a3d
git revert 6d20fc0a7ac0027aea0309ac78b4f316256f7516
git revert b8b5ab26aeaf2eae26025eba5ebe4e8c61aa4f85
git revert cd2d4387fc64fd8ce35dd0f9bb22e5d6c8039be1
```

`5bc658f` 的中途权重已由 `872aa15` 改写为最终权重；不要通过单独回退
`5bc658f` 构造发布包。

## 最终 sparse-alignment 增量

`cd2d438` 在最终发布中提供：

- 来源显式 Episode number 与稳定 ordinal 分离；
- 默认零偏移使 Spy Family S3 的库内 E10 保持映射来源 E10；
- 显式锚点保持编号差，使 Frieren E29→来源 E1、E31→来源 E3；
- 编号不可靠时对整个 segment window 使用稳定位置 fallback，窗口内不混合；
- `SourceStartEpisodeId` 为权威来源锚点，number-only 只允许在全源可靠且唯一时解析；
- V22 映射协议、V28 安装标记、完整计划 fingerprint 和 stale-write fence；
- explicit single-Season S0 可用，whole-Series 与 unattended/media-import 在搜索前跳过 S0；
- retry/replay 使用冻结的 local/source EpisodeId/CommentId，不进入新的对齐解析器。

## 最终发布产物

当前发布清单只包含 `review-package-v28-sparse-0cda5510` 中的最终配对：

| 文件 | 大小 | SHA-256 |
| --- | ---: | --- |
| `Emby.Plugin.Danmu.dll` | 1656832 | `0cda55107003c5e491c763969b55c02a51601f3870610a0a6a5418d03833b1aa` |
| `DanmuSmartMatch.CustomCssJS.js` | 233307 | `01c7c6400f70146a22b2d1cbac65691017ed159a86398e1e653bb1e2fc0fcbb3` |

以下候选均为 superseded，并从当前发布清单排除：

- V27/V21 的全部早期 review package；
- DLL hash 以 `4892a83a` 开头的 sparse package；
- DLL hash 以 `0b05af38` 开头的 sparse package。

## 最终提交边界

最终源码 delta commit 为 `cd2d4387fc64fd8ce35dd0f9bb22e5d6c8039be1`。
提交 `680d31f7cd4039091fb8ffd03efccf1e568bc6b4` 只更新自动稀疏计划测试契约。
本文件不预先指定或推测发布文档 commit、tag、push、merge 或 GitHub Release
身份；发布记录应引用实际创建并回读的不可变对象。
