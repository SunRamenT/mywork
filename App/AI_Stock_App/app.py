"""
AI株価予測 Webアプリケーション バックエンド (Flask)

このサーバーは、事前に学習・保存されたAIモデルを読み込み、
リクエストに応じて高速に予測結果を返すことに専念する。
"""
import yfinance as yf
import pandas as pd
import requests
from bs4 import BeautifulSoup
import os
import warnings
import datetime
import lightgbm as lgb
import numpy as np
from flask import Flask, render_template, jsonify
from flask_cors import CORS
import joblib # モデルの読み込み用

# --- グローバル設定 ---
warnings.filterwarnings('ignore')

# ==============================================================================
# 1. 設定エリア (CONFIG)
# ==============================================================================
CONFIG = {
    # 予測に必要な最小限の過去データの日数を定義 (余裕を持って設定)
    "data_fetch_days": 60,
    "features": [
        '前日比', '寄り引け変動率', '乖離率(25日)',
        'S&P500前日比', 'Nasdaq前日比',
        'RSI', 'BB_Width', 'Volume_Ratio'
    ],
    "trading_rule": {
        "num_rank_trades": 10,
    }
}

# ==============================================================================
# 2. モデルの読み込み
# ==============================================================================
# アプリケーション起動時に一度だけ、保存されたモデルを読み込む
try:
    # スクリプト自身の場所を基準に、モデルファイルのパスを構築
    script_dir = os.path.dirname(os.path.abspath(__file__))
    model_path = os.path.join(script_dir, 'model.lgb')
    model = joblib.load(model_path)
    print(f"--- 事前学習済みモデル'{model_path}'の読み込み完了 ---")
except FileNotFoundError:
    print("❌ エラー: 'model.lgb'が見つかりません。先にtrain_model.pyを実行し、モデルファイルを生成してください。")
    model = None

# ==============================================================================
# 3. 予測パイプライン関数（軽量版）
# ==============================================================================
def get_topix100_codes():
    """TOPIX100構成銘柄の証券コード取得"""
    url = "https://search.sbisec.co.jp/v2/popwin/info/stock/pop690_topix100.html"
    response = requests.get(url, timeout=15)
    response.raise_for_status()
    soup = BeautifulSoup(response.content, "html.parser")
    codes = [
        cols[0].text.strip()
        for row in soup.select("table tr")
        if (cols := row.find_all("td")) and len(cols) > 1 and cols[0].text.strip().isdigit()
    ]
    if not codes: raise ValueError("銘柄コードの取得に失敗。")
    return codes

def download_prediction_data(start_date, end_date, topix_100):
    """予測に必要な最小限のデータをダウンロード"""
    tickers_jp = [f"{code}.T" for code in topix_100]
    raw_jp_data = yf.download(tickers_jp, start=start_date, end=end_date, auto_adjust=True, progress=False)
    raw_us_indices = yf.download(["^GSPC", "^IXIC"], start=start_date, end=end_date, auto_adjust=True, progress=False)
    return raw_jp_data, raw_us_indices

def calculate_features(df):
    """各種テクニカル指標（特徴量）の計算"""
    df = df.sort_values(['code', 'Date'])
    df['前日比'] = df.groupby('code')['Close'].pct_change(1) * 100
    df['寄り引け変動率'] = (df['Close'] - df['Open']) / df['Open'] * 100
    df['SMA_25'] = df.groupby('code')['Close'].transform(lambda x: x.rolling(window=25, min_periods=25).mean())
    df['乖離率(25日)'] = ((df['Close'] - df['SMA_25']) / df['SMA_25']) * 100
    def rsi(series, period=14):
        delta = series.diff(1)
        gain = delta.where(delta > 0, 0)
        loss = -delta.where(delta < 0, 0)
        avg_gain = gain.rolling(window=period, min_periods=period).mean()
        avg_loss = loss.rolling(window=period, min_periods=period).mean()
        rs = avg_gain / avg_loss
        return 100 - (100 / (1 + rs))
    df['RSI'] = df.groupby('code')['Close'].transform(lambda x: rsi(x))
    df['SMA_20'] = df.groupby('code')['Close'].transform(lambda x: x.rolling(window=20).mean())
    df['STD_20'] = df.groupby('code')['Close'].transform(lambda x: x.rolling(window=20).std())
    df['BB_Width'] = (df['SMA_20'] + 2 * df['STD_20'] - (df['SMA_20'] - 2 * df['STD_20'])) / df['SMA_20']
    df['Volume_SMA_25'] = df.groupby('code')['Volume'].transform(lambda x: x.rolling(window=25).mean())
    df['Volume_Ratio'] = df['Volume'] / df['Volume_SMA_25']
    return df

def prepare_prediction_dataframe(raw_jp_data, raw_us_indices, config):
    """予測用にデータを整形する"""
    raw_jp_data.columns.names = ['feature', 'code']
    df_jp = raw_jp_data.stack(level='code').reset_index()
    df_jp['code'] = df_jp['code'].str.replace('.T', '', regex=False)
    df_jp_featured = calculate_features(df_jp)
    df_us = pd.DataFrame(index=raw_us_indices.index)
    df_us['S&P500_Close'] = raw_us_indices.get(('Close', '^GSPC'))
    df_us['Nasdaq_Close'] = raw_us_indices.get(('Close', '^IXIC'))
    df_us.reset_index(inplace=True)
    df_us["Date"] = pd.to_datetime(df_us["Date"]).dt.tz_localize(None)
    df_us["S&P500前日比"] = df_us['S&P500_Close'].pct_change() * 100
    df_us["Nasdaq前日比"] = df_us['Nasdaq_Close'].pct_change() * 100
    df_merged = pd.merge_asof(
        df_jp_featured.sort_values('Date'),
        df_us[["Date", "S&P500前日比", "Nasdaq前日比"]].dropna(),
        on="Date",
        direction="backward"
    )
    features_to_shift = config["features"]
    for feature in features_to_shift:
        if feature in df_merged.columns:
            df_merged[f'{feature}_lag1'] = df_merged.groupby('code')[feature].shift(1)
    final_features = [f'{col}_lag1' for col in features_to_shift]
    df_final = df_merged.dropna(subset=final_features).copy()
    return df_final, final_features

def run_prediction_pipeline():
    """軽量化された予測パイプライン"""
    print("--- 予測パイプライン開始 ---")
    if model is None:
        raise ValueError("モデルがロードされていません。")

    # 予測に必要な最小限の期間のデータを取得
    end_date = datetime.datetime.now()
    start_date = end_date - datetime.timedelta(days=CONFIG["data_fetch_days"])
    
    topix_100_codes = get_topix100_codes()
    jp_data, us_data = download_prediction_data(start_date.strftime("%Y-%m-%d"), end_date.strftime("%Y-%m-%d"), topix_100_codes)
    final_df, feature_names = prepare_prediction_dataframe(jp_data, us_data, CONFIG)

    print("--- 予測のための最新データを準備 ---")
    latest_data = final_df.loc[final_df.groupby('code')['Date'].idxmax()]
    if latest_data.empty: raise ValueError("予測に使用できる最新データが見つからない。")
    print(f"最新データの日付: {latest_data['Date'].min().date()}")

    print("--- 翌営業日の上昇確率を予測 ---")
    predictions = model.predict(latest_data[feature_names])
    
    df_prediction = pd.DataFrame({'code': latest_data['code'], 'prediction': predictions})
    num_trades = CONFIG["trading_rule"]["num_rank_trades"]
    df_prediction_sorted = df_prediction.sort_values('prediction', ascending=False)
    df_buy = df_prediction_sorted.head(num_trades)
    df_sell = df_prediction_sorted.tail(num_trades).sort_values('prediction', ascending=True)
    
    print("--- 予測パイプライン完了 ---")
    return {
        "latest_data_date": latest_data['Date'].min().strftime("%Y-%m-%d"),
        "buy_recommendations": df_buy.to_dict(orient='records'),
        "sell_recommendations": df_sell.to_dict(orient='records')
    }

# ==============================================================================
# 4. Flask Webサーバーエリア
# ==============================================================================
app = Flask(__name__, static_folder='static', template_folder='templates')
CORS(app)

@app.route('/')
def index():
    return render_template('index.html')

@app.route('/predict', methods=['POST'])
def predict():
    try:
        result = run_prediction_pipeline()
        return jsonify(result)
    except Exception as e:
        import traceback
        traceback.print_exc()
        return jsonify({"error": str(e)}), 500

if __name__ == '__main__':
    app.run(debug=False, host='0.0.0.0', port=5000)

