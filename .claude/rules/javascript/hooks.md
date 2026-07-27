# JavaScript Hooks & Automation — FinFlow Frontend

## フロントエンド確認コマンド（ビルドステップなし）

```bash
# ブラウザで直接開く（静的ファイルサーブ）
# APIサーバー経由でアクセス
dotnet run --project src/FinFlow.Api
# → http://localhost:5212 でフロントエンドが配信される

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
- [ ] **CSS/JS を変更したら `src/frontend/index.html` の `?v=` を更新した**（下記参照）
- [ ] 全画面を覆う要素を追加したら `pointer-events` を規定した（下記参照）

## アセットのバージョンクエリ（`?v=`）

ビルドステップを持たない方針のため、キャッシュ破棄は `index.html` の手書きの
バージョン文字列で行う。**`src/frontend/css/` または `src/frontend/js/` を
変更したら、必ず `src/frontend/index.html` の `?v=` を更新すること。**

```bash
# 現在の値を確認
grep -o '?v=[0-9]*' src/frontend/index.html | sort -u
```

更新を忘れると、サーバーが `Cache-Control: no-cache` を返していても
**すでに端末に焼き付いたキャッシュ**が使われ続け、「直したのに実機で直らない」
という切り分けの難しい不具合になる（実際に iPhone で発生した）。

> 制約: ES モジュールの import 指定子にクエリは伝播しないため、
> `app.js` が import する `router.js` / `utils/*.js` / `pages/*.js` には
> `?v=` が付かない。それらはサーバー側の `Cache-Control: no-cache`
> （`Program.cs`）に依存する。

## 全画面オーバーレイの規約

`position: fixed` + `inset: 0` の全画面要素は、**見えていなくてもヒットテストは
生きている**。`opacity: 0` や `transform` だけでは当たり判定は消えず、配下の
入力欄・リンクへのタップを丸ごと吸い込む（`.sidebar-overlay` で実際に発生）。

```css
/* 非表示時は必ず pointer-events を切る */
.some-overlay            { pointer-events: none; }
.some-overlay--visible   { pointer-events: auto; }
```

`hidden` 属性だけに頼らないこと。`display` を指定する作成者スタイルが競合すると
`[hidden]` が負ける（`main.css` の `[hidden]{display:none!important}` ガードは
その保険であって、当たり判定の設計を代替するものではない）。

また、`document.body` 直下に付けるオーバーレイ（`.loading-overlay` 等）は
`#page-container` のクリアでは消えないため、**ルート遷移時に明示的に片付ける**
（`router.js` の `loadingManager.reset()`、`app.js` の `closeSidebar()`）。

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
