AI株価予測 Webアプリケーション
1. 概要
過去の株価データとテクニカル指標を基に、AI（LightGBM）が翌営業日の日本株（TOPIX100）の寄り引け価格が上昇するか下落するかを予測するWebアプリケーション。

単なる分析スクリプトに留まらず、Flaskによるサーバー化、そして実際に操作可能なWebインターフェースの実装までを行うことで、データ分析からWeb開発、デプロイまでの一連のパイプライン構築能力を証明することを目的とする。

2. 主な機能
AIによる予測: 複数の特徴量（前日比、移動平均乖離率、RSI、米国市場指数など）を基に、翌営業日の上昇確率を銘柄ごとに予測。

推奨銘柄の提示: 予測確率に基づき、「買い推奨」「売り推奨」の銘柄をランキング形式で表示。

Webインターフェース: スマートフォンやPCのブラウザから、ボタン一つで最新の予測を実行可能。

PWA対応: iPhoneのホーム画面にアプリアイコンを追加し、ネイティブアプリのように起動可能。

3. 使用技術
バックエンド: Python, Flask, Gunicorn

機械学習: LightGBM

データ処理: pandas, NumPy

データ取得: yfinance (Yahoo Finance API), requests, BeautifulSoup4

フロントエンド: HTML, CSS, JavaScript

デプロイ環境（想定）: VPS (Linux), Nginx

4. 実行環境とデプロイ方法
a. ローカル環境での実行
フォルダ構成: プロジェクトをapp.pyと同じ階層にtemplates, staticフォルダを作成し、ファイルを配置する。

ライブラリのインストール:

pip install -r requirements.txt

サーバーの起動:

python app.py

アクセス: ブラウザで http://127.0.0.1:5000 にアクセスする。

b. VPSへのデプロイ（本番環境）
プロジェクトの配置: GitHub等を経由して、サーバーにプロジェクトファイルをアップロードする。

Python仮想環境の構築:

python3 -m venv venv
source venv/bin/activate
pip install -r requirements.txt

Gunicornによるアプリケーションサーバーの起動:

gunicorn --bind 0.0.0.0:5000 app:app