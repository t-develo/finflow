# JavaScript Hooks & Automation — FinFlow Frontend

## フロントエンド確認コマンド（ビルドステップなし）

```bash
# ブラウザで直接開く（静的ファイルサーブ）
# APIサーバー経由でアクセス
dotnet run --project src/FinFlow.Api
# → http://localhost:5000 でフロントエンドが配信される

# または簡易HTTPサーバー
python3 -m http.server 3000 -d src/frontend
```

## コンソールログのチェック

実装ファイル変更後、デバッグ用 `console.log` が残っていないか確認:

```bash
# .js ファイル内の console.log を検索
grep -rn "console\.log" src/frontend/js/ --include="*.js"
```

## コミット前チェックリスト

- [ ] `console.log` のデバッグ出力が残っていない
- [ ] `innerHTML` に未サニタイズの値を代入していない
- [ ] Web Components の `ff-` プレフィックスが付いている
- [ ] モックから実APIへの切り替えが適切（Sprint 2移行時）
- [ ] JWT の不適切な露出がない

## CSS クラス命名確認

BEM記法が正しく使われているか確認:

```
.expense-form           # ブロック
.expense-form__input    # エレメント
.expense-form__input--error  # モディファイア（エラー状態）
.expense-form__button--disabled  # モディファイア（無効状態）
```

## Sprint 切り替え時の確認

Sprint 2 でモックから実APIに切り替える際:

```javascript
// 全ファイルで USE_MOCK フラグを確認
grep -rn "USE_MOCK\|mocks/" src/frontend/js/
```
