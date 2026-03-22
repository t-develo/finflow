# Hooks — FinFlow

## 設定済みフック（`.claude/settings.json`）

| フック | タイミング | 目的 |
|--------|-----------|------|
| `SessionStart` | セッション開始時 | `SESSION_STATE.md` を表示してコンテキストを復元 |
| `PreCompact` | コンテキスト圧縮前 | 状態保存のリマインド |
| `Stop` | 各レスポンス後 | `SESSION_STATE.md` 更新のリマインド |

## セッション状態の管理（iOS対応）

**セッション開始時:**
- フックが自動で `SESSION_STATE.md` を表示
- 前回の作業状況を把握してから作業開始

**作業中:**
- 重要な決定・進捗があれば `SESSION_STATE.md` を更新

**セッション終了前:**
```
SESSION_STATE.md に以下を記録してから終了:
- 完了したタスク
- 進行中のタスク
- 次にやること
- ブロッカー
```

## TodoWrite の活用

マルチステップのタスク（3ステップ以上）は `TodoWrite` ツールで進捗を管理:
- 作業開始前に `in_progress` に設定
- 完了したら即 `completed` に設定
- 同時に `in_progress` は1つのみ

## フック環境変数

```bash
# フックの動作レベル制御（everything-claude-code プラグイン）
export ECC_HOOK_PROFILE=standard  # minimal | standard | strict
export ECC_DISABLED_HOOKS="pre:bash:tmux-reminder"
```

## 注意事項

- `--no-verify` でgitフックをスキップしない
- `dangerouslySkipPermissions` はtrustedな計画実行時のみ
- 探索フェーズでは auto-accept を無効化する
