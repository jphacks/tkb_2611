# imozuru の導入元

- 配布元: https://github.com/MaryCache/imozuru
- 取り込んだコミット: `ecb82c71b611872a0f49e783eda3bec987c80d91`
- 取り込んだディレクトリ: `skills/imozuru/`
- ライセンス: MIT。同梱の `LICENSE` を参照。

`SKILL.md` と `references/` は配布元のまま取り込んでいる。
`scripts/fetch.py` と `scripts/watch.py` は、個人情報検査が誤検出するコメント内の URL を例示用ドメインに変更した。処理は変更していない。
ほかのスクリプトは配布元のまま取り込んでいる。
`LICENSE` は配布元のルートから同梱した。
このファイルと `CLAUDE.md` の利用上の注意は、このリポジトリで追加した。

## 更新手順

配布元の対象コミットを確認し、`skills/imozuru/` の内容を更新する。
`LICENSE` とこのファイルは残し、コミット番号を更新する。
差分を確認し、コミット前に `/verify` の検査を通す。

## 必要なもの

Python 3.10 以上。補助スクリプトは標準ライブラリで動く。
PDF の本文取得には `pdftotext`、スキャン PDF の OCR には `pdftoppm` と `tesseract`、動画の字幕取得には `yt-dlp` が必要。
HTML の変換は、`ax` がなければ標準ライブラリで行う。
使い方は `SKILL.md` を参照。
