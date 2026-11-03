# KotoSub 使用说明

## 订阅

```text
#sub add https://example.com/feed.xml news    订阅，并命名为 news
#sub add https://github.com/owner/repo        订阅仓库的提交
#sub list                                     查看当前会话的订阅
#sub latest news                              立即查看 news 的最新条目
#sub remove news                              退订
```

- 订阅以群或私聊为单位。别名只在当前会话内有效，不同会话可以用同一个别名。
- 不写别名时，会根据地址自动取一个。
- 支持 RSS 2.0、Atom 1.0、RDF/RSS 1.0。

## 命令

`#sub` 和 `#rss` 等价。`#sub help` 会列出全部命令。

| 命令 | 说明 | 谁能用 |
| --- | --- | --- |
| `#sub add <url> [别名]` | 添加地址并订阅 | 群主 / 群管理员 / Bot 主人；私聊本人 |
| `#sub remove <别名>` | 退订 | 同上 |
| `#sub rename <旧别名> <新别名>` | 重命名别名 | 同上 |
| `#sub test <别名>` | 立即推送最新一条 | 同上 |
| `#sub list` | 列出当前会话的订阅 | 所有人 |
| `#sub status` | 查看当前会话状态 | 所有人 |
| `#sub latest <别名> [tag] [n=N]` | 查看最新条目，可按标签过滤、指定条数 | 所有人 |
| `#sub config [key] [value]` | 查看 / 修改配置 | Bot 主人 |
| `#sub interval <别名> <秒>` | 设置订阅源的轮询间隔 | Bot 主人 |
| `#sub reload` | 重新加载配置与状态 | Bot 主人 |
| `#sub feeds` | 查看全部订阅源概况 | Bot 主人 |

群里的普通成员在 `list` / `status` 中只能看到订阅地址的域名，完整地址只对管理者显示。

## 配置

默认配置见 [`Assets/config.toml`](Assets/config.toml)，每项都有注释。修改 `config.toml` 后宿主会自动应用；也可以用 `#sub config <key> <value>` 修改。

| 配置项 | 默认 | 说明 |
| --- | --- | --- |
| `enabled` | `true` | 是否启用 |
| `default_interval_seconds` | `5` | 默认轮询间隔（秒） |
| `min_interval_seconds` | `2` | 允许设置的最短轮询间隔 |
| `request_timeout_seconds` | `30` | 单次请求超时 |
| `max_items_per_push` | `3` | 每次轮询最多推送的条目数，其余记为已读 |
| `max_description_length` | `200` | 摘要最大字符数 |
| `latest_max_n` | `5` | `latest` 的 `n=` 上限 |
| `include_image` | `false` | 推送时附带首张图片 |
| `enable_rendered_cards` | `true` | 发送渲染卡片，不可用时回退为文字 |
| `enable_markdown` | `true` | QQ 官方机器人使用 Markdown |
| `enable_action_buttons` | `true` | QQ 官方机器人附带「阅读原文」「查看最新」按钮 |
| `markdown_template` | 见文件 | Markdown 模板，可用 `{title}` `{description}` `{image}` `{feed}` `{feed_id}` `{published}` `{link}` `{link_button}` |
| `allow_private_urls` | `false` | 允许订阅内网 / 本机地址 |
| `user_agent` | 浏览器 UA | 抓取时使用的 User-Agent |
| `last_seen_capacity` | `100` | 每个订阅源记住的已读条目数 |
| `backoff_max_seconds` | `3600` | 抓取失败后重试间隔的上限 |

## 数据文件

保存在插件目录（`plugins/Shirobot.Plugin.KotoSub/`）：

| 文件 | 内容 |
| --- | --- |
| `subscriptions.json` | 各会话的订阅与别名 |
| `state.json` | 抓取进度与推送记录 |

## 适配器

QQ 官方机器人的卡片、Markdown 和按钮需要配套的 QQPlatform 适配器；其他适配器发送文字和图片。
