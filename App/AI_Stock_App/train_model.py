"""
AIモデル学習・保存スクリプト

このスクリプトは、Webサーバーを起動する前に【PC上で一度だけ】実行する。
全期間のデータをダウンロード・整形し、AIモデルを学習させ、
その結果を「model.lgb」というファイルに保存する役割を持つ。
"""
import yfinance as yf
import pandas as pd
import requests
from bs4 import BeautifulSoup
import os
import warnings
import datetime
import lightgbm as lgb
import joblib # モデルの保存・読み込み用

# --- グローバル設定 ---
warnings.filterwarnings('ignore')
pd.set_option('display.max_rows', 100)

# ==============================================================================
# 1. 設定エリア (CONFIG)
# ==============================================================================
CONFIG = {
    # 学習にはPCの日付を基準に全期間のデータを使用
    "start_date": "2021-08-01",
    "end_date": datetime.datetime.now().strftime("%Y-%m-%d"),
    "target_variable": "寄り引け変動率",
    "features": [
        '前日比', '寄り引け変動率', '乖離率(25日)',
        'S&P500前日比', 'Nasdaq前日比',
        'RSI', 'BB_Width', 'Volume_Ratio'
    ],
    "lgbm_params": {
        "objective": "binary", "metric": "auc", "learning_rate": 0.01,
        "verbosity": -1, "seed": 42, "feature_fraction": 0.8,
        "bagging_fraction": 0.8, "bagging_freq": 1,
    }
}

# ==============================================================================
# 2. データ処理・学習関数
# ==============================================================================
def get_topix100_codes():
    """TOPIX100構成銘柄の証券コード取得"""
    print("--- TOPIX100 銘柄コードの取得 ---")
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
    print(f"TOPIX100銘柄数: {len(codes)}")
    return codes

def download_data(start_date, end_date, topix_100):
    """株価データと米国市場データのダウンロード"""
    print(f"--- 株価データの一括取得（期間: {start_date} ～ {end_date}）---")
    tickers_jp = [f"{code}.T" for code in topix_100]
    raw_jp_data = yf.download(tickers_jp, start=start_date, end=end_date, auto_adjust=True, progress=False)
    raw_us_indices = yf.download(["^GSPC", "^IXIC"], start=start_date, end=end_date, auto_adjust=True, progress=False)
    return raw_jp_data, raw_us_indices

def calculate_features(df):
    """各種テクニカル指標（特徴量）の計算"""
    print("--- 特徴量の計算 ---")
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

def prepare_final_dataframe(raw_jp_data, raw_us_indices, config):
    """データ整形と最終的な学習用データフレームの作成"""
    print("--- 学習用データの最終準備 ---")
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
    df_merged['target_sign'] = (df_merged[config["target_variable"]] > 0).astype(int)
    features_to_shift = config["features"]
    for feature in features_to_shift:
        if feature in df_merged.columns:
            df_merged[f'{feature}_lag1'] = df_merged.groupby('code')[feature].shift(1)
    final_features = [f'{col}_lag1' for col in features_to_shift]
    target = 'target_sign'
    df_final = df_merged.dropna(subset=final_features + [target]).copy()
    return df_final, final_features, target

# ==============================================================================
# 3. メイン処理実行エリア
# ==============================================================================
if __name__ == '__main__':
    print("--- モデル学習と保存を開始 ---")
    topix_100_codes = get_topix100_codes()
    jp_data, us_data = download_data(CONFIG["start_date"], CONFIG["end_date"], topix_100_codes)
    final_df, feature_names, target_name = prepare_final_dataframe(jp_data, us_data, CONFIG)

    print("\n--- 全データを使用してモデルを学習 ---")
    d_full_train = lgb.Dataset(final_df[feature_names], final_df[target_name])
    final_model = lgb.train(CONFIG["lgbm_params"], d_full_train, num_boost_round=100)

    # スクリプト自身の場所を基準に、保存先パスを構築
    script_dir = os.path.dirname(os.path.abspath(__file__))
    save_path = os.path.join(script_dir, 'model.lgb')
    
    # モデルを指定したパスに保存
    joblib.dump(final_model, save_path)
    print(f"\n--- モデルを'{save_path}'として保存完了 ---")
    print("次に、このプロジェクトをGitHubにアップロードし、VPSにデプロイしてください。")

