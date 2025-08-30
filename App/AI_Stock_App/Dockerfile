# ベースとなる公式Pythonイメージを選択
FROM python:3.9-slim

# コンテナ内の作業ディレクトリを設定
WORKDIR /app

# 必要なライブラリ一覧を先にコピーし、インストール
# （コード変更のたびにライブラリを再インストールするのを防ぐため）
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# アプリケーションのコード全体をコピー
COPY . .

# Gunicornサーバーを起動するコマンド
CMD ["gunicorn", "--bind", "0.0.0.0:5000", "app:app"]
