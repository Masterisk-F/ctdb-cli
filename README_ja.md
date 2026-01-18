# ctdb-cli

CTDB (CUETools Database) と対話し、CUEシートと音声ファイルを用いてメタデータの取得、パリティ計算、検証、修復、アップロードを行う Linux 用コマンドラインツール

## 特徴
1. CUE + WAV からの CTDB パリティ計算
2. CTDBパリティデータを使用したエラー修復
3. CTDB へのメタデータおよびパリティの送信
4. Linux (.NET 8.0) 環境での動作サポート

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
ctdb-cli <command> <cue_file>
```

```bash
dotnet run --project CTDB.CLI/CTDB.CLI.csproj -- <command> <cue_file>
```


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
修復されたファイルは `元ファイル名_repaired.wav` として保存されます。
```bash
ctdb-cli repair test.cue
```

> **注意**: 修復にはCTDB上に十分なパリティデータが必要です。
> 修復不可能な場合はエラーメッセージが表示されます。

#### 5. Submit (送信)
計算したパリティとメタデータをCTDBに送信します。
**正しいデータであることの確度が高いときに限り実行してください。**
```bash
ctdb-cli submit test.cue
```

