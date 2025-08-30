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

# --- 初期設定 ---
warnings.filterwarnings('ignore')
pd.set_option('display.max_rows', 100)

# データ取得期間を固定
START_DATE = "2023-08-01"
END_DATE = "2024-08-01"

# --- 1. TOPIX100銘柄コード取得 ---
print("--- TOPIX100の銘柄コードを取得しています ---")
try:
    url = "https://search.sbisec.co.jp/v2/popwin/info/stock/pop690_topix100.html"
    response = requests.get(url, timeout=15)
    response.raise_for_status()
    soup = BeautifulSoup(response.content, "html.parser")
    topix_100 = [
        cols[0].text.strip()
        for row in soup.select("table tr")
        if (cols := row.find_all("td")) and len(cols) > 1 and cols[0].text.strip().isdigit()
    ]
    if not topix_100: raise ValueError("銘柄コードの取得に失敗")
    print(f"TOPIX100銘柄数: {len(topix_100)}")
except Exception as e:
    print(f"❌ 銘柄コードの取得中にエラーが発生: {e}")
    exit()

# --- 2. 株価データ一括取得＆整形 ---
print(f"\n--- 全銘柄の株価データを一括で取得しています（期間: {START_DATE} ～ {END_DATE}）---")
tickers_with_suffix = [f"{code}.T" for code in topix_100]
raw_data = yf.download(tickers_with_suffix, start=START_DATE, end=END_DATE, auto_adjust=True)
raw_data.columns.names = ['feature', 'code']
df = raw_data.stack(level='code').reset_index()
df['code'] = df['code'].str.replace('.T', '', regex=False)

# --- 3. 基本的な特徴量の計算 ---
print("\n--- 基本的な特徴量を計算しています ---")
df = df.sort_values(['code', 'Date'])
df['前日比'] = df.groupby('code')['Close'].pct_change(1) * 100
df['寄り引け変動率'] = (df['Close'] - df['Open']) / df['Open'] * 100
df['SMA_25'] = df.groupby('code')['Close'].transform(lambda x: x.rolling(window=25, min_periods=25).mean())
df['乖離率(25日)'] = ((df['Close'] - df['SMA_25']) / df['SMA_25']) * 100

# --- 4. S&P500データの準備とマージ ---
print("\n--- S&P500のデータを取得し、マージしています ---")
raw_sp500 = yf.download("^GSPC", start=START_DATE, end=END_DATE, auto_adjust=True, progress=False)
df_sp500 = pd.DataFrame(index=raw_sp500.index)
df_sp500['Close'] = raw_sp500['Close']
df_sp500.reset_index(inplace=True)
df_sp500["Date"] = pd.to_datetime(df_sp500["Date"]).dt.tz_localize(None)
df_sp500["S&P500前日比"] = df_sp500["Close"].pct_change() * 100
df_merge = pd.merge_asof(
    df.sort_values('Date'), 
    df_sp500[["Date", "S&P500前日比"]].dropna(), 
    on="Date", 
    direction="backward"
)

# ★★★★★★★★★★★★★★★★ ここからが最重要修正点 ★★★★★★★★★★★★★★★★
# --- 5. 予測のための特徴量エンジニアリング（未来の情報漏洩を完全に防ぐ） ---
print("\n--- 予測に使うための特徴量を作成しています（未来の情報漏洩を防止）---")
target = "寄り引け変動率"
features_to_shift = ['前日比', '寄り引け変動率', '乖離率(25日)', 'S&P500前日比']

# 全ての特徴量を1日ずらす（昨日の情報を使って今日を予測するため）
for feature in features_to_shift:
    df_merge[f'{feature}_lag1'] = df_merge.groupby('code')[feature].shift(1)

# 新しく作成したラグ特徴量とターゲットを定義
features = [f'{col}_lag1' for col in features_to_shift]

# NaNを含む行（シフトによって発生）を削除
df_final = df_merge.dropna(subset=features + [target]).copy()
# ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★

# --- 7. 学習データ準備 ---
print("\n--- モデル学習の準備をしています ---")
train_idx = df_final["Date"] < datetime.datetime(2024, 6, 1)
valid_idx = df_final["Date"] >= datetime.datetime(2024, 6, 1)
df_train = df_final.loc[train_idx, :]
df_valid = df_final.loc[valid_idx, :]
print(f"学習データ期間: {df_train['Date'].min().date()} ～ {df_train['Date'].max().date()}")
print(f"検証データ期間: {df_valid['Date'].min().date()} ～ {df_valid['Date'].max().date()}")
dtrain = lgb.Dataset(df_train[features], df_train[target])
dvalid = lgb.Dataset(df_valid[features], df_valid[target], reference=dtrain)
params = {
    "objective": "regression_l1", "metric": "rmse", "learning_rate": 0.01,
    "verbosity": -1, "seed": 42, "feature_fraction": 0.8,
    "bagging_fraction": 0.8, "bagging_freq": 1,
}

# --- 8. モデル学習 ---
print("\n--- LightGBMモデルの学習を開始します ---")
model = lgb.train(
    params, dtrain, num_boost_round=1000, valid_sets=[dtrain, dvalid],
    valid_names=["train", "valid"], callbacks=[lgb.early_stopping(stopping_rounds=50, verbose=True)]
)

# --- 9. 特徴量重要度 ---
print("\n--- 特徴量の重要度 ---")
df_importance = pd.DataFrame({
    "特徴量": model.feature_name(),
    "重要度": model.feature_importance(importance_type="gain")
}).sort_values("重要度", ascending=False)
print(df_importance)

# --- 10. 予測＆ポジション評価 ---
print("\n--- 予測とバックテストによる評価を行っています ---")
valid_pred = model.predict(df_valid[features], num_iteration=model.best_iteration)
df_eval = df_valid.copy()
df_eval["prediction"] = valid_pred
df_eval["rank"] = df_eval.groupby("Date")["prediction"].rank(ascending=False)
df_eval["position"] = 0
df_eval.loc[df_eval["rank"] <= (df_eval.groupby("Date")["rank"].transform("max") * 0.2), "position"] = 1
df_eval.loc[df_eval["rank"] >= (df_eval.groupby("Date")["rank"].transform("max") * 0.8), "position"] = -1
df_eval = df_eval[df_eval["position"] != 0]
df_eval["return"] = df_eval[target] * df_eval["position"]

# --- 11. 累積リターンの計算 ---
daily_return = df_eval.groupby("Date")["return"].mean()
cumulative_return = daily_return.cumsum()

# --- 12. パフォーマンス指標の表示 ---
print("\n--- バックテスト パフォーマンス評価 ---")
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
print("------------------------------------")

# --- 13. 累積リターンプロット ---
print("\n--- 累積リターンのグラフをプロットします ---")
plt.figure(figsize=(12, 6))
cumulative_return.plot()
plt.title("バックテストにおける累積リターン（検証期間）")
plt.xlabel("日付")
plt.ylabel("累積リターン (%)")
plt.grid()
plt.tight_layout()
plt.show()
