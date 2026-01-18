# CTDB CLI Tool

CTDB (CUETools Database) と対話し、CUEシートと音声ファイルを用いてメタデータの取得、パリティ計算、検証、アップロードを行う Linux 用コマンドラインツールです。

## 特徴
- CUE + WAV からの AccurateRip / CTDB パリティ計算
- CTDB へのメタデータおよびパリティのアップロード
- Linux (.NET 8.0) 環境での動作サポート

## セットアップ

本ツールは `cuetools.net` のライブラリに依存していますが、Linux上でのビルド互換性を確保するため、サブモジュールではなくセットアップスクリプト経由で依存関係を取得・パッチ適用します。

### 1. 依存関係の取得とパッチ適用

以下のスクリプトを実行して、`cuetools.net` のソースコードを取得し、Linuxビルド用の修正パッチを適用します。

```bash
./setup.sh
```

**パッチが必要な理由について**:
- **Freedb.csproj**: オリジナルのプロジェクトファイルは古い形式であり、Linux上の `dotnet build` で正しく扱えないため、SDKスタイル形式に変換しています。
- **TagLib**: オリジナルの `CUESheet.cs` が依存している一部のプロパティ（`AudioSampleCount`等）が現在の TagLib Sharp に不足しているため、これを補う修正を適用しています。

### 2. ビルド

```bash
dotnet build CTDB.CLI/CTDB.CLI.csproj
```

## ビルド成果物の配布 (Distribution)

他者に配布する場合や、単体で実行可能な状態にするには `publish` コマンドを使用してください。

### 1. 軽量版 (Framework-dependent)
相手が .NET 8.0 Runtime をインストールしている場合におすすめです。サイズが小さいです。
```bash
dotnet publish CTDB.CLI/CTDB.CLI.csproj -c Release -o publish/dependent
```
- **配布物**: `publish/dependent` フォルダの中身すべて（DLLファイル等を含む）
- **サイズ**: 約 4 MB

### 2. 単体動作版 (Self-contained / Single File)
相手の環境に .NET がなくても動作します。1つの実行ファイルにまとまりますが、サイズは大きくなります。
```bash
dotnet publish CTDB.CLI/CTDB.CLI.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/standalone
```
- **配布物**: `publish/standalone/CTDB.CLI` (この1ファイルのみで動作します)
- **サイズ**: 約 68 MB


## 使用方法

`dotnet run` を使用するか、ビルド済みバイナリを使用します。

```bash
dotnet run --project CTDB.CLI/CTDB.CLI.csproj -- <command> <cue_file>
```

### コマンド一覧

#### 1. Lookup (メタデータ検索)
CUEシートに基づいてCTDBからメタデータを検索し、Raw XMLを表示します。
```bash
dotnet run --project CTDB.CLI/CTDB.CLI.csproj -- lookup test.cue
```

#### 2. Calc (パリティ/CRC計算)
ローカルの音声ファイルを読み込み、AccurateRip CRC および CTDB CRC を計算します。
```bash
dotnet run --project CTDB.CLI/CTDB.CLI.csproj -- calc test.cue
```

#### 3. Verify (検証)
CTDB上のデータとローカルファイルを照合し、リッピングの正確性を検証します。
```bash
dotnet run --project CTDB.CLI/CTDB.CLI.csproj -- verify test.cue
```

#### 4. Upload (アップロード)
計算したパリティとメタデータをCTDBに送信します。
```bash
dotnet run --project CTDB.CLI/CTDB.CLI.csproj -- upload test.cue
```
