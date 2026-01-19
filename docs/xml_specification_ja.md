# ctdb-cli 出力XML仕様

`ctdb-cli` で `--xml` オプションを指定した際の出力フォーマット

## 共通仕様
- **文字コード**: UTF-8
- **出力**: 標準出力（stdout）にXMLデータが出力されます。進行状況やエラーログは標準エラー出力（stderr）に出力されます。
- **名前空間**: ルート要素 `ctdb` は `http://db.cuetools.net/ns/mmd-1.0#` に属します。
- **エラーハンドリング**: 命令の実行中にエラーが発生した場合でも `null` は返されず、各コマンドの結果要素（`verify_result` など）の `status` 属性にエラー内容が含まれた状態で XML が出力されます。

## 1. lookup コマンド
CTDB `lookup2.php`エンドポイントから返されたXMLをそのまま出力します。

**例:**
```xml
<ctdb xmlns="http://db.cuetools.net/ns/mmd-1.0#">
  <entry id="..." crc32="..." ... />
  <metadata source="..." artist="..." album="...">
    ...
  </metadata>
</ctdb>
```

## 2. calc コマンド
パリティ計算結果を出力します。

**構造:**
- `calc_result`: 計算結果のルート
    - `@status`: ステータス (`success` または `failure`)
    - `@message`: エラーメッセージ（`failure` 時のみ）
    - `@toc_id`: TOC ID
    - `@ctdb_crc`: ディスク全体のCRC (8桁16進数)
    - `track`: 各トラックの情報
        - `@number`: トラック番号
        - `@crc`: AccurateRip CRC
        - `@crc32`: CRC32

## 3. verify コマンド
検証結果の詳細を出力します。

**構造:**
- `verify_result`: 検証結果のルート
    - `@toc`: 対象のTOC ID
    - `@status`: 検証ステータス (`found`, `not_found`, または `failure`)
    - `@message`: エラーメッセージ（`failure` 時のみ）
    - `@confidence`: 全体の信頼度
    - `@total_entries`: DBから見つかったエントリ数
    - `entry`: 各DBエントリとの比較結果
        - `@id`: エントリのインデックス
        - `@conf`: そのエントリの信頼度
        - `@crc`: そのエントリのCRC
        - `@offset`: オフセット
        - `@status`: そのエントリに対する検証ステータス (CUETools由来の文字列)
        - `@has_errors`: エラーの有無
        - `@can_recover`: 修復可能か
        - `track`: 各トラックの比較結果
            - `@number`: トラック番号
            - `@local_crc`: 手元のファイルのCRC
            - `@remote_crc`: DB側のCRC
            - `@matched`: 一致したか (true/false)
        - `repair`: 修復情報（修復可能な場合のみ）
            - `@correctable_errors`: 修正可能なエラー数
            - `@affected_sectors`: 影響を受けているセクタ

## 4. submit コマンド
送信内容と結果を出力します。

**構造:**
- `submit_result`: 送信結果のルート
    - `@status`: 全体のステータス（`submitted`, `dry_run`, または `failure`）
    - `@message`: エラーメッセージ（`failure` 時のみ）
    - `submitted_metadata`: 送信したメタデータ
        - `@artist`, `@title`, `@barcode`, `@drive`, `@quality`
    - `response`: APIからのレスポンス
        - `@status`: 送信ステータス (`submitted`, `dry_run`, または `failure`)
        - `@message`: メッセージ
        - `@parity_needed`: パリティファイルのアップロードが必要か
    - `raw_response`: CTDB `submit2.php`エンドポイントからのレスポンスを再シリアライズしたXML（元のXMLとは完全に一致しない可能性があります）

## 5. repair コマンド
修復処理の結果を出力します。

**構造:**
- `repair_result`: 修復結果のルート
    - `@status`: ステータス (`repaired`, `clean`, `unrecoverable`, または `failure`)
    - `@message`: エラーメッセージ（`failure` 時のみ）
    - `@output_path`: 出力ファイルパス
    - `@samples_written`: 書き込まれたサンプル数
    - `entry`: DBエントリのリスト。`verify_result`-`entry`と同様の構造だが以下の要素が追加される
        - `@used_for_repair`: そのエントリが実際に修復に使用されたか (true/false)
