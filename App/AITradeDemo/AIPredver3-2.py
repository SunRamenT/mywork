"""
AI株価予測モデル 構築・バックテストパイプライン

本スクリプトは、以下の手順でAIによる株価予測モデルの構築と評価を行うものである。
1. 対象銘柄リスト（TOPIX100）の取得
2. 株価データおよび米国市場のインデックスデータのダウンロード
3. テクニカル指標などの「特徴量」の計算
4. AIモデル（LightGBM）の学習
5. 学習済みモデルによる未来の株価予測と、バックテストでの戦略評価
6. 結果の数値化およびグラフでの可視化
7. 全データでモデルを再学習し、翌営業日の推奨銘柄を出力
"""

import yfinance as yf
import pandas as pd
import requests
from bs4 import BeautifulSoup
import os
import warnings
import datetime
import lightgbm as lgb
import matplotlib.pyplot as plt
import numpy as np
# pip install japanize-matplotlib を実行してください
import japanize_matplotlib

# --- グローバル設定 ---
warnings.filterwarnings('ignore')
pd.set_option('display.max_rows', 100)


# ==============================================================================
# 1. 設定エリア (CONFIG)
# ==============================================================================
# 戦略のパラメータや設定は、すべてこのセクションで管理。
# これにより、コードの本体を変更することなく、容易な実験や調整が可能。
# ==============================================================================
CONFIG = {
    # --- データ期間設定 ---
    # 実行日の前日までのデータを取得するため、end_dateは未来の日付に設定
    "start_date": "2021-08-01",
    "end_date": datetime.datetime.now().strftime("%Y-%m-%d"),
    "validation_start_date": "2025-05-01", # この日付以降を検証データとする。

    # --- 予測対象 (ターゲット) ---
    "target_variable": "寄り引け変動率",

    # --- 特徴量 (AIモデルへのヒント) ---
    # 新しい特徴量名を追加するだけで、自動的に計算・学習に使用。
    "features": [
        '前日比', '寄り引け変動率', '乖離率(25日)',
        'S&P500前日比', 'Nasdaq前日比',
        'RSI', 'BB_Width', 'Volume_Ratio'
    ],

    # --- AIモデル設定 (LightGBM) ---
    "lgbm_params": {
        "objective": "binary",      # 目的：2値分類（上昇／下落）
        "metric": "auc",            # 評価指標：AUC
        "learning_rate": 0.01,
        "verbosity": -1,
        "seed": 42,
        "feature_fraction": 0.8,    # 学習毎に使用する特徴量の割合。
        "bagging_fraction": 0.8,    # 学習毎に使用するデータの割合。
        "bagging_freq": 1,
    },
    
    # --- 取引ルール設定 ---
    "trading_rule": {
        "type": "ranking", # 'ranking'（相対順位）または 'threshold'（絶対確率）。
        "num_rank_trades": 10,  # 'ranking'ルールの場合、1日あたりの取引銘柄数。
    }
}


# ==============================================================================
# 2. 関数エリア
# ==============================================================================
# 各処理を独立した関数に分割し、コードの見通し、再利用性、メンテナンス性を向上。
# ==============================================================================

def get_topix100_codes():
    """TOPIX100構成銘柄の証券コード取得"""
    print("--- TOPIX100 銘柄コードの取得 ---")
    try:
        url = "https://search.sbisec.co.jp/v2/popwin/info/stock/pop690_topix100.html"
        response = requests.get(url, timeout=15)
        response.raise_for_status()
        soup = BeautifulSoup(response.content, "html.parser")
        codes = [
            cols[0].text.strip()
            for row in soup.select("table tr")
            if (cols := row.find_all("td")) and len(cols) > 1 and cols[0].text.strip().isdigit()
        ]
        if not codes:
            raise ValueError("銘柄コードの取得に失敗。")
        print(f"TOPIX100銘柄数: {len(codes)}")
        return codes
    except Exception as e:
        print(f"X 銘柄コード取得中のエラー: {e}")
        exit()

def download_data(start_date, end_date, topix_100):
    """株価データと米国市場データのダウンロード"""
    print(f"\n--- 株価データの一括取得（期間: {start_date} ～ {end_date}）---")
    
    # 日本株データ
    tickers_jp = [f"{code}.T" for code in topix_100]
    raw_jp_data = yf.download(tickers_jp, start=start_date, end=end_date, auto_adjust=True)
    
    # 米国市場データ
    raw_us_indices = yf.download(["^GSPC", "^IXIC"], start=start_date, end=end_date, auto_adjust=True, progress=False)
    
    return raw_jp_data, raw_us_indices

def calculate_features(df):
    """各種テクニカル指標（特徴量）の計算"""
    print("\n--- 特徴量の計算 ---")
    df = df.sort_values(['code', 'Date'])
    
    # 基本的な特徴量
    df['前日比'] = df.groupby('code')['Close'].pct_change(1) * 100
    df['寄り引け変動率'] = (df['Close'] - df['Open']) / df['Open'] * 100
    df['SMA_25'] = df.groupby('code')['Close'].transform(lambda x: x.rolling(window=25, min_periods=25).mean())
    df['乖離率(25日)'] = ((df['Close'] - df['SMA_25']) / df['SMA_25']) * 100

    # テクニカル指標
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
    
    # 出来高関連
    df['Volume_SMA_25'] = df.groupby('code')['Volume'].transform(lambda x: x.rolling(window=25).mean())
    df['Volume_Ratio'] = df['Volume'] / df['Volume_SMA_25']
    
    return df

def prepare_final_dataframe(raw_jp_data, raw_us_indices, config):
    """データ整形と最終的な学習用データフレームの作成"""
    print("\n--- 学習用データの最終準備 ---")
    
    # 日本株データの整形
    raw_jp_data.columns.names = ['feature', 'code']
    df_jp = raw_jp_data.stack(level='code').reset_index()
    df_jp['code'] = df_jp['code'].str.replace('.T', '', regex=False)
    
    # 特徴量計算
    df_jp_featured = calculate_features(df_jp)
    
    # 米国市場データの整形
    df_us = pd.DataFrame(index=raw_us_indices.index)
    df_us['S&P500_Close'] = raw_us_indices.get(('Close', '^GSPC'))
    df_us['Nasdaq_Close'] = raw_us_indices.get(('Close', '^IXIC'))
    df_us.reset_index(inplace=True)
    df_us["Date"] = pd.to_datetime(df_us["Date"]).dt.tz_localize(None)
    df_us["S&P500前日比"] = df_us['S&P500_Close'].pct_change() * 100
    df_us["Nasdaq前日比"] = df_us['Nasdaq_Close'].pct_change() * 100
    
    # データのマージ
    df_merged = pd.merge_asof(
        df_jp_featured.sort_values('Date'), 
        df_us[["Date", "S&P500前日比", "Nasdaq前日比"]].dropna(), 
        on="Date", 
        direction="backward"
    )
    
    # 予測ターゲット（目的変数）の作成
    df_merged['target_sign'] = (df_merged[config["target_variable"]] > 0).astype(int)
    
    # 未来の情報漏洩を防ぐためのラグ特徴量作成
    features_to_shift = config["features"]
    for feature in features_to_shift:
        if feature in df_merged.columns:
            df_merged[f'{feature}_lag1'] = df_merged.groupby('code')[feature].shift(1)
        
    final_features = [f'{col}_lag1' for col in features_to_shift]
    target = 'target_sign'
    
    # NaNを含む行を削除し、最終データセットを完成
    df_final = df_merged.dropna(subset=final_features + [target]).copy()
    
    return df_final, final_features, target

def train_and_evaluate(df_final, features, target, config):
    """モデル学習、予測、バックテスト評価"""
    
    # --- 学習・検証データ分割 ---
    print("\n--- モデル学習の準備 ---")
    validation_start = datetime.datetime.strptime(config["validation_start_date"], "%Y-%m-%d")
    train_idx = df_final["Date"] < validation_start
    valid_idx = df_final["Date"] >= validation_start
    df_train = df_final.loc[train_idx, :]
    df_valid = df_final.loc[valid_idx, :]
    
    if df_train.empty or df_valid.empty:
        print("⚠️ 学習データまたは検証データが空のため、バックテストをスキップ。")
        return None, None

    print(f"学習データ期間: {df_train['Date'].min().date()} ～ {df_train['Date'].max().date()}")
    print(f"検証データ期間: {df_valid['Date'].min().date()} ～ {df_valid['Date'].max().date()}")
    
    # --- LightGBM用データセット作成 ---
    dtrain = lgb.Dataset(df_train[features], df_train[target])
    dvalid = lgb.Dataset(df_valid[features], df_valid[target], reference=dtrain)
    
    # --- モデル学習 ---
    print("\n--- LightGBMモデルの学習開始 ---")
    model = lgb.train(
        config["lgbm_params"], dtrain, num_boost_round=1000, valid_sets=[dtrain, dvalid],
        valid_names=["train", "valid"], callbacks=[lgb.early_stopping(stopping_rounds=50, verbose=True)]
    )

    # --- 予測とポジション決定 ---
    print("\n--- 予測とバックテストによる評価 ---")
    valid_pred = model.predict(df_valid[features], num_iteration=model.best_iteration)
    df_eval = df_valid.copy()
    df_eval["prediction"] = valid_pred
    
    # --- 取引ルール適用 ---
    if config["trading_rule"]["type"] == "ranking":
        num_trades = config["trading_rule"]["num_rank_trades"]
        df_eval["rank"] = df_eval.groupby("Date")["prediction"].rank(ascending=False)
        df_eval['position'] = 0
        df_eval.loc[df_eval['rank'] <= num_trades, 'position'] = 1
        df_eval.loc[df_eval['rank'] > (df_eval.groupby('Date')['rank'].transform('max') - num_trades), 'position'] = -1
        
    df_final_eval = df_eval[df_eval['position'] != 0].copy()
    
    return df_final_eval, model

def display_results(df_eval, model):
    """バックテスト結果（特徴量重要度、パフォーマンス指標、グラフ）の表示"""
    
    if df_eval is None or model is None:
        return

    # --- 特徴量重要度 ---
    print("\n--- 特徴量重要度 ---")
    df_importance = pd.DataFrame({
        "特徴量": model.feature_name(),
        "重要度": model.feature_importance(importance_type="gain")
    }).sort_values("重要度", ascending=False)
    print(df_importance)
    
    # --- パフォーマンス評価 ---
    trade_days = df_eval['Date'].nunique()
    total_trades = len(df_eval)
    print(f"\n取引日数: {trade_days}日 / {df_eval['Date'].nunique()}日")
    print(f"総取引回数: {total_trades}回")

    df_eval["return"] = df_eval[CONFIG["target_variable"]] * df_eval["position"]
    daily_return = df_eval.groupby("Date")["return"].mean()
    cumulative_return = daily_return.cumsum()

    print("\n--- バックテスト パフォーマンス評価 ---")
    if not cumulative_return.empty:
        total_return = cumulative_return.iloc[-1]
        print(f"最終累積リターン: {total_return:.2f}%")
        avg_daily_return = daily_return.mean()
        std_daily_return = daily_return.std()
        print(f"平均日次リターン: {avg_daily_return:.4f}%")
        sharpe_ratio = (avg_daily_return / std_daily_return) * np.sqrt(252) if std_daily_return != 0 else 0
        print(f"シャープレシオ（年率換算）: {sharpe_ratio:.2f}")
        win_rate = (daily_return > 0).sum() / len(daily_return) if len(daily_return) > 0 else 0
        print(f"勝率: {win_rate:.2%}")
        running_max = cumulative_return.cummax()
        drawdown = running_max - cumulative_return
        max_drawdown = drawdown.max()
        print(f"最大ドローダウン: {max_drawdown:.2f}%")
    else:
        print("期間中に取引条件を満たす銘柄がなく、評価指標は計算不能。")
    print("------------------------------------")

    # --- 累積リターンプロット ---
    print("\n--- 累積リターングラフのプロット ---")
    plt.figure(figsize=(12, 6))
    if not cumulative_return.empty:
        cumulative_return.plot()
    else:
        plt.plot([], [])
        print("取引がなかったため、グラフはプロットされない。")
    plt.title("バックテストにおける累積リターン（検証期間）")
    plt.xlabel("日付")
    plt.ylabel("累積リターン (%)")
    plt.grid()
    plt.tight_layout()
    plt.show()

def predict_next_day_trades(df_final, features, target, config):
    """全データでモデルを再学習し、翌営業日の推奨銘柄を予測・出力する。"""
    print("\n\n==================================================")
    print("=== 翌営業日の取引銘柄予測を開始 ===")
    print("==================================================")
    
    # --- 1. 全データを使用して本番用モデルを再学習 ---
    print("\n--- 全データを使用して本番用モデルを再学習 ---")
    d_full_train = lgb.Dataset(df_final[features], df_final[target])
    
    # early_stoppingを使わないため、学習回数を固定（バックテスト時のbest_iterationなど）
    # ここでは仮に100回とするが、チューニングの余地あり
    num_boost_round = 100 
    
    final_model = lgb.train(
        config["lgbm_params"],
        d_full_train,
        num_boost_round=num_boost_round
    )
    print("本番用モデルの学習完了。")
    
    # --- 2. 予測に必要な最新のデータを準備 ---
    print("\n--- 予測のための最新データを準備 ---")
    # 各銘柄の最終行（＝最新のデータ）を取得
    latest_data = df_final.loc[df_final.groupby('code')['Date'].idxmax()]
    
    # 予測に使用する特徴量が存在するか確認
    if latest_data.empty:
        print("X 予測に使用できる最新データが見つからない。")
        return
        
    print(f"最新データの日付: {latest_data['Date'].min().date()}")

    # --- 3. 翌営業日の予測を実行 ---
    print("\n--- 翌営業日の上昇確率を予測 ---")
    predictions = final_model.predict(latest_data[features])
    
    # --- 4. 予測結果を整形し、推奨銘柄を出力 ---
    df_prediction = pd.DataFrame({
        'code': latest_data['code'],
        'prediction': predictions
    })
    
    # ランキングルールに基づいて推奨銘柄を決定
    num_trades = config["trading_rule"]["num_rank_trades"]
    df_prediction_sorted = df_prediction.sort_values('prediction', ascending=False)
    
    df_buy = df_prediction_sorted.head(num_trades)
    df_sell = df_prediction_sorted.tail(num_trades).sort_values('prediction', ascending=True)

    print("\n\n--- 予測結果 ---")
    print(f"【買い推奨銘柄】 (予測確率上位 {num_trades}銘柄)")
    print("-------------------------")
    print(df_buy)
    print("\n")
    print(f"【売り推奨銘柄】 (予測確率下位 {num_trades}銘柄)")
    print("-------------------------")
    print(df_sell)
    print("\n")

# ==============================================================================
# 3. メイン処理実行エリア
# ==============================================================================
# 本スクリプトが直接実行された場合に、以下の処理を実行。
# ==============================================================================
if __name__ == '__main__':
    # ステップ1: 銘柄コード取得
    topix_100_codes = get_topix100_codes()
    
    # ステップ2: データダウンロード
    jp_data, us_data = download_data(CONFIG["start_date"], CONFIG["end_date"], topix_100_codes)
    
    # ステップ3: 学習用データフレームの準備
    final_df, feature_names, target_name = prepare_final_dataframe(jp_data, us_data, CONFIG)
    
    # ステップ4: モデル学習とバックテスト評価
    evaluation_df, trained_model = train_and_evaluate(final_df, feature_names, target_name, CONFIG)
    
    # ステップ5: バックテスト結果表示
    display_results(evaluation_df, trained_model)

    # ★★★★★ ステップ6: 翌営業日の推奨銘柄を予測・出力 ★★★★★
    predict_next_day_trades(final_df, feature_names, target_name, CONFIG)

