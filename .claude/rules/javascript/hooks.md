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

### .NET SDK が無い環境での E2E

`playwright.config.js` は `dotnet run` で本物の API を起動する。使えるならそちらが正しい
（本物の契約を検証できる唯一の構成）。SDK が入っていない作業環境では、
`src/frontend` を静的配信しつつ `/api/*` をインメモリで返すモックサーバーを使う:

```bash
npm ci                  # 初回のみ（ブラウザは /opt/pw-browsers に既存、install 不要）
npm run test:e2e:mock   # playwright.mock.config.js + tests/e2e/mock-server/server.js
```

モックは**サーバー実装に依存しないスペック**（描画・リスナー・タップ領域の回帰）専用。
API 契約や DB の検証はこの構成では**できない**。

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
- [ ] **再描画されない要素へのリスナーを、再描画のたびに貼り直していない**（下記）
- [ ] **`innerHTML` で UI を退避・復元していない**（下記）
- [ ] **モバイル用のオーバーライドを、ベース定義と同じ CSS ファイルに書いた**（下記）
- [ ] **Shadow DOM 内のタップ領域をコンポーネント内で自前指定した**（下記）
- [ ] **CSS/JS を変更したら `src/frontend/index.html` の `?v=` を更新した**（下記参照）
- [ ] **モジュールを跨ぐ新 API はオプショナル呼び出し（`?.`）で導入した**（下記参照）
- [ ] `app.js` 末尾の `window.__ffBooted = true` を消していない（起動ガードの成功判定）
- [ ] 全画面を覆う要素を追加したら `pointer-events` を規定した（下記参照）

## イベントリスナーの寿命（実際に二重登録を起こした）

ページの DOM は多くの場合 2 層になっている。

| 層 | 作られるタイミング | 例 |
|---|---|---|
| **shell** | ページ描画時に **1 回だけ**。以後作り直されない | `#add-subscription-btn` / モーダル一式 / `#modal-save-btn` / 一覧の**コンテナ要素そのもの** |
| **list** | データ再取得のたびに `innerHTML` ごと差し替わる | コンテナの**中身** |

**規約: shell 側へのリスナーは、ページ描画時に 1 回だけ貼る。
`loadAndRender()` のような再取得関数からは絶対に呼ばない。**
一覧の項目は、永続するコンテナへの**イベント委譲**で扱う（委譲元は作り直されないので、
中身が何度差し替わってもリスナーは 1 本のまま）。

```javascript
// NG: 再取得のたびに、作り直されない要素へリスナーが積み上がる
async function loadAndRender(container) {
  contentArea.innerHTML = buildListHtml(items);
  attachEventListeners(container);      // ← #modal-save-btn に毎回 1 本増える
}

// OK: shell 用は 1 回だけ。list はコンテナへの委譲で扱う
export function renderPage(container) {
  container.innerHTML = buildShell();
  attachShellListeners(container, state);   // ← ここだけ
  loadAndRender(container, state);
}
```

実際に起きた事故: 増えたリスナーがそれぞれ `loadAndRender()` を呼ぶため、
リスナー数が **1 → 2 → 4 → 8** と指数的に増え、1 回の「保存」で POST が
2 回・4 回と飛んで**サブスクが二重・三重に登録**された。

**`btn.disabled = true` では防げない。** 1 回のクリックのディスパッチは、
途中で `disabled` にしても**その要素に登録済みの全リスナーを最後まで呼ぶ**
（`stopImmediatePropagation()` を呼ばない限り）。人間の二重タップには効くが、
多重リスナーには無力。保険を置くなら `state.busy` のような
**ハンドラ側のフラグ**にすること。

回帰テスト: `tests/e2e/subscription-duplicate-regression.spec.js`

## `innerHTML` で UI を退避・復元しない（実際に削除ボタンを殺した）

```javascript
// NG: 復元された要素は「リスナーを 1 本も持たない別物」になる
const original = footer.innerHTML;
footer.innerHTML = confirmUi;
cancelBtn.addEventListener('click', () => { footer.innerHTML = original; });
```

`innerHTML` への代入は要素を作り直すため、退避元に付いていたリスナーは復元されない。
実際、サブスク画面で「削除 → キャンセル」を一度でも操作すると、以後
**保存も削除も完全に無反応**になっていた（「削除ボタンだけ反応が鈍い」の正体）。

確認 UI が要るときは、共通の `confirmDialog`（`js/components/ff-confirm-dialog.js`）を使う。
自前で出す場合も、要素の作り直しではなく `hidden` 属性の切り替えで行うこと。

回帰テスト: `tests/e2e/delete-flow.spec.js`

## Shadow DOM にはグローバル CSS が届かない

`main.css` の `@media (max-width:768px){ .btn{min-height:44px} }` は
**Shadow 境界を越えない**。そのため `ff-confirm-dialog` の「削除する／キャンセル」は
実測 34px、`ff-toast` の閉じるボタンは 14px 四方のままだった。

- タップ領域・レイアウトは**コンポーネント内の `<style>` に自前で書く**
- 色やサイズのトークン（CSS カスタムプロパティ）は**継承されるので `var()` で参照できる**。
  ただし `:root` に**実際に定義されているか**を確認すること
  （`--font-family` / `--border-radius-lg` / `--color-text-secondary` は
  参照されていたのに未定義で、ずっとフォールバック値が使われていた）
- 検査ツールも `document.querySelectorAll` では貫通しない。
  `tests/e2e/helpers/shadow-dom.js` の `tapTargetIssuesDeep()` を使う

## モバイル用オーバーライドを書く場所

CSS の読み込み順は **main.css → components.css → pages.css**（`index.html`）。
同じ詳細度なら**後に読まれた方が勝つ**。

**規約: あるクラスのモバイル用オーバーライドは、そのクラスの
ベース定義があるファイルと同じファイルに書く。**

```css
/* components.css にベースがある .fab を、main.css のメディアクエリで
   display:flex にしても、後から読まれる components.css の
   `display: none`（ベース宣言）に負けて出てこない。 */
```

`.fab` と `.modal__footer` で実際にこれを踏んだ。`.modal__dialog` にも
同種の注意書きが `components.css` の冒頭にある。

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

### 新旧モジュールの混在（実際に白画面を起こした）

上の制約は「古いままになる」だけでは終わらない。**`?v=` が付く `app.js` は必ず新版、
付かない import 先は古いまま**という**混在**が成立する。各モジュールは 1 ファイル 1 URL で
独立したキャッシュエントリなので、まとめて新しくなる保証がない。

実際に起きた事故: 後から `api-client.js` に追加した `loadingManager.reset()` を、
端末に残っていた古い `api-client.js` が持っておらず、新しい `router.js` がそれを呼んで
`TypeError`。しかもそれが `handleRoute()` の**最初の実行文**（`container.innerHTML` に
触れる前）だったため、**全ルートが何も描画されないまま停止＝真っ白**になった。
プライベートブラウザではキャッシュが空で全モジュールが整合するため正常に動き、
「プライベートだと直る」という切り分けにくい症状になった。

**規約: モジュールを跨ぐ新しい API は、最低 1 リリースの間オプショナル呼び出しで導入する。**

```javascript
// NG: 古い api-client.js と組み合わさると TypeError → 白画面
loadingManager.reset();

// OK: 最悪でも「その機能が効かない」だけで済む
loadingManager?.reset?.();
```

これは `index.html` のインライン起動ガード（下記）とセットで機能する。
片方だけでは守り切れない。

## 起動ガード（`index.html` のインライン `<script>`）

`index.html` の `<head>` 先頭に、**インラインの**起動ガードがある。

- 起動失敗（例外・リソース読み込み失敗・無言のハング）を検知する
- 1 回だけ `fetch(url, { cache: 'reload' })` でキャッシュを破棄して自己修復する
  （`cache: 'reload'` は URL を変えずに焼き付いたエントリを置換できる唯一の手段）
- それでも駄目なら、白画面ではなく**原因の見えるエラー画面**と復旧ボタンを出す

**必ずインラインのまま維持すること。** 外部 `.js` に切り出すと、それ自体がキャッシュ
事故の対象になり、肝心なときに動かない。`index.html` はサーバーの `no-cache` で
毎回再検証されるため、ここだけは確実に最新版が動く。

`app.js` は末尾で `window.__ffBooted = true` を立てる。これがガードの成功判定なので、
**`app.js` の末尾からこの行を消さないこと**。

回帰テスト: `tests/e2e/boot-guard-regression.spec.js`

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
