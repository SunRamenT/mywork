# ベースとなる公式Pythonイメージを選択
FROM python:3.9-slim

# コンテナ内の作業ディレクトリを設定
WORKDIR /app

# 必要なライブラリ一覧を先にコピーし、インストール
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# アプリケーションのコードと学習済みモデルをコピー
COPY . .

# Gunicornサーバーを起動するコマンド
# --timeout 600 : タイムアウトを600秒（10分）に延長
CMD ["gunicorn", "--bind", "0.0.0.0:5000", "--timeout", "600", "app:app"]