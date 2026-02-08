# ctdb-cli

CTDB (CUETools Database) と対話し、CUEシートと音声ファイルを用いてメタデータの取得、パリティ計算、検証、修復、アップロードを行う Linux 用コマンドラインツール

## 特徴
1. CUE + WAV/FLAC からの CTDB パリティ計算 (マルチファイルCUE対応)
2. CTDBパリティデータを使用したエラー修復
3. CTDB へのメタデータおよびパリティの送信
4. Linux (.NET 8.0) 環境での動作サポート
5. CUEシートの文字コード自動検出

## セットアップ

本ツールは [cuetools.net](https://github.com/gchudov/cuetools.net) のライブラリに依存しています。

### ビルドとインストール

```bash
./configure
make
sudo make install
```

オプション:
- `./configure --prefix=/usr` - インストール先を変更（デフォルト: `/usr/local`）
- `make DESTDIR=/path/to/dest install` - パッケージング用にDESTDIRを指定

### 開発用ビルド

依存関係の取得とパッチ適用:
```bash
./setup.sh
```

> **パッチが必要な理由について**
> - **Freedb.csproj**: オリジナルのプロジェクトファイルは古い形式であり、Linux上の `dotnet build` で正しく扱えないため、SDKスタイル形式に変換しています。
> - **TagLib**: オリジナルの `CUESheet.cs` が依存している一部のプロパティ（`AudioSampleCount`等）が現在の TagLib Sharp に不足しているため、これを補う修正を適用しています。
> - **UTF.Unknown**: CUEシートの文字コードを自動検出します。

ビルド:
```bash
# テストビルド
dotnet build CTDB.CLI/CTDB.CLI.csproj

# 軽量版 (Framework-dependent)
dotnet publish CTDB.CLI/CTDB.CLI.csproj -c Release -o publish/dependent

# 単体動作版 (Self-contained / Single File)
# 相手の環境に .NET がなくても動作しますが、サイズは大きくなります。
dotnet publish CTDB.CLI/CTDB.CLI.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/standalone
```

## 使用方法

`dotnet run` を使用するか、ビルド済みバイナリを使用します。

```bash
ctdb-cli [options] <command> <cue_file> [command_options]
```

```bash
dotnet run --project CTDB.CLI/CTDB.CLI.csproj -- [options] <command> <cue_file> [command_options]
```

### グローバルオプション

| オプション | 説明 |
|------------|------|
| `--xml` | 実行結果を XML 形式で **標準出力 (stdout)** に出力します。ログやエラーは **標準エラー出力 (stderr)** に出力されます。([XML出力形式](./docs/xml_specification_ja.md)) |


### コマンド一覧

#### 1. Lookup (メタデータ検索)
CUEシートに基づいてCTDBからメタデータを検索し、Raw XMLを表示します。
```bash
ctdb-cli lookup test.cue
```

#### 2. Calc (パリティ/CRC計算)
ローカルの音声ファイルを読み込み、AccurateRip CRC および CTDB CRC を計算します。
```bash
ctdb-cli calc test.cue
```

#### 3. Verify (検証)
CTDB上のデータとローカルファイルを照合し、リッピングの正確性を検証します。
```bash
ctdb-cli verify test.cue
```

#### 4. Repair (修復)
CTDBのパリティデータを使用して、エラーのあるリップファイルを修復します。
修復されたファイルは `CUEファイル名_repaired.wav` として保存されます。
```bash
ctdb-cli repair test.cue
```

> **注意**: 修復にはCTDB上に十分なパリティデータが必要です。
> 修復不可能な場合はエラーメッセージが表示されます。

> **注意**: 入力が単一ファイルCUEかマルチファイルCUEかにかかわらず、出力は単一のWAVファイルになります。

#### 5. Submit (送信)
計算したパリティとメタデータをCTDBに送信します。

**正しいデータであることの確度が高いときに限り実行してください。**

```bash
ctdb-cli submit test.cue --drive "Drive Name" --quality 100
```

| 引数 | 必須 | 説明 |
|------|------|------|
| `--drive` | ○ | ドライブ名 (例: "PLEXTOR PX-716A") |
| `--quality` | ○ | 品質 (1-100) |

> **ドライブ名の確認方法**
> 
> `cd-info` コマンド (libcdio) で取得できます。
> 
> ```bash
> $ cd-info -A --no-cddb
> ...
> Vendor                      : Optiarc 
> Model                       : DVD RW AD-7290H 
> ...
> ```
> この場合`--drive "Optiarc - DVD RW AD-7290H"` と指定できます。
> 
> [ソース](https://github.com/gchudov/db.cue.tools/blob/master/utils/docker/ctdbweb/db.cue.tools/submit2.php)によれば、
> ドライブ名が受領されるためには、文字列の先頭が登録されたベンダー名（おそらく PIONEER, ASUS, HL-DT-ST など）で始まり、
> その後に任意の文字が続き、かつどこかにハイフン (`-`) が含まれている必要があります。

デフォルトでは送信内容を表示するだけの **dry-run** モードで動作します。
実際に送信を行うには、環境変数 `CTDB_CLI_CALLER` に呼び出し元のアプリケーション名を設定してください。

```bash
env CTDB_CLI_CALLER="your-app-name" ctdb-cli submit test.cue --drive "drive name" --quality 100
```

